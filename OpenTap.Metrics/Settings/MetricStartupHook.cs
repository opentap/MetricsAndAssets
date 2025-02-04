using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenTap.Package;

namespace OpenTap.Metrics.Settings;

public class MetricStartupHook : IStartupInfo
{
    public void LogStartupInfo()
    {
        MetricInfoTypeDataSearcher.InitialInfos = [.. MetricManager.GetMetricInfos(), ..MetricManager.GetAbstractMetricInfos()];
    }
}

public class EnsureMetricsBrowsableAction : ICustomPackageAction
{
    public int Order() => 999;
    

    public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
    {
        foreach (var file in package.Files)
        {
            foreach (var plugin in file.Plugins)
            {
                if (plugin.Name.StartsWith(MetricInfoTypeData.MetricTypePrefix))
                {
                    plugin.Browsable = true;
                }
            }
        }
        return true;
    }

    public PackageActionStage ActionStage => PackageActionStage.Create;
}
