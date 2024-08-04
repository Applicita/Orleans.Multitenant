using Orleans.Multitenant.Internal;
using Orleans.Streams;

namespace Orleans.Multitenant;

/// <summary>A tenant-specific stream provider</summary>
/// <remarks>
/// In a <see cref="Grain"/> context use the <see cref="GrainExtensions"/> methods to instantiate;<br />
/// In a <see cref="IClusterClient"/> context use the <see cref="ClusterClientExtensions"/> methods to instantiate
/// </remarks>
[SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Instances of the value type will not be compared to each other")]
public readonly struct TenantStreamProvider
{
    readonly IStreamProvider provider;

    readonly ReadOnlyMemory<byte> tenantId;
    // We don't use ReadOnlySpan here because that would require TenantStreamProvider to be a ref struct
    // which would severely limit where developers can store TenantStreamProviders

    internal TenantStreamProvider(ReadOnlySpan<byte> tenantId, IStreamProvider provider)
    { this.tenantId = tenantId.ToArray(); this.provider = provider; }

    /// <summary>Gets the tenant specific stream with the specified identity</summary>
    /// <typeparam name="T">The stream element type</typeparam>
    /// <param name="streamId">
    /// The stream identifier.<br />
    /// Note that the key only needs to specify the key within the tenant of this <see cref="TenantStreamProvider"/>.<br />
    /// A key that includes a tenant ID is also valid, as long as that tenant is the tenant of this <see cref="TenantStreamProvider"/>.<br />
    /// Specifying a different tenant will throw an <see cref="ArgumentException"/>
    /// </param>
    /// <returns>The stream</returns>
    public TenantStream<T> GetStream<T>(StreamId streamId)
    {
        string keyWithinTenant = streamId.Key.Span.GetKeyAndTenant(out var specifiedTenantId);
        if (!specifiedTenantId.IsEmpty && !MemoryExtensions.SequenceEqual(specifiedTenantId, tenantId.Span))
            throw new ArgumentException($"streamId {streamId} for tenant {specifiedTenantId.TenantIdString()} cannot be retrieved from a stream provider for tenant {tenantId.Span.TenantIdString()}", nameof(streamId));

        return new(provider.GetStream<TenantEvent<T>>(StreamId.Create(
            streamId.Namespace.Span,
            tenantId.Span.GetTenantQualifiedKey(keyWithinTenant).AsSpan()
        )));
    }

    /// <summary>Gets the tenant specific stream with the specified namespace and key</summary>
    /// <typeparam name="T">The stream element type</typeparam>
    /// <param name="namespace">The stream namespace</param>
    /// <param name="keyWithinTenant">The part of the stream key that identifies it within this <see cref="TenantStreamProvider"/>'s tenant.</param>
    /// <returns>The stream</returns>
    public TenantStream<T> GetStream<T>(string @namespace, string keyWithinTenant)
    => GetStream<T>(StreamId.Create(@namespace, keyWithinTenant));
}

/// <summary>A tenant-specific <see cref="IAsyncStream{T}"/> equivalent, offering all the <see cref="IAsyncStream{T}"/> methods plus all <see cref="AsyncObservableExtensions"/> extension methods</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Is a stream")]
public readonly record struct TenantStream<T> : IComparable<TenantStream<T>>
{
    static readonly Func<Exception, Task> defaultOnError = _ => Task.CompletedTask;
    static readonly Func<Task> defaultOnCompleted = () => Task.CompletedTask;

    readonly IAsyncStream<TenantEvent<T>> stream;

    internal TenantStream(IAsyncStream<TenantEvent<T>> stream) => this.stream = stream;

    /// <inheritdoc cref="IAsyncStream.IsRewindable"/>
    public bool IsRewindable => stream.IsRewindable;

    /// <inheritdoc cref="IAsyncStream.ProviderName"/>
    public string ProviderName => stream.ProviderName;

    /// <inheritdoc cref="IAsyncStream.StreamId"/>
    public StreamId StreamId => stream.StreamId;

    /// <inheritdoc cref="IAsyncStream{T}.GetAllSubscriptionHandles"/>
    public Task<IList<StreamSubscriptionHandle<TenantEvent<T>>>> GetAllSubscriptionHandles() => stream.GetAllSubscriptionHandles();

    /// <inheritdoc cref="IAsyncObserver{T}.OnCompletedAsync"/>
    public Task OnCompletedAsync() => stream.OnCompletedAsync();

    /// <inheritdoc cref="IAsyncObserver{T}.OnErrorAsync(Exception)"/>
    public Task OnErrorAsync(Exception ex) => stream.OnErrorAsync(ex);

    /// <inheritdoc cref="IAsyncObserver{T}.OnNextAsync(T, StreamSequenceToken?)"/>
    public Task OnNextAsync(T item, StreamSequenceToken? token = null) => stream.OnNextAsync(item, token);

    /// <inheritdoc cref="IAsyncBatchProducer{T}.OnNextBatchAsync(IEnumerable{T}, StreamSequenceToken)"/>
    public Task OnNextBatchAsync(IEnumerable<T> batch, StreamSequenceToken? token = null) => stream.OnNextBatchAsync(batch.Cast<TenantEvent<T>>(), token);

    /// <inheritdoc cref="IAsyncObservable{T}.SubscribeAsync(IAsyncObserver{T})"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(IAsyncObserver<T> observer)
                                                => stream.SubscribeAsync(new TenantStreamObserver<T>(observer));

    /// <inheritdoc cref="IAsyncObservable{T}.SubscribeAsync(IAsyncObserver{T}, StreamSequenceToken?, string?)"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(IAsyncObserver<T> observer, StreamSequenceToken? token, string? filterData = null)
                                                => stream.SubscribeAsync(new TenantStreamObserver<T>(observer), token, filterData);

    /// <inheritdoc cref="IAsyncBatchObservable{T}.SubscribeAsync(IAsyncBatchObserver{T})"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(IAsyncBatchObserver<T> observer)
                                                => stream.SubscribeAsync(new TenantStreamBatchObserver<T>(observer));

    /// <inheritdoc cref="IAsyncBatchObservable{T}.SubscribeAsync(IAsyncBatchObserver{T}, StreamSequenceToken?)"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(IAsyncBatchObserver<T> observer, StreamSequenceToken? token)
                                                => stream.SubscribeAsync(new TenantStreamBatchObserver<T>(observer), token);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, Func{Exception, Task}, Func{Task})"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        Func<Exception, Task> onErrorAsync,
        Func<Task> onCompletedAsync)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), onErrorAsync, onCompletedAsync);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, Func{Exception, Task})"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        Func<Exception, Task> onErrorAsync)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), onErrorAsync, defaultOnCompleted);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, Func{Task})"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        Func<Task> onCompletedAsync)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), defaultOnError, onCompletedAsync);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task})"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), defaultOnError, defaultOnCompleted);


    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, Func{Exception, Task}, Func{Task}, StreamSequenceToken)"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        Func<Exception, Task> onErrorAsync,
        Func<Task> onCompletedAsync,
        StreamSequenceToken token)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), onErrorAsync, onCompletedAsync, token);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, Func{Exception, Task}, StreamSequenceToken)"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        Func<Exception, Task> onErrorAsync,
        StreamSequenceToken token)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), onErrorAsync, defaultOnCompleted, token);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, Func{Task}, StreamSequenceToken)"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        Func<Task> onCompletedAsync,
        StreamSequenceToken token)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), defaultOnError, onCompletedAsync, token);

    /// <inheritdoc cref="AsyncObservableExtensions.SubscribeAsync{T}(IAsyncObservable{T}, Func{T, StreamSequenceToken, Task}, StreamSequenceToken)"/>
    public Task<StreamSubscriptionHandle<TenantEvent<T>>> SubscribeAsync(
        Func<T, StreamSequenceToken, Task> onNextAsync,
        StreamSequenceToken token)
    => stream.SubscribeAsync((item, token) => onNextAsync(item.Event, token), defaultOnError, defaultOnCompleted, token);

    public int CompareTo(TenantStream<T> other) => stream.CompareTo(other.stream);

    public static bool operator <(TenantStream<T> left, TenantStream<T> right) => left.CompareTo(right) < 0;

    public static bool operator <=(TenantStream<T> left, TenantStream<T> right) => left.CompareTo(right) <= 0;

    public static bool operator >(TenantStream<T> left, TenantStream<T> right) => left.CompareTo(right) > 0;

    public static bool operator >=(TenantStream<T> left, TenantStream<T> right) => left.CompareTo(right) >= 0;
}

/// <summary>A lightweight wrapper for a stream event; indicates that an event was sent with a tenant aware API</summary>
/// <typeparam name="T">The type of the stream event</typeparam>
/// <remarks>Note that this wrapper does not add any data to <typeparamref name="T"/>; it only marks <typeparamref name="T"/> with an internal interface,
/// which is used by the stream filter to guard tenant separation - the filter block events sent with a tenant unaware API through a tenant specific stream</remarks>
[GenerateSerializer]
[SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Instances of the value type will not be compared to each other")]
public readonly struct TenantEvent<T> : ITenantEvent
{
    [Id(0)] internal T Event { get; init; }
    public static implicit operator TenantEvent<T>(T @event) => new() { Event = @event };
}
