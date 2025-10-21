//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using OpenTap.Metrics.AssetDiscovery;

namespace OpenTap.Metrics.UnitTest;

[Display("Test Metric Producer")]
public class TestMetricSource : IMetricSource
{
    [Metric][Unit("I")] public double X { get; private set; }

    [Metric]
    [Unit("V")]
    public double Y { get; private set; }

    [Metric(kind: MetricKind.PushPoll)]
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
                MetricManager.UpdateAvailability(zMetric, false);
        }
    }
}

[Display("Full Test Metric Producer")]
public class FullMetricSource : IMetricSource
{
    [Metric(kind: MetricKind.PushPoll, DefaultEnabled = true)]
    public double DoubleMetric { get; private set; }

    [Metric(kind: MetricKind.PushPoll, DefaultPollRate = 3)]
    public double? DoubleMetricNull { get; private set; }

    [Metric(kind: MetricKind.PushPoll)]
    public bool BoolMetric { get; private set; }

    [Metric(kind: MetricKind.PushPoll)]
    public bool? BoolMetricNull { get; private set; }

    [Metric(kind: MetricKind.PushPoll)]
    public int IntMetric { get; private set; }

    [Metric(kind: MetricKind.PushPoll)]
    public int? IntMetricNull { get; private set; }

    [Metric(kind: MetricKind.PushPoll)]
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

            Assert.That(metricMap.Contains(metricV), Is.True);
            Assert.That(metricMap.Contains(currentV), Is.True);
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

        public readonly List<IMetric> MetricValues = [];

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

        var testMetric = metrics.FirstOrDefault(m => m.MetricFullName == "INST \\ Test");
        Assert.IsNotNull(testMetric);
        var range = testMetric.Attributes.OfType<RangeAttribute>().FirstOrDefault();
        Assert.IsNotNull(range);
        Assert.That(range.Minimum == 0.0, Is.True);

        Assert.That(metrics.Any(m => m.MetricFullName == "INST \\ v"), Is.True);

        Assert.Contains("Test Metric Producer \\ Y", metrics.Select(m => m.MetricFullName).ToArray());
        InstrumentSettings.Current.Remove(instrTest);
        metrics = MetricManager.GetMetricInfos().ToArray();

