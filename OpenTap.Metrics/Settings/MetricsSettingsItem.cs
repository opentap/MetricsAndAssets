using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Metrics.Settings;

public interface IMetricsSettingsItem : ITapPlugin
{
    IEnumerable<MetricInfo> Metrics { get; }
    int PollRate { get; }
    bool IsEnabled { get; }
}

[Display("Metric", "The configuration for a specific metric.")]
[Browsable(false)]
public class MetricsSettingsItem : ValidatingObject, IMetricsSettingsItem
{
    // KS8400 uses `ToString()` to get the name of this item
    // Runner gets the `Name` property with reflection
    public override string ToString() => Name;
    static string secondsToReadableString(int v)
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
    
    static string pollRateReadable(int pollRate, bool isDefault) => secondsToReadableString(pollRate) + (isDefault ? " (Default)" : "");
    void InitPollRates()
    {
        List<int> defaultPollRates = [5, 10, 30, 60, 300, 900, 1800, 3600, 7200, 86400];
        if (_specifier.DefaultPollRate != 0 && !defaultPollRates.Contains(_specifier.DefaultPollRate))
        {
            var idx = defaultPollRates.FindIndex(i => i > _specifier.DefaultPollRate);
            if (idx == -1) idx = defaultPollRates.Count;
            defaultPollRates.Insert(idx, _specifier.DefaultPollRate);
        }

        AvailablePollRates = defaultPollRates;
        AvailablePollRateStrings =
            defaultPollRates.Select(x => pollRateReadable(x, x == _specifier.DefaultPollRate)).ToArray();
        {
            if (PollRate == 0) PollRate = _specifier.DefaultPollRate;
            if (PollRate == 0) PollRate = 300;
            var idx = AvailablePollRates.IndexOf(PollRate);
            _pollRateString = AvailablePollRateStrings[idx];
        } 
    }

    #region Serialized Settings
    [Browsable(false)] 
    [DeserializeOrder(1)]
    public int PollRate { get; set; } 
    
    [Browsable(false)]
    [DeserializeOrder(2)]
    public MetricSpecifier Specifier
    {
        get => _specifier;
        set
        {
            _specifier = value;
            InitPollRates(); 
        }
    } 
    #endregion
    #region Displayed Settings 
    [Browsable(false)] public bool CanPoll => Specifier.Kind.HasFlag(MetricKind.Poll); 
    [Display("Enabled", "Whether or not this metric should be polled.", Order: 0.9)]
    public bool IsEnabled { get; set; } = true;
    [Browsable(true)]
    [Display("Group", "The group of this metric.", Order: 1.0)]
    public string MetricGroup => Specifier.Group;
    [Browsable(true)]
    [Display("Name", "The name of this metric.", Order: 1.1)]
    public string Name => Specifier.Name ?? "Unknown";
    
    /// <summary> The type of this metric. </summary>
    [Browsable(true)]
    [Display("Type", "The type of this metric.", Order: 3)]
    public MetricType Type => Specifier.Type;
   
    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    [Display("Kind", "The kind of this metric.", Order: 4), Browsable(true)]
    public MetricKind Kind => Specifier.Kind; 

    [Browsable(false)]
    [XmlIgnore]
    public List<int> AvailablePollRates { get; private set; } 

    [Browsable(false)]
    [XmlIgnore]
    public string[] AvailablePollRateStrings { get; private set; }

    private string _pollRateString;
    private MetricSpecifier _specifier = new();

    [Display("Poll Rate", "The poll rate of this metric.", Order: 5)]
    [AvailableValues(nameof(AvailablePollRateStrings))]
    [EnabledIf(nameof(CanPoll)), XmlIgnore, Browsable(true)]
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

    private string MetricSourceName(MetricInfo m)
    {
        string str = (m.Source as IResource)?.Name;
        if (string.IsNullOrWhiteSpace(str))
            str = m.Member.DeclaringType.GetDisplayAttribute().GetFullName();
        return str;
    }
    [Display("Current Sources", "The sources that currently provide this metric.", Order: 6)]
    [Browsable(true)]
    public string CurrentSources => string.Join(", ", Metrics.Select(MetricSourceName));
    #endregion

    public IEnumerable<MetricInfo> Metrics => MetricManager.GetMetricInfos().Where(Specifier.Matches); 

    public MetricsSettingsItem(MetricSpecifier d)
    {
        Specifier = d;
    } 
    public MetricsSettingsItem()
    {
    }

    protected override string GetError(string propertyName = null)
    {
        if (propertyName == nameof(Name))
        {
            if (MetricsSettings.Current.Any(m => !ReferenceEquals(this, m) && (m as MetricsSettingsItem)?.Specifier.Equals(Specifier) == true))
                return "Metric has duplicate entries.";
        }
        else if (propertyName == nameof(CurrentSources))
        {
            try
            {
                var sources = CurrentSources;
                if (string.IsNullOrWhiteSpace(sources))
                {
                    var availableFrom = TypeData.GetDerivedTypes<IMetricSource>()
                        .Where(m => m.GetMetricSpecifiers().Contains(Specifier))
                        .Select(td => td.GetDisplayAttribute().GetFullName());
                    var str = string.Join("\n", availableFrom);
                    return $"No sources configured. This metric is available from the following instruments:\n{str}";
                }
            }
            catch (Exception ex)
            {
                return $"Error getting sources: '{ex.Message}'.";
            }
        }

        return null;
    } 
}