using System.Linq;

namespace OpenTap.Metrics.AssetDiscovery;

[Display("Asset Discovery Providers", "List of asset discovery implementations to use.")]
public class AssetDiscoverySettings : ComponentSettingsList<AssetDiscoverySettings, IAssetDiscoveryProvider>
{
    public override void Initialize()
    {
        // By default, add all available asset discovery providers.
        // this will be overridden by the deserializer, if we have a saved configuration.
        var assetDiscoveryProviders = TypeData.GetDerivedTypes<IAssetDiscoveryProvider>().Where(x => x.CanCreateInstance)
            .Select(x => x.CreateInstance() as IAssetDiscoveryProvider);
        foreach (var provider in assetDiscoveryProviders)
        {
            Add(provider);
        }
    }
}



