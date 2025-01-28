using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace OpenTap.Metrics.Settings;

[Display("Enabled Metrics", "List of enabled metrics that should be monitored.")]
[ComponentSettingsLayout(ComponentSettingsLayoutAttribute.DisplayMode.DataGrid)]
public class MetricSettings : ComponentSettingsList<MetricSettings, IMetricInfo>
{ 
    private static readonly TraceSource log = Log.CreateSource("Metric Settings");
    private void removeDuplicates(object snder, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action != NotifyCollectionChangedAction.Replace && args.Action != NotifyCollectionChangedAction.Add)
            return;
        var lst = this.ToArray();
        var toRemove = new List<int>();
        for (int i = 0; i < lst.Length; i++)
        {
            var i1 = lst[i];
            for (int j = i + 1; j < lst.Length; j++)
            {
                var i2 = lst[j];
                if (i1.Equals(i2))
                {
                    log.Info($"Removing duplicate metric info: '{i2.MetricFullName}'");
                    toRemove.Add(j);
                }
            }
        }

        // Remove all distinct duplicates in reverse order.
        foreach (var r in toRemove.OrderByDescending(x => x).Distinct())
        {
            RemoveAt(r);
        }
    }

    public MetricSettings()
    {
        // OpenTAP does not allow us to block duplicates from being added to the list. Therefore we need to remove them isntead.
        // This causes unhandled gui errors in KS8400, but nothing appears to break.
        this.CollectionChanged += removeDuplicates;
    }
}

