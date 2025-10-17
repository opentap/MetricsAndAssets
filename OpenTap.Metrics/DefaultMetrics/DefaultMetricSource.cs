using System;
using System.Diagnostics;

namespace OpenTap.Metrics.DefaultMetrics;

[Display("Default Metric Source")]
public class DefaultMetricSource : IMetricSource
{
    private int processId = Process.GetCurrentProcess().Id;

    private DateTime lastMeasure = DateTime.Now;
    private double lastProcessorTime = MetricUtils.GetCPUUsageForProcessInSeconds(Process.GetCurrentProcess().Id);

    
    [Metric("Memory Usage",  kind: MetricKind.Poll, DefaultPollRate = 5, DefaultEnabled = true)]
    [Unit("B")]
    public double MemoryUsage => MetricUtils.GetMemoryUsageForProcess(processId);

    private double lastCpuUsagePct = 0.0;
    private readonly int refreshIntervalSeconds = 5;

    [Unit("%cores")]
    [Metric("CPU Usage",  kind: MetricKind.Poll, DefaultPollRate = 5, DefaultEnabled = true)]
    public double CpuUsagePercent
    {
        get
        {
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
    [Metric("Disk Usage",  kind: MetricKind.Poll, DefaultPollRate = 5, DefaultEnabled = true)]
    public double DiskUsage => MetricUtils.GetDiskUsage();

}