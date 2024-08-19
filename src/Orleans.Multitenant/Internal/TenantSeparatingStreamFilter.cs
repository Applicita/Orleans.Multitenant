using Microsoft.Extensions.Logging;
using Orleans.Streams.Filtering;

namespace Orleans.Multitenant.Internal;

interface ITenantEvent { }

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is instantiated through DI")]
sealed class TenantSeparatingStreamFilter : IStreamFilter
{
    readonly ILogger logger;

    public TenantSeparatingStreamFilter(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger(nameof(TenantSeparatingStreamFilter));
        logger.LogInformation("created");
    }

    public bool ShouldDeliver(StreamId streamId, object item, string filterData)
    {
        if (item is ITenantEvent)
            return true; // This forces the tenant aware API to be used

        logger.TenantUnawareStreamApiUsed(streamId, item);
        return false;
    }
}
