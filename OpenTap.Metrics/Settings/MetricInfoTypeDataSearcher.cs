using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataSearcher : ITypeDataSearcher, ITypeDataSourceProvider
{
    public void Search()
    {
        // _types = null;
    } 
    private bool getting = false;
    private MetricSpecifier[] metricSpecifiers
    {
        get
        {
            if (getting) return [];
            {
                getting = true;
                try
                {
                    return MetricManager.GetMetricInfos()
                        .Select(x => new MetricSpecifier(x.Member))
                        .Distinct()
                        .ToArray();
                }
                finally
                {
                    getting = false;
                }
            }
        }
    }

    // TODO: caching this is difficult for a few reasons:
    // 1. The cache should be invalidated when a dut / instrument is added or removed (i.e. this cache is now bench-profile specific)
    // 2. The cache should be invalidated when an IAdditionalMetrics implementation adds or removes a metric
    // 2) cannot really be detected, so we can only discover that fact when GetMetricInfos() is actually called
    // I have opted to disable caching for now, but we can look into 
    // private MetricInfoTypeData[] _types = null;

    public IEnumerable<ITypeData> Types => metricSpecifiers.Select(MetricInfoTypeData.FromMetricSpecifier).ToArray();
    public ITypeDataSource GetSource(ITypeData typeData)
    {
        if (MetricInfoTypeDataSource.TryFromTypeData(typeData, out var src))
            return src;
        return null;
    }
}

[DebuggerDisplay("{Name}")]
class MetricInfoTypeData : ITypeData
{
    public const string MetricTypePrefix = "Metric:";
    public readonly MetricSpecifier Specifier;
    private static readonly ConcurrentDictionary<MetricSpecifier, MetricInfoTypeData> _cache = [];
    private static readonly ConcurrentDictionary<string, MetricInfoTypeData> _nameCache = [];

    public static MetricInfoTypeData FromMetricSpecifier(MetricSpecifier disc)
    {
        return _cache.GetOrAdd(disc, _ => new MetricInfoTypeData(disc));
    } 

    public static MetricInfoTypeData FromMetricName(string name)
    {
        if (_nameCache.TryGetValue(name, out var td)) return td;
        return null;
    }

    private DisplayAttribute displayAttribute = null;

    private MetricInfoTypeData(MetricSpecifier specifier)
    {
        Specifier = specifier;
        BaseType = TypeData.FromType(typeof(MetricsSettingsItem));
        _nameCache.TryAdd(this.Name, this);
    }

    private ITypeData innerType => BaseType;
    public IEnumerable<object> Attributes => [ new BrowsableAttribute(ShouldBeBrowsable), displayAttribute ??= new DisplayAttribute(Specifier.Name, Group: Specifier.Group) , ..innerType.Attributes];

    private bool ShouldBeBrowsable => true;

    public string Name => string.IsNullOrWhiteSpace(Specifier.Group)
        ? $"{MetricTypePrefix}{Specifier.Name}"
        : $"{MetricTypePrefix}{Specifier.Group}_{Specifier.Name}";

    public IEnumerable<IMemberData> GetMembers()
    {
        return innerType.GetMembers();
    }

    public IMemberData GetMember(string name)
    {
        return innerType.GetMember(name);
    }

    public object CreateInstance(object[] arguments)
    {
        return new MetricsSettingsItem(Specifier);
    }

    public ITypeData BaseType { get; }

    // This plugin type will not be added to a plugins package xml if it cannot be instantiated.
    // If this metric requires a concrete instrument, we should still advertise being able to instantiate it,
    // but add [Unbrowsable] to the abstract type. The concrete type can still be instantiated normally.
    public bool CanCreateInstance => true; 
}
