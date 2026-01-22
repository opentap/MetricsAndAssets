using System;
using System.Linq;
using System.Reflection;

namespace OpenTap.Metrics.AssetDiscovery;

[Display("Asset Discovery", "List of asset discovery providers to use.")]
public class AssetDiscoverySettings : ComponentSettingsList<AssetDiscoverySettings, IAssetDiscoveryProvider>
{
    private static TraceSource log = Log.CreateSource("Asset Discovery Settings");
    
    public override void Initialize()
    {
        // By default, add all available asset discovery providers.
        // this will be overridden by the deserializer, if we have a saved configuration.
        
        foreach (var providerType in TypeData.GetDerivedTypes<IAssetDiscoveryProvider>().Where(x => x.CanCreateInstance))
        {
            try
            {
                var provider = (IAssetDiscoveryProvider)providerType.CreateInstance();
                Add(provider);
            }
            catch (Exception e0)
            {
                // log
                Exception e = (e0 as TargetInvocationException)?.InnerException ?? e0;
                log.Error($"Asset provider {providerType} threw an exception: {e.Message}.");
                log.Debug(e);

            }
        }
    }
}



