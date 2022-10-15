using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Storage;
using Orleans.TestingHost;

namespace OrleansMultitenant.Tests;

public sealed class ClusterFixture : IDisposable
{
    internal const string TenantAwareStreamProviderName = "TenantAwareStreamProvider";
    internal const string TenantUnawareStreamProviderName = "TenantUnawareStreamProvider";

    public ClusterFixture()
    {
        var builder = new TestClusterBuilder()
            .AddSiloBuilderConfigurator<SiloConfigurator>()
            .AddClientBuilderConfigurator<ClientConfigurator>();

        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose() => Cluster.StopAllSilos();

    public TestCluster Cluster { get; }

    class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder) => siloBuilder
            .ConfigureLogging(l => l.AddProcessing())
            .AddMultitenantCommunicationSeparation(_ => new CrossTenantAccessAuthorizer())
            .AddMultitenantGrainStorageAsDefault<
                MemoryGrainStorage,
                MemoryGrainStorageOptions,
                MemoryGrainStorageOptionsValidator>((siloBuilder, name) => siloBuilder.AddMemoryGrainStorage(name))
            .AddMultitenantStreams(
                TenantAwareStreamProviderName, (silo, name) => silo
                .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(name)
                .AddMemoryGrainStorage(name)
             )
            .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(TenantUnawareStreamProviderName)
            .AddMemoryGrainStorage(TenantUnawareStreamProviderName);
    }

    class ClientConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) => clientBuilder
            .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(TenantAwareStreamProviderName)
            .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(TenantUnawareStreamProviderName);
    }
}

class CrossTenantAccessAuthorizer : ICrossTenantAuthorizer
{
    /// <remarks>static can be used to access the same object instances in silo's and tests, because <see cref="TestCluster"/> uses in-process silo's</remarks>
    internal static ConcurrentQueue<(string? sourceTenantId, string? targetTenantId)> AccessChecks { get; } = new();

    public bool IsAccessAuthorized(string? sourceTenantId, string? targetTenantId)
    {
        if (sourceTenantId == targetTenantId) throw new InvalidOperationException($"sourceTenantId and targetTenantId are equal ({sourceTenantId ?? "NULL"})");
        AccessChecks.Enqueue((sourceTenantId, targetTenantId));
        return false;
    }
}
