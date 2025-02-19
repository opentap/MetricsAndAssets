//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;

namespace OpenTap.Metrics;

/// <summary>  A metric. This can either be a DoubleMetric  or a BooleanMetric metric. </summary>
public interface IMetric
{
    /// <summary> The metric information. </summary>
    MetricInfo Info { get; }
    /// <summary>
    /// Metadata about the metric
    /// </summary>
    Dictionary<string, string> MetaData { get; }

    /// <summary> The value of the metric. </summary>
    object Value { get; }

    /// <summary> The time the metric was recorded. </summary>
    DateTime Time { get; }
}
