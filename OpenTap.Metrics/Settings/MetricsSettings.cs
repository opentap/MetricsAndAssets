using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Metrics.Settings;

public class MetricsSettingsRow : ITapPlugin
{
    private string _selectedMetricString;

    [Display("Metric", "The full name of this metric.", Order: 1)]
    [AvailableValues(nameof(AvailableMetricNames))]
    public string SelectedMetricString
    {
        get => _selectedMetricString;
        set
        {
            _selectedMetricString = value;
            SelectedMetric = getSelectedMetric();
            if (CanPoll)
            {
                PollRateString = AvailablePollRateStrings.FirstOrDefault(s => s.Contains("Default")) ??
                                 AvailablePollRateStrings[SuggestedPollRates.IndexOf(300)];
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
    public MetricType Type => SelectedMetric.Type;

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    [Display("Kind", "The kind of this metric.", Order: 4), Browsable(true)]
    public MetricKind Kind => SelectedMetric.Kind; 

    [Browsable(false)]
    public int[] SuggestedPollRates => new int[] { 5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 86400 }
        .Concat(SelectedMetric.DefaultPollRate == 0 ? [] : [SelectedMetric.DefaultPollRate]).OrderBy(x => x).Distinct().ToArray();

    [Browsable(false)] public bool CanPoll => SelectedMetric.Kind.HasFlag(MetricKind.Poll);
    string[] getSuggestedPollRateStrings()
    {
        if (!CanPoll)
            return ["Disabled"];
        Dictionary<int, string> lookup = new()
        {
            [5] = "Every 5 Seconds",
            [10] = "Every 10 Seconds",
            [30] = "Every 30 Seconds",
            [60] = "Every Minute",
            [300] = "Every 5 Minutes",
            [900] = "Every 15 Minutes",
            [1800] = "Every 30 Minutes", 
            [3600] = "Every Hour", 
            [7200] = "Every 2 Hours", 
            [86400] = "Every Day", 
        };
        var spr = SuggestedPollRates;
        var strings = new string[spr.Length];
        for (int i = 0; i < strings.Length; i++)
        {
            if (lookup.TryGetValue(spr[i], out var s))
            {
                strings[i] = s;
                if (spr[i] == SelectedMetric.DefaultPollRate && SelectedMetric.DefaultPollRate != 0)
                {
                    strings[i] += " (Default)";
                }
            }
            else
            {
                var unit = "Seconds";
                var v = spr[i];
                if (v > 0 && v % 60 == 0)
                {
                    v /= 60;
                    unit = "Minutes";
                }

                if (v > 0 && v % 60 == 0)
                {
                    v /= 60;
                    unit = "Hours";
                }

                if (v > 0 && v % 24 == 0)
                {
                    v /= 24;
                    unit = "Days";
                }
                strings[i] = $"Every {v} {unit} (Default)";
            }
        }

        return strings;
    }

    [Browsable(false)] public int PollRate => SuggestedPollRates[AvailablePollRateStrings.IndexOf(PollRateString)];

    public string[] AvailablePollRateStrings => getSuggestedPollRateStrings();
    [Display("Poll Rate", "The poll rate of this metric.", Order: 5)]
    [AvailableValues(nameof(AvailablePollRateStrings))]
    [EnabledIf(nameof(CanPoll))]
    public string PollRateString { get; set; } 

    private MetricInfo[] getAvailableMetrics()
    {
        var infos = MetricManager.GetMetricInfos().ToList();
        for (int i = infos.Count - 1; i >= 0; i--)
        {
            var m = infos[i];
            if (m.MetricFullName == SelectedMetricString) continue;
            try
            {
                if (MetricsSettings.Current.Any(x => x.SelectedMetric?.MetricFullName == m.MetricFullName))
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
            var idx = IndexOf(SelectedMetricString);
            if (idx != -1)
            {
                return AvailableMetrics[idx];
            }
        }

        return null;

        int IndexOf(string s)
        {
            var av = AvailableMetricNames;
            for (int i = 0; i < av.Length; i++)
            {
                if (av[i] == s)
                    return i;
            }

            return -1;
        }
    }

    [Browsable(false)]
    [XmlIgnore]
    public MetricInfo SelectedMetric { get; set; }
    public MetricsSettingsRow()
    {
        SelectedMetricString = AvailableMetricNames.FirstOrDefault();
    }
}

[ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
[Display("Metrics", "List of enabled metrics that should be monitored.")]
public class MetricsSettings : ComponentSettingsList<MetricsSettings, MetricsSettingsRow>
{
    
}

[Display("First Metric Source")]
public class TestMetricSource : IMetricSource
{
    private int _counter;

    [Metric("Counter", DefaultEnabled = true, DefaultPollRate = 180)]
    public int Counter => _counter++;
    
    [Metric]
    public DateTime CalibrationDate => DateTime.Now;
}

[Display("Other Metric Source")]
public class TestMetricSource2 : IMetricSource
{
    private int _counter;

    [Metric("Counter")]
    public int Counter => _counter++;
    
    [Metric(DefaultPollRate = 86400 * 2)]
    public DateTime CalibrationDate => DateTime.Now;
    
    [Metric(kind: MetricKind.Push)]
    public int MyPollMetric { get; set; }
}
