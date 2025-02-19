//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap.Metrics;

/// <summary>  A string metric. </summary>
public readonly struct StringMetric : IMetric
{
    /// <summary> The metric information. </summary>
    public MetricInfo Info { get; }

    public Dictionary<string, string> MetaData { get; }

    /// <summary> The value of the metric. </summary>
    public string Value { get; }

    /// <summary> The time the metric was recorded. </summary>
    public DateTime Time { get; }
        
    /// <summary> Creates a new instance of the double metric. </summary>
    public StringMetric(MetricInfo info, string value, DateTime? time = null) : this(info, value, new(), time)
    {
    }

    /// <summary> Creates a new instance of the double metric. </summary>
    public StringMetric(MetricInfo info, string value, Dictionary<string, string> metaData, DateTime? time = null)
    {
        Value = value;
        MetaData = metaData;
        Info = info;
        Time = time ?? DateTime.Now;
    }

    /// <summary> Returns a string representation of the boolean metric. </summary>
    public override string ToString()
    {
        return $"{Info.MetricFullName}: {Value} at {Time}";
    }

    object IMetric.Value => Value;
}
