using OpenTap;
using OpenTap.Metrics;

namespace TestMetrics;

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

[Display("Instrument Metric Source 2")]
public class InstrumentMetricSource2 : Instrument, IMetricSource
{
    [Metric("New Unique Metric", DefaultEnabled = true ) ]
    public int SomeMetric { get; set; }
}
