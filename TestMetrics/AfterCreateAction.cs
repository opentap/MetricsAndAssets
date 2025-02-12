using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenTap;
using OpenTap.Metrics.Settings;
using OpenTap.Package;

namespace TestMetrics;

public class AfterCreateAction : ICustomPackageAction
{
    public int Order() => 999;
    

    public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
    {
        var tds = TypeData.GetDerivedTypes<IMetricsSettingsItem>().ToArray();
        using var ms = new MemoryStream();
        package.SaveTo(ms);
        var str = Encoding.UTF8.GetString(ms.ToArray());
        Console.WriteLine(str);
        return true;
    }

    public PackageActionStage ActionStage => PackageActionStage.Create;
}