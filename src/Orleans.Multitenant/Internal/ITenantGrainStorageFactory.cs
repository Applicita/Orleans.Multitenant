using Orleans.Storage;

namespace Orleans.Multitenant.Internal;

interface ITenantGrainStorageFactory
{
    IGrainStorage Create(string tenantId);
}
