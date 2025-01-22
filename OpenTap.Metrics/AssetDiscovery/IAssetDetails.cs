using System;
using System.Collections.Generic;
using System.Linq;
using OpenTap.Package;

namespace OpenTap.Metrics.AssetDiscovery;

/// <summary>
/// Plugin interface associated with asset discovery.
/// Implementations of this interface can be used to discover additional details for a specific assets.
/// </summary>
public interface IAssetDetails : OpenTap.IResource
{
    /// <summary>
    /// When called on a connected resource, makes queries to the resource to gather additional details
    /// </summary>
    public DiscoveredAsset GetAssetDetails();
}

/// <summary>
/// Attribute that must be used to decorate IAssetDetails implementations to specify which asset manufacturer and model they provide details for.
/// </summary>
public class AssetDetailsSelectorAttribute : Attribute
{
    public AssetDetailsSelectorAttribute(string manufacturer, string model, double priority = 0)
    {
        Manufacturer = manufacturer;
        Model = model;
        Priority = priority;
    }

    public string Manufacturer { get; }
    public string Model { get; }
    public double Priority { get; set; }
}

/// <summary>
/// Custom package action plugin that exposes the AssetDetailsSelectorAttribute in the package metadata.
/// </summary>
public class AssetDetailsSelectorPackageAction : OpenTap.Package.ICustomPackageAction
{
    public PackageActionStage ActionStage => PackageActionStage.Create;
    public int Order() => 0;

    public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
    {
        foreach (PluginFile file in package.Files)
        {
            bool isAssetDetailsImplementation = file.BaseType.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Any(tn => tn == typeof(AssetDetailsSelectorAttribute).FullName);


        }
