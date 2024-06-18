using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics;

/// <summary>
///  Class for managing metrics.
/// </summary>
public static class MetricManager
{
    /// <summary>
    /// NOTE: This method only exists to clear between unit tests.
    /// This should never be used
    /// </summary>
    internal static void Reset()
    {
        _consumers.Clear();
        _interestLookup.Clear();
        _metricProducers.Clear();
    }
    
    static readonly HashSet<IMetricListener> _consumers =
        new HashSet<IMetricListener>();

    /// <summary> Register a metric consumer. </summary>
    /// <param name="listener"></param>
    public static void RegisterListener(IMetricListener listener)
    {
        _consumers.Add(listener);
    }

    private static readonly ConcurrentDictionary<IMetricListener, HashSet<MetricInfo>> _interestLookup =
        new ConcurrentDictionary<IMetricListener, HashSet<MetricInfo>>();
    
    /// <summary>
    /// Set the interest set of a given metric listener. The current interest set is overwritten.
    /// </summary>
    /// <param name="listener">The listener expressing interest.</param>
    /// <param name="interest">The set of metric infos of interest.</param>
    public static void ShowInterest(IMetricListener listener, IEnumerable<MetricInfo> interest)
    {
        var hs = interest.ToHashSet();
        if (hs.Any())
            _interestLookup[listener] = hs;
        else
            _interestLookup.TryRemove(listener, out _);
    } 

    /// <summary> Unregister a metric consumer. </summary>
    /// <param name="listener"></param>
    public static void UnregisterListener(IMetricListener listener)
    {
        _consumers.Remove(listener);
        _interestLookup.TryRemove(listener, out _);
    }

    /// <summary> Returns true if a metric has interest. </summary>
    public static bool HasInterest(MetricInfo metric) => _interestLookup.Values.Any(x => x.Contains(metric));
        
    /// <summary> Get information about the metrics available to query. </summary>
    /// <returns></returns>
    public static IEnumerable<MetricInfo> GetMetricInfos()
    {
        var types = TypeData.GetDerivedTypes<IMetricSource>().Where(x => x.CanCreateInstance);
        List<IMetricSource> producers = new List<IMetricSource>();
        foreach (var type in types)
        {
            // DUT and Instrument settings will explicitly added later if they are configured on the bench,
            // regardless of whether or not they are IMetricSources.
            if (type.DescendsTo(typeof(DutSettings)) || type.DescendsTo(typeof(InstrumentSettings)))
                continue;
            if (type.DescendsTo(typeof(ComponentSettings)))
            {
                if(ComponentSettings.GetCurrent(type) is IMetricSource producer)
                    producers.Add(producer);
            }
            else
            {
                if (_metricProducers.GetOrAdd(type, t => (IMetricSource)t.CreateInstance()) is IMetricSource m)
                    producers.Add(m);
            }
        }

        foreach (var metricSource in InstrumentSettings.Current.Cast<object>().Concat(DutSettings.Current)
                     .Concat(producers))
        {

            var type1 = TypeData.GetTypeData(metricSource);
                
            string sourceName = (metricSource as IResource)?.Name ?? type1.GetDisplayAttribute().Name;
            var memberGrp = type1.GetMembers()
                .Where(member => member.HasAttribute<MetricAttribute>() && TypeIsSupported(member.TypeDescriptor))
                .ToLookup(type2 => type2.GetAttribute<MetricAttribute>().Group ?? sourceName);
            foreach (var member in memberGrp)
            {
                foreach(var mem in member)
                    yield return new MetricInfo(mem, member.Key, metricSource);
            }
            if (metricSource is IAdditionalMetricSources source2)
            {
                foreach (var metric in source2.AdditionalMetrics)
                    yield return metric;
            }
        }
    }
        
    /// <summary> For now only string, double, int, and bool type are supported. </summary>
    /// <param name="td"></param>
    /// <returns></returns>
    static bool TypeIsSupported(ITypeData td)
    {
        var type = td.AsTypeData().Type;
        return type == typeof(double) || type == typeof(bool) || type == typeof(int) || type == typeof(string);
    }

    private static readonly ConcurrentDictionary<ITypeData, IMetricSource> _metricProducers =
        new ConcurrentDictionary<ITypeData, IMetricSource>();

    /// <summary> Push a double metric. </summary>
    public static void PushMetric(MetricInfo metric, double value)
    {
        PushMetric(new DoubleMetric(metric, value));
    }
        
    /// <summary> Push a boolean metric. </summary>
    public static void PushMetric(MetricInfo metric, bool value)
    {
        PushMetric(new BooleanMetric(metric, value));
    }
    /// <summary> Push a string metric. </summary>
    public static void PushMetric(MetricInfo metric, string value)
    {
        PushMetric(new StringMetric(metric, value));
    }
        
    /// <summary>
    /// Push a non-specific metric. This method is private to avoid pushing any kind of metric.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    static void PushMetric(IMetric metric)
    {
        foreach (var consumer in _consumers.ToList())
        {
            if (_interestLookup.TryGetValue(consumer, out var interest) && interest.Contains(metric.Info))
                consumer.OnPushMetric(metric);
        }
    }
        
    static readonly TraceSource log = Log.CreateSource(nameof(MetricManager));

    /// <summary> Poll metrics. </summary>
    public static IEnumerable<IMetric> PollMetrics(IEnumerable<MetricInfo> interestSet)
    {
        var interest = interestSet.Where(i => i.Kind.HasFlag(MetricKind.Poll)).ToHashSet();
        interest.RemoveWhere(i => !i.Kind.HasFlag(MetricKind.Poll));
        
        foreach (var source in interest.GroupBy(i => i.Source))
        {
            if (source.Key is IOnPollMetricsCallback producer)
            {
                try
                {
                    producer.OnPollMetrics(source);
                }
                catch (Exception ex)
                {
                    log.Warning($"Unhandled exception in OnPollMetrics on '{producer}': '{ex.Message}'");
                }
            }
        }

        foreach (var metric in interest)
        {
            var metricValue = metric.GetValue(metric.Source);
            switch (metricValue)
            {
                case bool v:
                    yield return new BooleanMetric(metric, v);
                    break;
                case double v:
                    yield return new DoubleMetric(metric, v);
                    break;
                case int v:
                    yield return new DoubleMetric(metric, v);
                    break;
                case string v:
                    yield return new StringMetric(metric, v);
                    break;
                default:
                    log.ErrorOnce(metric, "Metric value is not a supported type: {0} of type {1}", metric.Name, metricValue?.GetType().Name ?? "null");
                    break;
            }
        }
    }
         
    /// <summary> Get metric information from the system. </summary>
    public static MetricInfo GetMetricInfo(object source, string member)
    {
        var type = TypeData.GetTypeData(source);
        var mem = type.GetMember(member);
        if (mem?.GetAttribute<MetricAttribute>() is MetricAttribute metric)
        {
            if (TypeIsSupported(mem.TypeDescriptor))
            {
                return new MetricInfo(mem,
                    metric.Group ?? (source as IResource)?.Name ?? type.GetDisplayAttribute()?.Name, source);
            }
        }
        return null;
    }
}
