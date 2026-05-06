using System.Text.Json;

namespace DndGame.Gateway.Validation;

/// <summary>
/// JSON Schema 验证器，使用 JsonSchema.Net 验证 LLM 输出。
/// </summary>
public class SchemaValidator
{
    /// <summary>
    /// 验证 JSON 字符串是否符合指定的 schema 结构。
    /// 当前为基础实现，后续集成 JsonSchema.Net。
    /// </summary>
    public ValidationResult Validate(string agentId, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ValidationResult.Fail("JSON 为空");

        try
        {
            JsonDocument.Parse(json);
            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail($"JSON 解析失败: {ex.Message}");
        }
    }
}

/// <summary>
/// 验证结果。
/// </summary>
public record ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string message) => new() { IsValid = false, ErrorMessage = message };
}
