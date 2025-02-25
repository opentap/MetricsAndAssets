using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics.AssetDiscovery;

public static class AssetDiscoveryManager
{
    private static TraceSource log = OpenTap.Log.CreateSource("Asset Discovery");

    /// <summary>
    /// Returns all discovered assets from all available providers.
    /// </summary>
    public static Dictionary<IAssetDiscoveryProvider, DiscoveryResult> DiscoverAllAssets()
    {
        Dictionary<IAssetDiscoveryProvider, DiscoveryResult> assets = new Dictionary<IAssetDiscoveryProvider, DiscoveryResult>();
        foreach (var provider in AssetDiscoverySettings.Current.OrderByDescending(x => x.Priority))  // Higher (numeric value) priority should be used first.
        {
            try
            {
                log.Debug($"Asking provider {provider.GetType().Name} to discover assets.");
                var result = provider.DiscoverAssets();
                log.Debug($"Provider {provider.GetType().Name} returned.");
                assets[provider] = result;
            }
            catch (Exception ex)
            {
                log.Error($"Error while discovering assets from {provider.GetType().Name}: {ex.Message}");
                assets[provider] = new DiscoveryResult
                {
                    IsSuccess = false,
                    Error = ex.Message
                };
            }
        }
        return assets;
    }


    // public static void PushDiscoveredAssets(DiscoveredAsset asset)
    // {
    //
    // }
}
