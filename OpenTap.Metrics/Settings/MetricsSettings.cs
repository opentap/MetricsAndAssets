using System.Collections.Generic;
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
    public string Name => Metric?.Name ?? "Unknwon";

    private string _selectedMetricString;
    [Display("Metric", "The full name of this metric.", Order: 1)]
    [Browsable(false)]
    public string SelectedMetricString
    {
        get => _selectedMetricString;
        set
        {
            if (_selectedMetricString == value) return;
            _selectedMetricString = value;
            Metric = MetricManager.GetMetricByName(value);
            if (CanPoll)
            {
                var pollrate = Metric.DefaultPollRate == 0 ? 300 : Metric.DefaultPollRate;
                PollRateString = AvailablePollRateStrings[SuggestedPollRates.IndexOf(pollrate)];
            }
            else
            {
                PollRateString = AvailablePollRateStrings[0];
            }
        }
    }

    /// <summary> The type of this metric. </summary>
    [Browsable(true)]
    [Display("Type", "The type of this metric.", Order: 3)]
    public MetricType Type => Metric.Type;

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    [Display("Kind", "The kind of this metric.", Order: 4), Browsable(true)]
    public MetricKind Kind => Metric.Kind;

    [Browsable(false)]
    [XmlIgnore]
    public List<int> SuggestedPollRates { get; private set; }

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

    [Browsable(false)] public bool CanPoll => Metric?.Kind.HasFlag(MetricKind.Poll) ?? false;
    string[] getPollRates()
    {
        if (!CanPoll)
            return ["Disabled"];
        var spr = SuggestedPollRates;
        var strings = new string[spr.Count];
        for (int i = 0; i < strings.Length; i++)
        {
            var pollrate = spr[i];
            var readable = secondsToReadableString(pollrate);
            if (Metric.DefaultPollRate == pollrate) readable += " (Default)";
            strings[i] = readable;
        }

        return strings;
    }

    [Browsable(false)] public int PollRate => SuggestedPollRates[AvailablePollRateStrings.IndexOf(PollRateString)];

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
            if (!AvailablePollRateStrings.Contains(value))
                return;
            _pollRateString = value;
        }
    }


    private MetricInfo _metric;

    [Browsable(false)]
    [XmlIgnore]
    public MetricInfo Metric
    {
        get => _metric;
        private set
        {
            if (Equals(_metric, value)) return;
            _metric = value;
            List<int> defaultPollRates = [5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 86400];
            if (_metric.DefaultPollRate != 0)
            {
                var insertAt = defaultPollRates.FindIndex(i => i > _metric.DefaultPollRate);
                if (insertAt == -1) insertAt = defaultPollRates.Count;
                defaultPollRates.Insert(insertAt, _metric.DefaultPollRate);
            }
            SuggestedPollRates = defaultPollRates;
            AvailablePollRateStrings = getPollRates();
        }
    }

    public MetricsSettingsItem()
    {
    }

    public MetricsSettingsItem(MetricInfo metric)
    {
        SelectedMetricString = metric.MetricFullName;
    }

    protected override string GetError(string propertyName = null)
    {
        if (propertyName == nameof(SelectedMetricString))
        {
            if (MetricsSettings.Current.Any(m => !ReferenceEquals(this, m) && m.Metric?.MetricFullName == Metric?.MetricFullName))
                return "Metric has duplicate entries.";
        }

        return null;
    }
}

[ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
[Display("Metrics", "List of enabled metrics that should be monitored.")]
public class MetricsSettings : ComponentSettingsList<MetricsSettings, IMetricsSettingsItem>
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
}
