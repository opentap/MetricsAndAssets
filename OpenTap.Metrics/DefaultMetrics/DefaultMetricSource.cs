using System;
using System.Diagnostics;

namespace OpenTap.Metrics.DefaultMetrics;

[Display("Default Metrics")]
public class DefaultMetricSource : IMetricSource
{
    private int processId = Process.GetCurrentProcess().Id;
    private DateTime lastMeasure = DateTime.Now;
    private double lastProcessorTime = MetricUtils.GetCPUUsageForProcessInSeconds(Process.GetCurrentProcess().Id);
    private double lastCpuUsagePct = 0.0;
    private readonly int refreshIntervalSeconds = 5;
    
    [Metric("Memory Usage",  kind: MetricKind.Poll, DefaultPollRate = 10, DefaultEnabled = true)]
    [Unit("B")]
    public double MemoryUsage => MetricUtils.GetMemoryUsageForProcess(processId);


    [Unit("%cores")]
    [Metric("CPU Usage",  kind: MetricKind.Poll, DefaultPollRate = 10, DefaultEnabled = true)]
    public double CpuUsagePercent
    {
        get
        {
            // we monitor CPU usage for a window down to the last 5 seconds.
            // if asked more than once every 5 seconds we will respond with the same value within that window.
            
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - lastMeasure).TotalSeconds;

            // reuse if still within 5 seconds
            if (elapsedSeconds < refreshIntervalSeconds)
                return lastCpuUsagePct;

            // calculate deltas
            var currentProcessorTime = MetricUtils.GetCPUUsageForProcessInSeconds(processId);
            var cpuTimeDelta = currentProcessorTime - lastProcessorTime;

            // compute CPU usage percentage (no core scaling)
            var cpuUsage = (cpuTimeDelta / elapsedSeconds) * 100.0;

            // update stored values
            lastProcessorTime = currentProcessorTime;
            lastMeasure = now;
            lastCpuUsagePct = Math.Max(0, Math.Min(100, cpuUsage)); // clamp to [0, 100]

            return lastCpuUsagePct;
        }
    }

    [Unit("%")]
    [Metric("Disk Utilization",  kind: MetricKind.Poll, DefaultPollRate = 10, DefaultEnabled = true)]
    public double DiskUtilization => MetricUtils.GetDiskUtilization();

}