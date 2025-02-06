﻿using System;
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
    }

    public class NatsMetrics : IMetricSource, IOnPollMetricsCallback
    {
        private static readonly TraceSource log = Log.CreateSource("Metrics");

        [MetaData]
        public string StreamName => NatsMetricPusher.MetricsStreamName;
        [Metric("Storage_Usage", "", DefaultPollRate = 15, DefaultEnabled = true)]
        // [Unit("MB")]
        public double MetricsStreamSize { get; set; }
        [Metric("Storage_Age", "", DefaultPollRate = 15, DefaultEnabled = false)]
        // [Unit("h")]
        public int MetricsStreamAge { get; set; }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            try
            {
                var jsm = RunnerExtension.Connection.CreateJetStreamManagementContext();
                var info = jsm.GetStreamInfo(StreamName);
                MetricsStreamSize = info.State.Bytes / 1024 / 1024;
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
