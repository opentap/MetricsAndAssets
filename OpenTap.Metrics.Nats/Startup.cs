using System;

namespace OpenTap.Metrics.Nats
{
    public class MetricsPollingStartup : IStartupInfo
    {
        public void LogStartupInfo()
        {
            var log = Log.CreateSource("Metrics");
            {
                log.Info("Setup metrics endpoints");
                try
                {
                    new AssetDiscoveryEndpoint();
                    new EnableMetricsPollingEndpoint();
                }
                catch (Exception e)
                {
                    // This happens e.g. in the Runner process itself when there is no leafnode yet.
                    log.Error("Error setting up metrics endpoints");
                    log.Debug(e);
                }
            }
        }

    }
}
