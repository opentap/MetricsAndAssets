using System.Linq;

namespace OpenTap.Metrics.AssetDiscovery;

[Display("Asset Discovery", "List of asset discovery implementations to use.")]
public class AssetDiscoverySettings : ComponentSettingsList<AssetDiscoverySettings, IAssetDiscovery>
{
    public override void Initialize()
    {
        // By default, add all available asset discovery providers.
        // this will be overridden by the deserializer, if we have a saved configuration.
        var assetDiscoveryProviders = TypeData.GetDerivedTypes<IAssetDiscovery>().Where(x => x.CanCreateInstance)
            .Select(x => x.CreateInstance() as IAssetDiscovery);
        foreach (var provider in assetDiscoveryProviders)
        {
            Add(provider);
        }
    }
}



