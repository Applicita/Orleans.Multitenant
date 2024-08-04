using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Storage;

namespace Orleans.Multitenant.Internal;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is instantiated through DI")]
sealed class MultitenantStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>
{
    readonly string name;
    readonly MultitenantStorageOptions options;
    readonly ITenantGrainStorageFactory tenantGrainStorageFactory;
    readonly ILogger<MultitenantStorage> logger;
    readonly ConcurrentDictionary<string, AsyncLock> createTenantStorageProviderLocks = new();
    SiloLifecycleRepeater? siloLifecycleRepeater;
    readonly ConcurrentDictionary<string, IGrainStorage> tenantStorageProviders = new();

    // - Accessing a grain implies that it exists so we dont need a check on tenant existance
    // - Deleting a tenant is part of tenant management, which is by design outside the scope of Orleans.Multitenant.
    //   One approach to do this:
    //   1) Orleans clients check a tenant manager before they invoke any tenant grain, so no access to the deleted tenant occurs (caching with timeout).
    //   2) Then the in-memory grains die out naturally (is there a way to know when / force this to happen?)
    //   3) The storage can be deleted

    public MultitenantStorage(
        string name,
        MultitenantStorageOptions options,
        IServiceProvider serviceProvider,
        ILogger<MultitenantStorage> logger)
     => (this.name, this.options, tenantGrainStorageFactory, this.logger) = 
        (name, options, serviceProvider.GetRequiredKeyedService<ITenantGrainStorageFactory>(name), logger);

    public async Task ClearStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var provider = await GetTenantStorageProvider(grainId).ConfigureAwait(false);
        await provider.ClearStateAsync(grainType, grainId, grainState).ConfigureAwait(false);
    }

    public async Task ReadStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var provider = await GetTenantStorageProvider(grainId).ConfigureAwait(false);
        await provider.ReadStateAsync(grainType, grainId, grainState).ConfigureAwait(false);
    }

    public async Task WriteStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var provider = await GetTenantStorageProvider(grainId).ConfigureAwait(false);
        await provider.WriteStateAsync(grainType, grainId, grainState).ConfigureAwait(false);
    }

    public void Participate(ISiloLifecycle observer)
    {
        logger.LogInformation(LoggingEvent.MultiTenantStorageProviderParticipating.Id(), "provider name = {" + nameof(LoggingParameter.ProviderName) + "}", name);
        siloLifecycleRepeater = new(observer, logger);
    }

    async Task<IGrainStorage> GetTenantStorageProvider(GrainId grainId)
    {
        string tenantId = grainId.GetTenantId() ?? options.TenantIdForNullTenant;

        if (!tenantStorageProviders.TryGetValue(tenantId, out var grainStorage))
        {
            var createTenantStorageProviderLock = createTenantStorageProviderLocks.GetOrAdd(tenantId, _ => new AsyncLock());
            using (await createTenantStorageProviderLock.LockAsync().ConfigureAwait(false))
            {
                if (!tenantStorageProviders.TryGetValue(tenantId, out grainStorage))
                {
                    grainStorage = tenantGrainStorageFactory.Create(tenantId);

                    if (grainStorage is ILifecycleParticipant<ISiloLifecycle> participant)
                    {
                        if (siloLifecycleRepeater is null)
                            throw new InvalidOperationException("Attempt to access grain storage before storage provider has participated in silo life cycle");

                        var simulator = new SiloLifecycleSimulator(siloLifecycleRepeater, logger);
                        participant.Participate(simulator); // Subscriptions to the simulator are registered here

                        logger.StartingTenantProvider(tenantId, options.TenantStorageProviderInitTimeout.TotalSeconds);
                        using var cts = new CancellationTokenSource();
                        cts.CancelAfter(options.TenantStorageProviderInitTimeout);
                        await simulator.ReplayOnStartHistory(cts.Token).ConfigureAwait(false); // Invokes any subscriptions registered on the simulator
                                                                         // Before a storage provider is fully started by Orleans, it is not accessed for grain state
                                                                         // So we treat the first grain access for a tenant as the point where the start recording is completed, and replay it.
                        logger.StartedTenantProvider(tenantId);
                    }

                    tenantStorageProviders[tenantId] = grainStorage;
                }
                _ = createTenantStorageProviderLocks.TryRemove(tenantId, out _);
            }
        }

        return grainStorage;
    }
}

sealed class MultitenantStorageOptionsValidator(MultitenantStorageOptions options, string name) : IConfigurationValidator
{
    public void ValidateConfiguration()
    {
        double timeout = options.TenantStorageProviderInitTimeout.TotalSeconds;
        double min = MultitenantStorageOptions.MinimumTenantStorageProviderInitTimeoutInSeconds;
        double max = MultitenantStorageOptions.MaximumTenantStorageProviderInitTimeoutInSeconds;
        if (timeout < min || timeout > max)
            throw new ArgumentOutOfRangeException(nameof(options.TenantStorageProviderInitTimeout), $"Timeout for provider {name} of {timeout} seconds falls outside the valid range of [{min}..{max}] seconds");
    }
}

static class MultitenantStorageFactory
{
    public static MultitenantStorage Create(IServiceProvider services, string name)
    {
        var optionsMonitor = services.GetRequiredService<IOptionsMonitor<MultitenantStorageOptions>>();
        return ActivatorUtilities.CreateInstance<MultitenantStorage>(services, name, optionsMonitor.Get(name));
    }
}
