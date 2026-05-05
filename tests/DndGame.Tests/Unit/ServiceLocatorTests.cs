using FluentAssertions;
using DndGame.Core;

namespace DndGame.Tests.Unit;

/// <summary>
/// 服务定位器（ServiceLocator）的单元测试。
/// 测试服务的注册、解析、重复注册保护、终态锁定、重置等核心功能。
/// </summary>
public class ServiceLocatorTests
{
    /// <summary>
    /// 测试用的服务接口，用于验证 ServiceLocator 的注册与解析功能。
    /// </summary>
    public interface ITestService
    {
        /// <summary>
        /// 返回测试字符串值。
        /// </summary>
        /// <returns>固定字符串 "test-value"。</returns>
        string GetValue();
    }

    /// <summary>
    /// <see cref="ITestService"/> 的简单实现，返回固定值用于测试。
    /// </summary>
    private class TestService : ITestService
    {
        public string GetValue() => "test-value";
    }

    public ServiceLocatorTests()
    {
        ServiceLocator.Reset();
    }

    /// <summary>
    /// 验证注册服务后可以通过 Get 方法正确解析。
    /// </summary>
    [Xunit.Fact]
    public void Register_NewService_CanBeResolved()
    {
        // Arrange: 创建并注册测试服务
        var service = new TestService();
        ServiceLocator.Register<ITestService>(service);

        // Act: 解析已注册的服务
        var resolved = ServiceLocator.Get<ITestService>();

        // Assert: 解析结果应与注册的实例一致
        resolved.GetValue().Should().Be("test-value");
    }

    /// <summary>
    /// 验证同一类型重复注册时会抛出 <see cref="InvalidOperationException"/>，防止服务被意外覆盖。
    /// </summary>
    [Xunit.Fact]
    public void Register_DuplicateType_ThrowsInvalidOperationException()
    {
        // Arrange: 注册一次服务
        ServiceLocator.Register<ITestService>(new TestService());

        // Act: 再次注册同一类型的服务
        var act = () => ServiceLocator.Register<ITestService>(new TestService());

        // Assert: 应抛出异常，提示已注册
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*已被注册*");
    }

    /// <summary>
    /// 验证在调用 <see cref="ServiceLocator.FinalizeRegistration"/> 后注册服务会抛出异常，确保终态锁定机制正常工作。
    /// </summary>
    [Xunit.Fact]
    public void Register_AfterFinalize_ThrowsInvalidOperationException()
    {
        // Arrange: 锁定注册状态
        ServiceLocator.FinalizeRegistration();

        // Act: 在终态后尝试注册
        var act = () => ServiceLocator.Register<ITestService>(new TestService());

        // Assert: 应抛出异常，提示已终态化
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*已冻结*");
    }

    /// <summary>
    /// 验证解析未注册的服务类型时会抛出 <see cref="InvalidOperationException"/>，提供明确的错误提示。
    /// </summary>
    [Xunit.Fact]
    public void Get_UnregisteredService_ThrowsInvalidOperationException()
    {
        // Act: 尝试解析未注册的服务
        var act = () => ServiceLocator.Get<ITestService>();

        // Assert: 应抛出异常，提示未注册
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*未注册*");
    }

    /// <summary>
    /// 验证 <see cref="ServiceLocator.TryGet{T}"/> 在服务已注册时返回 true 并正确输出服务实例。
    /// </summary>
    [Xunit.Fact]
    public void TryGet_RegisteredService_ReturnsTrueAndService()
    {
        // Arrange: 创建并注册测试服务
        var service = new TestService();
        ServiceLocator.Register<ITestService>(service);

        // Act: 尝试安全解析
        var success = ServiceLocator.TryGet<ITestService>(out var resolved);

        // Assert: 应成功解析，输出实例与注册实例一致
        success.Should().BeTrue();
        resolved.Should().Be(service);
    }

    /// <summary>
    /// 验证 <see cref="ServiceLocator.TryGet{T}"/> 在服务未注册时返回 false 且输出值为 null，避免抛出异常。
    /// </summary>
    [Xunit.Fact]
    public void TryGet_UnregisteredService_ReturnsFalseAndNull()
    {
        // Act: 尝试安全解析未注册的服务
        var success = ServiceLocator.TryGet<ITestService>(out var resolved);

        // Assert: 应返回 false，输出值为 null
        success.Should().BeFalse();
        resolved.Should().BeNull();
    }

    /// <summary>
    /// 验证 <see cref="ServiceLocator.Reset"/> 能够清除所有已注册服务并重置终态锁定状态，使服务定位器恢复到初始可用状态。
    /// </summary>
    [Xunit.Fact]
    public void Reset_ClearsAllRegistrations()
    {
        // Arrange: 注册服务并锁定
        ServiceLocator.Register<ITestService>(new TestService());
        ServiceLocator.FinalizeRegistration();

        // Act: 重置服务定位器
        ServiceLocator.Reset();

        // Assert: 重置后应能正常注册且终态标志为 false
        var act = () => ServiceLocator.Register<ITestService>(new TestService());
        act.Should().NotThrow();
        ServiceLocator.IsFinalized.Should().BeFalse();
    }

    /// <summary>
    /// 验证调用 <see cref="ServiceLocator.FinalizeRegistration"/> 后 <see cref="ServiceLocator.IsFinalized"/> 属性返回 true，表示终态锁定生效。
    /// </summary>
    [Xunit.Fact]
    public void IsFinalized_AfterFinalizeRegistration_ReturnsTrue()
    {
        // Arrange: 确保初始状态已重置
        ServiceLocator.Reset();

        // Act: 执行终态锁定
        ServiceLocator.FinalizeRegistration();

        // Assert: 终态标志应为 true
        ServiceLocator.IsFinalized.Should().BeTrue();
    }
}
