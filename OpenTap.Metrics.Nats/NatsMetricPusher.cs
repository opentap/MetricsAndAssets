using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NATS.Client.Internals;
using NATS.Client.JetStream;
using Newtonsoft.Json;
using OpenTap.Metrics.Settings;
using OpenTap.Runner.Client;

namespace OpenTap.Metrics.Nats
{
    public class NatsMetricPusher : IMetricsSink
    {
        private TraceSource log = Log.CreateSource("NatsMetricPusher");
        private readonly string MetricsStreamName = "Metric";
        private RunnerExtension runnerConnection;
        private IJetStream _jetStream;


        public NatsMetricPusher()
        {
            TapThread.Start(() =>
            {
                try
                {
                    SetupMetricsStream();
                    StartMetricsPollThread();
                }
                catch (Exception e)
                {
                    log.Error("Error setting up metrics stream");
                    log.Debug(e);
                }
            });
        }

        private void StartMetricsPollThread()
        {
            int seconds = 0;
            while (!TapThread.Current.AbortToken.IsCancellationRequested)
            {
                if (MetricsSettings.Current.Enabled)
                {
                    var pollMetrics = MetricsSettings.Current.Where(s => s.Metric.DefaultPollRate % seconds == 0).Select(p => p.Metric).ToList();
                    if (pollMetrics.Any())
                    {
                        var polledMetrics = MetricManager.PollMetrics(pollMetrics);
                        foreach (var metric in polledMetrics)
                        {
                            StoreMetricOnJetStream(metric);
                        }
                    }

                    seconds++;
                    TapThread.Sleep(1000);
                }
            }
        }

        private void StoreMetricOnJetStream(IMetric metric)
        {
            var cleanFullName = Regex.Replace(metric.Info.MetricFullName, @"[> \.\*]", "_");
            string subject = $"{runnerConnection.BaseSubject}.Metrics.{cleanFullName}";
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
                .WithAllowDirect(true)
                .WithMaxAge(Duration.OfDays(2))
                .Build();
            IList<string> streamNames = jsManagementContext.GetStreamNames();
            StreamInfo streamInfo = streamNames.Contains(metricsStream.Name) ? jsManagementContext.UpdateStream(metricsStream) : jsManagementContext.AddStream(metricsStream);
            log.Info($"Storage '{metricsStream.Name}' is configured. Currently has {streamInfo.State.Messages} messages");
            _jetStream = runnerConnection.Connection.CreateJetStreamContext();
        }
    }

    internal class MetricDto
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
}