        Assert.IsFalse(metrics.Any(m => m.MetricFullName == "INST \\ v"));
    }

    [Test]
    public void TestMetricNames_MetricSource()
    {
        MetricManager.Reset();
        var metricInfos = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ DoubleMetric"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ DoubleMetricNull"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ BoolMetric"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ BoolMetricNull"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ IntMetric"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ IntMetricNull"));
        Assert.That(metricInfos, Has.One.Matches<MetricInfo>(m => m.MetricFullName == "Full Test Metric Producer \\ StringMetric"));
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
    public void TestMetricAvailability_MetricSource_Poll()
    {
        MetricManager.Reset();
        var interestSet = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();
        var metricInfos = MetricManager.PollMetrics(interestSet).ToArray();

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(metricInfos.Select(m => m.Info.IsAvailable), Is.All.True);
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public void TestMetricAvailability_MetricSource_Push(bool isAvailable, bool expected)
    {
        MetricManager.Reset();
        var metricInfos = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).ToArray();
        foreach (var metric in metricInfos)
            MetricManager.UpdateAvailability(metric, isAvailable);

        Assert.That(metricInfos, Has.Length.EqualTo(7));
        Assert.That(metricInfos.Select(m => m.IsAvailable), Is.All.EqualTo(expected));
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
        Assert.That(allMetrics.Length, Is.EqualTo(returned.Length));

        {
            var listener2 = new TestMetricsListener();
            MetricManager.Subscribe(listener2, allMetrics);

            // Verify that all metrics are currently of interest
            foreach (var m in allMetrics)
            {
                Assert.That(MetricManager.HasInterest(m), Is.True);
            }

            MetricManager.Subscribe(listener, []);

            // Verify that all metrics are still of interest
            foreach (var m in allMetrics)
            {
                Assert.That(MetricManager.HasInterest(m), Is.True);
            }

            MetricManager.Subscribe(listener2, []);

            // Verify that no metrics are of interest
            foreach (var m in allMetrics)
            {
                Assert.That(MetricManager.HasInterest(m), Is.False);
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
            Assert.That(managerInfo.Length, Is.EqualTo(1));
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

        Assert.That(a2.Length, Is.EqualTo(a1.Length));

        for (int i = 0; i < a1.Length; i++)
        {
            var m1 = a1[i];
            var m2 = a2[i];

            Assert.That(m2.GetHashCode(), Is.EqualTo(m1.GetHashCode()));
            Assert.That(m2, Is.EqualTo(m1));
        }
    }

    [Test]
    public void TestPollDefaultMetrics()
    {
        var interestSet = MetricManager.GetMetricInfos()
            .Where(info => info.GroupName == "System" || info.GroupName == "Process").ToArray();
        var metrics = MetricManager.PollMetrics(interestSet, true).ToDictionary(info => info.Info.Name);
        var cpuUsage = metrics["CPU Usage"];
        var memoryUsage = metrics["Memory Usage"];
        var availableMemory = metrics["Available Memory"];
        var availableDiskSpace = metrics["Available Disk Space"];
        var usedDiskSpace = metrics["Used Disk Space"];
        Assert.That((double)cpuUsage.Value, Is.GreaterThanOrEqualTo(0.0));
        Assert.That((double)memoryUsage.Value, Is.GreaterThan(1));
        Assert.That((double)availableDiskSpace.Value, Is.GreaterThanOrEqualTo(0.0));
        Assert.That((double)usedDiskSpace.Value, Is.GreaterThanOrEqualTo(0.0));
        if(availableMemory.Value != null)
            Assert.That((double)availableMemory.Value, Is.GreaterThan(1));
        Assert.That(cpuUsage.Info.Description, Contains.Substring("The CPU usage"));
        Assert.That(cpuUsage.Info.GroupName, Is.EqualTo("Process"));
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
        Assert.That(interestSet.Count(m => m.Kind.HasFlag(MetricKind.Poll)), Is.EqualTo(metrics.Count()));


        instrTest.PushRangeValues();

        var results0 = listener.MetricValues.ToArray();
        Assert.That(results0.Length, Is.EqualTo(10));

        listener.Clear();
        interestSet.RemoveAll(x => x.Name == "Test");
        MetricManager.Subscribe(listener, interestSet);
        metrics = MetricManager.PollMetrics(interestSet);
        Assert.That(interestSet.Count(m => m.Kind.HasFlag(MetricKind.Poll)), Is.EqualTo(metrics.Count()));
        instrTest.PushRangeValues();
        var results2 = listener.MetricValues.ToArray();
        Assert.That(results2.Length, Is.EqualTo(0));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void TestPushMetricRetainAvailability_WhenMetricInfoIsRetrieved(bool isAvailable, bool expected)
    {
        MetricManager.Reset();
        var source = new FullMetricSource();
        var metricInfo = MetricManager.GetMetricInfo(source, nameof(source.DoubleMetric));
        MetricManager.UpdateAvailability(metricInfo, isAvailable);

        metricInfo = MetricManager.GetMetricInfo(source, nameof(source.DoubleMetric));

        Assert.That(metricInfo.IsAvailable, Is.EqualTo(expected));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void TestPushMetricRetainAvailability_WhenMetricInfosAreRetrieved(bool isAvailable, bool expected)
    {
        MetricManager.Reset();
        var metricInfo = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).First(m => m.Name == nameof(FullMetricSource.DoubleMetric));
        MetricManager.UpdateAvailability(metricInfo, isAvailable);

        metricInfo = MetricManager.GetMetricInfos().Where(m => m.Source is FullMetricSource).First(m => m.Name == nameof(FullMetricSource.DoubleMetric));

        Assert.That(metricInfo.IsAvailable, Is.EqualTo(expected));
    }

    public abstract class ScpiInstrumentMock : Instrument, IAsset
    {
        public string SerialNumber { get; set; }
        public string Manufacturer { get; set; } = "Keysight";
        public string FirmwareVersion { get; set; } = "10.5.34";
        public string ScpiQuery(string query) => DateTime.Today.ToString(CultureInfo.InvariantCulture);
        [MetaData]
        public string IdnString => $"{Manufacturer},{Model},{SerialNumber},{FirmwareVersion}";

        public string Model { get; set; }
        public string AssetIdentifier { get; set; }
    }
    public class TestMXA : ScpiInstrumentMock, IAsset, IOnPollMetricsCallback
    {
        [Metric]
        public DateTime CalibrationDate { get; set; }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            bool shouldClose = false;
            if (!this.IsConnected)
            {
                this.Open();
                shouldClose = true;
            }

            try
            {
                var parts = this.IdnString.Split([','], StringSplitOptions.RemoveEmptyEntries);
                this.AssetIdentifier = parts[0] + parts[1] + parts[2];
                this.Model = parts[1];
                this.CalibrationDate = DateTime.Parse(ScpiQuery("CALibrationdate?"));
            }
            finally
            {
                if (shouldClose)
                    this.Close();
            }
        }
    }

    [Test]
    public void TestAssetMetadata()
    {
        using var s = OpenTap.Session.Create(SessionOptions.OverlayComponentSettings);
        InstrumentSettings.Current.Clear();
        var mxa1 = new TestMXA()
        {
            Name = "MXA 1",
            CalibrationDate = DateTime.Today,
            SerialNumber = "MXA1_SER",
            Model = "N9020"
        };
        InstrumentSettings.Current.Add(mxa1);
        var mxa2 = new TestMXA()
        {
            Name = "MXA 2",
            CalibrationDate = (DateTime.Today + TimeSpan.FromDays(1)),
            SerialNumber = "MXA2_SER",
            Model = "N9020"
        };
        InstrumentSettings.Current.Add(mxa2);

        var infos = MetricManager.GetMetricInfos().Where(m => m.Source is TestMXA).ToArray();
        var metrics = MetricManager.PollMetrics(infos).ToArray();

        Assert.That(infos.Length, Is.EqualTo(2));
        Assert.That(metrics.Length, Is.EqualTo(2));
        foreach (var m in metrics)
        {
            Assert.That(m.MetaData.Count, Is.EqualTo(4));
            Assert.That(m.MetaData["Model"], Is.EqualTo("N9020"));
            if (m.Info.Source == mxa1)
            {
                Assert.That(m.MetaData["AssetID"], Is.EqualTo($"{mxa1.Manufacturer}{mxa1.Model}{mxa1.SerialNumber}"));
            }
            else
            {
                Assert.That(m.MetaData["AssetID"], Is.EqualTo($"{mxa2.Manufacturer}{mxa2.Model}{mxa2.SerialNumber}"));
            }
        }
    }
}