using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using OpenTap.Metrics.Settings;
using Timer = System.Timers.Timer;

namespace OpenTap.Metrics;

// public class MetricsPollingStartup : IStartupInfo
// {
//     public void LogStartupInfo()
//     {
//         AutomaticMetricPoller.Start();
//     }
// }

class AutomaticMetricPoller
{
    private static long pollToken = 0;
    private static List<IMetricSink> Sinks = new();

    private static readonly TraceSource log = Log.CreateSource("Metric Poller");
    private static void UpdateSinks()
    {
        var sinkTypes = TypeData.GetDerivedTypes<IMetricSink>()
            .Where(s => s.CanCreateInstance)
            .Select(s => s.AsTypeData())
            .Where(s => s != null)
            .ToArray();
        var newSinks = Sinks.ToList();
        foreach (var t in sinkTypes)
        {
            if (newSinks.Any(s => s.GetType() == t.Type))
                continue;
            try
            {
                if (t.CreateInstance() is IMetricSink sink)
                    newSinks.Add(sink);
            }
            catch (Exception ex)
            {
                var displayName = t.GetDisplayAttribute().GetFullName();
                if(log.ErrorOnce(t.Type, $"Error instantiating sink '{displayName}': {ex.Message}"))
                    log.Debug(ex); 
            }
        }

        Sinks = newSinks;
    }
    public static void Start()
    {
        var token = Interlocked.Increment(ref pollToken);
        if (token != 1) return;
        TapThread.WithNewContext(() =>
        {
            TapThread.Start(() =>
            {
                PluginManager.PluginsChanged += (x, e) => UpdateSinks();
                UpdateSinks();
                var timer = new Timer() { Interval = 1000, AutoReset = true };
                timer.Elapsed += Tick;
                timer.Start();
                TapThread.Current.AbortToken.Register(() => timer.Stop());
            }, "Metric Poller");
        });
    }

    private static void Tick(object sender, ElapsedEventArgs e)
    {
        long seconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        if (Sinks.Any() && MetricsSettings.Current.Any())
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
                Parallel.ForEach(Sinks, sink =>
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