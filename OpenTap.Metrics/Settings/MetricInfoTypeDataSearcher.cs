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
        _types = null;
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

    // TODO: This should be invalidated when MetricManager discovers new metrics (e.g. from IAdditionalMetric sources)
    private MetricInfoTypeData[] _types = null;

    public IEnumerable<ITypeData> Types => _types ??= metricSpecifiers.Select(MetricInfoTypeData.FromMetricSpecifier).ToArray();
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
    public static MetricInfoTypeData FromMetricSpecifier(MetricSpecifier disc)
    {
        return _cache.GetOrAdd(disc, _ => new MetricInfoTypeData(disc));
    } 

    private DisplayAttribute displayAttribute = null;

    private MetricInfoTypeData(MetricSpecifier specifier)
    {
        Specifier = specifier;
        BaseType = TypeData.FromType(typeof(MetricsSettingsItem));
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