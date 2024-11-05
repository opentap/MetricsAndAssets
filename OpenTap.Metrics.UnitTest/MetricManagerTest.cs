//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace OpenTap.Metrics.UnitTest;

[Display("Test Metric Producer")]
public class TestMetricSource : IMetricSource
{
    [Metric][Unit("I")] public double X { get; private set; }

    [Metric]
    [Unit("V")]
    public double Y { get; private set; }

    [Metric]
    [Unit("U")]
    public double? Z { get; private set; }

    private int _offset = 0;
    public void PushMetric()
    {
        var xMetric = MetricManager.GetMetricInfo(this, nameof(X));
        var yMetric = MetricManager.GetMetricInfo(this, nameof(Y));
        var zMetric = MetricManager.GetMetricInfo(this, nameof(Z));
        if (!MetricManager.HasInterest(xMetric)) return;
        for (int i = 0; i < 100; i++)
        {
            _offset += 1;
            X = _offset;
            MetricManager.PushMetric(xMetric, X);
            MetricManager.PushMetric(yMetric, Math.Sin(_offset * 0.1));

            if (i % 20 == 0)
                MetricManager.PushMetric(zMetric, 1);
            else
                MetricManager.PushMetric(zMetric);
        }
    }
}

[Display("Full Test Metric Producer")]
public class FullMetricSource : IMetricSource
{
    [Metric]
    public double DoubleMetric { get; private set; }

    [Metric]
    public double? DoubleMetricNull { get; private set; }

    [Metric]
    public bool BoolMetric { get; private set; }

    [Metric]
    public bool? BoolMetricNull { get; private set; }

    [Metric]
    public int IntMetric { get; private set; }

    [Metric]
    public int? IntMetricNull { get; private set; }

    [Metric]
    public string StringMetric { get; private set; }
}

[TestFixture]
public class MetricManagerTest
{
    public class IdleResultTestInstrument : Instrument, IOnPollMetricsCallback
    {
        public IdleResultTestInstrument()
        {

        }

        public string StatusName => $"{Name}: {Voltage,2} V";

        readonly Stopwatch sw = Stopwatch.StartNew();

        [Browsable(true)]
        [Unit("V")]
        [Display("v", Group: "Metrics")]
        [Metric]
        public double Voltage { get; private set; }

        [Browsable(true)]
        [Unit("A")]
        [Display("I", Group: "Metrics")]
        [Metric]
        public double Current { get; private set; }

        [Metric]
        public string Id { get; set; }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            var metricV = MetricManager.GetMetricInfo(this, nameof(Voltage));
            var currentV = MetricManager.GetMetricInfo(this, nameof(Current));
            var idV = MetricManager.GetMetricInfo(this, nameof(Id));

            var metricMap = metrics.ToHashSet();

            Assert.IsTrue(metricMap.Contains(metricV));
            Assert.IsTrue(metricMap.Contains(currentV));
            Assert.IsFalse(metricMap.Contains(idV));

            Voltage = Math.Sin(sw.Elapsed.TotalSeconds * 100.0) + 2.5;
            Current = Math.Cos(sw.Elapsed.TotalSeconds * 100.0) * 0.1 + 1.5;
            Id = Guid.NewGuid().ToString();
        }


        [Metric(kind: MetricKind.Push)]
        [Unit("cm")]
        [Range(minimum: 0.0)]
        public int Test { get; private set; }

        public readonly int Count = 10;

