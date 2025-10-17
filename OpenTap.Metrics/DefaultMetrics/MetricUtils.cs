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
    
    /// <summary> Calculate the pct disk usage of the volume of the 'current directory'.
    /// Note if its on linux or macos, this gives the disk utilization of /. Which may or may not be the same as for the
    /// current directory. </summary>
    public static double GetDiskUtilization()
    {
        string currentDir = Directory.GetCurrentDirectory();
        string rootPath = Path.GetPathRoot(currentDir);
        var drive = new DriveInfo(rootPath);
        
        double percent = ((double)drive.AvailableFreeSpace / drive.TotalSize) * 100.0;
        return Math.Max(0, Math.Min(100, percent));
    }

}