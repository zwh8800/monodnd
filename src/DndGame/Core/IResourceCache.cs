namespace DndGame.Core;

/// <summary>
/// 资源缓存管理器接口，负责游戏中纹理、字体、音频等资源的预加载与生命周期管理。
/// 通过缓存减少重复加载开销，并在内存压力下自动淘汰非关键资源。
/// </summary>
public interface IResourceCache
{
    /// <summary>
    /// 预加载指定类别下的全部资源。
    /// </summary>
    /// <param name="category">资源类别名称（如 "textures/ui", "fonts", "audio/bgm"）。</param>
    /// <param name="ct">取消令牌。</param>
    Task PreloadAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// 从缓存中获取指定键的资源。
    /// </summary>
    /// <typeparam name="T">资源的预期类型。</typeparam>
    /// <param name="key">资源唯一键。</param>
    /// <returns>已缓存的资源实例；未命中则返回 null。</returns>
    T? Get<T>(string key) where T : class;

    /// <summary>
    /// 移除指定键对应的缓存资源。
    /// </summary>
    /// <param name="key">资源唯一键。</param>
    void Evict(string key);

    /// <summary>
    /// 清空全部缓存资源。
    /// </summary>
    void Clear();

    /// <summary>
    /// 获取当前缓存资源的近似内存占用（字节）。
    /// </summary>
    long GetCacheSizeBytes();
}
