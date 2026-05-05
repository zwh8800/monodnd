using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// 所有游戏场景的基类。
/// 每个场景拥有自己的实体集合和场景级组件（渲染器、系统等）。
/// </summary>
public abstract class Scene
{
    /// <summary>
    /// 场景内的所有实体列表。
    /// </summary>
    private readonly List<Entity> _entities = new();

    /// <summary>
    /// 场景级组件列表，用于管理渲染器、系统等全局行为。
    /// </summary>
    private readonly List<SceneComponent> _sceneComponents = new();

    /// <summary>
    /// 获取 GameRoot 实例引用，用于访问全局服务。
    /// </summary>
    protected GameRoot Game => GameRoot.Instance;

    /// <summary>
    /// 场景中所有实体的只读视图。
    /// </summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>
    /// 所有场景级组件（渲染器、系统）的只读视图。
    /// </summary>
    public IReadOnlyList<SceneComponent> SceneComponents => _sceneComponents;

    /// <summary>
    /// 在此场景中创建一个新实体，并自动关联到本场景。
    /// </summary>
    /// <param name="name">实体的唯一名称标识符。</param>
    /// <returns>创建后的实体实例。</returns>
    public Entity CreateEntity(string name)
    {
        var entity = new Entity(name, this);
        _entities.Add(entity);
        return entity;
    }

    /// <summary>
    /// 从场景中移除指定实体，并触发其移除回调。
    /// </summary>
    /// <param name="entity">要移除的实体实例。</param>
    public void RemoveEntity(Entity entity)
    {
        if (_entities.Remove(entity))
        {
            // 通知该实体已从场景中移除，触发组件清理逻辑
            entity.OnRemovedFromScene();
        }
    }

    /// <summary>
    /// 添加场景级组件（如渲染器、系统等）。
    /// 组件会自动收到场景添加通知。
    /// </summary>
    /// <typeparam name="T">场景组件的具体类型。</typeparam>
    /// <param name="component">要添加的场景组件实例。</param>
    /// <returns>返回添加的组件实例，便于链式调用。</returns>
    public T AddSceneComponent<T>(T component) where T : SceneComponent
    {
        _sceneComponents.Add(component);
        // 通知组件已添加到场景中
        component.OnAddedToScene(this);
        return component;
    }

    /// <summary>
    /// 场景激活后调用一次。在此方法中初始化实体和系统。
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// 在 Initialize 之后、第一帧 Update 之前调用一次。
    /// </summary>
    public virtual void Begin() { }

    /// <summary>
    /// 每帧在 Draw 之前调用。在此方法中更新游戏逻辑。
    /// 依次更新所有实体和场景级组件。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照。</param>
    public virtual void Update(GameTime gameTime)
    {
        // 更新所有实体的每帧逻辑
        foreach (var entity in _entities)
        {
            entity.Update(gameTime);
        }

        // 更新所有场景级组件的每帧逻辑
        foreach (var sc in _sceneComponents)
        {
            sc.Update(gameTime);
        }
    }

    /// <summary>
    /// 每帧在 Update 之后调用。在此方法中渲染所有内容。
    /// 先绘制场景级组件，再绘制实体。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照。</param>
    public virtual void Draw(GameTime gameTime)
    {
        // 绘制场景级组件（通常在实体底层）
        foreach (var sc in _sceneComponents)
        {
            sc.Draw(gameTime);
        }

        // 绘制所有实体（在场景级组件上层）
        foreach (var entity in _entities)
        {
            entity.Draw(gameTime);
        }
    }

    /// <summary>
    /// 场景被替换时调用。在此方法中清理资源和状态。
    /// 遍历所有实体和组件调用移除回调，并清空集合。
    /// </summary>
    public virtual void End()
    {
        // 通知所有实体从场景中移除
        foreach (var entity in _entities)
        {
            entity.OnRemovedFromScene();
        }
        _entities.Clear();

        // 通知所有场景级组件从场景中移除
        foreach (var sc in _sceneComponents)
        {
            sc.OnRemovedFromScene();
        }
        _sceneComponents.Clear();
    }
}
