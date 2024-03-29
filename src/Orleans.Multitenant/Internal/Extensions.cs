using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Storage;

namespace Orleans.Multitenant.Internal;

static class SiloBuilderExtensions
{
    internal static IServiceCollection AddMultitenantGrainStorage(
        this IServiceCollection services, string name, Func<IServiceProvider, string, ITenantGrainStorageFactory> factory, Action<OptionsBuilder<MultitenantStorageOptions>>? configureOptions = null)
    {
        configureOptions?.Invoke(services.AddOptions<MultitenantStorageOptions>(name));

        _ = services.AddTransient<IConfigurationValidator>(sp => new MultitenantStorageOptionsValidator(sp.GetRequiredService<IOptionsMonitor<MultitenantStorageOptions>>().Get(name), name))
                    .ConfigureNamedOptionForLogging<MultitenantStorageOptions>(name);

        if (string.Equals(name, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME, StringComparison.Ordinal))
        {
            services.TryAddSingleton<IGrainStorage>(sp => sp.GetRequiredKeyedService<MultitenantStorage>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME));
            services.TryAddSingleton(sp => sp.GetRequiredKeyedService<ITenantGrainStorageFactory>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME));
        }
        return services.AddKeyedSingleton(name, (s, _) => MultitenantStorageFactory.Create(s, name))
                       .AddKeyedSingleton<IGrainStorage>(name, (s, n) => s.GetRequiredKeyedService<MultitenantStorage>(n))
                       .AddKeyedSingleton(name, (s, n) => (ILifecycleParticipant<ISiloLifecycle>)s.GetRequiredKeyedService<IGrainStorage>(n))
                       .AddKeyedSingleton(name, (s, _) => factory(s, name));
    }
}

static class TenantIdExtensions
{
    // Separator chars must be <= 0x7f to be encoded in one byte in UTF8
    const char SeparatorChar = '|';
    const char SeparatorsSeparatorChar = '~';

    const byte SeparatorByte = (byte)SeparatorChar;
    const byte SeparatorsSeparatorByte = (byte)SeparatorsSeparatorChar;

    static readonly string unescapedSeparatorString = new(SeparatorChar, 1);
    static readonly string escapedSeparatorString = new(SeparatorChar, 2);

    /// <remarks>This is the inverse of <see cref="AsTenantId(string?)"/></remarks>
    internal static string? TenantIdString(this ReadOnlySpan<byte> tenantId)
        => tenantId.IsEmpty ? null : Encoding.UTF8.GetString(tenantId[..^1]).Replace(escapedSeparatorString, unescapedSeparatorString, StringComparison.Ordinal);

    /// <param name="tenantIdString">Can be any string, including an empty one</param>
    /// <returns>A non-empty tenant ID that is unique for <paramref name="tenantIdString"/></returns>
    /// <remarks>
    /// This is the inverse of <see cref="TenantIdString(ReadOnlySpan{byte})"/>
    /// All public API's where tenant ID's are passed in MUST use this method.
    /// This ensures that no knowledge on the formatting of tenant qualified keys is needed outside of this class.
    /// </remarks>
    internal static ReadOnlySpan<byte> AsTenantId(this string? tenantIdString)
        => tenantIdString is null ? default : Encoding.UTF8.GetBytes(tenantIdString.Replace(unescapedSeparatorString, escapedSeparatorString, StringComparison.Ordinal) + SeparatorChar);

    internal static IdSpan GetTenantQualifiedKey(this ReadOnlySpan<byte> tenantId, string keyWithinTenant)
    {
        string key = keyWithinTenant;

        if (tenantId.IsEmpty) // No tenant, a.k.a. the null tenant
        {
            key = key.Replace(unescapedSeparatorString, escapedSeparatorString, StringComparison.Ordinal);
        }
        else
        {
            if (key.Length > 0 && key[0] is SeparatorsSeparatorChar or SeparatorChar)
                key = SeparatorsSeparatorChar + key;

            key = Encoding.UTF8.GetString(tenantId) + key;
        }

        return IdSpan.Create(key);
    }

