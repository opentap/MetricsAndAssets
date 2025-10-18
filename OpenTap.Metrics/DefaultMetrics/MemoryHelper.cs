using System;
using System.Linq;
using System.Reflection;

namespace OpenTap.Metrics.DefaultMetrics;

public static class MemoryHelper
{
    // Cached reflection info
    private static readonly MethodInfo? s_getInfoMethod;
    private static readonly PropertyInfo? s_totalAvailableProp;
    private static readonly bool s_isSupported;

    static MemoryHelper()
    {
        try
        {
            // Try to locate GC.GetGCMemoryInfo()
            s_getInfoMethod = typeof(GC).GetMethods( BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == "GetGCMemoryInfo"&& x.GetParameters().Length == 0);

            if (s_getInfoMethod != null)
            {
                // Create an instance so we can find its property
                var infoType = s_getInfoMethod.ReturnType;
                s_totalAvailableProp = infoType.GetProperty(
                    "TotalAvailableMemoryBytes",
                    BindingFlags.Public | BindingFlags.Instance);

                s_isSupported = s_totalAvailableProp != null;
            }
        }
        catch
        {
            s_isSupported = false;
        }
    }

    /// <summary>
    /// Gets the GC's total available memory in bytes using reflection.
    /// Returns null if the API isn't supported on this runtime - only supported on .net 9.
    /// </summary>
    public static ulong? TryGetTotalAvailableMemoryBytes()
    {
         if (!s_isSupported || s_getInfoMethod == null || s_totalAvailableProp == null)
            return null;

        try
        {
            var info = s_getInfoMethod.Invoke(null, null);
            if (info == null)
                return null;

            var value = s_totalAvailableProp.GetValue(info);
            if (value == null)
                return null;

            // Convert.ChangeType handles boxed numeric conversions
            return Convert.ToUInt64(value);
        }
        catch
        {
            return null;
        }
    }
}