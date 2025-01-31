using System.Linq;

namespace OpenTap.Metrics.Settings;

public class MetricInfoTypeDataProvider : ITypeDataProvider
{
    private static readonly TraceSource log = Log.CreateSource("Metric Serializer");

    public ITypeData GetTypeData(string identifier)
    {
        if (identifier.StartsWith(MetricInfoTypeData.MetricTypePrefix))
        {
            var id = identifier.Substring(MetricInfoTypeData.MetricTypePrefix.Length);
            var info = MetricManager.GetMetricInfos()
                .FirstOrDefault(m => m.MetricFullName == id);
            if (info != null)
            {
                return MetricInfoTypeData.FromMetricInfo(info);
            }

            log.ErrorOnce(identifier, $"Metric '{id}' not found.");
        }

        return null;
    }

    public ITypeData GetTypeData(object obj)
    {
        if (obj is MetricInfo m)
            return MetricInfoTypeData.FromMetricInfo(m);
        return null;
    }

    public double Priority => 999;
}