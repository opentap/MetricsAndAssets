using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataSearcher : ITypeDataSearcherCacheInvalidated
{
    private InstrumentSettings prevInstruments = null;
    private DutSettings prevDuts = null;
    void SetupAndSearch(object a, object b)
    {
        // Update all handlers. If any settings instances were changed
        if (prevInstruments != null)
        {
            prevInstruments.CacheInvalidated -= SetupAndSearch;
            prevInstruments.CollectionChanged -= SetupAndSearch;
            prevInstruments.PropertyChanged -= SetupAndSearch;
        }

        if (prevDuts != null)
        {
            prevDuts.CacheInvalidated -= SetupAndSearch;
            prevDuts.CollectionChanged -= SetupAndSearch;
            prevDuts.PropertyChanged -= SetupAndSearch; 
        }

        var ins = InstrumentSettings.Current;
        var duts = DutSettings.Current;
        ins.CacheInvalidated += SetupAndSearch;
        ins.CollectionChanged += SetupAndSearch;
        ins.PropertyChanged += SetupAndSearch;
        duts.CacheInvalidated += SetupAndSearch;
        duts.CollectionChanged += SetupAndSearch;
        duts.PropertyChanged += SetupAndSearch;
        prevInstruments = ins;
        prevDuts = duts; 
        Search();
    }
    public MetricInfoTypeDataSearcher()
    {
        SetupAndSearch(null, null);
    }

    private long updateStarted = 0;

    private void Search2(object a, object b)
    {
        Search();
    }
    public void Search()
    {
        // Postpone updates while they happen within this interval
        const int debounce_ms = 200;
        // Don't postpone the update indefinitely..
        const int debounce_max_ms = 10000;
        var processingToken = Interlocked.Increment(ref updateStarted);
        if (processingToken != 1) return;
        
        // We need to search in a thread because GetMetricInfos() will cause a recursive call which will deadlock otherwise.
        TapThread.Start(() =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromMilliseconds(debounce_max_ms))
                {
                    var prev = updateStarted;
                    TapThread.Sleep(debounce_ms);
                    if (prev == updateStarted) break;
                }
                Infos = MetricManager.GetMetricInfos().Concat(MetricManager.GetResourceMetricInfos()).ToList();
                var changers = Infos.Select(i => i.Source).OfType<INotifyPropertyChanged>().Distinct();
                foreach (var ch in changers)
                {
                    ch.PropertyChanged -= Search2;
                    ch.PropertyChanged += Search2;
                }
                CacheInvalidated?.Invoke(this, new());
                long token2 = Interlocked.Exchange(ref updateStarted, 0);
                if (token2 != 1)
                {
                    // a search was inititated during sleep
                    Search();
                }
            }
            catch
            {
                Interlocked.Exchange(ref updateStarted, 0); 
            }
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

    public bool CanCreateInstance => MetricInfo is not AbstractMetricInfo;
}
