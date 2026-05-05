using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// 实体级组件的基类。
/// 组件包含实体的行为和数据，通过附加到实体上来扩展实体的功能。
/// </summary>
public abstract class Component
{
    /// <summary>
    /// 此组件所附加到的实体对象。
    /// 当组件从实体移除时，此值变为 null。
    /// </summary>
    public Entity? Entity { get; private set; }

    /// <summary>
    /// 指示此组件是否处于启用状态。
    /// 禁用时，组件不应执行其更新或渲染逻辑。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 当组件被添加到一个实体时调用。
    /// 在此方法中进行初始化操作，而非在构造函数中执行。
    /// </summary>
    /// <param name="entity">此组件所附加到的目标实体。</param>
    public virtual void OnAddedToEntity(Entity entity)
    {
        // 保存所属实体的引用，供子类使用
        Entity = entity;
    }

    /// <summary>
    /// 当组件从一个实体移除时调用。
    /// 在此方法中执行清理操作，如释放资源或取消订阅事件。
    /// </summary>
    public virtual void OnRemovedFromEntity()
    {
        // 清除对所属实体的引用，使其可被垃圾回收
        Entity = null;
    }

    /// <summary>
    /// 每帧调用一次。子类重写此方法以实现自定义游戏逻辑。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态，包含自上一帧以来的时间增量。</param>
    public virtual void Update(GameTime gameTime) { }

    /// <summary>
    /// 每帧调用一次。子类重写此方法以实现自定义渲染逻辑。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态，用于时间相关的渲染效果。</param>
    public virtual void Draw(GameTime gameTime) { }
}
