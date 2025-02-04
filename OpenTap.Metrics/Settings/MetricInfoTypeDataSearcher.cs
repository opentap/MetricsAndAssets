using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenTap.Package; 

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataSearcher : ITypeDataSearcherCacheInvalidated, ITypeDataSourceProvider
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
                Infos = MetricManager.GetMetricInfos().Concat(MetricManager.GetAbstractMetricInfos()).ToArray();
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

    internal static MetricInfo[] InitialInfos { get; set; } = [];
    private MetricInfo[] Infos { get; set; } = null; 

    public IEnumerable<ITypeData> Types => [.. (Infos ?? InitialInfos).Select(MetricInfoTypeData.FromMetricInfo)];

    public ITypeDataSource GetSource(ITypeData typeData)
    {
        if (typeData is MetricInfoTypeData m)
        {
            return new MetricTypeDataSource(m);
        }

        return null;
    }

    class MetricTypeDataSource : ITypeDataSource
    {
        public string Name => td.Name;
        public string Location => td.SourceAssembly.Location;

        public IEnumerable<ITypeData> Types => td.GetRelatedMetricInfoTypeDatas();
        public IEnumerable<object> Attributes => td.Attributes;
        public IEnumerable<ITypeDataSource> References => [];
        public string Version { get; }

        private readonly MetricInfoTypeData td;
        public MetricTypeDataSource(MetricInfoTypeData td)
        {
            this.td = td;
            Version = Installation.Current.FindPackageContainingFile(td.SourceAssembly.Location)?.Version?.ToString() ??
                      "1.0.0";
        }
    }

    public event EventHandler<TypeDataCacheInvalidatedEventArgs> CacheInvalidated;
}

[DebuggerDisplay("{Name}")]
class MetricInfoTypeData : ITypeData
{
    public const string MetricTypePrefix = "Metric:";
    public MetricInfo MetricInfo { get; }
    private static ConditionalWeakTable<MetricInfo, MetricInfoTypeData> _cache = new();
    private static ConditionalWeakTable<Assembly, ConcurrentBag<MetricInfoTypeData>> _sourceLookup = new();
    public static MetricInfoTypeData FromMetricInfo(MetricInfo source)
    {
        return _cache.GetValue(source, _ => new MetricInfoTypeData(source));
    }

    internal Assembly SourceAssembly => (MetricInfo as AbstractMetricInfo)?.Source.Assembly ?? MetricInfo.Source.GetType().Assembly;

    private MetricInfoTypeData(MetricInfo metricInfo)
    {
        this.MetricInfo = metricInfo;
        this.BaseType = TypeData.FromType(typeof(MetricsSettingsItem));
        var bag = _sourceLookup.GetValue(SourceAssembly, _ => new());
        bag.Add(this); 
    }

    internal IEnumerable<MetricInfoTypeData> GetRelatedMetricInfoTypeDatas() =>
        _sourceLookup.GetValue(SourceAssembly, _ => [this]);

    private DisplayAttribute displayAttribute = null;

    private ITypeData innerType => BaseType;
    public bool IsAbstract => MetricInfo is AbstractMetricInfo;
    public IEnumerable<object> Attributes => [ new BrowsableAttribute(ShouldBeBrowsable), displayAttribute ??= new DisplayAttribute(MetricInfo.Name, Group: MetricInfo.GroupName) , ..innerType.Attributes];

    private bool ShouldBeBrowsable => !IsAbstract && !MetricsSettings.Current.Any(m => Equals(m.Metric, MetricInfo));
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
        if (IsAbstract)
            throw new Exception($"Cannot instantiate an abstract metric.");
        return new MetricsSettingsItem(MetricInfo);
    }

    public ITypeData BaseType { get; }

    // This plugin type will not be added to a plugins package xml if it cannot be instantiated.
    // If this metric requires a concrete instrument, we should still advertise being able to instantiate it,
    // but add [Unbrowsable] to the abstract type. The concrete type can still be instantiated normally.
    public bool CanCreateInstance => true;
}