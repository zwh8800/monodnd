using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// 场景级组件的基类（渲染器、系统等）。
/// SceneComponent 类似于在场景层面运行的系统，
/// 不附着于单个实体，而是管理全局场景逻辑。
/// </summary>
public abstract class SceneComponent
{
    /// <summary>
    /// 此组件所属的场景对象。
    /// 当组件从场景分离时，此值变为 null。
    /// </summary>
    public Scene? Scene { get; private set; }

    /// <summary>
    /// 指示此组件是否处于启用状态。
    /// 禁用时，组件不会执行其更新或渲染逻辑。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 当组件被添加到一个场景时调用。
    /// 在此方法中完成场景级别的初始化工作。
    /// </summary>
    /// <param name="scene">此组件所添加到的目标场景。</param>
    public virtual void OnAddedToScene(Scene scene)
    {
        // 保存所属场景的引用，供子类使用
        Scene = scene;
    }

    /// <summary>
    /// 当场景被移除或组件从场景分离时调用。
    /// 在此方法中执行清理工作，如注销场景事件监听。
    /// </summary>
    public virtual void OnRemovedFromScene()
    {
        // 清除对所属场景的引用，避免内存泄漏
        Scene = null;
    }

    /// <summary>
    /// 每帧调用一次。子类重写此方法以实现场景级别的系统逻辑。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态，包含自上一帧以来的时间增量。</param>
    public virtual void Update(GameTime gameTime) { }

    /// <summary>
    /// 每帧调用一次。子类重写此方法以实现场景级别的渲染逻辑。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态，用于时间相关的渲染效果。</param>
    public virtual void Draw(GameTime gameTime) { }
}
