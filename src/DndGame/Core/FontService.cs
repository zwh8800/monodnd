using FontStashSharp;

namespace DndGame.Core;

/// <summary>
/// 字体服务接口，提供全局字体访问。
/// </summary>
public interface IFontService
{
    /// <summary>
    /// 获取指定大小的字体。
    /// </summary>
    DynamicSpriteFont GetFont(int size);
}

/// <summary>
/// 字体服务实现，加载并缓存 FontStashSharp 字体。
/// </summary>
public class FontService : IFontService
{
    private readonly FontSystem _fontSystem;

    public FontService(string fontPath)
    {
        _fontSystem = new FontSystem();
        _fontSystem.AddFont(File.ReadAllBytes(fontPath));
    }

    public DynamicSpriteFont GetFont(int size) => _fontSystem.GetFont(size);
}
