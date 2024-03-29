using OrleansMultitenant.Tests.Examples.GrainCalling;

namespace OrleansMultitenant.Tests.UnitTests;

[Collection(MultiPurposeCluster.Name)]
public class GrainCallingTests(ClusterFixture fixture)
{
    public static IEnumerable<object?[]> UnauthorizedTenantScenarios() => [
        //              scenarioId, sourceTenant, targetTenant
        ["1",    "TenantA",    "TenantB"],
        ["2",    "TenantA",         null],
        ["3",         null,    "TenantB"]
    ];

    readonly Orleans.TestingHost.TestCluster cluster = fixture.Cluster;

    [Fact]
    public async Task CallFilter_GrainCallWithinNonNullTenant_Succeeds()
    {
        var sourceGrain = Factory.ForTenant("TenantA").GetGrain<ISourceGrain>(ThisTestMethodId());
        await sourceGrain.CallTargetGrain(ThisTestMethodId());
    }

    [Fact]
    public async Task CallFilter_GrainCallWithinNullTenant_Succeeds()
    {
        var sourceGrain = Factory.ForTenant(null!).GetGrain<ISourceGrain>(ThisTestMethodId()); // Note that we pass in null tenantId by design - the internals must support null tenants, even though the public API does not allow it
        await sourceGrain.CallTargetGrain(ThisTestMethodId());
    }

    [Theory]
    [MemberData(nameof(UnauthorizedTenantScenarios))]
    public async Task CallFilter_GrainCallToUnauthorizedTenant_ThrowsUnauthorizedAccessException(string scenarioId, string? sourceTenant, string? targetTenant)
    {
        // Note that we pass in null tenantId's by design - the internals must support null tenants, even though the public API does not allow it
        var sourceGrain = Factory.ForTenant(sourceTenant!).GetGrain<ISourceGrain>(ThisTestMethodId(scenarioId));
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sourceGrain.CallTargetGrain(targetTenant, ThisTestMethodId(scenarioId)));
        Assert.Equal($"Tenant \"{sourceTenant ?? "NULL"}\" attempted to access tenant \"{targetTenant ?? "NULL"}\"", ex.Message);
    }

    IGrainFactory Factory => cluster.GrainFactory;
}
