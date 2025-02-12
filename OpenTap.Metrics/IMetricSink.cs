using System.Collections.Generic;
using OpenTap.Package;

namespace OpenTap.Metrics;

/// <summary>
/// Arguments provided to IMetricSink implementations
/// </summary>
public struct MetricsPolledEventArgs
{
    public IEnumerable<IMetric> Metrics;
}

/// <summary>
/// Public classes implementing this interface will be automatically instantiated by the Metric system,
/// and receive a callback when metrics configured in MetricsSettings are polled.
/// </summary>
public interface IMetricSink : ITapPlugin
{
    void OnMetricsPolled(MetricsPolledEventArgs e); 
}