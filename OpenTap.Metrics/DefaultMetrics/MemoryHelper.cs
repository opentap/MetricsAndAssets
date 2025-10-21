using System;
using System.Linq;
using System.Reflection;

namespace OpenTap.Metrics.DefaultMetrics;

using System;
using System.Runtime.InteropServices;

public static class MemoryInfo
{
    public static ulong GetTotalFreeMemory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetFreeMemoryWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetFreeMemoryLinux();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return GetFreeMemoryMac();
        
        throw new PlatformNotSupportedException("Unsupported OS platform");
    }

    // --- Windows ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static ulong GetFreeMemoryWindows()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
            return memStatus.ullAvailPhys;
        throw new InvalidOperationException("Unable to get memory info on Windows");
    }

    // --- Linux ---
    private static ulong GetFreeMemoryLinux()
    {
        string[] lines = System.IO.File.ReadAllLines("/proc/meminfo");
        ulong memAvailableKb = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("MemAvailable:"))
            {
                string value = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
                memAvailableKb = ulong.Parse(value);
                break;
            }
        }
        return memAvailableKb * 1024; // convert to bytes
    }

    // --- macOS ---
    private static ulong GetFreeMemoryMac()
    {
        var vmStats = new VmStatistics64();
        int count = Marshal.SizeOf(vmStats) / sizeof(int);
        int result = host_statistics64(mach_host_self(), HOST_VM_INFO64, ref vmStats, ref count);
        if (result != 0)
            throw new InvalidOperationException("Unable to get memory info on macOS");
        
        ulong pageSize;
        host_page_size(mach_host_self(), out pageSize);

        ulong freeMem = (vmStats.free_count + vmStats.inactive_count) * pageSize;
        return freeMem;
    }

    private const int HOST_VM_INFO64 = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct VmStatistics64
    {
        public uint free_count;
        public uint active_count;
        public uint inactive_count;
        public uint wire_count;
        public ulong zero_fill_count;
        public ulong reactivations;
        public ulong pageins;
        public ulong pageouts;
        public ulong faults;
        public ulong cow_faults;
        public ulong lookups;
        public ulong hits;
        public ulong purges;
        public uint purgeable_count;
        public uint speculative_count;
        public ulong decompressions;
        public ulong compressions;
        public ulong swapins;
        public ulong swapouts;
        public uint compressor_page_count;
        public uint throttled_count;
        public uint external_page_count;
        public uint internal_page_count;
        public uint total_uncompressed_pages_in_compressor;
    }

    [DllImport("libSystem.dylib")]
    private static extern int host_statistics64(IntPtr host, int flavor, ref VmStatistics64 stat, ref int count);

    [DllImport("libSystem.dylib")]
    private static extern IntPtr mach_host_self();

    [DllImport("libSystem.dylib")]
    private static extern int host_page_size(IntPtr host, out ulong pageSize);
}
