namespace DndGame.Core;

/// <summary>
/// 数据持久化服务接口，负责游戏存档的保存、加载与删除。
/// 通过与 sqlite-net + JSON 的组合实现跨平台数据持久化。
/// </summary>
public interface IDataPersistence
{
    /// <summary>
    /// 将指定游戏状态保存到指定存档槽位。
    /// </summary>
    /// <param name="slot">存档槽位编号（1-based）。</param>
    /// <param name="gameState">要持久化的游戏状态对象。</param>
    /// <param name="ct">取消令牌。</param>
    Task SaveAsync(int slot, object gameState, CancellationToken ct = default);

    /// <summary>
    /// 从指定存档槽位加载游戏状态。
    /// </summary>
    /// <param name="slot">存档槽位编号（1-based）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已加载的游戏状态对象；若槽位为空则返回 null。</returns>
    Task<object?> LoadAsync(int slot, CancellationToken ct = default);

    /// <summary>
    /// 删除指定存档槽位的数据。
    /// </summary>
    /// <param name="slot">存档槽位编号（1-based）。</param>
    /// <param name="ct">取消令牌。</param>
    Task DeleteAsync(int slot, CancellationToken ct = default);

    /// <summary>
    /// 获取所有已有存档的槽位编号列表。
    /// </summary>
    /// <returns>已使用的存档槽位编号（按槽位升序排列）。</returns>
    IReadOnlyList<int> GetAvailableSlots();
}
