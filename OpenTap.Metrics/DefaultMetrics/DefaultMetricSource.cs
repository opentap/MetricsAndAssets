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
    
    [Metric("Memory Usage", "Process", "The memory usage of the process.", kind: MetricKind.Poll, DefaultPollRate = 10, DefaultEnabled = true)]
    [Unit("MB")]
    public double MemoryUsage => Math.Round(MetricUtils.GetMemoryUsageForProcess(processId) / 1_000_000.0, 2);

    // this metric is only easily available on .net9
    [Metric("Available Memory", "System", "The available memory on the system.", kind: MetricKind.Poll, DefaultPollRate = 10, DefaultEnabled = true)]
    [Unit("MB")]
    public double? AvailableMemory
    {
        get
        {
            // on other platforms than .net9, this just returns null.
            var mem = MemoryHelper.GetTotalFreeMemory();
            if (mem is ulong m)
                return Math.Round(m / 1_000_000.0, 2);
            return null;
        }   
    }


    [Unit("%cores")]
    [Metric("CPU Usage", "Process", "The CPU usage of the process measured in % of one core.",   kind: MetricKind.Poll, DefaultPollRate = 10, DefaultEnabled = true)]
    public double CpuUsagePercent
    {
        get
        {
            // we monitor CPU usage for a window down to the last 5 seconds.
            // if asked more than once every 5 seconds we will respond with the same value within that window.
            
            var now = DateTime.Now;
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
            lastCpuUsagePct = Math.Max(0, Math.Min(100, Math.Round(cpuUsage, 2))); // clamp to [0, 100]

            return lastCpuUsagePct;
        }
    }

    [Unit("GB")]
    [Metric("Available Disk Space", "System", "The available disk space of the volume seen by the current process.",  kind: MetricKind.Poll, DefaultPollRate = 60, DefaultEnabled = true)]
    public double DiskAvailableSpace => Math.Round(MetricUtils.GetAvailableDiskSpace() / 1_000_000_000, 2);
    
    [Unit("GB")]
    [Metric("Used Disk Space", "System", "The used disk space of the volume seen by the current process.",  kind: MetricKind.Poll, DefaultPollRate = 60, DefaultEnabled = false)]
    public double DiskUsedSpace => Math.Round(MetricUtils.GetUsedDiskSpace() / 1_000_000_000, 2);

}