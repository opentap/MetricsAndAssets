using System.Collections.Generic;
using System.Threading;
using System;
using System.Xml.Serialization;

namespace OpenTap.Metrics.AssetDiscovery
{

    /// <summary>
    /// Base class for instruments that have asset metrics (like calibration due date) that can be polled.
    /// Contains logic to ensure that SCPI queries are not sent to the instrument from multiple threads/processes at the same time.
    /// </summary>
    public abstract class AssetMetricInstrument : Instrument, IAsset, IMetricSource, IOnPollMetricsCallback, IDisposable
    {
        #region Settings
        // Metadata used to associate any metrics defined by this class to an asset with the same identifier returned by the IAssetDiscovery implementation
        [XmlIgnore]
        public string AssetIdentifier { get; set; }
        [XmlIgnore]
        public string Manufacturer => "Keysight";
        [XmlIgnore]
        public string Model { get; set; }

        #endregion

        private readonly AssetMetricInstrumentLogic logic;
        protected abstract string Address { get; }

        public AssetMetricInstrument()
        {
            logic = new AssetMetricInstrumentLogic(this);
        }

        public override void Open()
        {
            // Take a system-wide mutex to ensure that only one instance of OpenTAP (think Runner Sessions)
            // this communicating with the instrument at a time.
            // This is especially important if the instrument has metrics that will get polled asynchronously
            // In the Runner, this polling will happen against a special "Idle Session" that is an independent
            // OpenTAP installation dir compared to any normal sessions that might be running a testplan. Hence
            // this needs to be a system-wide mutex.
            try
            {
                if (!logic.GetBusyMutex(Address).WaitOne(10_000))
                {
                    throw new Exception("Failed to connect. Another instance of OpenTAP is already connected to this inetrument.");
                }
            }
            catch (AbandonedMutexException)
            {
                // If the mutex was abandoned (a previous session crashed while it had the mutex), we can still continue
            }

            LockedOpen();

            // This is a good time to query for CalDueDate and other asset metrics, 
            // since we know we will not disrupt any ongoing measurements.
            // This is to ensure that a very busy station that is almost always running a testplan
            // will still get the asset metrics updated, even if all polls are blocked by the mutex.
            UpdateAssetMetrics();
            logic.PushMetrics();
        }

        protected virtual void LockedOpen()
        {
            base.Open();
        }

        protected virtual void LockedClose()
        {
            base.Close();
        }

        public override void Close()
        {
            logic.ReleaseMutex();
            LockedClose();
        }

        /// <summary> 
        /// When overridden in a derived class, update the values of the asset metric properties. Called just before those properties are polled.
        /// </summary>
        protected abstract void UpdateAssetMetrics();

        public virtual void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            // Make sure to update the metric properties before they get polled if possible (i.e. if the instrument is not busy)
            if (logic.GetBusyMutex(Address).WaitOne(0))
            {
                try
                {
                    LockedOpen();
                    UpdateAssetMetrics();
                }
                finally
                {
                    LockedClose();
                    logic.ReleaseMutex();
                }
            }
        }

        public void Dispose()
        {
            logic.Dispose();
        }
    }
}
