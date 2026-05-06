using Xunit;
using FluentAssertions;
using DndGame.Gateway.Validation;
using DndGame.Gateway.Cache;
using DndGame.Gateway.Fallback;

namespace DndGame.Tests.Unit.Gateway;

public class SchemaValidatorTests
{
    [Fact]
    public void Validate_ValidJSON_ReturnsSuccess()
    {
        var validator = new SchemaValidator();
        var result = validator.Validate("test", "{\"key\": \"value\"}");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidJSON_ReturnsFail()
    {
        var validator = new SchemaValidator();
        var result = validator.Validate("test", "not json");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("解析失败");
    }

    [Fact]
    public void Validate_EmptyString_ReturnsFail()
    {
        var validator = new SchemaValidator();
        var result = validator.Validate("test", "");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullString_ReturnsFail()
    {
        var validator = new SchemaValidator();
        var result = validator.Validate("test", null!);
        result.IsValid.Should().BeFalse();
    }
}

public class CacheManagerTests
{
    [Fact]
    public void Lookup_EmptyCache_ReturnsMiss()
    {
        var cache = new CacheManager();
        var result = cache.Lookup("agent", "hash");
        result.IsHit.Should().BeFalse();
    }

    [Fact]
    public void Store_AndLookup_ReturnsHit()
    {
        var cache = new CacheManager();
        cache.Store("agent", "hash", "response");
        var result = cache.Lookup("agent", "hash");
        result.IsHit.Should().BeTrue();
        result.Response.Should().Be("response");
    }

    [Fact]
    public void Store_WithTTL_Expires()
    {
        var cache = new CacheManager();
        cache.Store("agent", "hash", "response", TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        var result = cache.Lookup("agent", "hash");
        result.IsHit.Should().BeFalse();
    }

    [Fact]
    public void GenerateHash_SameInput_SameHash()
    {
        var hash1 = CacheManager.GenerateHash("test");
        var hash2 = CacheManager.GenerateHash("test");
        hash1.Should().Be(hash2);
    }
}

public class FallbackManagerTests
{
    [Fact]
    public void GetFallback_CombatNarration_ReturnsTemplate()
    {
        var fallback = new FallbackManager();
        var result = fallback.GetFallback("dm_agent", "combat_narration");
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetFallback_UnknownType_ReturnsDefault()
    {
        var fallback = new FallbackManager();
        var result = fallback.GetFallback("dm_agent", "unknown");
        result.Should().Be("战斗继续进行...");
    }
}
