using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Metrics.Settings;

public interface IMetricsSettingsItem : ITapPlugin
{
    public MetricInfo Metric { get; }
}

[Display("Metric", "The configuration for a specific metric.")]
public class MetricsSettingsItem : IMetricsSettingsItem
{
    public override string ToString() => Name;
    public string Name => Metric?.MetricFullName ?? "Unknown";
    private string _selectedMetricString;

    [Display("Metric", "The full name of this metric.", Order: 1)]
    [AvailableValues(nameof(AvailableMetricNames))]
    public string SelectedMetricString
    {
        get => _selectedMetricString;
        set
        {
            if (_selectedMetricString == value) return;
            _selectedMetricString = value;
            Metric = getSelectedMetric();
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
    [Display("Type", Description: "The type of this metric.", Order: 3)]
    public MetricType Type => Metric.Type;

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    [Display("Kind", "The kind of this metric.", Order: 4), Browsable(true)]
    public MetricKind Kind => Metric.Kind;

    [Browsable(false)]
    public int[] SuggestedPollRates => new int[] { 5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 86400 }
        .Concat(Metric.DefaultPollRate == 0 ? [] : [Metric.DefaultPollRate]).OrderBy(x => x).Distinct().ToArray();

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

    string[] getSuggestedPollRateStrings()
    {
        if (!CanPoll)
            return ["Disabled"];
        var spr = SuggestedPollRates;
        var strings = new string[spr.Length];
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

    [Browsable(false)] public string[] AvailablePollRateStrings { get; set; }

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

    private MetricInfo[] getAvailableMetrics()
    {
        var infos = MetricManager.GetMetricInfos().ToList();
        for (int i = infos.Count - 1; i >= 0; i--)
        {
            var m = infos[i];
            if (m.MetricFullName == SelectedMetricString) continue;
            try
            {
                if (MetricsSettings.Current.Any(x => x.Metric?.MetricFullName == m.MetricFullName))
                    infos.RemoveAt(i);
            }
            catch
            {
                // ignore
            }
        }

        return infos.ToArray();
    }

    public MetricInfo[] AvailableMetrics => getAvailableMetrics();
    [Browsable(false)] public string[] AvailableMetricNames => AvailableMetrics.Select(m => m.MetricFullName).ToArray();

    private MetricInfo getSelectedMetric()
    {
        if (AvailableMetrics is { Length: > 0 })
        {
            var idx = AvailableMetricNames.IndexOf(SelectedMetricString);
            if (idx >= 0 && idx < AvailableMetrics.Length)
            {
                return AvailableMetrics[idx];
            }
        }

        return null;
    }

    private MetricInfo _metric;

    [Browsable(false)]
    [XmlIgnore]
    public MetricInfo Metric
    {
        get => _metric;
        set
        {
            _metric = value;
            AvailablePollRateStrings = getSuggestedPollRateStrings();
        }
    }

    public MetricsSettingsItem()
    {
        SelectedMetricString = AvailableMetricNames.FirstOrDefault();
    }
}

[ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
[Display("Metrics", "List of enabled metrics that should be monitored.")]
public class MetricsSettings : ComponentSettingsList<MetricsSettings, IMetricsSettingsItem>
{
    private List<IMetricsSink> _sinks = new List<IMetricsSink>();

    public MetricsSettings()
    {
        var metricSinks = TypeData.GetDerivedTypes<IMetricsSink>();
        foreach (var metricSink in metricSinks)
        {
            if (metricSink.CanCreateInstance)
            {
                var sink = (IMetricsSink)metricSink.CreateInstance();
                _sinks.Add(sink);
            }
        }
    }

    [Browsable(false)] public bool Enabled { get; set; } = true;
}