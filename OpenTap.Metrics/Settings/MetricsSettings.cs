using System.Linq;

namespace OpenTap.Metrics.Settings; 

[ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
[Display("Metrics", "The current metric configuration.")]
public class MetricsSettings : ComponentSettingsList<MetricsSettings, IMetricsSettingsItem>, IDeserializedCallback
{
    public override void Initialize()
    {
        AddDefaultMetrics();
    } 

    public void OnDeserialized()
    {
        AddDefaultMetrics();
    }

    // Use statically discoverable metrics for adding default metric. This is not related to any deadlock issues,
    // but rather it is required because we need to add DefaultMetrics from e.g. resources which are not configured on the bench.
    private MetricSpecifier[] GetStaticMetricInfos() => 
        [.. MetricMemberHelpers.GetAllMetricSources().SelectMany(MetricMemberHelpers.GetMetricSpecifiers).Distinct()];

    private void AddDefaultMetrics()
    {
        bool anyAdded = false;
        // Ensure duts and instruments are both loaded. This is needed because MetricManager only looks at the cached values.
        var _1 = InstrumentSettings.Current;
        var _2 = DutSettings.Current;
        var existing = this.ToArray();
        var settings = TypeData.GetDerivedTypes<IMetricsSettingsItem>();
        foreach (var s in GetStaticMetricInfos())
        {
            if (s.DefaultEnabled == false) continue;
            if (existing.Any(x => s.Equals((x as MetricsSettingsItem)?.Specifier))) continue;
            Add(new MetricsSettingsItem(s));
            anyAdded = true;
        }

        // Save the settings. This is needed to ensure metrics settings are persisted if
        // the plugin providing some metric is removed.
        if (anyAdded) Save();
    }
}
