using System.Collections.Generic;
using System;

namespace OpenTap.Metrics.AssetDiscovery
{

    /// <summary>
    /// Base class for instruments that have asset metrics (like calibration due date) that can be polled.
    /// Contains logic to ensure that SCPI queries are not sent to the instrument from multiple threads/processes at the same time.
    /// </summary>
    public abstract class AssetMetricScpiInstrument : ScpiInstrument, IAsset, IOnPollMetricsCallback, IDisposable
    {
        #region Settings
        // Metadata used to associate any metrics defined by this class to an asset with the same identifier returned by the IAssetDiscovery implementation
        public string Identifier { get; private set; }
        public string Manufacturer { get; private set; }
        public string Model { get; private set; }
        #endregion

        private readonly AssetMetricInstrumentLogic logic;

        public AssetMetricScpiInstrument()
        {
            logic = new AssetMetricInstrumentLogic(this);
        }

        public override void Open()
        {
            base.Open();

            // Take a system-wide mutex to ensure that only one instance of OpenTAP (think Runner Sessions)
            // this communicating with the instrument at a time.
            // This is especially important if the instrument has metrics that will get polled asynchronously
            // In the Runner, this polling will happen against a special "Idle Session" that is an independent
            // OpenTAP installation dir compared to any normal sessions that might be running a testplan. Hence
            // this needs to be a system-wide mutex.
            logic.WaitForMutex(VisaAddress);

            // This is a good time to query for CalDueDate and other asset metrics, 
            // since we know we will not disrupt any ongoing measurements.
            // This is to ensure that a very busy station that is almost always running a testplan
            // will still get the asset metrics updated, even if all polls are blocked by the mutex.
            updateAssetMetricsInternal();
            logic.PushMetrics();
        }

        public override void Close()
        {
            logic.ReleaseMutex();
            base.Close();
        }

        private void updateAssetMetricsInternal()
        {
            // Make sure the IAsset metadata properties "Identifier" and "Model" are set correctly
            var manufacturer = IdnString.Split(',')[0];
            var model = IdnString.Split(',')[1];
            var serialNumber = IdnString.Split(',')[3];
            Identifier = $"{manufacturer},{model},{serialNumber}"; // Should be the same as returned by the IAssetDiscovery implementation
            Model = model;
            Manufacturer = manufacturer;

            // Query the instrument for asset metrics (calibration information in this case) 
            UpdateAssetMetrics();
        }

        /// <summary> 
        /// When overridden in a derived class, update the values of the asset metric properties. Called just before those properties are polled.
        /// </summary>
        protected abstract void UpdateAssetMetrics();

        public virtual void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            // Make sure to update the metric properties before they get polled if possible (i.e. if the instrument is not busy)
            if (logic.GetBusyMutex(VisaAddress).WaitOne(0))
            {
                try
                {
                    base.Open();
                    updateAssetMetricsInternal();
                }
                finally
                {
                    Close();
                }
            }
        }

        public void Dispose()
        {
            logic.Dispose();
        }
    }
}
