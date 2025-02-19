namespace OpenTap.Metrics.Nats
{
    public class EnableMetricsPollingEndpoint
    {
        private readonly TraceSource _log = Log.CreateSource("Metrics Endpoint");
        public EnableMetricsPollingEndpoint()
        {
            RunnerExtension.MapEndpoint<EnableMetricsPollingRequest, EnableMetricsPollingResponse>("SetupMetricsPolling", SetupMetricsPolling);
        }

        private EnableMetricsPollingResponse SetupMetricsPolling(EnableMetricsPollingRequest request)
        {
            if (request.Enabled)
            {
                NatsMetricPusher.Start();
            }
            else
            {
                NatsMetricPusher.Stop();
            }

            return new EnableMetricsPollingResponse
            {
                JetStreamName = NatsMetricPusher.MetricsStreamName,
                Enabled = request.Enabled
            };
        }
    }

    public class EnableMetricsPollingResponse
    {
        public string JetStreamName { get; set; }
        public bool Enabled { get; set; }
    }

    public class EnableMetricsPollingRequest
    {
        public bool Enabled { get; set; }
    }
}
