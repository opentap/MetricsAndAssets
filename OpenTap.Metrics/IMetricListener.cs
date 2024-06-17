using System.Collections.Generic;

namespace OpenTap.Metrics;

/// <summary> Defines that a class can consume metrics. </summary>
public interface IMetricListener
{
    /// <summary>  Event occuring when a metric producer generates out-of-band metrics. </summary>
    void OnPushMetric(IMetric table);
}