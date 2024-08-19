using Microsoft.Extensions.Logging;
using static Orleans.Multitenant.Internal.LoggingParameter;
using Event = Orleans.Multitenant.Internal.LoggingEvent;

namespace Orleans.Multitenant.Internal;

enum LoggingEvent
{
    MultiTenantStorageProviderParticipating = 1,
    CreatingTenantProvider = 2,
    StartingTenantProvider = 3,
    StartedTenantProvider = 4,
    RecordingSiloLifecycleStart = 5,
    ReplayingSiloLifecycleStartForTenant = 6,
    ForwardingSiloLifecycleStop = 7,
    ForwardingSiloLifecycleStopForTenant = 8,

    TenantUnawareStreamApiUsed = 1001
}

enum LoggingParameter
{
    ProviderName,
    GrainStorageType,
    TenantId,
    TimeoutSeconds,
    HighestCompletedStage,
    LowestStoppedStage,
    FirstStage,
    LastStage,
    Stage,
    ObserverCount,
    SubscriptionCount,

    TenantAwareStreamId, StreamItem, StreamItemType
}

static class LoggerExtensions
{
    static readonly Action<ILogger, string, string, string, Exception?> creatingTenantProvider = LoggerMessage.Define<string, string, string>(
        LogLevel.Information,
        Event.CreatingTenantProvider.Id(),
        $"Creating {{{GrainStorageType}}} provider for tenant '{{{TenantId}}}' with provider name '{{{ProviderName}}}'"
    );

    static readonly Action<ILogger, string, double, Exception?> startingTenantProvider = LoggerMessage.Define<string, double>(
        LogLevel.Information,
        Event.StartingTenantProvider.Id(),
        $"Starting provider for tenant '{{{TenantId}}}' (timeout = {{{TimeoutSeconds}}} seconds)"
    );

    static readonly Action<ILogger, string, Exception?> startedTenantProvider = LoggerMessage.Define<string>(
        LogLevel.Information,
        Event.StartedTenantProvider.Id(),
        $"Started provider for tenant '{{{TenantId}}}'"
    );

    static readonly Action<ILogger, int, int, int, Exception?> recordingSiloLifecycleStart = LoggerMessage.Define<int, int, int>(
        LogLevel.Information,
        Event.RecordingSiloLifecycleStart.Id(),
        $"Recording start of silo lifecycle with HighestCompletedStage = {{{nameof(HighestCompletedStage)}}} for stages {{{nameof(FirstStage)}}}..{{{nameof(LastStage)}}}"
    );

    static readonly Action<ILogger, int, int, int, int, Exception?> forwardingSiloLifecycleStop = LoggerMessage.Define<int, int, int, int>(
        LogLevel.Information,
        Event.ForwardingSiloLifecycleStop.Id(),
        $"Forwarding silo lifecycle stop event with LowestStoppedStage = {{{nameof(LowestStoppedStage)}}} to {{{ObserverCount}}} observers of stages {{{nameof(FirstStage)}}}..{{{nameof(LastStage)}}}"
    );

    static readonly Action<ILogger, int, int, Exception?> replayingSiloLifecycleStartForTenant = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        Event.ReplayingSiloLifecycleStartForTenant.Id(),
        $"Replaying silo lifecycle start to {{{SubscriptionCount}}} subscription(s) on stage {{{Stage}}}"
    );

    static readonly Action<ILogger, int, int, Exception?> forwardingSiloLifecycleStopForTenant = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        Event.ForwardingSiloLifecycleStopForTenant.Id(),
        $"Forwarding silo lifecycle stop to {{{SubscriptionCount}}} subscription(s) on stage {{{Stage}}}"
    );

    static readonly Action<ILogger, StreamId, object, string?, Exception?> tenantUnawareStreamApiUsed = LoggerMessage.Define<StreamId, object, string?>(
        LogLevel.Error,
        Event.TenantUnawareStreamApiUsed.Id(),
        $"Stream {{{TenantAwareStreamId}}} has a tenant aware stream provider, but event {{{StreamItem}}} of type {{{StreamItemType}}} was not sent with the tenant aware API; use {nameof(Multitenant.GrainExtensions.GetTenantStreamProvider)} instead of {nameof(ClientStreamingExtensions.GetStreamProvider)}"
    );

    internal static EventId Id(this Event e) => new((int)e, Enum.GetName(e));

    internal static void CreatingTenantProvider(this ILogger logger, Type grainStorageType, string tenantId, string providerName)
                      => creatingTenantProvider(logger, grainStorageType.Name, tenantId, providerName, null);

    internal static void StartingTenantProvider(this ILogger logger, string tenantId, double timeoutSeconds)
                      => startingTenantProvider(logger, tenantId, timeoutSeconds, null);

    internal static void StartedTenantProvider(this ILogger logger, string tenantId)
                      => startedTenantProvider(logger, tenantId, null);

    internal static void RecordingSiloLifecycleStart(this ILogger logger, int highestCompletedStage, int firstStage, int lastStage)
                      => recordingSiloLifecycleStart(logger, highestCompletedStage, firstStage, lastStage, null);

    internal static void ForwardingSiloLifecycleStop(this ILogger logger, int lowestStoppedStage, int observerCount, int firstStage, int lastStage)
                      => forwardingSiloLifecycleStop(logger, lowestStoppedStage, observerCount, firstStage, lastStage, null);

    internal static void ReplayingSiloLifecycleStartForTenant(this ILogger logger, int subscriptionCount, int stage)
                      => replayingSiloLifecycleStartForTenant(logger, subscriptionCount, stage, null);

    internal static void ForwardingSiloLifecycleStopForTenant(this ILogger logger, int subscriptionCount, int stage)
                      => forwardingSiloLifecycleStopForTenant(logger, subscriptionCount, stage, null);

    internal static void TenantUnawareStreamApiUsed(this ILogger logger, StreamId streamId, object streamItem)
                      => tenantUnawareStreamApiUsed(logger, streamId, streamItem, streamItem.GetType().FullName, null);

}
