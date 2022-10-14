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

    public string TenantIdForNullTenant { get; set; } = "Null";
}
