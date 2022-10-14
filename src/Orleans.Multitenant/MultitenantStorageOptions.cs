namespace Orleans.Multitenant;

public sealed class MultitenantStorageOptions
{
    /// <summary>
    /// Must be in the range [<inheritdoc cref="MinimumTenantStorageProviderInitTimeoutInSeconds" path="//value"/>..<inheritdoc cref="MaximumTenantStorageProviderInitTimeoutInSeconds" path="//value"/>] seconds<br />
    /// Default value is <inheritdoc cref="DefaultTenantStorageProviderInitTimeoutInSeconds" path="//value"/> seconds
    /// </summary>
    public TimeSpan TenantStorageProviderInitTimeout { get; set; } = TimeSpan.FromSeconds(DefaultTenantStorageProviderInitTimeoutInSeconds);

    /// <value>1</value>
    public const int MinimumTenantStorageProviderInitTimeoutInSeconds = 1;

    /// <value>20</value>
    public const int DefaultTenantStorageProviderInitTimeoutInSeconds = 20;

    /// <value>600</value>
    public const int MaximumTenantStorageProviderInitTimeoutInSeconds = 600;

    /// <summary>
    /// When a grain does not belong to a tenant (because the grain was not created via the tenant aware grain factory, so it's tenant id is null) and it's state is stored in a multitenant-enabled storage provider,
    /// the value of <see cref="TenantIdForNullTenant"/> is passed as the tenantId parameter of the configureTenantOptions action that is specified
    /// in AddMultitenantGrainStorage methods (e.g. <see cref="SiloBuilderExtensions.AddMultitenantGrainStorage{TGrainStorage, TGrainStorageOptions, TGrainStorageOptionsValidator}(Hosting.ISiloBuilder, string, Func{Hosting.ISiloBuilder, string, Hosting.ISiloBuilder}, Action{TGrainStorageOptions, string}?, Action{Microsoft.Extensions.Options.OptionsBuilder{MultitenantStorageOptions}}?)"/>)<br />
    /// This allows to differentiate between an empty string tenant Id and a null tenant Id in multitenant storage 
    /// </summary>
    public string TenantIdForNullTenant { get; set; } = "Null";
}
