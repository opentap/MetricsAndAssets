using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using NATS.Client.Internals;
using NATS.Client.JetStream;
using Newtonsoft.Json;
using OpenTap.Metrics.Settings;
using OpenTap.Runner.Client;

namespace OpenTap.Metrics.Nats
{
    public class NatsMetricPusher
    {
        internal const string MetricsStreamName = "Metric";

        private static readonly TraceSource log = Log.CreateSource("Metrics");
        private static readonly NatsMetricPusher instance = new NatsMetricPusher();

        private RunnerExtension runnerConnection;
        private IJetStream _jetStream;
        private Timer timer;

        public static void Start()
        {
            try
            {
                instance.SetupMetricsStream();
                instance.timer = new Timer()
                {
                    Interval = 1000,
                    AutoReset = true
                };
                instance.timer.Elapsed += instance.PollMetrics;
                instance.timer.Start();
                log.Info("Metrics polling started.");
            }
            catch (Exception e)
            {
                log.Error("Error setting up metrics stream");
                log.Debug(e);
            }
        }

        public static void Stop()
        {
            instance.timer.Stop();
            log.Info("Metrics polling stopped.");
        }

        private void PollMetrics(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (MetricsSettings.Current.Any())
                {
                    long seconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                    var pollMetrics = MetricsSettings.Current.Where(s => seconds % s.Metric.DefaultPollRate == 0).Select(p => p.Metric).ToList();
                    if (pollMetrics.Any())
                    {
                        log.Debug($"Polling {pollMetrics.Count} metrics");
                        var polledMetrics = MetricManager.PollMetrics(pollMetrics);
                        foreach (var metric in polledMetrics)
                        {
                            StoreMetricOnJetStream(metric);
                        }
                    }
                    else
                    {
                        log.Debug("No metrics needs to be polled");
                    }
                }
                else
                {
                    log.Debug("No metrics to enabled");
                }
            }
            catch (Exception ex)
            {
                log.Error("Error polling metrics");
                log.Debug(ex);
            }
        }

        private void StoreMetricOnJetStream(IMetric metric)
        {
            var name = escapeSubject(metric.Info.Name);
            var group = escapeSubject(metric.Info.GroupName);
            string subject = $"{runnerConnection.BaseSubject}.Metrics.{group}.{name}";
            MetricDto dto = new MetricDto()
            {
                Name = metric.Info.MetricFullName,
                Value = metric.Value,
                Metadata = metric.MetaData
            };

            if (!runnerConnection.Connection.IsClosed())
                _jetStream.Publish(subject, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto)));
            else
                log.Warning("Connection is closed, not publishing metrics");
        }

        private string escapeSubject(string subject)
        {
            // https://docs.nats.io/nats-concepts/subjects#characters-allowed-and-recommended-for-subject-names
            return subject
                .Replace(" ", "_")
                .Replace(">", "-")
                .Replace("*", "-")
                .Replace(".", "-");
        }

        private void SetupMetricsStream()
        {
            runnerConnection = RunnerExtension.GetConnection();

            var jsManagementContext = runnerConnection.Connection.CreateJetStreamManagementContext();
            StreamConfiguration metricsStream = StreamConfiguration.Builder()
                .WithName(MetricsStreamName)
                .WithStorageType(StorageType.File)
                .WithDiscardPolicy(DiscardPolicy.Old)
                .WithSubjects($"{runnerConnection.BaseSubject}.Metrics.>")
                .WithRetentionPolicy(RetentionPolicy.Limits)
                .WithSubjectTransform(new SubjectTransform($"{runnerConnection.BaseSubject}.Metrics.>", "Metric.>"))
                .WithAllowDirect(true)
                .WithMaxAge(Duration.OfDays(2))
                .Build();
            IList<string> streamNames = jsManagementContext.GetStreamNames();
            StreamInfo streamInfo = streamNames.Contains(metricsStream.Name) ? jsManagementContext.UpdateStream(metricsStream) : jsManagementContext.AddStream(metricsStream);
            log.Info($"Storage '{metricsStream.Name}' is configured. Currently has {streamInfo.State.Messages} messages");
            _jetStream = runnerConnection.Connection.CreateJetStreamContext();
        }
    }

    public class NatsMetrics : IMetricSource, IOnPollMetricsCallback
    {
        private static readonly TraceSource log = Log.CreateSource("Metrics");
        private readonly RunnerExtension runnerConnection;

        [MetaData]
        public string StreamName => NatsMetricPusher.MetricsStreamName;
        [Metric("Storage Usage [MB]", "Metrics", DefaultPollRate = 15, DefaultEnabled = true)]
        public double MetricsStreamSize { get; set; }
        [Metric("Storage Age [h]", "Metrics", DefaultPollRate = 15, DefaultEnabled = false)]
        public int MetricsStreamAge { get; set; }

        public NatsMetrics()
        {
            runnerConnection = RunnerExtension.GetConnection();
        }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            try
            {
                var jsm = runnerConnection.Connection.CreateJetStreamManagementContext();
                jsm.GetStreamInfo(StreamName);
                MetricsStreamSize = jsm.GetStreamInfo(StreamName).State.Bytes / 1024 / 1024;
                MetricsStreamAge = (DateTime.Now.ToUniversalTime() - jsm.GetStreamInfo(StreamName).State.FirstTime).Hours;
            }
            catch (Exception e)
            {
                log.Debug("Error polling metrics");
                log.Debug(e);
            }
        }
    }
}
