using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics.AssetDiscovery;

public static class AssetDiscoveryManager
{
    private static TraceSource log = OpenTap.Log.CreateSource("AssetDiscovery");

    /// <summary>
    /// Returns all discovered assets from all available providers.
    /// </summary>
    public static Dictionary<IAssetDiscovery, DiscoveryResult> DiscoverAllAssets()
    {
        Dictionary<IAssetDiscovery, DiscoveryResult> assets = new Dictionary<IAssetDiscovery, DiscoveryResult>();
        foreach (var provider in GetAssetDiscoveryProviders())
        {
            try
            {
                var result = provider.DiscoverAssets();
                assets[provider] = result;
            }
            catch (Exception ex)
            {
                log.Error($"Error while discovering assets from {provider.GetType().Name}: {ex.Message}");
                assets[provider] = new DiscoveryResult
                {
                    IsSuccess = false,
                    Error = ex.GetType().Name
                };
            }
        }
        return assets;
    }

    private static IEnumerable<IAssetDiscovery> _assetDiscoveryProviders;
    private static IEnumerable<IAssetDiscovery> GetAssetDiscoveryProviders()
    {
        if (_assetDiscoveryProviders == null)
        {
            _assetDiscoveryProviders = TypeData.GetDerivedTypes<IAssetDiscovery>().Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstance() as IAssetDiscovery)
                .OrderByDescending(x => x.Priority)  // Higher (numeric value) priority should be used first.
                .ToList();
        }
        return _assetDiscoveryProviders;
    }

    // public static void PushDiscoveredAssets(DiscoveredAsset asset)
    // {
    //
    // }
}
