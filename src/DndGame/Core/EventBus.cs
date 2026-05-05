namespace DndGame.Core;

public interface IEventBus
{
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
    void Publish<TEvent>(TEvent evt) where TEvent : notnull;
}

public class EventBus : IEventBus
{
    private readonly Dictionary<Type, Delegate> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        var type = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(type, out var existing))
            {
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        var type = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _handlers.Remove(type);
                else
                    _handlers[type] = result;
            }
        }
    }

    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
    {
        Delegate? handlers;
        lock (_lock)
        {
            _handlers.TryGetValue(typeof(TEvent), out handlers);
        }

        if (handlers is Action<TEvent> action)
        {
            action.Invoke(evt);
        }
    }
}
