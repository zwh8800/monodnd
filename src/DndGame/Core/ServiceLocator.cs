namespace DndGame.Core;

/// <summary>
/// 线程安全的全局服务注册表。
/// 服务按严格顺序注册，调用 <see cref="FinalizeRegistration"/> 后注册表将被锁定，
/// 防止后续非法修改，确保运行时服务结构的稳定性。
/// </summary>
public static class ServiceLocator
{
    // 存储所有已注册服务的字典：键为服务接口类型，值为服务实例
    private static readonly Dictionary<Type, object> _services = new();

    // 用于所有公共操作的锁对象，保证多线程环境下的线程安全
    // 所有 Register、FinalizeRegistration、Reset 操作都通过此锁同步
    private static readonly object _lock = new();

    // 标记注册表是否已冻结。一旦为 true，任何 Register 调用都将抛出异常
    private static bool _finalized;

    /// <summary>
    /// 获取一个值，指示服务注册表是否已冻结。
    /// 冻结后不能再注册新服务。
    /// </summary>
    public static bool IsFinalized => _finalized;

    /// <summary>
    /// 注册一个服务实例到全局注册表中。
    /// 同一接口类型只能注册一次。注册表冻结后无法注册。
    /// 初始化顺序必须遵循依赖关系：被依赖的服务必须先注册。
    /// </summary>
    /// <typeparam name="TInterface">要注册的服务接口类型。</typeparam>
    /// <param name="instance">服务接口的实现实例。</param>
    /// <exception cref="InvalidOperationException">
    /// 注册表已冻结时抛出，或指定接口类型的服务已注册时抛出。
    /// </exception>
    public static void Register<TInterface>(TInterface instance) where TInterface : notnull
    {
        // 使用锁同步，防止并发注册导致竞态条件
        lock (_lock)
        {
            // 检查冻结状态：一旦冻结，所有注册操作均被拒绝
            if (_finalized)
                throw new InvalidOperationException($"无法注册 {typeof(TInterface).Name}：注册表已冻结。");

            var type = typeof(TInterface);
            // 防止同一接口被重复注册，避免意外的服务覆盖
            if (_services.ContainsKey(type))
                throw new InvalidOperationException($"类型 {type.Name} 的服务已被注册。");

            _services[type] = instance;
        }
    }

    /// <summary>
    /// 从全局注册表中获取指定类型的服务实例。
    /// 如果服务未注册则抛出异常。
    /// 此方法本身不锁定，因为注册表在初始化完成后即进入只读状态，
    /// 对字典的并发读取在 .NET 中是线程安全的（没有写入时）。
    /// </summary>
    /// <typeparam name="TInterface">要获取的服务接口类型。</typeparam>
    /// <returns>已注册的服务实例。</returns>
    /// <exception cref="InvalidOperationException">指定类型的服务未注册时抛出。</exception>
    public static TInterface Get<TInterface>() where TInterface : notnull
    {
        var type = typeof(TInterface);
        // 尝试从字典中获取服务实例
        if (!_services.TryGetValue(type, out var service))
            throw new InvalidOperationException($"类型 {type.Name} 的服务未注册。");

        // 强制转换是安全的：Register 保证存入的类型与键类型匹配
        return (TInterface)service;
    }

    /// <summary>
    /// 尝试从全局注册表中获取指定类型的服务实例。
    /// 与 <see cref="Get{TInterface}"/> 不同，此方法在服务未注册时不会抛出异常。
    /// </summary>
    /// <typeparam name="TInterface">要获取的服务接口类型。</typeparam>
    /// <param name="service">
    /// 当方法返回 true 时，包含已注册的服务实例；
    /// 返回 false 时，包含类型的默认值。
    /// </param>
    /// <returns>如果服务已注册则返回 true；否则返回 false。</returns>
    public static bool TryGet<TInterface>(out TInterface? service) where TInterface : notnull
    {
        // TryGetValue 原子性地完成查找和获取，无需额外锁定
        if (_services.TryGetValue(typeof(TInterface), out var obj))
        {
            service = (TInterface)obj;
            return true;
        }

        // 未找到时返回默认值，调用方需检查返回值
        service = default;
        return false;
    }

    /// <summary>
    /// 冻结注册表，禁止后续的注册操作。
    /// 在所有服务完成初始化后调用，以确保运行时服务结构的完整性。
    /// 此操作是线程安全的。
    /// </summary>
    public static void FinalizeRegistration()
    {
        // 需要锁来保证 _finalized 写入的可见性
        lock (_lock)
        {
            _finalized = true;
        }
    }

    /// <summary>
    /// 仅用于测试——重置整个注册表。
    /// 清空所有已注册服务并将冻结状态解除。
    /// 不应在生产流程中调用。
    /// </summary>
    public static void Reset()
    {
        // 需要锁来保证字典清空和状态重置的原子性
        lock (_lock)
        {
            _services.Clear();
            _finalized = false;
        }
    }
}
