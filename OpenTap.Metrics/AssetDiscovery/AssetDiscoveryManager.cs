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
                    Error = ex.Message
                };
            }
        }
        return assets;
    }

    /// <summary>
    /// Given a generic asset as returned by DiscoverAllAssets, this method will return a more specialized asset type with additional details.
    /// This relies on being able to find IAssetDetails implementations that can provide the additional details for the given asset manufavturer and model.
    public statis DiscoveredAsset GetAssetDetails(DiscoveredAsset asset)
    {
        var detailsProviders = TypeData.GetDerivedTypes<IAssetDetails>().ToList();
        foreach (var provider in detailsProviders)
        {
            try
            {
                if (provider.Get(asset))
                {
                    return provider.GetAssetDetails(asset);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while getting details for asset {asset.Id} from {provider.GetType().Name}: {ex.Message}");
            }
        }
        return null;
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

    private static IEnumerable<IAssetDetails> _assetDetailsProviders;
    private static IEnumerable<IAssetDetails> GetAssetDetailsProviders()
    {
        if (_assetDetailsProviders == null)
        {
            _assetDetailsProviders = TypeData.GetDerivedTypes<IAssetDetails>().Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstance() as IAssetDetails)
                .OrderByDescending(x => x.Priority)  // Higher (numeric value) priority should be used first.
                .ToList();
        }
        return _assetDetailsProviders;
    }

    // public static void PushDiscoveredAssets(DiscoveredAsset asset)
    // {
    //
    // }
}
