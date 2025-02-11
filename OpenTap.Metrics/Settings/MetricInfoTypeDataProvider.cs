using System.Linq;

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataProvider : ITypeDataProvider
{
    private static readonly TraceSource log = Log.CreateSource("Metric Searcher");

    public ITypeData GetTypeData(string identifier)
    {
        if (identifier.StartsWith(MetricInfoTypeData.MetricTypePrefix))
        {
            return TypeData.GetDerivedTypes<IMetricsSettingsItem>().OfType<MetricInfoTypeData>()
                .FirstOrDefault(x => x.Name == identifier);
        }

        return null;
    }

    public ITypeData GetTypeData(object obj)
    {
        return null;
    }

    public double Priority => 999;
}