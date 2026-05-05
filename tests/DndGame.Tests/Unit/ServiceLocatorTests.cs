using FluentAssertions;
using DndGame.Core;

namespace DndGame.Tests.Unit;

public class ServiceLocatorTests
{
    public interface ITestService
    {
        string GetValue();
    }

    private class TestService : ITestService
    {
        public string GetValue() => "test-value";
    }

    public ServiceLocatorTests()
    {
        ServiceLocator.Reset();
    }

    [Xunit.Fact]
    public void Register_NewService_CanBeResolved()
    {
        var service = new TestService();
        ServiceLocator.Register<ITestService>(service);

        var resolved = ServiceLocator.Get<ITestService>();

        resolved.GetValue().Should().Be("test-value");
    }

    [Xunit.Fact]
    public void Register_DuplicateType_ThrowsInvalidOperationException()
    {
        ServiceLocator.Register<ITestService>(new TestService());

        var act = () => ServiceLocator.Register<ITestService>(new TestService());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Xunit.Fact]
    public void Register_AfterFinalize_ThrowsInvalidOperationException()
    {
        ServiceLocator.FinalizeRegistration();

        var act = () => ServiceLocator.Register<ITestService>(new TestService());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*finalized*");
    }

    [Xunit.Fact]
    public void Get_UnregisteredService_ThrowsInvalidOperationException()
    {
        var act = () => ServiceLocator.Get<ITestService>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Xunit.Fact]
    public void TryGet_RegisteredService_ReturnsTrueAndService()
    {
        var service = new TestService();
        ServiceLocator.Register<ITestService>(service);

        var success = ServiceLocator.TryGet<ITestService>(out var resolved);

        success.Should().BeTrue();
        resolved.Should().Be(service);
    }

    [Xunit.Fact]
    public void TryGet_UnregisteredService_ReturnsFalseAndNull()
    {
        var success = ServiceLocator.TryGet<ITestService>(out var resolved);

        success.Should().BeFalse();
        resolved.Should().BeNull();
    }

    [Xunit.Fact]
    public void Reset_ClearsAllRegistrations()
    {
        ServiceLocator.Register<ITestService>(new TestService());
        ServiceLocator.FinalizeRegistration();

        ServiceLocator.Reset();

        var act = () => ServiceLocator.Register<ITestService>(new TestService());
        act.Should().NotThrow();
        ServiceLocator.IsFinalized.Should().BeFalse();
    }

    [Xunit.Fact]
    public void IsFinalized_AfterFinalizeRegistration_ReturnsTrue()
    {
        ServiceLocator.Reset();

        ServiceLocator.FinalizeRegistration();

        ServiceLocator.IsFinalized.Should().BeTrue();
    }
}
