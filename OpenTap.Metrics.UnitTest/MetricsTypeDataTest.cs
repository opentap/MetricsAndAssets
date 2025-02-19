using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenTap.Metrics.Settings;
using System.Diagnostics;

namespace OpenTap.Metrics.UnitTest;

[TestFixture] 
public class MetricsTypeDataTest
{
    public class AdditionalMetricsProducer : IAdditionalMetricSources
    {
        public static AdditionalMetricsProducer Hack { get; private set; }

        public AdditionalMetricsProducer()
        {
            // This class should be instantiated by MetricManager exactly once.
            Hack = this;
        }
        public static List<MetricInfo> Metrics { get; set; } = new();
        public IEnumerable<MetricInfo> AdditionalMetrics => Metrics.ToArray();
    } 

    [Test]
    public void TestDiscoverAdditionalMetrics()
    {
        // Ensure AdditionalMetricsProducer is instantiated
        _ = MetricManager.GetMetricInfos().ToArray();
        double counter = 0;
        Assert.That(AdditionalMetricsProducer.Hack, Is.Not.Null,
            $"{nameof(AdditionalMetricsProducer)} should have been instantiated.");
        var dynamicPoll = MetricManager.CreatePollMetric(AdditionalMetricsProducer.Hack, () => counter++, "Counter", "Dynamic");
        var dynamicPush = MetricManager.CreatePushMetric<double>(AdditionalMetricsProducer.Hack, "Push Counter", "Dynamic");
        var pollSpec = new MetricSpecifier(dynamicPoll.Member);
        var pushSpec = new MetricSpecifier(dynamicPush.Member);
        { /* Verify the metric type does not exist */
            var specs = TypeData.GetDerivedTypes<IMetricsSettingsItem>().OfType<MetricInfoTypeData>()
                .Select(x => x.Specifier).ToArray();
            Assert.That(specs, Does.Not.Contain(pollSpec));
        }
        { /* Add the metric and verify it exists */
            AdditionalMetricsProducer.Metrics.Add(dynamicPoll);
            var specs = TypeData.GetDerivedTypes<IMetricsSettingsItem>().OfType<MetricInfoTypeData>()
                .Select(x => x.Specifier).ToArray();
            Assert.That(specs, Does.Contain(pollSpec));
        }
        { /* Add the push metric and verify they both exist */
            AdditionalMetricsProducer.Metrics.Add(dynamicPush);
            var specs = TypeData.GetDerivedTypes<IMetricsSettingsItem>().OfType<MetricInfoTypeData>()
                .Select(x => x.Specifier).ToArray();
            Assert.That(specs, Does.Contain(pollSpec));
            Assert.That(specs, Does.Contain(pushSpec));
        }
        { /* do some polling just to check I guess */
            var v = MetricManager.PollMetrics([dynamicPoll]).First().Value;
            Assert.That(v, Is.EqualTo(0));
            v = MetricManager.PollMetrics([dynamicPoll]).First().Value;
            Assert.That(v, Is.EqualTo(1));
        }
        { /* Remove the metrics and verify they disappear */
            AdditionalMetricsProducer.Metrics.Clear();
            var specs = TypeData.GetDerivedTypes<IMetricsSettingsItem>().OfType<MetricInfoTypeData>()
                .Select(x => x.Specifier).ToArray();
            Assert.That(specs, Does.Not.Contain(pollSpec));
            Assert.That(specs, Does.Not.Contain(pushSpec));
        }
    }

    [Test]
    public void TestMergingSimilarMetrics()
    {
        // Ensure AdditionalMetricsProducer is instantiated
        _ = MetricManager.GetMetricInfos().ToArray();
        double counter = 0;
        Assert.That(AdditionalMetricsProducer.Hack, Is.Not.Null,
            $"{nameof(AdditionalMetricsProducer)} should have been instantiated.");
        var dyn1 = MetricManager.CreatePollMetric(AdditionalMetricsProducer.Hack, () => counter++, "Counter", "Dynamic");
        var dyn2 = MetricManager.CreatePollMetric(AdditionalMetricsProducer.Hack, () => counter++, "Counter", "Dynamic");
        var dyn3 = MetricManager.CreatePollMetric(AdditionalMetricsProducer.Hack, () => counter++, "Counter", "Dynamic2");
        var spec1 = new MetricSpecifier(dyn1.Member);
        var spec2 = new MetricSpecifier(dyn2.Member); 
        Assert.That(spec1, Is.EqualTo(spec2));
        var count0 = TypeData.GetDerivedTypes<IMetricsSettingsItem>().Count();
        AdditionalMetricsProducer.Metrics.Add(dyn1);
        var count1 = TypeData.GetDerivedTypes<IMetricsSettingsItem>().Count();
        AdditionalMetricsProducer.Metrics.Add(dyn2);
        var count2 = TypeData.GetDerivedTypes<IMetricsSettingsItem>().Count();
        AdditionalMetricsProducer.Metrics.Add(dyn3);
        var count3 = TypeData.GetDerivedTypes<IMetricsSettingsItem>().Count();
        AdditionalMetricsProducer.Metrics.Clear();
        var count4 = TypeData.GetDerivedTypes<IMetricsSettingsItem>().Count();
        
        Assert.That(count0, Is.LessThan(count1));
        Assert.That(count1, Is.EqualTo(count2));
        Assert.That(count2, Is.LessThan(count3));
        Assert.That(count4, Is.EqualTo(count0));
        AdditionalMetricsProducer.Metrics.Clear();
    }
}