    /// <returns>The non-empty tenant id, or an empty span if <paramref name="grainId"/> does not contain a tenant id</returns>
    internal static ReadOnlySpan<byte> TryGetTenantId(this GrainId grainId)
        => TryGetTenantId(grainId.Key.Value.Span);

    /// <returns>The non-empty tenant id, or an empty span if <paramref name="streamId"/> does not contain a tenant id</returns>
    internal static ReadOnlySpan<byte> TryGetTenantId(this StreamId streamId)
        => TryGetTenantId(streamId.Key.Span);

    /// <returns>The non-empty tenant id, or an empty span if <paramref name="key"/> does not contain a tenant id</returns>
    static ReadOnlySpan<byte> TryGetTenantId(ReadOnlySpan<byte> key)
    {
        for (int i = 0; i < key.Length; i++)
        {
            if (key[i] == SeparatorByte && (++i == key.Length || key[i] != SeparatorByte)) // Will not match escapedSeparatorString
                return key[..i];
        }
        return default;
    }

    internal static string GetKeyAndTenant(this ReadOnlySpan<byte> qualifiedKey, out ReadOnlySpan<byte> tenantId)
    {
        tenantId = TryGetTenantId(qualifiedKey);
        return qualifiedKey.GetKey(tenantId);
    }

    internal static string GetKey(this ReadOnlySpan<byte> qualifiedKey)
    => qualifiedKey.GetKey(TryGetTenantId(qualifiedKey));

    static string GetKey(this ReadOnlySpan<byte> qualifiedKey, ReadOnlySpan<byte> tenantId)
    {
        string keyWithinTenant;

        if (tenantId.IsEmpty)
        {
            keyWithinTenant = Encoding.UTF8.GetString(qualifiedKey).Replace(escapedSeparatorString, unescapedSeparatorString, StringComparison.Ordinal);
        }
        else
        {
            int keyWithinTenantstart = tenantId.Length;

            if (keyWithinTenantstart + 2 <= qualifiedKey.Length
                && qualifiedKey[keyWithinTenantstart] == SeparatorsSeparatorChar
                && qualifiedKey[keyWithinTenantstart + 1] is SeparatorsSeparatorByte or SeparatorByte)
            {
                keyWithinTenantstart++;
            }

            keyWithinTenant = Encoding.UTF8.GetString(qualifiedKey[keyWithinTenantstart..]);
        }

        return keyWithinTenant;
    }
}

static class GrainExtensions
{
    static ICrossTenantAuthorizer? authorizer;
    static IServiceProvider? authorizerServiceProvider;
    static PropertyInfo? grainServiceProviderProperty;
    static PropertyInfo? grainFactoryProperty;

    internal static IGrainFactory GetGrainFactory(this Grain grain)
     => (IGrainFactory)
        (grainFactoryProperty ??= typeof(Grain).GetProperty("GrainFactory", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!)
        .GetValue(grain)!;

    internal static void ThrowIfAccessIsUnauthorized(this Grain grain, ReadOnlySpan<byte> targetTenantId)
    {
        var grainServiceProvider = grain.GetServiceProvider();
        if (grainServiceProvider is null) // E.g. when we are in a unit test context where grains are tested directly without silo context
        {
            authorizer = null;
            return;
        }

        if (!ReferenceEquals(authorizerServiceProvider, grainServiceProvider)) // Support multiple in-process silo's with different services, e.g. for unit testing
        {
            authorizer = grainServiceProvider.GetService<ICrossTenantAuthorizer>();
            authorizerServiceProvider = grainServiceProvider;
        }

        if (authorizer is null)
            return;

        var sourceTenantId = grain.GetGrainId().TryGetTenantId();

        if (MemoryExtensions.SequenceEqual(sourceTenantId, targetTenantId))
            return;

        string? source = sourceTenantId.TenantIdString(), target = targetTenantId.TenantIdString();
        if (!authorizer.IsAccessAuthorized(source, target))
            throw new UnauthorizedAccessException($"Tenant \"{source ?? "NULL"}\" attempted to access tenant \"{target ?? "NULL"}\"");
    }

    static IServiceProvider? GetServiceProvider(this Grain grain)
     => (IServiceProvider?)
        (grainServiceProviderProperty ??= typeof(Grain).GetProperty("ServiceProvider", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!)
        .GetValue(grain);
}
