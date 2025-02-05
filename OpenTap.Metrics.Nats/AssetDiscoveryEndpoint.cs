using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using OpenTap.Metrics.AssetDiscovery;

namespace OpenTap.Metrics.Nats
{
    public class AssetDiscoveryEndpoint
    {
        private readonly TraceSource _log = Log.CreateSource("Asset Discovery");
        public AssetDiscoveryEndpoint()
        {
            RunnerExtension.MapEndpoint("DiscoverAssets", DiscoverAllAssets);
        }

        private AssetDiscoveryResponse DiscoverAllAssets()
        {
            AssetDiscoveryResponse discoveryResponse = new AssetDiscoveryResponse
            {
                AssetProviders = AssetDiscoveryManager.DiscoverAllAssets().Select(kvp => new AssetDiscoveryResult
                {
                    Name = kvp.Key.GetType().Name,
                    Priority = kvp.Key.Priority,
                    IsSuccess = kvp.Value.IsSuccess,
                    Error = kvp.Value.Error,
                    DiscoveredAssets = kvp.Value.Assets?.ToList() ?? Enumerable.Empty<DiscoveredAsset>().ToList()
                }).ToList(),
                LastSeen = DateTime.UtcNow,
            };

            _log.Debug($"Asset Discovery: {string.Join(", ", discoveryResponse.AssetProviders.Select(s => $"{s.Name} ({s.DiscoveredAssets.Count} assets)"))}");
            return discoveryResponse;
        }
    }

    public class AssetDiscoveryResponse
    {
        public List<AssetDiscoveryResult> AssetProviders { get; set; }

        public DateTime LastSeen { get; set; }
    }

    public class AssetDiscoveryResult
    {
        public List<DiscoveredAsset> DiscoveredAssets { get; set; }
        public double Priority { get; set; }
        public string Name { get; set; }
        public bool IsSuccess { get; set; }
        public string Error { get; set; }
    }
}
