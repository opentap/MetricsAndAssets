using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Linq;

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataSearcher : ITypeDataSearcherCacheInvalidated
{
    public void Search()
    {
        // We need to search in a thread because GetMetricInfos() will cause a recursive call which will deadlock otherwise.
        TapThread.Start(() =>
        {
            Infos = MetricManager.GetMetricInfos().ToList(); 
            CacheInvalidated?.Invoke(this, new());
        });
    } 

    private List<MetricInfo> Infos { get; set; } = new(); 


    // It is tricky to filter away metrics that are currently configured for several reasons:
    // 1. Checking the currently configured metrics causes recursive typedata lookups, which can deadlock
    // 2. This can be called in other situations to discover specializations of IMetricInfo. If a configured MetricInfo
    // is not recognized as a specialization, we can run into weird behavior.
    public IEnumerable<ITypeData> Types => Infos.Select(MetricInfoTypeData.FromMetricInfo);//.Where(t => !MetricSettings.Current.Contains(t.MetricInfo));
    public event EventHandler<TypeDataCacheInvalidatedEventArgs> CacheInvalidated;
}

public class MetricInfoTypeDataProvider : ITypeDataProvider
{
    private static readonly TraceSource log = Log.CreateSource("Metric Serializer");

    public ITypeData GetTypeData(string identifier)
    {
        if (identifier.StartsWith(MetricInfoTypeData.MetricTypePrefix))
        {
            var id = identifier.Substring(MetricInfoTypeData.MetricTypePrefix.Length);
            var info = MetricManager.GetMetricInfos()
                .FirstOrDefault(m => m.MetricFullName == id);
            if (info != null)
            {
                return MetricInfoTypeData.FromMetricInfo(info);
            }

            log.ErrorOnce(identifier, $"Metric '{id}' not found.");
        }

        return null;
    }

    public ITypeData GetTypeData(object obj)
    {
        if (obj is MetricInfo m)
            return MetricInfoTypeData.FromMetricInfo(m);
        return null;
    }

    public double Priority => 999;
}

class MetricInfoTypeData : ITypeData
{
    public const string MetricTypePrefix = "met:";
    public MetricInfo MetricInfo { get; }
    private static ConditionalWeakTable<MetricInfo, MetricInfoTypeData> _cache = new();
    public static MetricInfoTypeData FromMetricInfo(MetricInfo source)
    {
        return _cache.GetValue(source, _ => new MetricInfoTypeData(source));
    }

    private MetricInfoTypeData(MetricInfo metricInfo)
    {
        this.MetricInfo = metricInfo;
        this.BaseType = TypeData.FromType(typeof(MetricInfo));
    }

    private DisplayAttribute displayAttribute = null;

    private ITypeData innerType => BaseType;
    public IEnumerable<object> Attributes => [ displayAttribute ??= new DisplayAttribute(MetricInfo.Name, Group: MetricInfo.GroupName) , ..innerType.Attributes];

    public string Name => $"{MetricTypePrefix}{MetricInfo.MetricFullName}";

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
        return MetricInfo;
    }

    public ITypeData BaseType { get; }

    public bool CanCreateInstance => true;
}
