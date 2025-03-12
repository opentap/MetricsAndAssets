# Metrics and Assets

This plugin provides interfaces to push, poll, and subscribe to metrics. This
can be used to e.g. make periodic measurements on a connected instrument. By
formalizing the concept of push (event-based) and poll (measurement-based)
metrics, and providing a common way to produce and consume them, this plugin
enables a great deal of interoperability between different metric providers and
listeners.

This package also defines an Asset Discovery plugin type and
related functionality. This is a semi independent feature, but it is related to
metrics in that it metrics can be associated with an asset instead of the default
of associating the metric with the entire system.

## Poll Metrics

Poll metrics represent values that can be measured at any point in time. It
could, for example, be the round-trip time for a ping, or the current voltage
of an instrument. As a rule of thumb, if something can be sampled, it is
probably a poll metric. A poll metric will typically be represented as a C#
property:

```cs
public class MetricSource : IMetricSource
{
    [Metric("My Metric")]
    public double MyMetric { get; set; }
}
```

The value of `MyMetric` can be measured at any time by calling
`MetricManager.PollMetrics(...)`.

It is possible to update the value of `MyMetric` before being polled. There are
two ways to achieve this:

1. Update MyMetric in the `get` method.
2. Implement the `IOnPollMetricsCallback`

```cs
/// <summary> Defines a class which can update metrics. </summary>
public interface IOnPollMetricsCallback
{
    /// <summary> Called right before the metric manager reads PollMetric properties. </summary>   
    ///  <param name="metrics">List of metrics from this class that are about to be polled.</param>
    void OnPollMetrics(IEnumerable<MetricInfo> metrics);
}
```

If an `IMetricSource` implements this interface, `OnPollMetrics` will be called
with a list of the metrics that should be updated immediately before it calls
the getters. This is useful in a scenario where e.g. a metric source can batch
update one or more metrics at the same time, instead of updating each metric
serially in a getter.

## Push Metrics

Push metrics represent events that occur independently. This could be triggered
by a door opening, or a user making an http request. Push metrics usually
cannot be sampled. For example, it would be nonsensical to ask "What is the
value of user making http request right now".

Like poll metrics, push metrics are usually backed by a property on a metric source:


```cs
public class MetricSource : IMetricSource
{
    [Metric("My Http Metric", kind: MetricKind.Push)]
    public string OnHttpMetric { get; set; }

    public void OnHttpRequestHandled(string someString)
    {
        OnHttpMetric = someString;
        // Create a MetricInfo representing this metric
        var metricInfo = MetricManager.GetMetricInfo(this, nameof(OnHttpMetric));
        MetricManager.PushMetric(metricInfo, OnHttpMetric);
    }
}
```


Unlike `Poll` metrics, `Push` metrics cannot be polled.

## Usage

Here are the primary usage scenarios considered while designing this plugin.

### Producing metrics

Producing metrics is intended to be as simple as possible. The IMetricSource
interface is empty for this reason. The only thing required for e.g. an
Instrument to start producing metrics is to apply the `[Metric]` attribute to a
property.

