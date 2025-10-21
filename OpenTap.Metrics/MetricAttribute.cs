//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Metrics;

/// <summary> Defines a property as a metric. </summary>
public class MetricAttribute : Attribute
{
    /// <summary> Optionally give the metric a name. </summary>
    public string Name { get; }

    /// <summary> Optionally give the metric a group. </summary>
    public string Group { get; }

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    public MetricKind Kind { get; }

    /// <summary> 
    /// The suggested default poll rate of the metric, in seconds. 
    /// This is a hint to the client. A UI is free to ignore this hint (or round it up/down).
    ///
    /// We recommend one of the following values, if applicable:
    /// 5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 86400
    /// These values will be displayed nicely in all UIs, and polling of different metrics in these intervals
    /// can easily be batched by metric consumers.
    /// </summary>
    public int DefaultPollRate { get; set; }

    /// <summary> 
    /// Suggestion to clients on whether to poll this metric by default. 
    /// This is a hint to the client. A UI is free to ignore this hint.
    /// </summary>
    public bool DefaultEnabled { get; set; } = false;
    
    /// <summary> Optional description of the metric. This may be null.</summary>
    public string Description { get; set; }

    /// <summary> Creates a new instance of the metric attribute </summary>
    ///  <param name="name">Optionally, the name of the metric.</param>
    ///  <param name="group">The group of the metric.</param>
    ///  <param name="kind"> The push / poll semantics of the metric. </param>
    public MetricAttribute(string name = null, string group = null, MetricKind kind = MetricKind.Poll)
    {
        Name = name;
        Group = group;
        Kind = kind;
    }

    /// <summary> Creates a new instance of the metric attribute </summary>
    ///  <param name="name">Optionally, the name of the metric.</param>
    ///  <param name="group">The group of the metric.</param>
    ///  <param name="kind"> The push / poll semantics of the metric. </param>
    /// <param name="description">The description of the metric.</param>
    public MetricAttribute(string name, string group, string description, MetricKind kind = MetricKind.Poll)
    {
        Name = name;
        Group = group;
        Kind = kind;
        Description = description;
    }

    /// <summary> Creates a new instance of the metric attribute.</summary>
    public MetricAttribute() : this(null)
    {
    }
}