        public void PushRangeValues()
        {
            var iMetric = MetricManager.GetMetricInfo(this, nameof(Test));
            if (MetricManager.HasInterest(iMetric) == false)
                return;
            for (int i = 0; i < Count; i++)
            {
                Test++;
                MetricManager.PushMetric(iMetric, Test);
            }

        }
    }

    public class TestMetricsListener : IMetricListener
    {

        public void Clear()
        {
            MetricValues.Clear();
        }

        public readonly List<IMetric> MetricValues = new List<IMetric>();

        public void OnPushMetric(IMetric table)
        {
            MetricValues.Add(table);
        }
    }

    [Test]
    public void TestMetricNames()
    {
        using var _ = Session.Create();
        MetricManager.Reset();
        InstrumentSettings.Current.Clear();
        var instrTest = new IdleResultTestInstrument();

        InstrumentSettings.Current.Add(instrTest);
        var metrics = MetricManager.GetMetricInfos().ToArray();

        var testMetric = metrics.FirstOrDefault(m => m.MetricFullName == "INST / Test");
        Assert.IsNotNull(testMetric);
        var range = testMetric.Attributes.OfType<RangeAttribute>().FirstOrDefault();
        Assert.IsNotNull(range);
        Assert.IsTrue(range.Minimum == 0.0);

        Assert.IsTrue(metrics.Any(m => m.MetricFullName == "INST / v"));

        Assert.Contains("Test Metric Producer / Y", metrics.Select(m => m.MetricFullName).ToArray());
        InstrumentSettings.Current.Remove(instrTest);
        metrics = MetricManager.GetMetricInfos().ToArray();

        Assert.IsFalse(metrics.Any(m => m.MetricFullName == "INST / v"));
    }

    [Test]
    public void TestMetricNames_MetricSource()
    {
        MetricManager.Reset();
        var metricInfos = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / DoubleMetric"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / DoubleMetricNull"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / BoolMetric"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / BoolMetricNull"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / IntMetric"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / IntMetricNull"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer / StringMetric"));
    }

    [Test]
    public void TestMetricTypes_MetricSource()
    {
        MetricManager.Reset();
        var metricInfos = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "DoubleMetric" && m.Type.HasFlag(MetricType.Double)));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "DoubleMetricNull" && m.Type.HasFlag(MetricType.Double | MetricType.Nullable)));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "BoolMetric" && m.Type.HasFlag(MetricType.Boolean)));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "BoolMetricNull" && m.Type.HasFlag(MetricType.Boolean | MetricType.Nullable)));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "IntMetric" && m.Type.HasFlag(MetricType.Double)));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "IntMetricNull" && m.Type.HasFlag(MetricType.Double | MetricType.Nullable)));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.Name == "StringMetric" && m.Type.HasFlag(MetricType.String)));
    }

    [Test]
    public void TestMetricAvailability_MetricSource()
    {
        MetricManager.Reset();
        var interestSet = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();
        var metricInfos = MetricManager.PollMetrics(interestSet).ToArray();

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "DoubleMetric" && m.Info.IsAvailable));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "DoubleMetricNull" && !m.Info.IsAvailable));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "BoolMetric" && m.Info.IsAvailable));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "BoolMetricNull" && !m.Info.IsAvailable));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "IntMetric" && m.Info.IsAvailable));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "IntMetricNull" && !m.Info.IsAvailable));
        Assert.That(metricInfos, Has.One.Matches<IMetric>(m => m.Info.Name == "StringMetric" && !m.Info.IsAvailable));
    }

    [Test]
    public void TestHasInterest()
    {
        MetricManager.Reset();
        CompareMetricLists(MetricManager.GetMetricInfos(), MetricManager.GetMetricInfos());

        var allMetrics = MetricManager.GetMetricInfos().Where(m => m.Kind.HasFlag(MetricKind.Poll)).ToArray();
        var listener = new TestMetricsListener();

        MetricManager.Subscribe(listener, allMetrics);

        var returned = MetricManager.PollMetrics(allMetrics).ToArray();
        Assert.AreEqual(returned.Length, allMetrics.Length);

        {
            var listener2 = new TestMetricsListener();
            MetricManager.Subscribe(listener2, allMetrics);

            // Verify that all metrics are currently of interest
            foreach (var m in allMetrics)
            {
                Assert.IsTrue(MetricManager.HasInterest(m));
            }

            MetricManager.Subscribe(listener, Array.Empty<MetricInfo>());

            // Verify that all metrics are still of interest
            foreach (var m in allMetrics)
            {
                Assert.IsTrue(MetricManager.HasInterest(m));
            }

            MetricManager.Subscribe(listener2, Array.Empty<MetricInfo>());

            // Verify that no metrics are of interest
            foreach (var m in allMetrics)
            {
                Assert.IsFalse(MetricManager.HasInterest(m));
            }
        }


        using (Session.Create())
        {
            InstrumentSettings.Current.Clear();
            var instrTest = new IdleResultTestInstrument();
            InstrumentSettings.Current.Add(instrTest);

            // Verify that the metric returned by GetMetricInfo is equal to the metrics created by MetricManager
            var currentMetricInfo = MetricManager.GetMetricInfo(instrTest, nameof(instrTest.Current));
            var managerInfo = MetricManager.GetMetricInfos().Where(m =>
                currentMetricInfo.GetHashCode().Equals(m.GetHashCode()) &&
                currentMetricInfo.Equals(m)).ToArray();
            Assert.AreEqual(1, managerInfo.Length);
        }
    }

    [Test]
    public void TestHasInterest_MetricSource()
    {
        MetricManager.Reset();
        var metricInfos = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();
        var listener = new TestMetricsListener();
        MetricManager.Subscribe(listener, metricInfos);
        var listener2 = new TestMetricsListener();
        MetricManager.Subscribe(listener2, metricInfos);

        var returned = MetricManager.PollMetrics(metricInfos).ToArray();

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(returned, Has.Length.EqualTo(metricInfos.Length));
        // Verify that all metrics are currently of interest
        Assert.That(metricInfos.Select(MetricManager.HasInterest), Has.All.True);
        MetricManager.Unsubscribe(listener);
        // Verify that all metrics are still of interest
        Assert.That(metricInfos.Select(MetricManager.HasInterest), Has.All.True);
        MetricManager.Unsubscribe(listener2);
        // Verify that no metrics are of interest
        Assert.That(metricInfos.Select(MetricManager.HasInterest), Has.All.False);
    }

    static void CompareMetricLists(IEnumerable<MetricInfo> left, IEnumerable<MetricInfo> right)
    {
        MetricInfo[] a1 = left.OrderBy(m => m.GetHashCode()).ToArray();
        MetricInfo[] a2 = right.OrderBy(m => m.GetHashCode()).ToArray();

        Assert.AreEqual(a1.Length, a2.Length);

        for (int i = 0; i < a1.Length; i++)
        {
            var m1 = a1[i];
            var m2 = a2[i];

            Assert.AreEqual(m1.GetHashCode(), m2.GetHashCode());
            Assert.AreEqual(m1, m2);
        }
    }

    [Test]
    public void TestGetMetrics()
    {
        MetricManager.Reset();
        using var _ = Session.Create();
        InstrumentSettings.Current.Clear();
        var listener = new TestMetricsListener();
        var instrTest = new IdleResultTestInstrument();
        InstrumentSettings.Current.Add(instrTest);

        var interestSet = MetricManager.GetMetricInfos().ToList();
        interestSet.Remove(MetricManager.GetMetricInfo(instrTest, nameof(instrTest.Id)));

        MetricManager.Subscribe(listener, interestSet);

        var metrics = MetricManager.PollMetrics(interestSet);
        Assert.AreEqual(metrics.Count(), interestSet.Count(m => m.Kind.HasFlag(MetricKind.Poll)));


        instrTest.PushRangeValues();

        var results0 = listener.MetricValues.ToArray();
        Assert.AreEqual(10, results0.Length);

        listener.Clear();
        interestSet.RemoveAll(x => x.Name == "Test");
        MetricManager.Subscribe(listener, interestSet);
        metrics = MetricManager.PollMetrics(interestSet);
        Assert.AreEqual(metrics.Count(), interestSet.Count(m => m.Kind.HasFlag(MetricKind.Poll)));
        instrTest.PushRangeValues();
        var results2 = listener.MetricValues.ToArray();
        Assert.AreEqual(0, results2.Length);
    }
}
