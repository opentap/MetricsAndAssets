using System.Threading;
using System;
using System.Linq;

namespace OpenTap.Metrics.AssetDiscovery
{
    /// <summary>
    /// Common place to put logic that is shared between AssetMetricInstrument and AssetMetricScpiInstrument
    /// </summary>
    class AssetMetricInstrumentLogic : IDisposable
    {
        private Mutex _busyMutex;
        private string _busyMutexName;
        public Mutex GetBusyMutex(string address)
        {
            if (_busyMutex == null || _busyMutexName != address)
            {
                _busyMutex?.Dispose();
                _busyMutex = new Mutex(false, "InstrumentBusy-" + address);
                _busyMutexName = address;
            }
            return _busyMutex;
        }

        public void ReleaseMutex()
        {
            _busyMutex?.ReleaseMutex();
        }

        public void WaitForMutex(string address)
        {
            try
            {
                if (!GetBusyMutex(address).WaitOne(10_000))
                {
                    throw new Exception("Failed to connect. Another instance of OpenTAP is already connected to this inetrument.");
                }
            }
            catch (AbandonedMutexException)
            {
                // If the mutex was abandoned (a previous session crashed while it had the mutex), we can still continue
            }
        }

        public void Dispose()
        {
            _busyMutex?.Dispose();
        }

        private readonly IAsset Asset;

        public AssetMetricInstrumentLogic(IAsset asset)
        {
            Asset = asset;
        }

        public void PushMetrics()
        {
            var metricProperties = TypeData.GetTypeData(Asset).GetMembers()
                .Where(p => p.GetAttribute<MetricAttribute>()?.Kind.HasFlag(MetricKind.Push) ?? false);
            var metrics = metricProperties.Select(p => MetricManager.GetMetricInfo(Asset, p.Name));
            foreach (var metric in metrics)
            {
                switch (metric.Type)
                {
                    case MetricType.String:
                    case MetricType.String | MetricType.Nullable:
                        MetricManager.PushMetric(metric, metric.GetValue(Asset).ToString());
                        break;
                    case MetricType.Double:
                    case MetricType.Double | MetricType.Nullable:
                        MetricManager.PushMetric(metric, (double)metric.GetValue(Asset));
                        break;
                    case MetricType.Boolean:
                    case MetricType.Boolean | MetricType.Nullable:
                        MetricManager.PushMetric(metric, (bool)metric.GetValue(Asset));
                        break;
                }
            }
        }
    }
}

