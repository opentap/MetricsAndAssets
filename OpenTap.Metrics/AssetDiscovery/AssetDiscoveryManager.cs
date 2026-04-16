using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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


    /* Asset providers can be really slow, so it is useful internally to have a mechanism for getting somewhat recent assets.
     * The alternative would be a massive slowdown since this path is triggered by TypeData searchers */
    private static readonly TimeSpan CacheStaleTime = TimeSpan.FromSeconds(3);
    internal static Dictionary<IAssetDiscoveryProvider, DiscoveryResult> GetRecentAssets()
    {
        if (_cachedAssets == null || DateTime.Now - _lastPoll > CacheStaleTime)
            DiscoverAllAssets();
        return new Dictionary<IAssetDiscoveryProvider, DiscoveryResult>(_cachedAssets);
    }

    private static DateTime _lastPoll = DateTime.MinValue;
    private static Dictionary<IAssetDiscoveryProvider, DiscoveryResult> _cachedAssets = null;

    internal static Task<T> StartAwaitableTapThread<T>(Func<T> action)
    {
        var result = new TaskCompletionSource<T>();
        TapThread.Start(() =>
        {
            try
            {
                result.SetResult(action());
            }
            catch (Exception inner)
            {
                result.SetException(inner);
            }
        });
        return result.Task;
    }


    /// <summary>
    /// Returns all discovered assets from all available providers.
    /// </summary>
    public static Dictionary<IAssetDiscoveryProvider, DiscoveryResult> DiscoverAllAssets()
    {
        /* if another thread is holding lockObj, we should just return that result instead of recomputing it. */
        if (!Monitor.TryEnter(lockObj, 0)) 
        {
            /* lock on lockObj to wait for the other thread to finish */
            lock (lockObj)
            {
                return new Dictionary<IAssetDiscoveryProvider, DiscoveryResult>(_cachedAssets);
            }
        }

        try
        {
            TimeSpan timeout = TimeSpan.FromSeconds(10);
            Dictionary<IAssetDiscoveryProvider, DiscoveryResult> assets =
                new Dictionary<IAssetDiscoveryProvider, DiscoveryResult>();
            var providers = AssetDiscoverySettings.Current.OrderByDescending(x => x.Priority).ToArray();
            
            foreach (var p in providers)
            {
                var provider = p; 
                // If the provider is already in the list, the Discover query timed out in the last time.
                // In that case, we should wait for the previous query to complete instead of starting a new one.
                if (_workQueue.ContainsKey(provider) == false)
                    _workQueue.TryAdd(provider, StartAwaitableTapThread(() => DiscoverAssets(provider)));
            }

            Task.WaitAll(_workQueue.Values.ToArray<Task>(), timeout);
            
            foreach (var provider in providers)
            {
                if (_workQueue.TryGetValue(provider, out var task))
                {
                    if (task.IsCompleted)
                    {
                        _workQueue.TryRemove(provider, out _);
                        assets[provider] = task.Result;
                    }
                    else
                    {
                        assets[provider] = new DiscoveryResult()
                        {
                            IsSuccess = false, Error = "Scan in progress"
                        };
                        log.Warning(
                            $"Provider {provider.GetType().Name} is taking a long time to complete. " +
                            $"This provider will not be queried again until it finished the current query.");
                    }
                }
            }

            _cachedAssets = assets;
            _lastPoll = DateTime.Now;
            /* return the result in a new dictionary to ensure callers can safely mutate the result without affecting users of the cache. */
            return new Dictionary<IAssetDiscoveryProvider, DiscoveryResult>(assets);
        }
        finally
        {
            Monitor.Exit(lockObj);
        }
    }


    // public static void PushDiscoveredAssets(DiscoveredAsset asset)
    // {
    //
    // }
}
