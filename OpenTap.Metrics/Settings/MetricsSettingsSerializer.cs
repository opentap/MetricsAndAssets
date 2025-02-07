using System;
using System.Xml.Linq;

namespace OpenTap.Metrics.Settings;

public class MetricsSettingsSerializer : TapSerializerPlugin
{
    public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
    {
        if (t.DescendsTo(typeof(IMetricsSettingsItem)))
        {
            var fullPath = node.Element(nameof(MetricsSettingsItem.MetricFullName))?.Value ?? null;
            var metricInfo = MetricManager.GetMetricByName(fullPath);
            if (metricInfo == null)
            {
                var msg = $"Missing metric '{fullPath}'. Has the resource or plugin for this metric been removed?";
                Serializer.PushError(node, msg);
            }
            else
            {
                setter(new MetricsSettingsItem(metricInfo));
            }
            return true;
        }

        return false;
    }

    public override bool Serialize(XElement node, object obj, ITypeData expectedType)
    {
        return false;
    }

    public double Order => 999;
}