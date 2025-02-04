namespace OpenTap.Metrics.Settings;

public class MetricStartupHook : IStartupInfo
{
    public void LogStartupInfo()
    {
        MetricInfoTypeDataSearcher.InitialInfos = [.. MetricManager.GetMetricInfos(), ..MetricManager.GetAbstractMetricInfos()];
    }
}