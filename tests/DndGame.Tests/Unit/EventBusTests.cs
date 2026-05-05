using FluentAssertions;
using DndGame.Core;

namespace DndGame.Tests.Unit;

/// <summary>
/// 事件总线（EventBus）的单元测试。
/// 测试事件的发布/订阅、多订阅者通知、取消订阅、类型隔离、及清理等核心功能。
/// </summary>
public class EventBusTests
{
    /// <summary>
    /// 用于测试的简单事件记录，包含一条字符串消息。
    /// </summary>
    /// <param name="Message">事件携带的消息文本。</param>
    private record TestEvent(string Message);

    /// <summary>
    /// 用于测试类型隔离的另一个事件记录，包含一个整数值。
    /// 验证事件总线按类型分发事件时不会混杂不同类型的事件。
    /// </summary>
    /// <param name="Value">事件携带的整数值。</param>
    private record AnotherEvent(int Value);

    /// <summary>
    /// 验证订阅处理器后，发布事件时对应的处理器能够正确接收到事件数据。
    /// </summary>
    [Xunit.Fact]
    public void Publish_SubscribedHandler_ReceivesEvent()
    {
        // Arrange: 创建事件总线，订阅 TestEvent 并捕获消息
        var bus = new EventBus();
        string? received = null;
        bus.Subscribe<TestEvent>(e => received = e.Message);

        // Act: 发布一个 TestEvent
        bus.Publish(new TestEvent("hello"));

        // Assert: 处理器应收到消息 "hello"
        received.Should().Be("hello");
    }

    /// <summary>
    /// 验证同一事件的多个订阅者都能接收到发布的事件，确保多播机制正确。
    /// </summary>
    [Xunit.Fact]
    public void Publish_MultipleSubscribers_AllReceiveEvent()
    {
        // Arrange: 创建事件总线，订阅两个独立的处理器
        var bus = new EventBus();
        var results = new List<string>();
        bus.Subscribe<TestEvent>(e => results.Add("first-" + e.Message));
        bus.Subscribe<TestEvent>(e => results.Add("second-" + e.Message));

        // Act: 发布一个 TestEvent
        bus.Publish(new TestEvent("world"));

        // Assert: 两个处理器都应收到事件并添加到结果列表
        results.Should().Contain("first-world");
        results.Should().Contain("second-world");
    }

    /// <summary>
    /// 验证取消订阅后，已移除的处理器不会再接收到后续发布的事件。
    /// </summary>
    [Xunit.Fact]
    public void Unsubscribe_RemovedHandler_DoesNotReceiveEvent()
    {
        // Arrange: 订阅处理器后立即取消订阅
        var bus = new EventBus();
        var received = new List<string>();
        Action<TestEvent> handler = e => received.Add(e.Message);
        bus.Subscribe(handler);

        // Act: 取消订阅并发布事件
        bus.Unsubscribe(handler);
        bus.Publish(new TestEvent("gone"));

        // Assert: 已取消的处理器不应收到事件
        received.Should().BeEmpty();
    }

    /// <summary>
    /// 验证事件总线按事件类型分发，发布不同类型的事件不会触发不相关的处理器。
    /// </summary>
    [Xunit.Fact]
    public void Publish_DifferentEventType_DoesNotTriggerWrongHandler()
    {
        // Arrange: 订阅 TestEvent，但准备发布 AnotherEvent
        var bus = new EventBus();
        var testReceived = false;
        bus.Subscribe<TestEvent>(_ => testReceived = true);

        // Act: 发布不同类型的事件
        bus.Publish(new AnotherEvent(42));

        // Assert: TestEvent 的处理器不应被触发
        testReceived.Should().BeFalse();
    }

    /// <summary>
    /// 验证逐一取消所有订阅后，事件总线仍能正常发布事件且不会抛出异常，确保清理操作正确。
    /// </summary>
    [Xunit.Fact]
    public void Unsubscribe_AllHandlers_ClearsSubscriptions()
    {
        // Arrange: 注册两个不同的处理器
        var bus = new EventBus();
        Action<TestEvent> handler1 = _ => { };
        Action<TestEvent> handler2 = _ => { };
        bus.Subscribe(handler1);
        bus.Subscribe(handler2);

        // Act: 逐一取消所有订阅后发布事件
        bus.Unsubscribe(handler1);
        bus.Unsubscribe(handler2);

        // Assert: 取消全部订阅后发布事件不应引发异常
        var action = () => bus.Publish(new TestEvent("x"));
        action.Should().NotThrow();
    }
}
