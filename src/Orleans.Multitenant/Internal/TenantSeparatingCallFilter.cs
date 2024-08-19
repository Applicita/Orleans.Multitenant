namespace Orleans.Multitenant.Internal;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is instantiated through DI")]
sealed class TenantSeparatingCallFilter(IGrainCallTenantSeparator separator, ICrossTenantAuthorizer authorizer) : IIncomingGrainCallFilter
{
    public Task Invoke(IIncomingGrainCallContext context)
    {
        if (separator.IsTenantSeparatedCall(context))
            ThrowIfTenantSeparatedCallIsUnauthorized(context);

        return context.Invoke();
    }

    void ThrowIfTenantSeparatedCallIsUnauthorized(IIncomingGrainCallContext context)
    {
        var sourceId = context.SourceId;
        if (!sourceId.HasValue)
            return; // The call not is coming from a grain, so it does not come from a tenant specific context => nothing to check

        var sourceGrainId = sourceId.Value;
        var sourceType = sourceGrainId.Type;
        if (sourceType.IsClient() || sourceType.IsSystemTarget())
            return; // The call not is coming from a grain, so it does not come from a tenant specific context => nothing to check

        var sourceTenantId = sourceGrainId.TryGetTenantId();
        var targetTenantId = context.TargetId.TryGetTenantId();
        ThrowIfAccessIsUnauthorized(sourceTenantId, targetTenantId);
    }

    void ThrowIfAccessIsUnauthorized(ReadOnlySpan<byte> sourceTenantId, ReadOnlySpan<byte> targetTenantId)
    {
        if (sourceTenantId.SequenceEqual(targetTenantId))
            return;

        string? source = sourceTenantId.TenantIdString();
        string? target = targetTenantId.TenantIdString();
        if (!authorizer.IsAccessAuthorized(source, target))
            throw new UnauthorizedAccessException($"Tenant \"{source ?? "NULL"}\" attempted to access tenant \"{target ?? "NULL"}\"");
    }
}

/// <summary>
/// Selects all calls as tenant separated, except calls to "Orleans.*" interfaces
/// Selects all streams as tenant separated
/// </summary>
sealed class DefaultGrainCallTenantSeparator : IGrainCallTenantSeparator
{
    public bool IsTenantSeparatedCall(IIncomingGrainCallContext context)
        => !context.InterfaceName.StartsWith("Orleans.", StringComparison.Ordinal); // All code in https://github.com/dotnet/orleans is under the Orleans namespace
}

sealed class DefaultCrossTenantAccessAuthorizer : ICrossTenantAuthorizer
{
    public bool IsAccessAuthorized(string? sourceTenantId, string? targetTenantId)
        => false;
}
