//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Metrics.UnitTest;

[TestFixture]
public class DynamicMetricTests
{
    public class DynamicMetricProvider : Instrument, IAdditionalMetricSources, IOnPollMetricsCallback
    {
        const string Group = "Dynamic Metric Test";
        IEnumerable<MetricInfo> IAdditionalMetricSources.AdditionalMetrics => [PollMetric, PushMetric];
        public MetricInfo PollMetric { get; }
        public MetricInfo PushMetric { get; }

        public double Counter { get; private set; } = 0;

        public DynamicMetricProvider()
        {
            PollMetric = MetricManager.CreatePollMetric(this, () => Counter, "Counter", Group);
            PushMetric = MetricManager.CreatePushMetric<double>(this, "Pusher", Group);
        }

        public void PushDouble(double value)
        {
            MetricManager.PushMetric(PushMetric, value);
        }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            if (metrics.Contains(PollMetric))
                Counter++;
        }
    }

    public class DynamicNullableMetricProvider : Instrument, IAdditionalMetricSources, IOnPollMetricsCallback
    {
        const string Group = "Dynamic Nullable Metric Test";
        IEnumerable<MetricInfo> IAdditionalMetricSources.AdditionalMetrics => [PollMetric, PushMetric];
        public MetricInfo PollMetric { get; }
        public MetricInfo PushMetric { get; }
        public double? Counter { get; private set; } = 0;

        public DynamicNullableMetricProvider()
        {
            PollMetric = MetricManager.CreatePollMetric(this, () => Counter, "Counter", Group);
            PushMetric = MetricManager.CreatePushMetric<double?>(this, "Pusher", Group);
        }

        public void PushDouble(double? value)
        {
            if (value is null)
            {
                MetricManager.UpdateAvailability(PushMetric, false);
            }
            else
            {
                MetricManager.PushMetric(PushMetric, value.Value);
                MetricManager.UpdateAvailability(PushMetric, true);
            }
        }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            if (metrics.Contains(PollMetric))
                Counter++;
        }
    }

    public class DynamicMetricListener : IMetricListener
    {
        public object LastMetric { get; private set; }
        public void OnPushMetric(IMetric table)
        {
            if (table.Info.MetricFullName is "Dynamic Metric Test \\ Pusher" or "Dynamic Nullable Metric Test \\ Pusher")
                LastMetric = table.Value;
        }
    }

    [Test]
    public void TestNewMetric()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        MetricInfo result = null;
        void onNewMetric(MetricCreatedEventArgs args)
        {
            MetricManager.OnMetricCreated -= onNewMetric;
            result = args.Metric;
        }
        var provider = new DynamicMetricProvider();
        MetricManager.OnMetricCreated += onNewMetric;
        var created = MetricManager.CreatePushMetric<double>(provider, "test", "group");
        Assert.IsNotNull(created);
        Assert.That(result, Is.EqualTo(created));
    }

    [Test]
    public void TestDynamicMetrics()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        var dyn = new DynamicMetricProvider();
        InstrumentSettings.Current.Add(dyn);
        var listener = new DynamicMetricListener();
        MetricManager.Subscribe(listener, [dyn.PushMetric]);
        Assert.That(listener.LastMetric, Is.EqualTo(null));

        for (double i = 0; i < 10; i++)
        {
            dyn.PushDouble(i);
            Assert.That(listener.LastMetric, Is.EqualTo(i));
        }

        for (double i = 1; i < 10; i++)
        {
            // DynamicMetricProvider is incrementing Counter whenever it is polled
            IMetric m = MetricManager.PollMetrics([dyn.PollMetric]).First();
            Assert.That(m.Value, Is.EqualTo(i));
            Assert.That(dyn.Counter, Is.EqualTo(i));
        }
    }

    [Test]
    public void TestNewMetric_Nullable()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        MetricInfo result = null;
        void onNewMetric(MetricCreatedEventArgs args)
        {
            MetricManager.OnMetricCreated -= onNewMetric;
            result = args.Metric;
        }
        var provider = new DynamicNullableMetricProvider();
        MetricManager.OnMetricCreated += onNewMetric;
        var created = MetricManager.CreatePushMetric<double?>(provider, "test", "group");
        Assert.That(created, Is.Not.Null);
        Assert.That(result, Is.EqualTo(created));
    }

    [Test]
    public void TestDynamicMetrics_Nullable()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        var provider = new DynamicNullableMetricProvider();
        InstrumentSettings.Current.Add(provider);
        var listener = new DynamicMetricListener();
        MetricManager.Subscribe(listener, [provider.PushMetric]);
        Assert.That(listener.LastMetric, Is.Null);

        for (double i = 0; i < 10; i++)
        {
            provider.PushDouble(i);
            Assert.That(listener.LastMetric, Is.EqualTo(i));
        }

        for (double i = 1; i < 10; i++)
        {
            // DynamicNullableMetricProvider is incrementing Counter whenever it is polled
            IMetric m = MetricManager.PollMetrics([provider.PollMetric]).First();
            Assert.That(m.Value, Is.EqualTo(i));
            Assert.That(provider.Counter, Is.EqualTo(i));
        }
    }

    [TestCase(null, false)]
    [TestCase(1.0, true)]
    public void TestDynamicMetricsAvailability(double? value, bool isAvailable)
    {
        MetricManager.Reset();
        var provider = new DynamicNullableMetricProvider();
        MetricInfo result = null;
        void onMetricAvailabilityChanged(MetricAvailabilityChangedEventsArgs args) { result = args.Metric; }
        MetricManager.OnMetricAvailabilityChanged += onMetricAvailabilityChanged;

        provider.PushDouble(value);

        Assert.That(result.IsAvailable, Is.EqualTo(isAvailable));
        MetricManager.OnMetricAvailabilityChanged -= onMetricAvailabilityChanged;
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TestDynamicMetricsAvailability_PollMetric_Exception(bool isAvailable)
    {
        var provider = new DynamicNullableMetricProvider();

        var ex = Assert.Throws<ArgumentException>(() => MetricManager.UpdateAvailability(provider.PollMetric, isAvailable));

        Assert.That(ex.Message, Is.EqualTo("Cannot update availability of a poll metric. (Parameter 'metric')"));
    }
}
