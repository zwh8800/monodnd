namespace DndGame.Core;

/// <summary>
/// 事件总线接口，定义事件的订阅、取消订阅和发布操作。
/// 用于解耦系统中各模块之间的直接引用，实现发布-订阅模式。
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 订阅指定类型的事件。
    /// 每次发布该类型事件时，所有已订阅的处理程序将按订阅顺序依次执行。
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须为非 null 类型。</typeparam>
    /// <param name="handler">事件处理委托，当事件发布时被调用。</param>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    /// <summary>
    /// 取消订阅指定类型的事件。
    /// 从该事件类型的处理程序链中移除指定的委托实例。
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须为非 null 类型。</typeparam>
    /// <param name="handler">要移除的事件处理委托。</param>
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    /// <summary>
    /// 发布指定类型的事件，触发所有已订阅的处理程序。
    /// 采用快照模式，在锁保护下获取当前处理程序列表后立即释放锁再执行调用。
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须为非 null 类型。</typeparam>
    /// <param name="evt">要发布的事件实例，包含处理程序所需的数据。</param>
    void Publish<TEvent>(TEvent evt) where TEvent : notnull;
}

/// <summary>
/// 事件总线的默认实现。
/// 内部使用字典存储事件类型到多播委托的映射，通过锁保护实现线程安全的
/// 订阅/取消订阅操作。发布操作采用快照模式以避免死锁和减少锁争用。
/// </summary>
public class EventBus : IEventBus
{
    /// <summary>
    /// 事件类型到处理委托的映射字典。
    /// 键是事件类型（Type），值是通过 Delegate.Combine 组合的多播委托，
    /// 可包含同一事件类型的多个处理程序。
    /// </summary>
    private readonly Dictionary<Type, Delegate> _handlers = new();

    /// <summary>
    /// 保护 _handlers 字典线程安全的锁对象。
    /// 所有对 _handlers 的读写操作必须在此锁的保护下进行，
    /// 确保多线程环境下的订阅/取消订阅操作互斥。
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// 订阅指定类型的事件。
    /// 将新的处理程序通过 Delegate.Combine 追加到该事件类型的现有委托链中。
    /// 如果该事件类型尚无订阅者，则直接创建新的委托链。
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须为非 null 类型。</typeparam>
    /// <param name="handler">要添加的事件处理委托。</param>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        var type = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(type, out var existing))
            {
                // Delegate.Combine 将新处理程序追加到现有委托链末尾，
                // 形成不可变的多播委托。这是线程安全的委托组合方式，
                // 确保同一事件类型的多个订阅者都能被依次调用，
                // 且不会影响正在执行中的快照副本。
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }
    }

    /// <summary>
    /// 取消订阅指定类型的事件。
    /// 使用 Delegate.Remove 从该事件类型的委托链中移除指定的处理程序实例。
    /// 如果移除后委托链为空（结果为 null），则从字典中删除该条目以避免内存泄漏。
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须为非 null 类型。</typeparam>
    /// <param name="handler">要移除的事件处理委托。</param>
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        var type = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(type, out var existing))
            {
                // Delegate.Remove 从多播委托链中移除指定的处理程序。
                // 如果同一处理程序在链中出现多次，仅移除第一次出现的实例；
                // 若处理程序不存在于链中，则返回原委托不变。
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    // 移除后委托链为空，清理字典条目避免内存泄漏
                    _handlers.Remove(type);
                else
                    _handlers[type] = result;
            }
        }
    }

    /// <summary>
    /// 发布指定类型的事件，触发所有已订阅的处理程序。
    /// 采用"快照-然后调用"（Snapshot-then-Invoke）模式：
    /// 先在锁保护下获取当前委托的快照，然后立即释放锁，
    /// 最后在锁外部依次调用所有处理程序。
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须为非 null 类型。</typeparam>
    /// <param name="evt">要发布的事件实例，将作为参数传递给每个处理程序。</param>
    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
    {
        Delegate? handlers;
        lock (_lock)
        {
            // 在锁保护下获取当前处理程序委托的快照
            _handlers.TryGetValue(typeof(TEvent), out handlers);
        }
        // 锁已释放。采用先释放锁再调用处理程序的策略，原因如下：
        // 1. 避免死锁：处理程序内部可能订阅或取消订阅其他事件，
        //    若在持有锁时执行将导致重入锁问题（同一线程无法重复获取 Monitor）
        // 2. 减少锁争用：长时间运行的处理程序不会阻塞其他订阅/取消订阅操作
        // 3. 快照语义：本次发布使用固定的委托列表，发布期间的订阅/取消不影响当前执行

        if (handlers is Action<TEvent> action)
        {
            action.Invoke(evt);
        }
    }
}
