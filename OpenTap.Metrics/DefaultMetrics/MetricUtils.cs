using System;
using System.Diagnostics;
using System.IO;

namespace OpenTap.Metrics.DefaultMetrics;

static class MetricUtils
{
    public static long GetMemoryUsageForProcess(int processId)
    {
        if (processId == 0)
            return 0;
        try
        {
            var proc = Process.GetProcessById(processId);
            return proc.WorkingSet64;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("'Memory usage' for a process metric failed", ex);
        }
    }
    public static double GetCPUUsageForProcessInSeconds(int processId)
    {
        if (processId == 0)
            return 0.0;
        var proc = Process.GetProcessById(processId);
        return proc.TotalProcessorTime.TotalSeconds;
    }
    
    public static double GetDiskUsage()
    {
        string currentDir = Directory.GetCurrentDirectory();
        string rootPath = Path.GetPathRoot(currentDir);
        var drive = new DriveInfo(rootPath);
        
        double percent = ((double)drive.AvailableFreeSpace / drive.TotalSize) * 100.0;
        return Math.Max(0, Math.Min(100, percent));
    }

}