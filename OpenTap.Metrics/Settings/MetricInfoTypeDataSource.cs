using System.Collections.Generic;
using System.Linq;
using OpenTap.Package;

namespace OpenTap.Metrics.Settings;

class MetricInfoTypeDataSource : ITypeDataSource
{ 
    public static bool TryFromTypeData(ITypeData td, out MetricInfoTypeDataSource src)
    {
        src = null;
        if (td is not MetricInfoTypeData mtd) return false;
        // Because MetricSpecifiers are ambiguous by design (they are intended to merge similar metrics!!)
        // this operation is inherently ambiguous. If a metric is available from different plugins, we will have to 
        // just guess. 
        
        // 1. Find all sources
        var allSources = TypeData.GetDerivedTypes<IMetricSource>().ToArray();
        // 2. Filter the sources list down to the set of sources that provide this metric
        var metricSources = allSources.Where(x => x.GetMetricSpecifiers().Any(spec =>
            spec.Equals(mtd.Specifier))).ToArray();
        // 3. Prefer sources that do not come from a package, if possible
        // If we are currently creating a package, then a source which does not come from a package likely belongs
        // to the package we are currently creating.
        var preferredSource = metricSources.FirstOrDefault(x => Installation.Current.FindPackageContainingType(x) == null) ??
                     metricSources.FirstOrDefault();
        if (preferredSource == null)
            return false;
        var std = preferredSource.AsTypeData();
        if (std?.Assembly == null) return false;
        // 4. Find all sources which originale from the same assembly as this source
        var relatedSources = allSources.Where(x => x.AsTypeData()?.Assembly == std.Assembly)
            .SelectMany(x => x.GetMetricSpecifiers())
            .Select(MetricInfoTypeData.FromMetricSpecifier)
            .ToArray();
        src = new MetricInfoTypeDataSource(std.Assembly.Name, std.Assembly.Location, relatedSources);
        return true;
    }
    private MetricInfoTypeDataSource(string name, string location, IEnumerable<ITypeData> types)
    {
        Name = name;
        Location = location;
        Types = types;
    }
    public string Name { get; }
    public string Location { get; }
    public IEnumerable<ITypeData> Types { get; }
    public IEnumerable<object> Attributes => [];
    public IEnumerable<ITypeDataSource> References => [];
    public string Version => "1.0.0";
}