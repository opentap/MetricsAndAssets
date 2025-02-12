using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using OpenTap.Metrics.Settings;

namespace OpenTap.Metrics;

[Display("Metric Sink Settings", "The configured metric sinks. When sinks are enabled, metrics will automatically be polled in their configured intervals.")]
public class MetricSinkSettings : ComponentSettingsList<MetricSinkSettings, IMetricSink>
{
    private static readonly TraceSource log = Log.CreateSource("Metric Sink");
    private Timer timer { get; set; }
    public MetricSinkSettings()
    { 
        timer = new Timer() { Interval = 1000, AutoReset = true };
        timer.Elapsed += Tick;
        timer.Start();
    } 
    ~MetricSinkSettings()
    {
        timer.Stop();
    }
    private void Tick(object sender, ElapsedEventArgs e)
    {
        var sinks = this.ToArray();
        long seconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        if (sinks.Any() && MetricsSettings.Current.Any())
        {
            var pollMetrics = MetricsSettings.Current
                .OfType<MetricsSettingsItem>()
                .Where(s => s.IsEnabled && seconds % s.PollRate == 0)
                .DistinctBy(x => x.Specifier)
                .SelectMany(p => p.Metrics)
                .Where(m => m.Kind.HasFlag(MetricKind.Poll)).ToList();
            if (pollMetrics.Any())
            {
                log.Debug($"Polling {pollMetrics.Count} metrics.");
                var metrics = MetricManager.PollMetrics(pollMetrics).ToArray();
                Parallel.ForEach(sinks, sink =>
                {
                    try
                    {
                        sink.OnMetricsPolled(new MetricsPolledEventArgs() { Metrics = metrics });
                    }
                    catch (Exception ex)
                    {
                        var displayName = TypeData.FromType(sink.GetType()).GetDisplayAttribute().GetFullName();
                        log.Error($"Unhandled error in sink '{displayName}': {ex.Message}");
                        log.Debug(ex);
                    }
                });
            } 
        }
    }
}