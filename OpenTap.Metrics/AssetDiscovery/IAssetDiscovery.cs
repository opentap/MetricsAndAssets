using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OpenTap.Metrics.AssetDiscovery;

/// <summary>
/// Plugin interface for asset discovery.
/// Implementations of this interface can be used to discover assets such as instruments or DUTs connected to the system.
/// </summary>
[Display("Asset Discoverer", Description: "OpenTAP plugin that can discover assets.")]
public interface IAssetDiscovery : OpenTap.ITapPlugin
{
    /// <summary>
    /// A name short name of this asset discovery provider/implementation.
    /// For use in UIs that list instances. Same function as the Name property on OpenTap.IResource.
    /// Inheriting classes should set this property in their constructor.
    /// </summary>
    [Browsable(false)]
    string Name { get; set; }

    /// <summary>
    /// Sets the priority of this provider. In case two implementations return the same 
    /// discovered asset (same Identifier), the one from the higher priority provider is used
    /// </summary>
    double Priority { get; }

    /// <summary>
    /// Discovers assets connected to the system.
    /// </summary>
    DiscoveryResult DiscoverAssets();
}

/// <summary>
/// Base class for asset discovery implementations.
/// This is a convenience class that implements the IAssetDiscovery interface.
/// </summary>
[Display("")]
public abstract class AssetDiscovery : ValidatingObject, IAssetDiscovery
{
    private string _name;

    /// <summary>
    /// A name short name of this asset discovery provider/implementation.
    /// For use in UIs that list instances. Same function as the Name property on OpenTap.IResource.
    /// Deriving classes should set this property in their constructor.
    /// </summary>
    [Display("Asset Discovery Name", "The name of the this asset discovery provider/implementation. ")]
    public string Name
    {
        get => _name;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Asset Discovery Name cannot be null.");
            if (value == _name) return;
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    /// <summary>
    /// Sets the priority of this provider. In case two implementations return the same
    /// discovered asset (same Identifier), the one from the higher priority provider is used
    /// </summary>
    public double Priority { get; set; } = 0;

    public abstract DiscoveryResult DiscoverAssets();

    /// <summary>
    /// Overrides ToString() to return the Name.
    /// </summary>
    public override string ToString() => Name;
}

public class DiscoveryResult
{
    /// <summary>
    /// If false, the discovery cannot be performed because of an error or because the system is busy. 
    /// E.g. a test is running that might be impacted by sending a *IDN? SCPI query.
    /// An empty list in this case does not mean that no assets are available.
    /// </summary>  
    public bool IsSuccess { get; set; }

    /// <summary>
    /// If IsSuccess is false, this property should contain a short message as to why the discovery failed.
    /// </summary>
    public string Error { get; set; }
    /// <summary>
    /// List of assets discovered by a specific implementation of IAssetDiscovery.
    /// Null or an empty list only means no assets were found if IsSuccess is true.
    /// </summary>
    public IEnumerable<DiscoveredAsset> Assets { get; set; }
}

/// <summary>
/// Represents a discovered asset.
/// IAssetDiscovery implementations can specialize this class to provide additional information.
/// </summary>
public class DiscoveredAsset : IAsset
{
    /// <summary>
    /// The manufacturer of the asset. E.g. "Keysight". 
    /// Should map to the first part of the *IDN? SCPI query, or the Vendor ID in a USB descriptor.
    /// </summary>
    public string Manufacturer { get; set; }


    /// <summary>
    /// The type of the asset. E.g. "N9020A".
    /// This can be used to determine a suitable driver for the asset
    /// so it can be used as an Asset in OpenTAP.
    /// Should map to the second part of the *IDN? SCPI query, or the Product ID in a USB descriptor.
    /// </summary>
    public string Model { get; set; }
    /// <summary>
    /// A unique identifier for the asset. This is used to identify the asset in the system.
    /// E.g. for an Instrument, this could be a combination of Manufacturer, Model and serial number.
    /// </summary>
    public string Identifier { get; set; }
}

/// <summary>
/// Interface for an object that represents an asset. 
/// Instruments and other Resources should implement this interface if they want to attach metrics to the asset.
/// </summary>
public interface IAsset
{
    /// <summary>
    /// The manufacturer of the asset. E.g. "Keysight". 
    /// Should map to the first part of the *IDN? SCPI query, or the Vendor ID in a USB descriptor.
    /// </summary>
    [MetaData(Name = "Manufacturer")]
    public string Manufacturer { get; }

    /// <summary>
    /// The type of the asset. E.g. "N9020A".
    /// This can be used to determine a suitable driver for the asset
    /// so it can be used as an Asset in OpenTAP.
    /// </summary>
    [MetaData(Name = "Model")]
    public string Model { get; }
    /// <summary>
    /// A unique identifier for the asset. This is used to identify the asset in the system.
    /// E.g. for an Instrument, this could be a combination of Manufacturer, Model and serial number.
    /// </summary>
    [MetaData(Name = "ID")]
    string Identifier { get; }
}
