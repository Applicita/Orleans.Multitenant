using Microsoft.Extensions.Configuration;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Storage;
using Orleans.TestingHost;
using OrleansMultitenant.Tests.Examples.Extensibility;

namespace OrleansMultitenant.Tests.UnitTests;

public sealed class ExtensibilityTests : IClassFixture<ExtensibilityTests.ClusterFixture>
{
    readonly TestCluster cluster;

    public ExtensibilityTests(ClusterFixture fixture) => cluster = fixture.Cluster;

    [Fact]
    public async Task ExtendedCrossTenantAccessAuthorizer_ForGrainCallWithinTenant_IsNotInvoked()
    {
        var tenantAGrain = Factory.ForTenant("TenantA").GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedCrossTenantAccessAuthorizer.AccessChecks.Clear();

        await tenantAGrain.CallTenantSpecificGrain(ThisTestMethodId("Other"));

        Assert.Empty(ExtendedCrossTenantAccessAuthorizer.AccessChecks);
    }

    [Fact]
    public async Task ExtendedCrossTenantAccessAuthorizer_ForAuthorizedGrainCallAcrossTenants_IsInvokedAndAllowsGrainCall()
    {
        var rootTenantGrain = Factory.ForTenant(ExtendedCrossTenantAccessAuthorizer.RootTenantId).GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedCrossTenantAccessAuthorizer.AccessChecks.Clear();

        await rootTenantGrain.CallTenantSpecificGrain("TenantB", ThisTestMethodId("Other"));

        var accessCheck = Assert.Single(ExtendedCrossTenantAccessAuthorizer.AccessChecks);
        Assert.Equal((ExtendedCrossTenantAccessAuthorizer.RootTenantId, "TenantB"), accessCheck);
    }

    [Fact]
    public async Task ExtendedCrossTenantAccessAuthorizer_ForUnauthorizedGrainCallAcrossTenants_IsInvokedAndThrowsException()
    {
        var tenantAGrain = Factory.ForTenant("TenantA").GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedCrossTenantAccessAuthorizer.AccessChecks.Clear();

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tenantAGrain.CallTenantSpecificGrain("TenantB", ThisTestMethodId("Other"))
        );

        var accessCheck = Assert.Single(ExtendedCrossTenantAccessAuthorizer.AccessChecks);
        Assert.Equal(("TenantA", "TenantB"), accessCheck);
    }

    [Fact]
    public async Task ExtendedCrossTenantAccessAuthorizer_ForGetTenantStreamProviderWithinTenant_IsNotInvoked()
    {
        var tenantAGrain = Factory.ForTenant("TenantA").GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedCrossTenantAccessAuthorizer.AccessChecks.Clear();

        await tenantAGrain.GetTenantSpecificStreamProvider(ClusterFixture.StreamProviderName);

        Assert.Empty(ExtendedCrossTenantAccessAuthorizer.AccessChecks);
    }

    [Fact]
    public async Task ExtendedCrossTenantAccessAuthorizer_ForAuthorizedGetTenantStreamProvider_IsInvokedAndAllowsStreamAccess()
    {
        var rootTenantGrain = Factory.ForTenant(ExtendedCrossTenantAccessAuthorizer.RootTenantId).GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedCrossTenantAccessAuthorizer.AccessChecks.Clear();

        await rootTenantGrain.GetTenantSpecificStreamProvider(ClusterFixture.StreamProviderName, "TenantB");

        var accessCheck = Assert.Single(ExtendedCrossTenantAccessAuthorizer.AccessChecks);
        Assert.Equal((ExtendedCrossTenantAccessAuthorizer.RootTenantId, "TenantB"), accessCheck);
    }

    [Fact]
    public async Task ExtendedCrossTenantAccessAuthorizer_ForUnauthorizedGetTenantStreamProvider_IsInvokedAndThrowsException()
    {
        var tenantAGrain = Factory.ForTenant("TenantA").GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedCrossTenantAccessAuthorizer.AccessChecks.Clear();

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tenantAGrain.GetTenantSpecificStreamProvider(ClusterFixture.StreamProviderName, "TenantB")
        );

        var accessCheck = Assert.Single(ExtendedCrossTenantAccessAuthorizer.AccessChecks);
        Assert.Equal(("TenantA", "TenantB"), accessCheck);
    }

    [Fact]
    public async Task ExtendedIncomingGrainCallTenantSeparator_ForCrossTenantNamespaceGrainCall_IsInvokedAndAllowsGrainCall()
    {
        var tenantAGrain = Factory.ForTenant("TenantA").GetGrain<ITenantSpecificGrain>(ThisTestMethodId());
        ExtendedIncomingGrainCallTenantSeparator.CrossTenantCallCount = 0;

        await tenantAGrain.CallCrossTenantGrain("TenantB", ThisTestMethodId("Other"));

        Assert.Equal(1, ExtendedIncomingGrainCallTenantSeparator.CrossTenantCallCount);
    }

    IGrainFactory Factory => cluster.GrainFactory;

    public sealed class ClusterFixture : IDisposable
    {
        internal const string StreamProviderName = "TheStreamProvider";

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
                .AddMultitenantCommunicationSeparation(
                    _ => new ExtendedCrossTenantAccessAuthorizer(),
                    _ => new ExtendedIncomingGrainCallTenantSeparator())
                .AddMultitenantGrainStorageAsDefault<
                    MemoryGrainStorage,
                    MemoryGrainStorageOptions,
                    MemoryGrainStorageOptionsValidator>((siloBuilder, name) => siloBuilder.AddMemoryGrainStorage(name))
                .AddMultitenantStreams(
                    StreamProviderName, (silo, name) => silo
                    .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(name)
                    .AddMemoryGrainStorage(name));
        }

        class ClientConfigurator : IClientBuilderConfigurator
        {
            public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) => clientBuilder
                .AddMemoryStreams<DefaultMemoryMessageBodySerializer>(StreamProviderName);
        }
    }
}
