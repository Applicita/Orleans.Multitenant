# Orleans.Multitenant
Secure, flexible tenant separation for Microsoft Orleans 4

- **Separate storage** per tenant for any Orleans storage provider, using your own logic:<br />
  ![Example Azure Table Storage](img/example-azure-table-storage.png)
- **Separate communication** for grain calls and streams, or use your own logic to allow specific access between tenants:<br /> 
  ![Example Access Authorizer](img/example-access-authorizer.png)<br />

- **Choose where to use** multitenant does not have to be all-in; combine regular stream/storage providers with multitenant ones. Want to add multitenant storage to an existing application? You can bring along existing grain state in the null tenant. Or add another named storage provider for multitenant grains and keep the existing non-multitenant provider as well<br />

- **Secure** guards against development mistakes: using a non-tenant aware API on a tenant aware stream is blocked and logged. Unauthorized access to a tenant specific grain or stream throws an `UnauthorizedException`<br />

Scope and limitations:
- Orleans.Multitenant guards against unauthorized access from within a grain, since only in a grain there exists a tenant-specific context (the grain ID contains the tenant ID). Guarding against unauthorized tenant access from outside a grain (e.g. in an ASP.NET controller) is in the domain of the application developer, since what constitutes a tenant context there is application specific.

- Only `IGrainWithStringKey` grains can be tenant specific.

## Features
All multitenant features can be independenty enabled and configured at silo startup, with the `ISiloBuilder` `AddMultitenant*` extension methods.
See the inline documentation for more details on how to use the API's that are mentioned in this readme. All the public API's come with full inline documentation.

### Add multitenant storage
Use `AddMultitenantGrainStorage` and `AddMultitenantGrainStorageAsDefault` on an `ISiloBuilder` or `IServiceCollection` to add tenant storage separation to any Orleans storage provider:

```csharp 
siloBuilder
.AddMultitenantGrainStorageAsDefault<AzureTableGrainStorage, AzureTableStorageOptions, AzureTableGrainStorageOptionsValidator>(
    (silo, name) => silo.AddAzureTableGrainStorage(name, options =>
        options.ConfigureTableServiceClient(tableStorageConnectionString)),
        // Called during silo startup, to ensure that any common dependencies
        // needed for tenant-specific provider instances are initialized

    configureTenantOptions: (options, tenantId) => {
        options.ConfigureTableServiceClient(tableStorageConnectionString);
        options.TableName = $"OrleansGrainState{tenantId}";
    }   // Called on the first grain state access for a tenant in a silo,
        // to initialize the options for the tenant-specific provider instance
        // just before it is instantiated
 )
```

### 
Use `AddMultitenantStreams` to configure a silo to use a specific stream provider type as a named stream provider, with tenant separation. Any Orleans stream provider can be used:
```csharp
.AddMultitenantStreams(
    TenantAwareStreamProviderName, (silo, name) => silo
    .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(name)
    .AddMemoryGrainStorage(name)
 )
```

### Enforce tenant separation for grain communication
Use `AddMultitenantGrainCommunicationSeparation` to configure a silo to use tenant separation for grain communication. Separation will be enforced for both grain calls and streams (the latter if used together with `AddMultitenantStreams`)

Optionally pass in an `ICrossTenantAuthorizer` factory and/or an `IGrainCallTenantSeparator` factory, to control which tenants are authorized to communicate, and which grain calls require authorization:
```csharp
.AddMultitenantGrainCommunicationSeparation(_ => new CrossTenantAccessAuthorizer())
```
```csharp
class ExtendedCrossTenantAccessAuthorizer : ICrossTenantAuthorizer
{
    internal const string RootTenantId = "RootTenant";

    public bool IsAccessAuthorized(string? sourceTenantId, string? targetTenantId)
    =>  string.CompareOrdinal(sourceTenantId, RootTenantId) == 0;
    // Allow access from the root tenant to any tenant
}
```

By default different tenants are not authorized to communicate, and only calls to `Orleans.*` grain interfaces are exempted from authorization.

- An attempt to make an unauthorized grain call causes an `UnauthorizedAccessException` to be thrown. The call does not reach the target grain.
- An unauthorized attempt to access a stream provider using `GetTenantStreamProvider` causes an `UnauthorizedAccessException` to be thrown.
- An attempt to publish an event to a multitenant stream without using `GetTenantStreamProvider` (i.e. using the Orleans built-in `GetStreamProvider` API) causes the event to be blocked by the stream filter; an error with event Id `TenantUnawareStreamApiUsed` is logged in the silo log.<br />
  (also see `AddMultitenantStreams`)

### Access tenant specific grains and streams in a grain
- use the `IGrainFactory` extension method `factory.ForTenantOf(this)` to get a tenant grain factory from within an `IAddressable` (i.e. a grain), for the tenant that this grain belongs to. This is a very lightweight, allocation-free factory wrapper (`readonly struct`); it can be stored/cached if desired but it's overhead is extremely low even without that.<br />

- use `factory.ForTenant("tenant id")` to access grains that belong to another tenant.<br />

- use the `Grain` extension method `this.GetTenantStreamProvider("provider name") to Get a tenant stream provider for the tenant that this grain belongs to. This is a very lightweight, allocation-free stream provider wrapper (`readonly struct`); it can be stored/cached if desired but it's overhead is extremely low even without that.<br />

- use `this.GetTenantStreamProvider("provider name", "tenant id") to access streams that belong to another tenant.<br />

When `AddMultitenantGrainCommunicationSeparation` is used, all of the above is guarded against unautorized access.

### Access tenant specific grains and streams outside a grain
Outside a grain, e.g. in a `IClusterClient`:
- use `this.GetTenantStreamProvider("provider name", "tenant id") to access streams that belong to another tenant.<br />

- use `factory.ForTenant("tenant id")` to access grains that belong to a tenant.<br />

**Note** that guarding against unauthorized tenant access from outside a grain (e.g. in an ASP.NET controller) is in the domain of the application developer, since what constitutes a tenant context there is application specific.

### Grain/Stream key and tenant id
Tenant id's are stored in the key of a tenant specific `GrainId` / `StreamId`. Use these methods when you need to access the individual parts of the key:
```csharp
string? GetTenantId(this IAddressable grain)
string GetKeyWithinTenant(this IAddressable grain)

string? GetTenantId(this StreamId streamId)
string GetKeyWithinTenant(this StreamId streamId)
```

Tenant Id's can be any string; the creation and lifecycle management of tenant Id's is in the application domain; as far as Orleans.Multitenant is concerned, tenants are **virtual** just like grains and streams - so conceptually all possible tenant id's always exist.



