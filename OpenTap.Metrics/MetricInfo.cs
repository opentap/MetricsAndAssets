//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OpenTap.Metrics;

internal class AbstractMetricInfo : MetricInfo
{
    public new Type Source { get; }
    public AbstractMetricInfo(IMemberData mem, string groupName, Type source) : base(mem, groupName, source)
    {
        Source = source;
    } 
}
/// <summary> Information about a given metric, </summary>
public class MetricInfo
{
    /// <summary> The name of the metric group. </summary>
    [Display("Group Name", "The group of this metric.", Order: 1), Browsable(true)]
    public string GroupName { get; }

    /// <summary> The name of the metric. </summary>
    [Display("Name", "The name of this metric.", Order: 2), Browsable(true)]
    public string Name { get; }

    /// <summary> The type of this metric. </summary>
    [Browsable(true)]
    [Display("Type", Description: "The type of this metric.", Order: 3)]
    public MetricType Type => GetMetricType(Member);

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    [Display("Kind", "The kind of this metric.", Order: 4), Browsable(true)]
    public MetricKind Kind { get; }

    /// <summary> 
    /// The suggested default poll rate for this metric, in seconds. 
    /// This is a hint to the client. A UI is free to ignore this hint (or round it up/down).
    /// </summary>
    [Display("Default Poll Rate", "The suggested poll default poll rate of this metric.", Order: 5), Unit("s"),
     Browsable(true)]
    public int DefaultPollRate { get; }

    /// <summary> The object that produces this metric. </summary>
    public object Source { get; }

    /// <summary> The metric member object. </summary>
    internal IMemberData Member { get; }

    /// <summary> The attributes of the metric. </summary>
    public IEnumerable<object> Attributes { get; }

    /// <summary> Gets the full name of the metric. </summary>
    public string MetricFullName => $"{GroupName} / {Name}";


    /// <summary> Indicates if the metric is available. </summary>
    public bool IsAvailable { get; internal set; }

    /// <summary> 
    /// Suggestion to clients on whether to poll this metric by default. 
    /// This is a hint to the client. A UI is free to ignore this hint.
    /// </summary>
    public bool DefaultEnabled { get; protected set; } = false;

    /// <summary> Creates a new metric info based on a member name. </summary>
    /// <param name="mem">The metric member object.</param>
    /// <param name="groupName">The name of the metric group.</param>
    /// <param name="source">The object that produces this metric.</param>
    public MetricInfo(IMemberData mem, string groupName, object source)
    {
        Member = mem;
        GroupName = groupName;
        Attributes = Member.Attributes.ToArray();
        var metricAttr = Attributes.OfType<MetricAttribute>().FirstOrDefault();
        Kind = metricAttr?.Kind ?? MetricKind.Poll;
        Name = metricAttr?.Name ?? Member.GetDisplayAttribute()?.Name;
        Source = source;
        IsAvailable = true;
        DefaultPollRate = metricAttr?.DefaultPollRate ?? 0;
        DefaultEnabled = metricAttr?.DefaultEnabled ?? false;
    }

    /// <summary> Creates a new metric info based on custom data. </summary>
    /// <param name="name">The name of the metric.</param>
    /// <param name="groupName">The name of the metric group.</param>
    /// <param name="attributes">The attributes of the metric.</param>
    ///  <param name="kind">The push / poll semantics of the metric. </param>
    /// <param name="source">The object that produces this metric.</param>
    /// <param name="defaultPollRate">Optional suggested poll rate of the metric, in seconds.</param>
    /// <param name="suggestedInitialState">Optionally indicate the suggested initial state of the metric.</param>
    public MetricInfo(string name, string groupName, IEnumerable<object> attributes, MetricKind kind, object source,
        int defaultPollRate, bool defaultEnabled)
    {
        Name = name;
        Member = null;
        GroupName = groupName;
        Attributes = attributes;
        Kind = kind;
        Source = source;
        IsAvailable = true;
        DefaultPollRate = defaultPollRate;
        DefaultEnabled = defaultEnabled;
    }

    /// <summary> Creates a new metric info based on custom data. </summary>
    /// <param name="name">The name of the metric.</param>
    /// <param name="groupName">The name of the metric group.</param>
    /// <param name="attributes">The attributes of the metric.</param>
    ///  <param name="kind">The push / poll semantics of the metric. </param>
    /// <param name="source">The object that produces this metric.</param>
    public MetricInfo(string name, string groupName, IEnumerable<object> attributes, MetricKind kind, object source)
    {
        Name = name;
        Member = null;
        GroupName = groupName;
        Attributes = attributes;
        Kind = kind;
        Source = source;
        IsAvailable = true;
        DefaultPollRate = 0;
    }

    /// <summary>
    /// Provides name for the metric.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"Metric: {MetricFullName}";

    /// <summary>
    /// Implements equality for metric info.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj is MetricInfo o)
            return string.Equals(GroupName, o.GroupName, StringComparison.Ordinal) &&
                   string.Equals(Name, o.Name, StringComparison.Ordinal) &&
                   Equals(Member, o.Member) &&
                   Equals(Source, o.Source) &&
                   Equals(IsAvailable, o.IsAvailable);

        return false;
    }

    /// <summary>
    /// Hash code for metrics.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        var hc = HashCode.Combine(Name.GetHashCode(), GroupName?.GetHashCode() ?? 0, Member?.GetHashCode());
        return HashCode.Combine(Source?.GetHashCode(), hc, 5639212);
    }

    /// <summary> Gets the value of the metric. </summary>
    public object GetValue(object metricSource)
    {
        return Member?.GetValue(metricSource);
    }

    /// <summary>
    /// Gets the metric type for all supported types including nullable.
    /// </summary>
    private MetricType GetMetricType(IMemberData memberData)
    {
        if (memberData == null) return MetricType.Unknown;
        return memberData.TypeDescriptor switch
        {
            var d when d.IsNumeric() => MetricType.Double,
            var d when d.DescendsTo(typeof(string)) => MetricType.String,
            var d when d.DescendsTo(typeof(bool)) => MetricType.Boolean,
            var d when d.DescendsTo(typeof(Nullable<>)) => GetNullableMetricType(d),
            var d when d.DescendsTo(typeof(IConvertible)) => MetricType.String,
            _ => MetricType.Unknown
        };

        static MetricType GetNullableMetricType(ITypeData typeData)
        {
            var underlyingType = Nullable.GetUnderlyingType(typeData.AsTypeData().Type);
            return underlyingType switch
            {
                var d when d.IsNumeric() => MetricType.Nullable | MetricType.Double,
                var d when d == typeof(bool) => MetricType.Nullable | MetricType.Boolean,
                _ => MetricType.Unknown
            };
        }
    }
}