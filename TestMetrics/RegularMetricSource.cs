using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using OpenTap;
using OpenTap.Cli;
using OpenTap.Metrics;
using OpenTap.Metrics.Settings;
using OpenTap.Plugins.BasicSteps;

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

[Display("Log Metric Sink", "This sink will log polled metrics using the OpenTAP logging system.")]
public class LogMetricSink : IMetricSink
{
    [Display("Log Metadata", "When enabled, metadata will also be logged.")]
    public bool LogMetadata { get; set; }

    [Display("Metadata Log Level", "The level at which metadata will be logged.")]
    [EnabledIf(nameof(LogMetadata), HideIfDisabled = true)]
    public LogSeverity MetadataLogSeverity { get; set; } = LogSeverity.Info;
    private static readonly TraceSource log = Log.CreateSource("Log Poller"); 
    public void OnMetricsPolled(MetricsPolledEventArgs e)
    {
        foreach (var metric in e.Metrics)
        {
            log.Info($"Metric: {metric.Info.MetricFullName}");
            log.Info($"Value: {metric.Value}");
            if (LogMetadata)
            {
                foreach (var kvp in metric.MetaData)
                {
                    if (MetadataLogSeverity == LogSeverity.Info)
                    {
                        log.Info($"\t{kvp.Key}={kvp.Value}");
                    }
                    else if (MetadataLogSeverity == LogSeverity.Debug)
                    {
                        log.Debug($"\t{kvp.Key}={kvp.Value}");
                    }
                }
            }
        }
    }
}