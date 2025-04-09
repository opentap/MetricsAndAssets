using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

    public static IEnumerable<ITypeData> GetAllMetricSources() =>
        TypeData.GetDerivedTypes<IMetricSource>()
            .Concat(TypeData.GetDerivedTypes<IResource>())
            .Where(x => x.CanCreateInstance);

    public static IEnumerable<IMemberData> GetAllMetricMembers() => GetAllMetricSources().SelectMany(x => x.GetMetricMembers()); 
    public static IEnumerable<MetricSpecifier> GetMetricSpecifiers(this ITypeData td) =>
        td.GetMetricMembers().Select(x => new MetricSpecifier(x));
}