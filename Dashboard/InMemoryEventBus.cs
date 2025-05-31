using System.Collections.Concurrent;

namespace Dashboard;

public class InMemoryEventBus : IEventBus
{
    private readonly ILogger<InMemoryEventBus> _logger;

    // Stores delegate callbacks: EventType -> List of (Delegate, unique ID) tuples
    // We use a List now because we need to find and remove specific items by reference/ID.
    // We also use ConcurrentDictionary for thread-safe access to the lists themselves.
    private readonly ConcurrentDictionary<Type, List<(Delegate Callback, Guid Id)>> _callbacks;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
        _callbacks = new ConcurrentDictionary<Type, List<(Delegate Callback, Guid Id)>>();
    }

    // Subscribe using an asynchronous callback, returning an IDisposable
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> callback) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        var subscriptionId = Guid.NewGuid();
        var callbackEntry = (Callback: (Delegate)callback, Id: subscriptionId);

        // Get or add the list for this event type, then add the callback
        _callbacks.AddOrUpdate(eventType,
            _ => new List<(Delegate Callback, Guid Id)> { callbackEntry },
            (_, currentList) => {
                lock (currentList) // Lock the specific list for thread safety
                {
                    currentList.Add(callbackEntry);
                }
                return currentList;
            });

        _logger.LogDebug("Subscribed async callback for event {EventType} with ID {SubscriptionId}", eventType.Name, subscriptionId);

        // Return a disposable token that encapsulates the unsubscription logic
        return new EventSubscriptionToken(() => UnsubscribeInternal(eventType, subscriptionId), _logger);
    }

    // Subscribe using a synchronous callback, returning an IDisposable
    public IDisposable Subscribe<TEvent>(Action<TEvent> callback) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        var subscriptionId = Guid.NewGuid();
        var callbackEntry = (Callback: (Delegate)callback, Id: subscriptionId);

        _callbacks.AddOrUpdate(eventType,
            _ => new List<(Delegate Callback, Guid Id)> { callbackEntry },
            (_, currentList) => {
                lock (currentList) // Lock the specific list for thread safety
                {
                    currentList.Add(callbackEntry);
                }
                return currentList;
            });

        _logger.LogDebug("Subscribed sync callback for event {EventType} with ID {SubscriptionId}", eventType.Name, subscriptionId);

        return new EventSubscriptionToken(() => UnsubscribeInternal(eventType, subscriptionId), _logger);
    }

    // Internal method to handle the actual unsubscription logic
    private void UnsubscribeInternal(Type eventType, Guid subscriptionId)
    {
        if (_callbacks.TryGetValue(eventType, out var currentList))
        {
            lock (currentList) // Lock the specific list for thread safety during removal
            {
                var removedCount = currentList.RemoveAll(entry => entry.Id == subscriptionId);
                if (removedCount > 0)
                {
                    _logger.LogDebug("Unsubscribed callback for event {EventType} with ID {SubscriptionId}", eventType.Name, subscriptionId);
                }
                else
                {
                    _logger.LogWarning("Attempted to unsubscribe callback for event {EventType} with ID {SubscriptionId}, but it was not found.", eventType.Name, subscriptionId);
                }
            }
        }
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        _logger.LogInformation("Publishing event: {EventType}", eventType.Name);

        if (_callbacks.TryGetValue(eventType, out var handlers))
        {
            // IMPORTANT: Create a copy of the handlers to iterate over.
            // This prevents issues if a handler unsubscribes itself or another
            // handler during the publish loop (modifying the collection while iterating).
            List<(Delegate Callback, Guid Id)> handlersToExecute;
            lock (handlers) // Lock to ensure a consistent copy
            {
                handlersToExecute = handlers.ToList();
            }

            foreach (var handlerEntry in handlersToExecute)
            {
                try
                {
                    if (handlerEntry.Callback is Func<TEvent, Task> asyncHandler)
                    {
                        await asyncHandler(@event);
                        _logger.LogDebug("Invoked async callback for event {EventType} (ID: {SubscriptionId})", eventType.Name, handlerEntry.Id);
                    }
                    else if (handlerEntry.Callback is Action<TEvent> syncHandler)
                    {
                        // Ensure synchronous callbacks don't block the async flow
                        await Task.Run(() => syncHandler(@event));
                        _logger.LogDebug("Invoked synchronous callback for event {EventType} (ID: {SubscriptionId})", eventType.Name, handlerEntry.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventType} with callback (ID: {SubscriptionId}).", eventType.Name, handlerEntry.Id);
                    // Decide how to handle exceptions: rethrow, log and continue, etc.
                }
            }
        }
        else
        {
            _logger.LogWarning("No callbacks registered for event {EventType}", eventType.Name);
        }
    }
}