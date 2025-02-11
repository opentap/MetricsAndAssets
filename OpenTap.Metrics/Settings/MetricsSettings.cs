using System.Collections.Specialized;
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

    public MetricsSettings()
    {
        CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            foreach (var rm in e.OldItems.Cast<IMetricsSettingsItem>() ?? [])
            {
                if (rm is MetricsSettingsItem { Specifier.DefaultEnabled: true } it)
                {
                    MetricsBlockList.Current.Block(it.Specifier);
                }
            }
        }
    }

    public void OnDeserialized()
    {
        AddDefaultMetrics();
    }

    private void AddDefaultMetrics()
    {
        var existing = this.ToArray();
        var settings = TypeData.GetDerivedTypes<IMetricsSettingsItem>();
        foreach (var s in settings.OfType<MetricInfoTypeData>())
        {
            if (s.Specifier.DefaultEnabled == false) continue;
            if (MetricsBlockList.Current.IsBlocked(s.Specifier)) continue;
            if (existing.Any(x => s.Specifier.Equals((x as MetricsSettingsItem)?.Specifier))) continue;
            Add(s.CreateInstance() as IMetricsSettingsItem);
        }
    }
}