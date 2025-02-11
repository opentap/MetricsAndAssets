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

public class MetricInfoTypeDataSearcher : ITypeDataSearcherCacheInvalidated
{
    private long updateStarted = 0;
    public void Search()
    {
        var processingToken = Interlocked.Increment(ref updateStarted);
        if (processingToken != 1) return;

        TapThread.Start(() =>
        {
            try
            {
                HashSet<MetricSpecifier> uniqueMetrics = [];
                var groups = TypeData.GetDerivedTypes<IMetricSource>().SelectMany(src => src.GetMembers())
                    .Where(mem => mem.HasAttribute<MetricAttribute>())
                    .GroupBy(x =>
                    {
                        var attr = x.GetAttribute<MetricAttribute>();
                        var grp = string.IsNullOrWhiteSpace(attr.Group) ? null : attr.Group;
                        return (Group: grp, Name: attr.Name, Type: MetricInfo.GetMemberMetricType(x));
                    });
                foreach (var grp in groups)
                {
                    MetricSpecifier d = new MetricSpecifier(grp.Key.Name, grp.Key.Group, grp.Key.Type);
                    foreach (var g in grp)
                    {
                        var attr = g.GetAttribute<MetricAttribute>();
                        if (attr.DefaultPollRate != 0) d.DefaultPollRate = attr.DefaultPollRate;
                        if (attr.DefaultEnabled) d.DefaultEnabled = true;
                        if (attr.Kind.HasFlag(MetricKind.Poll)) d.Kind |= MetricKind.Poll;
                        if (attr.Kind.HasFlag(MetricKind.Push)) d.Kind |= MetricKind.Push;
                    }

                    uniqueMetrics.Add(d);
                }

                metricSpecifiers = uniqueMetrics.ToArray();
                CacheInvalidated?.Invoke(this, new());
            }
            finally
            {
                Interlocked.Exchange(ref updateStarted, 0);
            }
        });
    }

    private MetricSpecifier[] metricSpecifiers = [];

    public IEnumerable<ITypeData> Types => metricSpecifiers.Select(MetricInfoTypeData.FromMetricSpecifier);

    public event EventHandler<TypeDataCacheInvalidatedEventArgs> CacheInvalidated;
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