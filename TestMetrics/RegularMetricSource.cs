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

public class TestAddRemoveResourcesCliAction : ICliAction
{
    private static readonly TraceSource log = Log.CreateSource("Test Metrics");
    public int Execute(CancellationToken cancellationToken)
    {
        var src1 = new InstrumentMetricSource();
        var src2 = new InstrumentMetricSource();
        using var s = Session.Create(SessionOptions.OverlayComponentSettings);
        MetricsSettings.Current.Clear();
        MetricsSettings.Current.Initialize();

        {
            log.Info("Cleared");
            log.Info($"{MetricsSettings.Current.Count} metrics");
        }
        {
            log.Info("Add src1");
            InstrumentSettings.Current.Add(src1);
            log.Info($"{MetricsSettings.Current.Count} metrics");
        }
        {
            log.Info("Add src2");
            InstrumentSettings.Current.Add(src2);
            log.Info($"{MetricsSettings.Current.Count} metrics");
        }
        MetricsSettings.Current.Add(new MetricsSettingsItem(MetricManager.GetMetricInfos()
            .FirstOrDefault(i => i.DefaultEnabled && i.Source == src1)));
        using (var s2 = Session.Create(SessionOptions.OverlayComponentSettings))
        {
            {
                log.Info("Remove src1");
                InstrumentSettings.Current.RemoveAt(0);
                log.Info($"{MetricsSettings.Current.Count} metrics");
            }
            {
                log.Info("Remove src2");
                InstrumentSettings.Current.RemoveAt(0);
                log.Info($"{MetricsSettings.Current.Count} metrics");
            }
        }

        return 0;
    }
}