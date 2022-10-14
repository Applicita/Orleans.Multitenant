namespace Orleans.Multitenant;

/// <summary>To control which incoming grain calls are considered cross-tenant, register an implementation of this interface with <see cref="SiloBuilderExtensions.AddMultitenantGrainCommunicationSeparation(Hosting.ISiloBuilder, Func{IServiceProvider, Orleans.Multitenant.ICrossTenantAuthorizer}?, Func{IServiceProvider, Orleans.Multitenant.IGrainCallTenantSeparator}?)"/></summary>
public interface IGrainCallTenantSeparator
{
    /// <summary>Is called to determine whether the incoming grain call should be authorized</summary>
    /// <param name="context">the incoming grain call context</param>
    /// <returns>
    /// true if the call should be authorized (<see cref="ICrossTenantAuthorizer.IsAccessAuthorized(string?, string?)"/> will be called if the access is cross-tenant), 
    /// false if not (<see cref="ICrossTenantAuthorizer.IsAccessAuthorized(string?, string?)"/> will not be called)
    /// </returns>
    /// <remarks>
    /// Note that this is called for ALL grain calls in Orleans - including the internal Orleans grains.
    /// The default implementation returns <code>!context.InterfaceName.StartsWith("Orleans.", StringComparison.Ordinal);</code>
    /// A registered implementation should include similar logic to exclude internal Orleans grains.
    /// This extensibility point can be used to e.g. allow access from all tenants to specific 3rd party grains,
    /// or to specific namespaces in the application grains, er even to specific methods on otherwise tenant separated grains.
    /// </remarks>
    bool IsTenantSeparatedCall(IIncomingGrainCallContext context);
}
