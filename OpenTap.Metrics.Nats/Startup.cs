namespace OpenTap.Metrics.Nats
{

    public class MetricsPollingStartup : IStartupInfo
    {
        public void LogStartupInfo()
        {
            new NatsMetricPusher();
            new AssetDiscoveryEndpoint();
        }
    }
}
