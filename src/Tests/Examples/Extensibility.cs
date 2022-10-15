using System.Collections.Concurrent;

namespace OrleansMultitenant.Tests.Examples.Extensibility
{
    class ExtendedCrossTenantAccessAuthorizer : ICrossTenantAuthorizer
    {
        internal const string RootTenantId = "RootTenant";

        /// <remarks>static can be used to access the same object instances in silo's and tests, because <see cref="Orleans.TestingHost.TestCluster"/> uses in-process silo's</remarks>
        internal static ConcurrentQueue<(string? sourceTenantId, string? targetTenantId)> AccessChecks { get; } = new();

        public bool IsAccessAuthorized(string? sourceTenantId, string? targetTenantId)
        {
            if (sourceTenantId == targetTenantId) throw new InvalidOperationException($"sourceTenantId and targetTenantId are equal ({sourceTenantId ?? "NULL"})");
            AccessChecks.Enqueue((sourceTenantId, targetTenantId));
            return string.CompareOrdinal(sourceTenantId, RootTenantId) == 0; // Allow access from the root tenant to any tenant
        }
    }

    class ExtendedIncomingGrainCallTenantSeparator : IGrainCallTenantSeparator
    {
        /// <remarks>static can be used to access the same object instances in silo's and tests, because <see cref="Orleans.TestingHost.TestCluster"/> uses in-process silo's</remarks>
        internal static int CrossTenantCallCount;

        public bool IsTenantSeparatedCall(IIncomingGrainCallContext context)
        {
            if (context.InterfaceName.StartsWith("OrleansMultitenant.Tests.Examples.Extensibility.CrossTenant.", StringComparison.Ordinal))
            {
                CrossTenantCallCount++;
                return false;
            }
            return !context.InterfaceName.StartsWith("Orleans.", StringComparison.Ordinal); // All code in https://github.com/dotnet/orleans is under the Orleans namespace
        }
    }

    interface ITenantSpecificGrain : IGrainWithStringKey
    {
        Task CallTenantSpecificGrain(string targetGrainId);
        Task CallTenantSpecificGrain(string targetTenantId, string targetGrainId);
        Task CallCrossTenantGrain(string targetTenantId, string targetGrainId);

        Task GetTenantSpecificStreamProvider(string name);
        Task GetTenantSpecificStreamProvider(string name, string targetTenantId);

        Task AMethod();
    }

    class TenantSpecificGrain : Grain, ITenantSpecificGrain
    {
        public async Task CallTenantSpecificGrain(string targetGrainId)
         => await this.GetTenantGrainFactory().GetGrain<ITenantSpecificGrain>(targetGrainId).AMethod();

        public async Task CallTenantSpecificGrain(string targetTenantId, string targetGrainId)
         => await GrainFactory.ForTenant(targetTenantId).GetGrain<ITenantSpecificGrain>(targetGrainId).AMethod();

        public async Task CallCrossTenantGrain(string targetTenantId, string targetGrainId)
         => await GrainFactory.ForTenant(targetTenantId).GetGrain<CrossTenant.ICrossTenantGrain>(targetGrainId).AMethod();

        public Task GetTenantSpecificStreamProvider(string name)
        {
            _ = this.GetTenantStreamProvider(name);
            return Task.CompletedTask;
        }

        public Task GetTenantSpecificStreamProvider(string name, string targetTenantId)
        {
            _ = this.GetTenantStreamProvider(name, targetTenantId);
            return Task.CompletedTask;
        }

        public Task AMethod() => Task.CompletedTask;
    }
}

namespace OrleansMultitenant.Tests.Examples.Extensibility.CrossTenant
{
    interface ICrossTenantGrain : IGrainWithStringKey
    {
        Task CallTenantSpecificGrain(string targetTenantId, string targetGrainId);
        Task CallCrossTenantGrain(string targetTenantId, string targetGrainId);
        Task AMethod();
    }

    class CrossTenantGrain : Grain, ICrossTenantGrain
    {
        public async Task CallTenantSpecificGrain(string targetTenantId, string targetGrainId)
         => await GrainFactory.ForTenant(targetTenantId).GetGrain<ITenantSpecificGrain>(targetGrainId).AMethod();

        public async Task CallCrossTenantGrain(string targetTenantId, string targetGrainId)
         => await GrainFactory.ForTenant(targetTenantId).GetGrain<ICrossTenantGrain>(targetGrainId).AMethod();

        public Task AMethod() => Task.CompletedTask;
    }
}
