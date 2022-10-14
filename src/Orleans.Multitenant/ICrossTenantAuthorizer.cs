namespace Orleans.Multitenant;

/// <summary>To control cross-tenant grain calls and stream events, register an implementation of this interface with <see cref="SiloBuilderExtensions.AddMultitenantGrainCommunicationSeparation(Hosting.ISiloBuilder, Func{IServiceProvider, Orleans.Multitenant.ICrossTenantAuthorizer}?, Func{IServiceProvider, Orleans.Multitenant.IGrainCallTenantSeparator}?)"/></summary>
/// <remarks>An implementation of this interface can invoke grain calls by adding a <see cref="IGrainFactory"/> parameter to it's constructor</remarks>
public interface ICrossTenantAuthorizer
{
    /// <summary>Is called to determine whether access from tenant <paramref name="sourceTenantId"/> to tenant <paramref name="targetTenantId"/> is authorized</summary>
    /// <param name="sourceTenantId">Always != <paramref name="targetTenantId"/></param>
    /// <param name="targetTenantId">Always != <paramref name="sourceTenantId"/></param>
    /// <returns>true if access from tenant <paramref name="sourceTenantId"/> to tenant <paramref name="targetTenantId"/> is authorized; otherwise false (which causes an <see cref="UnauthorizedAccessException"/> to be thrown)</returns>
    /// <remarks>Tenant Id's can be any string, including null and String.Empty (wich are considered distinct values just like any other string)</remarks>
    bool IsAccessAuthorized(string? sourceTenantId, string? targetTenantId);
}
