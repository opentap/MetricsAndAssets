using System;

namespace OpenTap.Metrics.Settings;

public class MetricSpecifier : IEquatable<MetricSpecifier>
{
    public bool Matches(MetricInfo i)
    {
        var attr = i.Member.GetAttribute<MetricAttribute>();
        var grp = string.IsNullOrWhiteSpace(attr.Group) ? null : attr.Group;
        return i.Name == Name && grp == Group && i.Type.HasFlag(Type);
    }
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Group)) return $"{Type} \\ {Name}";
        return $"{Type} \\ {Group} \\ {Name}";
    }

    public string Name { get; set; }
    public string Group { get; set; }
    public MetricType Type { get; set; }
    public MetricKind Kind { get; set; }
    public bool DefaultEnabled { get; set; }
    public int DefaultPollRate { get; set; }

    public MetricSpecifier(string name, string group, MetricType type)
    {
        Name = name;
        Group = group;
        Type = type;
    }
    public MetricSpecifier()
    { 
    }

    public bool Equals(MetricSpecifier other)
    {
        return Name == other.Name && Group == other.Group && Type == other.Type;
    }

    public override bool Equals(object obj)
    {
        return obj is MetricSpecifier other && Equals(other);
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
}