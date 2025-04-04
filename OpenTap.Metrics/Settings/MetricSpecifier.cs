using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics.Settings;

public class MetricSpecifier : IEquatable<MetricSpecifier>
{
    public bool Matches(MetricInfo i)
    {
        return new MetricSpecifier(i.Member).Equals(this);
    }
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Group)) return $"{Type} \\ {Name}";
        return $"{Type} \\ {Group} \\ {Name}";
    } 

    public string Name { get; set; }
    public string Group { get; set; }
    public MetricType Type { get; set; }
    public MetricKind Kind => _mergedMetricInfo.Kind;
    public bool DefaultEnabled => _mergedMetricInfo.DefaultEnabled;
    public List<int> DefaultPollRates => _mergedMetricInfo.DefaultPollRates;

    public MetricSpecifier(IMemberData x)
    {
        var attr = x.GetAttribute<MetricAttribute>();
        Name = attr.Name ?? x.GetDisplayAttribute().Name;
        Group = string.IsNullOrWhiteSpace(attr.Group) ? null : attr.Group;
        Type = MetricInfo.GetMemberMetricType(x);
        // Consider two metrics equal regardless of nullability
        if (Type.HasFlag(MetricType.Nullable))
            Type -= MetricType.Nullable;
    }

    public MetricSpecifier()
    { 
    }


    public override bool Equals(object obj)
    {
        return ReferenceEquals(this, obj) || obj is MetricSpecifier other && Equals(other);
    }
    
    public bool Equals(MetricSpecifier other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Group == other.Group && Type == other.Type;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Name != null ? Name.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Group != null ? Group.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (int)Type;
            return hashCode;
        }
    }

    private class MergedMetricInfo
    {
        public bool DefaultEnabled;
        public List<int> DefaultPollRates = new();
        public MetricKind Kind;
    }

    private static readonly ConcurrentDictionary<MetricSpecifier, MergedMetricInfo> _cache = new();

    private MergedMetricInfo _mergedMetricInfo => _cache.GetOrAdd(this, static x =>
    {
        var mmi = new MergedMetricInfo();
        var metricSpecifiers = TypeData.GetDerivedTypes<IMetricSource>().SelectMany(src => src.GetMetricMembers())
            .Where(x2 => new MetricSpecifier(x2).Equals(x))
            .ToArray();
        foreach (var g in metricSpecifiers)
        {
            var attr = g.GetAttribute<MetricAttribute>();
            if (attr.DefaultPollRate != 0) mmi.DefaultPollRates.Add(attr.DefaultPollRate);
            if (attr.DefaultEnabled) mmi.DefaultEnabled = true;
            if (attr.Kind.HasFlag(MetricKind.Poll)) mmi.Kind |= MetricKind.Poll;
            if (attr.Kind.HasFlag(MetricKind.Push)) mmi.Kind |= MetricKind.Push;
        }

        mmi.DefaultPollRates = mmi.DefaultPollRates.Distinct().ToList();
        mmi.DefaultPollRates.Sort();

        return mmi;
    }); 
}
