using OrleansMultitenant.Tests.Examples.UnauthorizedStreaming;

namespace OrleansMultitenant.Tests.UnitTests;

[Collection(MultiPurposeCluster.Name)]
public class UnauthorizedStreamingTests(ClusterFixture fixture)
{
    readonly Orleans.TestingHost.TestCluster cluster = fixture.Cluster;

    public static IEnumerable<object?[]> TenantScenarios() => [
        //              scenarioId, providerIsTenantAware, streamTenant, producerTenant, subscriberTenant
        ["1",                  true,    "TenantA",      "TenantA",        "TenantA"],
        ["2",                  true,    "TenantA",      "TenantA",        "TenantB"],
        ["3",                  true,    "TenantA",      "TenantB",        "TenantA"],
        // TODO: fix test scenario ["4",                 false,    "TenantA",      "TenantA",        "TenantA"],
        ["5",                 false,    "TenantA",      "TenantA",        "TenantB"],
        ["6",                 false,    "TenantA",      "TenantB",        "TenantA"],
    ];

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task GetTenantStreamProvider_FromGrain_ThrowsWhenCrossTenant(string scenarioId, bool providerIsTenantAware, string streamTenant, string producerTenant, string subscriberTenant)
    {
        string provider = providerIsTenantAware ? ClusterFixture.TenantAwareStreamProviderName : ClusterFixture.TenantUnawareStreamProviderName;

        var producer = Factory.ForTenant(producerTenant).GetGrain<ICrossTenantStreamProducerGrain>(ThisTestMethodId("_Producer" + scenarioId + Constants.TenantAwareGrainKeySuffix));
        var subscriber = Factory.ForTenant(subscriberTenant).GetGrain<ICrossTenantExplicitStreamSubscriberGrain>(ThisTestMethodId("_Subscriber" + scenarioId + Constants.TenantAwareGrainKeySuffix));
        CrossTenantAccessAuthorizer.AccessChecks.Clear();

        if (subscriberTenant != streamTenant)
        {
            _ = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                subscriber.Subscribe(provider, "Stream2Namespace", streamTenant, ThisTestMethodId(scenarioId)));
            return;
        }
        await subscriber.Subscribe(provider, "Stream2Namespace", streamTenant, ThisTestMethodId(scenarioId));

        if (producerTenant != streamTenant)
        {
            _ = Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                producer.ProduceEvent(provider, "Stream2Namespace", streamTenant, ThisTestMethodId(scenarioId), 31415));
            return;
        }
        await producer.ProduceEvent(provider, "Stream2Namespace", streamTenant, ThisTestMethodId(scenarioId), 31415);

        int? value = null;
        (string? sourceTenantId, string? targetTenantId) accessCheck = (null, null);

        await WaitUntilAsync(async _ =>
        {
            value = await subscriber.ExtractLastValue();
            return value is not null || CrossTenantAccessAuthorizer.AccessChecks.TryDequeue(out accessCheck);
        }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.1));

        Assert.Equal(31415, value);
        Assert.Equal((null, null), accessCheck);
    }

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task StreamFilter_StreamUsingTenantUnawareAPI_BlocksEventDeliveryWhenProviderIsTenantAware(string scenarioId, bool providerIsTenantAware, string streamTenant, string producerTenant, string subscriberTenant)
    {
        string provider = providerIsTenantAware ? ClusterFixture.TenantAwareStreamProviderName : ClusterFixture.TenantUnawareStreamProviderName;

        var producer = Factory.ForTenant(producerTenant).GetGrain<ICrossTenantStreamProducerGrain>(ThisTestMethodId("_Producer" + scenarioId));
        var subscriber = Factory.ForTenant(subscriberTenant).GetGrain<ICrossTenantExplicitStreamSubscriberGrain>(ThisTestMethodId("_Subscriber" + scenarioId));

        bool tenantAwareApiNotUsedErrorIsLogged = false;
        var processorId = ProcessingLogger.Instance.AddLogEventProcessor(ProcessLogEntry);
        int? value = null;
        try
        {
            await subscriber.Subscribe(provider, "Stream3Namespace", streamTenant, ThisTestMethodId(scenarioId));
            await producer.ProduceEvent(provider, "Stream3Namespace", streamTenant, ThisTestMethodId(scenarioId), 31415);

            await WaitUntilAsync(async _ =>
            {
                value = await subscriber.ExtractLastValue();
                return value is not null || tenantAwareApiNotUsedErrorIsLogged;
            }, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.1));
        }
        finally
        {
            _ = ProcessingLogger.Instance.RemoveLogEventProcessor(processorId);
        }

        if (providerIsTenantAware)
        {
            Assert.True(tenantAwareApiNotUsedErrorIsLogged);
            Assert.Null(value);
        }
        else
        {
            Assert.Equal(31415, value);
            Assert.False(tenantAwareApiNotUsedErrorIsLogged);
        }

        void ProcessLogEntry(Microsoft.Extensions.Logging.LogLevel level, Exception? exception, string message)
        {
            if (level == Microsoft.Extensions.Logging.LogLevel.Error
                && exception is null
                && message.Contains(" event 31415 of type System.Int32 was not sent with the tenant aware API", StringComparison.Ordinal))
            {
                tenantAwareApiNotUsedErrorIsLogged = true;
            }
        }
    }

    [Fact]
    public void GetStreamFromTenantStreamProvider_ForStreamIdWithDifferentTenant_ThrowsException()
    {
        var tenantBStreamId =
            cluster.Client.GetTenantStreamProvider(ClusterFixture.TenantAwareStreamProviderName, "TenantB").GetStream<int>("ns", "key").StreamId;

        var ex = Assert.Throws<ArgumentException>(() =>
            cluster.Client.GetTenantStreamProvider(ClusterFixture.TenantAwareStreamProviderName, "TenantA").GetStream<int>(tenantBStreamId)
        );
        Assert.Equal(@"streamId ns/TenantB|key for tenant TenantB cannot be retrieved from a stream provider for tenant TenantA (Parameter 'streamId')", ex.Message);
    }

    IGrainFactory Factory => cluster.GrainFactory;
}
