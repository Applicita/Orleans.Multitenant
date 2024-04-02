using Orleans.Runtime;
using Orleans.Streams;
using OrleansMultitenant.Tests.Examples.AuthorizedStreaming;

namespace OrleansMultitenant.Tests.UnitTests;

[Collection(MultiPurposeCluster.Name)]
public class AuthorizedStreamingTests(ClusterFixture fixture)
{
    string? tenantId;
    int tenantValueOffset;
    bool tenantAware;

    readonly Orleans.TestingHost.TestCluster cluster = fixture.Cluster;

    public static TheoryData<string /*scenarioId*/, string? /*tenantId*/, int /*tenantValueOffset*/, bool /*tenantAware*/> TenantScenarios => new () {
        { "1",      null,       0, false }, // Verify that common streaming scenario's work with the (tenant-unaware) built-in Orleans API's
        { "2", "TenantA", 100_000,  true }, // Verify that common streaming scenario's work with a tenant that has a non-empty tenant ID
        { "3",        "", 200_000,  true }, // Verify that common streaming scenario's work with a tenant that has an empty tenant ID
    };

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task OnAsync_ProducedFromGrainToImplicitSubscriber_ReceivesSentEvent(string scenarioId, string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        InitScenario(tenantId, tenantValueOffset, tenantAware);
        var producer = GetGrain<IStreamProducerGrain>(ThisTestMethodId("_Producer" + scenarioId));
        var subscriber = GetGrain<IImplicitStreamSubscriberGrain>(ThisTestMethodId(scenarioId));

        await producer.ProduceEvent(Constants.Stream1Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix), Value(31414));
        int? value = null;
        await WaitUntilAsync(async _ =>
        {
            value = await subscriber.ExtractLastValue();
            return value is not null;
        }, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1));

        Assert.Equal(Value(31414), value);
    }

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task OnAsync_ProducedFromGrainToExplicitSubscriber_ReceivesSentEvent(string scenarioId, string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        InitScenario(tenantId, tenantValueOffset, tenantAware);
        var producer = GetGrain<IStreamProducerGrain>(ThisTestMethodId("_Producer" + scenarioId));
        var subscriber = GetGrain<IExplicitStreamSubscriberGrain>(ThisTestMethodId("_Subscriber" + scenarioId));
        await subscriber.Subscribe(Constants.Stream1Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix));

        await producer.ProduceEvent(Constants.Stream1Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix), Value(31415));
        int? value = null;
        await WaitUntilAsync(async _ =>
        {
            value = await subscriber.ExtractLastValue();
            return value is not null;
        }, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1));

        Assert.Equal(Value(31415), value);
    }

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task OnAsync_TwoStreamsProducedFromGrainToExplicitSubscribers_SeparatelyReceivesSentEvent(string scenarioId, string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        InitScenario(tenantId, tenantValueOffset, tenantAware);
        var producer1 = GetGrain<IStreamProducerGrain>(ThisTestMethodId("p1" + scenarioId));
        var subscriber1 = GetGrain<IExplicitStreamSubscriberGrain>(ThisTestMethodId("s1" + scenarioId));
        await subscriber1.Subscribe(Constants.Stream1Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix));

        var producer2 = GetGrain<IStreamProducerGrain>(ThisTestMethodId("p2" + scenarioId));
        var subscriber2 = GetGrain<IExplicitStreamSubscriberGrain>(ThisTestMethodId("s2" + scenarioId));
        await subscriber2.Subscribe(Constants.Stream2Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix));

        await Task.WhenAll(
            producer1.ProduceEvent(Constants.Stream1Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix), Value(31415)),
            producer2.ProduceEvent(Constants.Stream2Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix), Value(61415))
        );
        int? value1 = null, value2 = null;
        await WaitUntilAsync(async _ =>
        {
            value1 ??= await subscriber1.ExtractLastValue();
            value2 ??= await subscriber2.ExtractLastValue();
            return value1 is not null && value2 is not null;
        }, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.1));

        Assert.Equal(Value(31415), value1);
        Assert.Equal(Value(61415), value2);
    }

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task OnAsync_ProducedFromClientToImplicitSubscriber_ReceivesSentEvent(string scenarioId, string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        InitScenario(tenantId, tenantValueOffset, tenantAware);
        var subscriber = GetGrain<IImplicitStreamSubscriberGrain>(ThisTestMethodId(scenarioId));

        var streamId = StreamId.Create(Constants.Stream1Namespace, ThisTestMethodId(scenarioId + TenantAwareSuffix));
        if (tenantAware)
            await GetTenantAwareStream(streamId).OnNextAsync(Value(31416));
        else
            await GetTenantUnawareStream(streamId).OnNextAsync(Value(31416));

        int? value = null;
        await WaitUntilAsync(async _ =>
        {
            value = await subscriber.ExtractLastValue();
            return value is not null;
        }, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1));

        Assert.Equal(Value(31416), value);
    }

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task OnAsync_ProducedFromClientToExplicitSubscriber_ReceivesSentEvent(string scenarioId, string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        InitScenario(tenantId, tenantValueOffset, tenantAware);
        string key = ThisTestMethodId(scenarioId + TenantAwareSuffix);
        var subscriber = GetGrain<IExplicitStreamSubscriberGrain>(ThisTestMethodId("different-from-key" + scenarioId));
        await subscriber.Subscribe(Constants.Stream1Namespace, key);

        var streamId = StreamId.Create(Constants.Stream1Namespace, key);
        if (tenantAware)
            await GetTenantAwareStream(streamId).OnNextAsync(Value(31417));
        else
            await GetTenantUnawareStream(streamId).OnNextAsync(Value(31417));

        int? value = null;
        await WaitUntilAsync(async _ =>
        {
            value = await subscriber.ExtractLastValue();
            return value is not null;
        }, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1));

        Assert.Equal(Value(31417), value);
    }

    [Theory]
    [MemberData(nameof(TenantScenarios))]
    public async Task OnAsync_TwoStreamsProducedFromClientToExplicitSubscriber_SeparatelyReceivesSentEvent(string scenarioId, string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        InitScenario(tenantId, tenantValueOffset, tenantAware);
        string key = ThisTestMethodId(scenarioId + TenantAwareSuffix);
        var subscriber1 = GetGrain<IExplicitStreamSubscriberGrain>(ThisTestMethodId("different-from-key-1" + scenarioId));
        await subscriber1.Subscribe(Constants.Stream1Namespace, key);
        var subscriber2 = GetGrain<IExplicitStreamSubscriberGrain>(ThisTestMethodId("different-from-key-2" + scenarioId));
        await subscriber2.Subscribe(Constants.Stream2Namespace, key);

        var streamId1 = StreamId.Create(Constants.Stream1Namespace, key);
        if (tenantAware)
            await GetTenantAwareStream(streamId1).OnNextAsync(Value(31417));
        else
            await GetTenantUnawareStream(streamId1).OnNextAsync(Value(31417));

        var streamId2 = StreamId.Create(Constants.Stream2Namespace, key);
        if (tenantAware)
            await GetTenantAwareStream(streamId2).OnNextAsync(Value(61417));
        else
            await GetTenantUnawareStream(streamId2).OnNextAsync(Value(61417));

        int? value1 = null, value2 = null;
        await WaitUntilAsync(async _ =>
        {
            value1 ??= await subscriber1.ExtractLastValue();
            value2 ??= await subscriber2.ExtractLastValue();
            return value1 is not null && value2 is not null;
        }, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.1));

        Assert.Equal(Value(31417), value1);
        Assert.Equal(Value(61417), value2);
    }

    T GetGrain<T>(string key = "") where T : IGrainWithStringKey
    {
        key += TenantAwareSuffix;
        var factory = cluster.GrainFactory;
        return tenantAware
            ? factory.ForTenant(tenantId!).GetGrain<T>(key) // Note that we pass in null tenantId by design - the internals must support null tenants, even though the public API does not allow it
            : factory.GetGrain<T>(key);
    }

    TenantStream<int> GetTenantAwareStream(StreamId id)
     => cluster.Client.GetTenantStreamProvider(ClusterFixture.TenantAwareStreamProviderName, tenantId!).GetStream<int>(id); // Note that we pass in null tenantId by design - the internals must support null tenants, even though the public API does not allow it

    IAsyncStream<int> GetTenantUnawareStream(StreamId id) => cluster.Client.GetStreamProvider(ClusterFixture.TenantUnawareStreamProviderName).GetStream<int>(id);

    string TenantAwareSuffix => tenantAware ? Constants.TenantAwareGrainKeySuffix : "";

    int Value(int value) => tenantValueOffset + value;

    void InitScenario(string? tenantId, int tenantValueOffset, bool tenantAware)
    {
        this.tenantId = tenantId;
        this.tenantValueOffset = tenantValueOffset;
        this.tenantAware = tenantAware;
    }
}