The first time `MetricManager` is used, it
will create an instance of all `IMetricSource` implementations. The exception
to this rule is `ComponentSettings`, `IDUT`, and `IInstrument`. Such types are
instantiated and handled by OpenTAP. Instead of instantiating these types, this
plugin will instead query any currently configured instances of this type from
OpenTAP by using the [`ComponentSettings.Current` family of
functions](https://doc.opentap.io/Developer%20Guide/Component%20Setting/Readme.html#reading-and-writing-component-settings).
The current component settings will be queried by `MetricManager` as needed, so
newly configured resources should become available as they are added.

Because instantiation is handled by `MetricManager`, it can be difficult to get
a handle to the instances. If such a handle is required, it is possible to
query with an expression such as:
`MetricManager.GetMetricInfos().Select(metricInfo =>
metricInfo.Source).OfType<MyMetricSource>().FirstOrDefault()`

This will query all currently available metrics, select their source object,
and select the first instance which matches the type we are looking for.

As shown in the above examples, `Poll` metrics are polled automatically without
any action needed by the producer, and `Push` metrics must be manually
published with `MetricManager.PushMetric`.

The only work required to add a `Poll` metric is to apply the `[Metric]`
attribute to a property. Without specifying any parameters, the
property will become a `Poll` metric, and the name of the property 
will be used for the metric. For greater control of the display name
of the metric, or to create a `Push` metric, see the constructor options:

```cs
/// <summary> Creates a new instance of the metric attribute </summary>
///  <param name="name">Optionally, the name of the metric.</param>
///  <param name="group">The group of the metric.</param>
///  <param name="kind"> The push / poll semantics of the metric. </param>
public MetricAttribute(string name = null, string group = null, MetricKind kind = MetricKind.Poll)
{
    Name = name;
    Group = group;
    Kind = kind;
}
```


### Consuming metrics

Consuming metrics is, by nature, a bit more involved. 
Since there are two different kinds of metrics with different semantics,
there are naturally two different ways to consume them.

#### Consuming poll metrics

There are two steps to consume poll metrics:

1. Select the metrics you would like to poll. 

A list of all available metrics can be obtained by calling
`MetricManager.GetMetricInfos()`. Because metrics are provided by plugins, this
list can contain an unbounded number of metrics. While it is possible to simply
poll all metrics, it is not advised unless you know the exact environment where
your metric consumer will be used. Note that `GetMetricInfos()` also contains
`Push` metrics, which cannot be polled. The metric kind can be checked

2. Call `MetricManager.PollMetrics(selectedMetrics)` 

This will return an `IEnumerable<IMetric>`, with one `IMetric` for each
requested `poll` metric. Any `push` metrics requested will be ignored. We will
explore `IMetric` in a short moment.

#### Consuming push metrics

Consuming push metrics also requires a few steps:

1. Create an `IMetricListener`

Let's take a look at the `IMetricListener` interface:
```cs
/// <summary> Defines that a class can consume metrics. </summary>
public interface IMetricListener
{
    /// <summary>  Event occuring when a metric is pushed. </summary>
    void OnPushMetric(IMetric table);
}
```

We observe that the interface is quite simple. Unlike `IMetricSource`, classes
implementing this interface are not automatically instantiated. This is because
`MetricManager` has no way of knowing what metrics the listener is interested
in. Instead, a metric listener must be manually instantiated. In order to
start receiving metrics, the listener must then *subscribe* to the set of metrics
it would like to receive:

```cs
var listener = new MyListener();
var available = MetricManager.GetMetricInfos();
MetricManager.Subscribe(listener, available); // subscribe to all metrics
```

Similarly to being able to poll `Push` metrics, it is also possible to
subscribe to `Poll` metrics, but the event will never fire.

Let's take a look at the `IMetric` interface:

```cs
/// <summary>  A metric. This can either be a DoubleMetric  or a BooleanMetric metric. </summary>
public interface IMetric
{
    /// <summary> The metric information. </summary>
    MetricInfo Info { get; }
    /// <summary> The value of the metric. </summary>
    object Value { get; }
    /// <summary> The time the metric was recorded. </summary>
    DateTime Time { get; }
}
```

We see that we have access to the `MetricInfo` which produced this metric, the
object value, and the time. The object value is problematic because we would
like to know the type of the metric. There are two ways to get this
information:

1. Check `Info.Type`
`Info.Type` is an instance of the following enum:
```cs
public enum MetricType
{
    Unknown,
    Double,
    Boolean,
    String
}
```

By inspecting this enum, it is possible to determine an appropriate cast.

2. Try upcasting to an `IMetric` implementation There are only three different
   types of metrics, and each metric contains an appropriately typed property
for the value, so it is also possible to use a common upcasting pattern like
the following:

```cs
IMetric m;
if (m is StringMetric s)
{
    string val = s.Value;
}
else if (m is DoubleMetric d)
{
    double val = d.Value;
}
else if (m is BooleanMetric b)
{
    bool val = b.Value;
}
```

In the provided example, you may have noted that we subscribe to all metrics.
You may be wondering why `OnPushMetric` is not simply fired for all metrics at
all times. This would indeed be a simpler design, but it prevents
`IMetricSource`s from knowing if anyone is interested in an event.

`MetricManager` keeps track of the interest set of all listeners. A metric
source can check if anyone is subscribed to an event by using the `bool
MetricManager.HasInterest(MetricInfo metric)` method. This trade-off has been
made because it is not necessarily *free* to watch for an event, because it
could involve interacting with the real world, or a remote resource.

## Dynamically creating new metrics

Using attributes to declare metrics is straight-forward and easy for most use
cases, but it is not possible in scenarios where all possible metrics are not
known ahead of time. In some domains, new metrics may be added in the future,
which would require creating a new release of your metric consumer in order to
support the new metrics. For such use-cases, we support dynamic metrics through
the `IAdditionalMetrics` interface:

```cs
/// <summary> Defines a class which can provide additional metrics. </summary>
public interface IAdditionalMetricSources : IMetricSource
{
    /// <summary> The list of metrics provided by this class. 
    /// This value is read every time the set of available metrics is queried in MetricManager. </summary>
    IEnumerable<MetricInfo> AdditionalMetrics { get; }
}
```

Implementors of this interface can define a collection of available metrics
which can be updated dynamically. Dynamic metrics are not backed by properties.
Instead, they can be created at runtime by creating new instances of
`MetricInfo`. The simplest way to achieve this is to use the provided helper
methods, `MetricManager.CreatePollMetric` and `MetricManager.CreatePushMetric`.

Any metrics created in this way must be made available through the
`AdditionalMetrics` property getter before they can be used by MetricManager
and other consumers.

Dynamic metrics behave in much the same way as regular metrics. 
Let's take a look at their respective signatures:

```cs
MetricInfo CreatePollMetric<T>(IAdditionalMetricSources owner, Func<T> pollFunction, string name, string groupName) where T : IConvertible
```

`CreatePollMetric` requires a few things:

1. A reference to the owner, which must be an `IAdditionalMetricSources`.
2. A pollFunction for polling the current value.
3. A name and a group.
4. A generic type parameter T, which can usually be derived from the poll function.

The reference to the owner serves two purposes; 
1. It must be possible for the metric owner to implement the
   `IOnPollMetricsCallback`, and
2. MetricManager must be able to distinguish between equal metric names from
   different source objects.

```cs
MetricInfo CreatePushMetric<T>(IAdditionalMetricSources owner, string name, string groupName) where T : IConvertible
```

`CreatePushMetric` has similar requirements, but of course does not require a poll function. 
For this reason, though, the type parameter must be manually specified.

Let's create a simple example:

```cs

public class DynamicMetricProvider : IAdditionalMetricSources, IOnPollMetricsCallback
{
    IEnumerable<MetricInfo> IAdditionalMetricSources.AdditionalMetrics => new[] { PollMetric, PushMetric };
    public MetricInfo PollMetric { get; }
    public MetricInfo PushMetric { get; }

    public double Counter { get; private set; } = 0;

    public DynamicMetricProvider()
    {
        // Create a poll metric.
        PollMetric = MetricManager.CreatePollMetric(this, () => Counter, "Poll Metric Name", "Test Group");
        PushMetric = MetricManager.CreatePushMetric<double>(this, "Push Metric Name", "Test Group");
    }

    public void PushDouble(double value)
    {
        // Push the dynamically created push metric
        MetricManager.PushMetric(PushMetric, value);
    }

    // Update value of poll metric before it is polled
    public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
    {
        if (metrics.Contains(PollMetric))
            Counter++;
    }
}

public class DynamicMetricListener : IMetricListener
{
    public void OnPushMetric(IMetric table)
    {
        Console.WriteLine($"Got metric {table.Info.MetricFullName} with value {table.Value}");
    }
}

/// main
// Create a new listener
DynamicMetricListener listener = new DynamicMetricListener();
// Get a handle to the dynamic metric provider
DynamicMetricProvider provider = MetricManager.GetMetricInfos().Select(metricInfo => metricInfo.Source).OfType<DynamicMetricProvider>().FirstOrDefault();
// Subscribe to the dynamic metric
MetricManager.Subscribe(listener, new[] { provider.PushMetric });
// Push the dynamic metric
MetricManager.PushMetric(provider.PushMetric, 1.23);
// Poll the dynamic metric
IMetric poll = MetricManager.PollMetrics(new []{ provider.PollMetric });
Assert.That(poll.Value, Is.EqualTo(provider.Counter));
```

In this example, we created a new push and poll metric at runtime, and we
managed to push and poll a value. As you can see, it is simple to dynamically
create new metrics once everything is wired up correctly.

We run into one small challenge here, however. In our example, we knew to
subscribe our listener after our provider had created a new push metric.
However, in the other scenarios, a dynamic metric could have been created
without our knowledge in another part of the program. In order to be notified
when a new metric is created, we can subscribe to the `OnMetricCreated` event.

Let's expand a bit on the previous example:

```cs
DynamicMetricListener listener; 
/* code */
void onMetricCreated(MetricCreatedEventArgs args)
{
    MetricInfo metric = args.Metric;
    /* Handle the new metric */
}
MetricManager.OnMetricCreated += onMetricCreated;
```

The OnMetricCreated event handler is called immediately when the new metric is
created, so any listeners have the opportunity to subscribe to the first
instance of a new `Push` metric.

# Assets

This package also contains definitions for the `IAssetDiscoveryProvider` plugin type and
related functionality. This is a semi independent feature, but it is related to
metrics in that it metrics can be associated with an asset instead of the default
of associating the metric with the entire system.

## Discovering Assets

To add asset discovery capabilities to opentap, you should return a list of 
`IAsset` from the `DiscoverAssets` method in your `IAssetDiscoveryProvider` 
implementation. When used in e.g. a Runner DiscoverAssets() will be called periodically
so the results can be collected in KS8500.
    
```cs
public class MyAssetDiscovery : IAssetDiscovery
{
    public DiscoveryResult DiscoverAssets()
    {
        // TODO: Code that queries e.g. the network, or other peripherals to find assets
    }
}
```

Each `IAsset` must contain a unique `AssetIdentifier`, a `Manufacturer` and a `Model`. The identifier
is used to identify this specific asset and should be the same if the asset is later connected
to a different system (e.g. Runner/Station). The manufacturer and model together describe the asset
model and can be be used to lookup a suitable driver for the asset.

`IAssetDiscoveryProvider` implementations can also add additional properties when implementing the `IAsset` 
model to include additional information (e.g. firmware version).

As assets from different implementations are usually displayed in the same list, it is recommended
to use these property Display names when applicable to provide a consistent user experience.

| Property Display Name | Meaning |
| --- | --- |
| AssetName | A user friendly name used in UIs to refer to the asset. Users can edit this later. |
| AssetIdentifier | A unique identifier for the asset (required) |
| Model | The model of the asset (e.g. "N9030A" as provided by *IDN?) (required) |
| Manufacturer | The manufacturer of the asset (e.g. "Keysight Technologies" as provided by *IDN?) (required) |
| Address | The address of the asset (e.g. VISA address for an instrument) |
| SerialNumber | The serial number of the asset (e.g. "US00071182" as provided by *IDN?) |
| FirmwareVersion | The firmware version of the asset (e.g. "A.14.16" as provided by *IDN?) |
| Alias | A user-friendly name for the connection string as normally used in VISA |
| Class | The class of the asset (e.g. HID device class for USB) |
| InterfaceType | The type of interface used to connect to the asset (e.g. GPIB,LAN,PXI,USB) |

Some additional metadata is attached to the asset. These values does not have to be specified by the 
`IAssetDiscoveryProvider` implementation. However, many of them can be overridden if more control is needed,
e.g. a provider can set LastSeen to a specific time if the provider is returning older cached results.

| Property Display Name | Meaning |
| --- | --- |
| LastSeen | The last time the asset was returned from DiscoverAssets(). |
| Provider | The name of the IAssetDiscovery implementation returning the asset |
| RunnerId | The ID of the Runner/Station that discovered the asset |
| ProviderError | An error message explaining why assets could not be discovered |
| AutoDiscovered | A flag indicating if the asset was discovered automatically 


## Adding information to an asset using Metrics

In some situations, the `IAssetDiscovery` implementation cannot generically get the desired 
information from all possible asset models that it can discover. In these cases, the asset 
driver can provide additional information about the asset using metrics. To do this, the driver
should implement IAsset and be sure to set the same string in the `IAsset.Identifier` property
as the `DiscoveredAsset.Identifier` property.

```cs
public class MyInstrument : ScpiInstrument, IOnPollMetricCallback, IAsset
{
    [Metadata]
    public string Identifier { get; }

    [Metric("Calibration Date", DefaultPollRate: 3600, DefaultEnabled: true)]
    public DateTime CalibrationDate { get; private set; }

    public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
    {
        bool shouldClose = false;
        if (!this.IsConnected)
        {
            this.Open();
            shouldClose = true;
        }

        try
        {
            var parts = this.IdnString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            this.Identifier = parts[0] + parts[1] + parts[2];
            this.Model = parts[1];
            var calInfoResponse = ScpiQuery("SYSTem:SERVice:MANagement:CALibration:INFormation?"));
            var calInfo = JsonConvert.DeserializeObject<Dictionary<string, string>>(calInfoResponse);
            this.CalibrationDate = DateTime.Parse(calInfo["CalDate"]);
        }
        finally
        {
            if (shouldClose)
                this.Close();
        }
    }
}
```

This package also provides `AssetMetricInstrument` and `AssetMetricScpiInstrument` base classes
that can be used to simplify the implementation in the above example. These classes also implement a 
mutex to ensure that the instrument is only Opened once at a time to prevent metrics polling from 
interfering with ongoing TestPlan runs. Using these classes, the above example can be simplified to:

```cs
public class MyInstrument : AssetMetricScpiInstrument
{
    [Metric("Calibraion Date", null, MetricKind.PushPoll, DefaultPollRate = 3600, DefaultEnabled = true)]
    public DateTime CalibrationDate { get; set; }

    protected override void UpdateAssetMetrics()
    {
        string calInfoRaw = ScpiQuery("SYSTem:SERVice:MANagement:CALibration:INFormation?"); 
        var calInfo = JsonConvert.DeserializeObject<Dictionary<string, string>>(calInfoResponse);
        this.CalibrationDate = DateTime.Parse(calInfo["CalDate"]);
    }
}
```
