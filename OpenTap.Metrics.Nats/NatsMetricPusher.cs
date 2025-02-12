using System;
using System.Collections.Generic;
using System.Text;
using NATS.Client.Internals;
using NATS.Client.JetStream;
using Newtonsoft.Json;

namespace OpenTap.Metrics.Nats
{
    public class NatsMetricPusher : IMetricSink
    {
        public NatsMetricPusher()
        {
            SetupMetricsStream();
        }
        internal const string MetricsStreamName = "Metric";

        private static readonly TraceSource log = Log.CreateSource("Metrics");

        private IJetStream _jetStream; 
        private void StoreMetricOnJetStream(IMetric metric)
        {
            var name = escapeSubject(metric.Info.Name);
            var group = escapeSubject(metric.Info.GroupName);
            string subject = $"{RunnerExtension.BaseSubject}.Metrics.{group}.{name}";
            MetricDto dto = new MetricDto()
            {
                Name = metric.Info.MetricFullName + (metric.Info.Unit != null ? $" [{metric.Info.Unit}]" : ""),
                Value = metric.Value,
                Metadata = metric.MetaData
            };

            if (!RunnerExtension.Connection.IsClosed())
                _jetStream.Publish(subject, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto)));
            else
                log.Warning("Connection is closed, not publishing metrics");
        }

        private string escapeSubject(string subject)
        {
            if (subject == "")
            {
                // an empty string is not a valid subject token
                return "EMPTY";
            }
            // https://docs.nats.io/nats-concepts/subjects#characters-allowed-and-recommended-for-subject-names
            return subject
                .Replace(" ", "_")
                .Replace(">", "-")
                .Replace("*", "-")
                .Replace(".", "-");
        }

        private void SetupMetricsStream()
        {
            var jsManagementContext = RunnerExtension.Connection.CreateJetStreamManagementContext();
            StreamConfiguration metricsStream = StreamConfiguration.Builder()
                .WithName(MetricsStreamName)
                .WithStorageType(StorageType.File)
                .WithDiscardPolicy(DiscardPolicy.Old)
                .WithSubjects($"{RunnerExtension.BaseSubject}.Metrics.>")
                .WithRetentionPolicy(RetentionPolicy.Limits)
                .WithSubjectTransform(new SubjectTransform($"{RunnerExtension.BaseSubject}.Metrics.>", "Metric.>"))
                .WithAllowDirect(true)
                .WithMaxAge(Duration.OfDays(2))
                .Build();
            IList<string> streamNames = jsManagementContext.GetStreamNames();
            StreamInfo streamInfo = streamNames.Contains(metricsStream.Name) ? jsManagementContext.UpdateStream(metricsStream) : jsManagementContext.AddStream(metricsStream);
            log.Info($"Storage '{metricsStream.Name}' is configured. Currently has {streamInfo.State.Messages} messages");
            _jetStream = RunnerExtension.Connection.CreateJetStreamContext();
        }

        public void OnMetricsPolled(MetricsPolledEventArgs e)
        {
            foreach (var metric in e.Metrics)
            {
                StoreMetricOnJetStream(metric);
            }
        }
    }

    public class NatsMetrics : IMetricSource, IOnPollMetricsCallback
    {
        private static readonly TraceSource log = Log.CreateSource("Metrics");

        [MetaData]
        public string StreamName => NatsMetricPusher.MetricsStreamName;
        [Metric("Usage (MB)", "Runner Metric Storage", DefaultPollRate = 15, DefaultEnabled = true)]
        // [Unit("MB")]
        public double MetricsStreamSize { get; set; }
        [Metric("Age (Hours)", "Runner Metric Storage", DefaultPollRate = 15, DefaultEnabled = false)]
        // [Unit("h")]
        public int MetricsStreamAge { get; set; }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            try
            {
                var jsm = RunnerExtension.Connection.CreateJetStreamManagementContext();
                var info = jsm.GetStreamInfo(StreamName);
                MetricsStreamSize = (double)info.State.Bytes / (1024 * 1024);
                MetricsStreamAge = (DateTime.Now.ToUniversalTime() - info.State.FirstTime).Hours;
            }
            catch (Exception e)
            {
                log.Debug("Error polling metrics");
                log.Debug(e);
            }
        }
    }
}
