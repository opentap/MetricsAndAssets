//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap.Metrics;

/// <summary>  A boolean metric. </summary>
public readonly struct BooleanMetric : IMetric
{
    /// <summary> The metric information. </summary>
    public MetricInfo Info { get; }

    public Dictionary<string, string> MetaData { get; }

    /// <summary> The value of the metric. </summary>
    public bool Value { get; }
    /// <summary> The time the metric was recorded. </summary>
    public DateTime Time { get; }

    /// <summary> Creates a new instance of the boolean metric. </summary>
    public BooleanMetric(MetricInfo info, bool value, DateTime? time = null) : this(info, value, new(), time)
    {
    }

    /// <summary> Creates a new instance of the boolean metric. </summary>
    public BooleanMetric(MetricInfo info, bool value, Dictionary<string, string> metaData, DateTime? time = null)
    {
        Info = info;
        Value = value;
        MetaData = metaData;
        Time = time ?? DateTime.Now;   
    }
        
    /// <summary> Returns a string representation of the boolean metric. </summary>
    public override string ToString()
    {
        return $"{Info.MetricFullName}: {Value} at {Time}";
    }
        
    object IMetric.Value => Value;
}
