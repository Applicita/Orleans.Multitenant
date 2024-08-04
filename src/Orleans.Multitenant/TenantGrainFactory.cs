using Orleans.Multitenant.Internal;

namespace Orleans.Multitenant;

/// <summary>A tenant-specific grain factory</summary>
/// <remarks>
/// In a <see cref="IAddressable"/> context (e.g. a <see cref="Grain"/>) use the <see cref="AddressableExtensions"/> methods to instantiate;<br />
/// In other contexts (e.g. an <see cref="IClusterClient"/>) use the <see cref="GrainFactoryExtensions"/> methods to instantiate
/// </remarks>
[SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Instances of the value type will not be compared to each other")]
public readonly struct TenantGrainFactory
{
    internal readonly IGrainFactory factory;

    internal readonly ReadOnlyMemory<byte> tenantId;
    // We don't use ReadOnlySpan here because that would require TenantGrainFactory to be a ref struct
    // which would severely limit where developers can store TenantGrainFactories

    internal TenantGrainFactory(IGrainFactory factory, IAddressable grain)
    {
        this.factory = factory;
        tenantId = new(grain.GetGrainId().TryGetTenantId().ToArray());
    }

    internal TenantGrainFactory(IGrainFactory factory, string? tenantIdString = null)
    {
        this.factory = factory;
        tenantId = tenantIdString.AsTenantId().ToArray();
    }

    /// <summary>Gets a reference to a tenant speficic grain.</summary>
    /// <typeparam name="TGrainInterface">The interface type.</typeparam>
    /// <param name="keyWithinTenant">The part of the grain key that identifies it within this <see cref="TenantGrainFactory"/>'s tenant.</param>
    /// <param name="grainClassNamePrefix">An optional class name prefix used to find the runtime type of the grain.</param>
    /// <returns>A reference to the specified grain.</returns>
    public TGrainInterface GetGrain<TGrainInterface>(string keyWithinTenant, string? grainClassNamePrefix = null) where TGrainInterface : IGrainWithStringKey
    {
        ArgumentNullException.ThrowIfNull(keyWithinTenant);
        return factory.GetGrain<TGrainInterface>(tenantId.Span.GetTenantQualifiedKey(keyWithinTenant).ToString(), grainClassNamePrefix);
    }

    /// <summary>Returns a reference to the tenant specific grain which is the primary implementation of the provided interface type and has the provided primary key.</summary>
    /// <param name="grainInterfaceType">The grain interface type which the returned grain reference must implement.</param>
    /// <param name="keyWithinTenant">The part of the grain key that identifies it within this <see cref="TenantGrainFactory"/>'s tenant.</param>
    /// <returns>A reference to the grain which is the primary implementation of the provided interface type.</returns>
    public IGrain GetGrain(Type grainInterfaceType, string keyWithinTenant)
    {
        ArgumentNullException.ThrowIfNull(keyWithinTenant);
        return factory.GetGrain(grainInterfaceType, tenantId.Span.GetTenantQualifiedKey(keyWithinTenant).ToString());
    }
}
