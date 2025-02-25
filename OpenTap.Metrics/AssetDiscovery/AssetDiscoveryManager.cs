using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenTap.Metrics.AssetDiscovery;

public static class AssetDiscoveryManager
{
    private static TraceSource log = OpenTap.Log.CreateSource("Asset Discovery");

    private static readonly ConcurrentDictionary<IAssetDiscoveryProvider, Task<DiscoveryResult>> _workQueue = new();

    private static readonly object lockObj = new object();
    private static DiscoveryResult DiscoverAssets(IAssetDiscoveryProvider provider)
    {
        try
        {
            log.Debug($"Asking provider {provider.GetType().Name} to discover assets.");
            var result = provider.DiscoverAssets();
            log.Debug($"Provider {provider.GetType().Name} returned.");
            return result;
        }
        catch (Exception ex)
        {
            log.Error($"Error while discovering assets from {provider.GetType().Name}: {ex.Message}");
            return new DiscoveryResult { IsSuccess = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Returns all discovered assets from all available providers.
    /// </summary>
    public static Dictionary<IAssetDiscoveryProvider, DiscoveryResult> DiscoverAllAssets()
    {
        lock (lockObj)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(5);
            Dictionary<IAssetDiscoveryProvider, DiscoveryResult> assets =
                new Dictionary<IAssetDiscoveryProvider, DiscoveryResult>();
            var providers = AssetDiscoverySettings.Current.OrderByDescending(x => x.Priority).ToArray();
            
            foreach (var p in providers)
            {
                var provider = p; 
                // If the provider is already in the list, the Discover query timed out in the last time.
                // In that case, we should wait for the previous query to complete instead of starting a new one.
                if (_workQueue.ContainsKey(provider) == false)
                    _workQueue.TryAdd(provider, Task.Run(() => DiscoverAssets(provider)));
            }

            Task.WaitAll(_workQueue.Values.ToArray<Task>(), timeout);
            
            foreach (var provider in providers)
            {
                if (_workQueue.TryGetValue(provider, out var task))
                {
                    if (task.IsCompleted)
                    {
                        _workQueue.TryRemove(provider, out var value);
                        assets[provider] = value.Result;
                    }
                    else
                    {
                        assets[provider] = new DiscoveryResult()
                        {
                            IsSuccess = false, Error = "Timeout"
                        };
                        log.Warning(
                            $"Provider {provider.GetType().Name} is taking a long time to complete. " +
                            $"This provider will not be queried again until it finished the current query.");
                    }
                }
            }

            return assets;
        }
    }


    // public static void PushDiscoveredAssets(DiscoveredAsset asset)
    // {
    //
    // }
}
