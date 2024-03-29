using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Storage;

namespace Orleans.Multitenant.Internal;

static class TenantGrainStorageFactoryFactory
{
    public static ITenantGrainStorageFactory Create<TGrainStorage, TGrainStorageOptions, TGrainStorageOptionsValidator>(IServiceProvider services, string name, Action<TGrainStorageOptions, string>? configureTenantOptions = null)
        where TGrainStorage : IGrainStorage
        where TGrainStorageOptions : class, new()
        where TGrainStorageOptionsValidator : class, IConfigurationValidator
     => configureTenantOptions is null ?
            ActivatorUtilities.CreateInstance<TenantGrainStorageFactory<TGrainStorage, TGrainStorageOptions, TGrainStorageOptionsValidator>>(services, name) :
            ActivatorUtilities.CreateInstance<TenantGrainStorageFactory<TGrainStorage, TGrainStorageOptions, TGrainStorageOptionsValidator>>(services, name, configureTenantOptions);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is instantiated through DI")]
sealed class TenantGrainStorageFactory<TGrainStorage, TGrainStorageOptions, TGrainStorageOptionsValidator> : ITenantGrainStorageFactory
    where TGrainStorage : IGrainStorage
    where TGrainStorageOptions : class, new()
    where TGrainStorageOptionsValidator : class, IConfigurationValidator
{
    readonly string name;
    readonly Action<TGrainStorageOptions, string>? configureTenantOptions;
    readonly IServiceProvider services;
    readonly ILogger<MultitenantStorage> logger;

    public TenantGrainStorageFactory(string name, IServiceProvider services, ILogger<MultitenantStorage> logger)
    {
        this.name = name;
        this.services = services;
        this.logger = logger;
    }

    public TenantGrainStorageFactory(string name, Action<TGrainStorageOptions, string> configureTenantOptions, IServiceProvider services, ILogger<MultitenantStorage> logger)
    {
        this.name = name;
        this.configureTenantOptions = configureTenantOptions;
        this.services = services;
        this.logger = logger;
    }

    public IGrainStorage Create(string tenantId)
    {
        string tenantProviderName = string.IsNullOrEmpty(tenantId) ? name : $"{tenantId}_{name}";
        logger.CreatingTenantProvider(typeof(TGrainStorage), tenantId, tenantProviderName);

        var options = ActivatorUtilities.CreateInstance<TGrainStorageOptions>(services);

        configureTenantOptions?.Invoke(options, tenantId);

        if (options is IStorageProviderSerializerOptions serializerOptions && serializerOptions.GrainStorageSerializer == default)
            serializerOptions.GrainStorageSerializer = services.GetKeyedService<IGrainStorageSerializer>(name) ?? services.GetRequiredService<IGrainStorageSerializer>();

        var validator = ActivatorUtilities.CreateInstance<TGrainStorageOptionsValidator>(services, options, tenantProviderName);
        validator.ValidateConfiguration();

        return ActivatorUtilities.CreateInstance<TGrainStorage>(services, tenantProviderName, options);
    }
}
