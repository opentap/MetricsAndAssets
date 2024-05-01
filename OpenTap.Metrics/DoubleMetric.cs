using System;

namespace OpenTap.Metrics;

/// <summary>  A double metric. </summary>
public readonly struct DoubleMetric : IMetric
{
    /// <summary> The metric information. </summary>
    public MetricInfo Info { get; }
        
    /// <summary> The value of the metric. </summary>
    public double Value { get; }
        
    /// <summary> The time the metric was recorded. </summary>
    public DateTime Time { get; }
        
    /// <summary> Creates a new instance of the double metric. </summary>
    public DoubleMetric(MetricInfo info, double value, DateTime? time = null)
    {
        Value = value;
        Info = info;
        Time = time ?? DateTime.Now;
    }
        
    /// <summary> Returns a string representation of the double metric. </summary>
    public override string ToString()
    {
        return $"{Info.MetricFullName}: {Value} at {Time}";
    }

    object IMetric.Value => Value;
}