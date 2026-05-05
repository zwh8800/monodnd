namespace DndGame.Core;

/// <summary>
/// Thread-safe global service registry. Services are registered in strict order
/// and the registry is locked after FinalizeRegistration().
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly object _lock = new();
    private static bool _finalized;

    public static bool IsFinalized => _finalized;

    public static void Register<TInterface>(TInterface instance) where TInterface : notnull
    {
        lock (_lock)
        {
            if (_finalized)
                throw new InvalidOperationException($"Cannot register {typeof(TInterface).Name}: registry is finalized.");

            var type = typeof(TInterface);
            if (_services.ContainsKey(type))
                throw new InvalidOperationException($"Service of type {type.Name} is already registered.");

            _services[type] = instance;
        }
    }

    public static TInterface Get<TInterface>() where TInterface : notnull
    {
        var type = typeof(TInterface);
        if (!_services.TryGetValue(type, out var service))
            throw new InvalidOperationException($"Service of type {type.Name} is not registered.");

        return (TInterface)service;
    }

    public static bool TryGet<TInterface>(out TInterface? service) where TInterface : notnull
    {
        if (_services.TryGetValue(typeof(TInterface), out var obj))
        {
            service = (TInterface)obj;
            return true;
        }

        service = default;
        return false;
    }

    public static void FinalizeRegistration()
    {
        lock (_lock)
        {
            _finalized = true;
        }
    }

    /// <summary>
    /// For testing only — resets the entire registry.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _services.Clear();
            _finalized = false;
        }
    }
}
