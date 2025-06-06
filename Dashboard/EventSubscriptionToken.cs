namespace Dashboard;

public class EventSubscriptionToken : IDisposable
{
    private readonly Action _unsubscribeAction;
    private bool _isDisposed = false;
    private readonly ILogger _logger;

    public EventSubscriptionToken(Action unsubscribeAction, ILogger logger)
    {
        _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
        _logger = logger;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _unsubscribeAction?.Invoke(); // Execute the stored unsubscribe action
            _isDisposed = true;
            GC.SuppressFinalize(this); // Prevent finalizer from running
            _logger.LogDebug("Event subscription token disposed.");
        }
    }

    ~EventSubscriptionToken()
    {
        // This finalizer is a fallback in case Dispose() is not called explicitly.
        // It's good practice to have it, but you should always aim to call Dispose() manually.
        if (!_isDisposed)
        {
            _logger.LogWarning("EventSubscriptionToken finalized without being disposed. Ensure Dispose() is called explicitly.");
            Dispose(); // Call Dispose to perform cleanup
        }
    }
}
