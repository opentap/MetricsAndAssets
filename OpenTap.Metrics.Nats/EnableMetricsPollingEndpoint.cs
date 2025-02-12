using System.Linq;

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
            var pusher = MetricSinkSettings.Current.OfType<NatsMetricPusher>().FirstOrDefault();
            if (request.Enabled)
            {
                if (pusher == null)
                    MetricSinkSettings.Current.Add(new NatsMetricPusher());
            }
            else
            {
                if (pusher != null)
                    MetricSinkSettings.Current.Remove(pusher);
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
