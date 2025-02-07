using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Metrics.Settings;

public interface IMetricsSettingsItem : ITapPlugin
{
    public MetricInfo Metric { get; }
    public int PollRate { get; }
}

[Display("Metric", "The configuration for a specific metric.")]
[Browsable(false)]
public class MetricsSettingsItem : ValidatingObject, IMetricsSettingsItem
{
    // KS8400 uses `ToString()` to get the name of this item
    // Runner gets the `Name` property with reflection
    public override string ToString() => Name;

    [Browsable(true)]
    [Display("Group", "The group of this metric.", Order: 1.0)]
    public string MetricGroup => Metric?.GroupName ?? "Unknown";
    [Browsable(true)]
    [Display("Name", "The name of this metric.", Order: 1.1)]
    public string Name => Metric?.Name ?? "Unknown"; 

    /// <summary> The type of this metric. </summary>
    [Browsable(true)]
    [Display("Type", "The type of this metric.", Order: 3)]
    public MetricType Type => Metric.Type;

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    [Display("Kind", "The kind of this metric.", Order: 4), Browsable(true)]
    public MetricKind Kind => Metric.Kind;

    [Browsable(false)]
    [XmlIgnore]
    public List<int> AvailablePollRates { get; private set; }

    private string secondsToReadableString(int v)
    {
        var unit = "Second";
        if (v > 0 && v % 60 == 0)
        {
            v /= 60;
            unit = "Minute";

            if (v % 60 == 0)
            {
                v /= 60;
                unit = "Hour";

                if (v % 24 == 0)
                {
                    v /= 24;
                    unit = "Day";
                }
            }
        }

        var plural = v != 1;
        return plural ? $"Every {v} {unit}s" : $"Every {unit}";
    }

    [Browsable(false)] public bool CanPoll { get; } 
    [Browsable(false)] public int PollRate { get; private set; } 

    [Browsable(false)]
    [XmlIgnore]
    public string[] AvailablePollRateStrings { get; private set; }

    private string _pollRateString;

    [Display("Poll Rate", "The poll rate of this metric.", Order: 5)]
    [AvailableValues(nameof(AvailablePollRateStrings))]
    [EnabledIf(nameof(CanPoll))]
    public string PollRateString
    {
        get => _pollRateString;
        set
        {
            var idx = AvailablePollRateStrings.IndexOf(value);
            if (idx == -1)
                return;
            _pollRateString = value;
            PollRate = AvailablePollRates[idx];
        }
    }


    [Browsable(false)]
    [XmlIgnore]
    public MetricInfo Metric { get; }
    
    /// <summary>
    /// Only needed for serialization
    /// </summary>
    [Browsable(false)]
    public string MetricFullName { get; set; }

    public MetricsSettingsItem(MetricInfo metric)
    {
        Metric = metric;
        MetricFullName = metric.MetricFullName;
        CanPoll = metric.Kind.HasFlag(MetricKind.Poll);

        if (CanPoll)
        {
            List<int> defaultPollRates = [5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 86400];

            if (metric.DefaultPollRate != 0)
            {
                var insertAt = defaultPollRates.FindIndex(i => i > metric.DefaultPollRate);
                if (insertAt == -1) insertAt = defaultPollRates.Count;
                defaultPollRates.Insert(insertAt, metric.DefaultPollRate);
            }

            AvailablePollRates = defaultPollRates;
            AvailablePollRateStrings = getPollRateStrings(defaultPollRates);
            var pollRate = Metric.DefaultPollRate == 0 ? 300 : Metric.DefaultPollRate;
            PollRateString = AvailablePollRateStrings[AvailablePollRates.IndexOf(pollRate)];
        }
        else
        {
            AvailablePollRateStrings = ["Disabled"];
            PollRateString = AvailablePollRateStrings[0];
        }
    } 
    string[] getPollRateStrings(List<int> pollRates)
    {
        var strings = new string[pollRates.Count];
        for (int i = 0; i < strings.Length; i++)
        {
            var pollRate = pollRates[i];
            var readable = secondsToReadableString(pollRate);
            if (Metric.DefaultPollRate == pollRate) readable += " (Default)";
            strings[i] = readable;
        }

        return strings;
    }
}

[ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
[Display("Metrics", "List of enabled metrics that should be monitored.")]
public class MetricsSettings : ComponentSettingsList<MetricsSettings, IMetricsSettingsItem>, IDeserializedCallback
{
    public override void Initialize()
    {
        // TODO: Handle when new Instruments / DUTs are added to the bench (default metrics should be added)
        // TODO: Handle when Instruments / DUTs are removed from the bench (metrics from such sources should be removed)
        // TODO: Handle when a plugin providing metrics is uninstalled.
        foreach (var defaultMetric in MetricManager.GetMetricInfos().Where(m => m.DefaultEnabled))
        {
            Add(new MetricsSettingsItem(defaultMetric));
        }
    }

    void AddRange(IEnumerable<IMetricsSettingsItem> metrics)
    {
        foreach (var m in metrics)
        {
            Add(m);
        }
    }
    int RemoveWhere(Predicate<IMetricsSettingsItem> match)
    {
        var indices = new List<int>();
        for (int i = 0; i < this.Count; i++)
        {
            if (match(this[i]))
                indices.Add(i);
        }

        indices.Reverse();
        foreach (var idx in indices)
        {
            this.RemoveAt(idx);
        }

        return indices.Count;
    }

    public void OnDeserialized()
    {
        // This is possible when elements are not correctly deserialized, e.g. when 
        RemoveWhere(x => x == null);
    }
}