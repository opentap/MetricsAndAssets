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

    private void AddDefaultMetrics()
    {
        // Ensure duts and instruments are both loaded. This is needed because MetricManager only looks at the cached values.
        var _1 = InstrumentSettings.Current;
        var _2 = DutSettings.Current;
        var existing = this.ToArray();
        var settings = TypeData.GetDerivedTypes<IMetricsSettingsItem>();
        foreach (var s in settings.OfType<MetricInfoTypeData>())
        {
            if (s.Specifier.DefaultEnabled == false) continue;
            if (existing.Any(x => s.Specifier.Equals((x as MetricsSettingsItem)?.Specifier))) continue;
            Add(s.CreateInstance() as IMetricsSettingsItem);
        }
    }
}