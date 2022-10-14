namespace OrleansMultitenant.Tests.Examples.GrainCalling;

interface ISourceGrain : IGrainWithStringKey
{
    Task CallTargetGrain(string targetGrainId);
    Task CallTargetGrain(string? targetTenantId, string targetGrainId);
}

interface ITargetGrain : IGrainWithStringKey
{
    Task AMethod();
}

class SourceGrain : Grain, ISourceGrain
{
    public async Task CallTargetGrain(string targetGrainId)
     => await GrainFactory.ForTenantOf(this).GetGrain<ITargetGrain>(targetGrainId).AMethod();

    public async Task CallTargetGrain(string? targetTenantId, string targetGrainId)
     => await GrainFactory.ForTenant(targetTenantId!).GetGrain<ITargetGrain>(targetGrainId).AMethod(); // Note that we pass in null tenantId by design - the internals must support null tenants, even though the public API does not allow it
}

class TargetGrain : Grain, ITargetGrain
{
    public Task AMethod() => Task.CompletedTask;
}
