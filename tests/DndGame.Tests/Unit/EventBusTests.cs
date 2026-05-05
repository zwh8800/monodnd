using FluentAssertions;
using DndGame.Core;

namespace DndGame.Tests.Unit;

public class EventBusTests
{
    private record TestEvent(string Message);
    private record AnotherEvent(int Value);

    [Xunit.Fact]
    public void Publish_SubscribedHandler_ReceivesEvent()
    {
        var bus = new EventBus();
        string? received = null;
        bus.Subscribe<TestEvent>(e => received = e.Message);

        bus.Publish(new TestEvent("hello"));

        received.Should().Be("hello");
    }

    [Xunit.Fact]
    public void Publish_MultipleSubscribers_AllReceiveEvent()
    {
        var bus = new EventBus();
        var results = new List<string>();
        bus.Subscribe<TestEvent>(e => results.Add("first-" + e.Message));
        bus.Subscribe<TestEvent>(e => results.Add("second-" + e.Message));

        bus.Publish(new TestEvent("world"));

        results.Should().Contain("first-world");
        results.Should().Contain("second-world");
    }

    [Xunit.Fact]
    public void Unsubscribe_RemovedHandler_DoesNotReceiveEvent()
    {
        var bus = new EventBus();
        var received = new List<string>();
        Action<TestEvent> handler = e => received.Add(e.Message);
        bus.Subscribe(handler);

        bus.Unsubscribe(handler);
        bus.Publish(new TestEvent("gone"));

        received.Should().BeEmpty();
    }

    [Xunit.Fact]
    public void Publish_DifferentEventType_DoesNotTriggerWrongHandler()
    {
        var bus = new EventBus();
        var testReceived = false;
        bus.Subscribe<TestEvent>(_ => testReceived = true);

        bus.Publish(new AnotherEvent(42));

        testReceived.Should().BeFalse();
    }

    [Xunit.Fact]
    public void Unsubscribe_AllHandlers_ClearsSubscriptions()
    {
        var bus = new EventBus();
        Action<TestEvent> handler1 = _ => { };
        Action<TestEvent> handler2 = _ => { };
        bus.Subscribe(handler1);
        bus.Subscribe(handler2);

        bus.Unsubscribe(handler1);
        bus.Unsubscribe(handler2);

        var action = () => bus.Publish(new TestEvent("x"));
        action.Should().NotThrow();
    }
}
