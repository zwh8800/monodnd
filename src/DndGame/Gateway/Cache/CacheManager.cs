namespace DndGame.Gateway.Cache;

/// <summary>
/// 缓存管理器，提供内存缓存用于 LLM 响应。
/// 当前为基础实现，后续集成 SQLite 语义缓存。
/// </summary>
public class CacheManager
{
    private readonly Dictionary<string, CacheEntry> _cache = new();

    /// <summary>
    /// 查找缓存。
    /// </summary>
    public CacheResult Lookup(string agentId, string contextHash)
    {
        var key = $"{agentId}:{contextHash}";
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            entry.LastAccessed = DateTime.UtcNow;
            entry.HitCount++;
            return new CacheResult { IsHit = true, Response = entry.Response };
        }
        return new CacheResult { IsHit = false };
    }

    /// <summary>
    /// 存储缓存。
    /// </summary>
    public void Store(string agentId, string contextHash, string response, TimeSpan? ttl = null)
    {
        var key = $"{agentId}:{contextHash}";
        _cache[key] = new CacheEntry
        {
            Response = response,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + (ttl ?? TimeSpan.FromHours(24)),
            LastAccessed = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 生成上下文哈希（简单实现）。
    /// </summary>
    public static string GenerateHash(string context)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(context)));
    }
}

public class CacheEntry
{
    public string Response { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime LastAccessed { get; set; }
    public int HitCount { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

public record CacheResult
{
    public bool IsHit { get; init; }
    public string? Response { get; init; }
}
