using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenTap.Metrics.Settings;
using System.Diagnostics;

namespace OpenTap.Metrics.UnitTest;

[TestFixture] 
public class MetricsSettingsTests
{
    public class AdditionalMetricsProducer : IAdditionalMetricSources
    {
        public static List<MetricInfo> Metrics { get; set; } = new();
        public IEnumerable<MetricInfo> AdditionalMetrics => Metrics.ToArray();
    }

    [Test]
    public void TestEnableMetrics()
    {
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        var sw = Stopwatch.StartNew();
        var tds = TypeData.GetDerivedTypes<IMetricsSettingsItem>()
            .Where(x => x.CanCreateInstance).ToArray();

        var took = sw.Elapsed;

        MetricsSettings.Current.Add(tds.First().CreateInstance() as IMetricsSettingsItem);
        var things = MetricsSettings.Current.ToArray();
    }
    
}