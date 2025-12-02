using System.Linq;

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataProvider : ITypeDataProvider
{
    private static readonly TraceSource log = Log.CreateSource("Metric Searcher");

    public ITypeData GetTypeData(string identifier)
    {
        if (identifier.StartsWith(MetricInfoTypeData.MetricTypePrefix))
        {
            return TypeData.FromType(typeof(MetricsSettingsItem));
        }

        return null;
    }

    public ITypeData GetTypeData(object obj)
    {
        if (obj is MetricsSettingsItem s && s.Specifier.Type != MetricType.Unknown && s.Specifier.Kind != 0)
            return MetricInfoTypeData.FromMetricSpecifier(s.Specifier);
        return null;
    }

    public double Priority => 999;
}