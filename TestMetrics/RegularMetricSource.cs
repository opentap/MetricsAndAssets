using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenTap;
using OpenTap.Metrics;
using OpenTap.Metrics.Settings;
using OpenTap.Package;

namespace TestMetrics;

[Display("Regular Metric Source")]
public class RegularMetricSource : IMetricSource
{
    [Metric("Grouped Metric", "Metric Group", kind: MetricKind.Poll, DefaultPollRate = 27)]
    public int GroupedPollMetric { get; set; }
    
    [Metric("Poll Metric", kind: MetricKind.Poll, DefaultPollRate = 27)]
    public int PollMetric { get; set; }
    
    [Metric("Push Metric", kind: MetricKind.Push, DefaultPollRate = 120)]
    public int PushMetric { get; set; }
    
    [Metric("PushPoll Metric", kind: MetricKind.PushPoll, DefaultPollRate = 1200)]
    public int PushPollMetric { get; set; } 
    
    [Metric("Quick Metric", kind: MetricKind.Poll, DefaultPollRate = 1)]
    public int QuickMetric { get; set; }
    [Metric("Slow Metric", kind: MetricKind.Poll, DefaultPollRate = 86400*100)]
    public int SlowMetric { get; set; }
    [Metric("Default Poll Metric", kind: MetricKind.Poll, DefaultPollRate = 600, DefaultEnabled = true)]
    public int DefaultPollMetric { get; set; }
}

[Display("Instrument Metric Source 2")]
public class InstrumentMetricSource2 : Instrument, IMetricSource
{
    [Metric("New Unique Metric", DefaultEnabled = true ) ]
    public int SomeMetric { get; set; }
}

[Display("Instrument Metric Source")]
public class InstrumentMetricSource : Instrument, IMetricSource
{
    [Metric("Poll Metric", kind: MetricKind.Poll, DefaultPollRate = 27)]
    public int PollMetric { get; set; }
    
    [Metric("Push Metric", kind: MetricKind.Push, DefaultPollRate = 120)]
    public int PushMetric { get; set; }
    
    [Metric("PushPoll Metric", kind: MetricKind.PushPoll, DefaultPollRate = 1200)]
    public int PushPollMetric { get; set; } 
}

public class AfterCreateAction : ICustomPackageAction
{
    public int Order() => 999;
    

    public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
    {
        var tds = TypeData.GetDerivedTypes<IMetricsSettingsItem>().ToArray();
        using var ms = new MemoryStream();
        package.SaveTo(ms);
        var str = Encoding.UTF8.GetString(ms.ToArray());
        Console.WriteLine(str);
        return true;
    }

    public PackageActionStage ActionStage => PackageActionStage.Create;
}