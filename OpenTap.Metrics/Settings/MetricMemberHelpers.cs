using System.Collections.Generic;
using System.Linq;
using OpenTap.Metrics.AssetDiscovery;

namespace OpenTap.Metrics.Settings;

internal static class MetricMemberHelpers
{
    public static int IndexOf<T>(this T[] arr, T elem)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]?.Equals(elem) == true)
                return i;
        return -1;
    }

    public static IEnumerable<IMemberData> GetMetricMembers(this ITypeData td) =>
        td.GetMembers().Where(mem => mem.HasAttribute<MetricAttribute>());

    private static bool IsConcrete(ITypeData td)
    {
        var td2 = td.AsTypeData();
        /* assume non-TypeData are concrete. */
        if (td2 == null) return true;
        return !(td2.Type.IsInterface || td2.Type.IsAbstract);
    }
    
    public static IEnumerable<ITypeData> GetAllMetricSources() =>
        TypeData.GetDerivedTypes<IMetricSource>()
            .Concat(TypeData.GetDerivedTypes<IResource>())
            .Where(x => x.CanCreateInstance)
            /* no need to filter IAsset implementations based on being able to create instances.
             The instances are created by AssetProvider implementations. */
            .Concat(TypeData.GetDerivedTypes<IAsset>().Where(IsConcrete))
            .Distinct();

    public static IEnumerable<IMemberData> GetAllMetricMembers() => GetAllMetricSources().SelectMany(x => x.GetMetricMembers()); 
    public static IEnumerable<MetricSpecifier> GetMetricSpecifiers(this ITypeData td) =>
        td.GetMetricMembers().Select(x => new MetricSpecifier(x));
}