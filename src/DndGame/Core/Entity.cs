using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// 表示场景中的一个游戏对象。
/// 实体通过 AddComponent 方法组合多个组件来实现功能。
/// </summary>
public class Entity
{
    /// <summary>
    /// 附加到此实体的所有组件列表。
    /// </summary>
    private readonly List<Component> _components = new();

    /// <summary>
    /// 实体在其所属场景中的唯一名称标识符。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 此实体所属的场景实例。
    /// </summary>
    public Scene Scene { get; }

    /// <summary>
    /// 实体在游戏世界中的位置坐标。
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// 控制实体是否激活。禁用时不会更新和绘制。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 标记实体是否已被销毁。销毁后应从场景中移除。
    /// </summary>
    public bool IsDestroyed { get; private set; }

    /// <summary>
    /// 所有组件的只读视图。
    /// </summary>
    public IReadOnlyList<Component> Components => _components;

    /// <summary>
    /// 构造函数。创建一个属于指定场景、具有指定名称的实体。
    /// </summary>
    /// <param name="name">实体的名称标识符。</param>
    /// <param name="scene">实体所属的场景。</param>
    internal Entity(string name, Scene scene)
    {
        Name = name;
        Scene = scene;
    }

    /// <summary>
    /// 向实体添加一个组件。返回实体实例以支持链式调用。
    /// </summary>
    /// <param name="component">要添加的组件实例。</param>
    /// <returns>返回实体自身，便于链式调用。</returns>
    public Entity AddComponent(Component component)
    {
        _components.Add(component);
        // 通知组件已添加到实体中
        component.OnAddedToEntity(this);
        return this;
    }

    /// <summary>
    /// 获取指定类型的第一个组件。
    /// </summary>
    /// <typeparam name="T">要查找的组件类型。</typeparam>
    /// <returns>匹配的组件实例，若未找到则返回 null。</returns>
    public T? GetComponent<T>() where T : Component
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// 从实体中移除指定组件，并触发其移除回调。
    /// </summary>
    /// <param name="component">要移除的组件实例。</param>
    public void RemoveComponent(Component component)
    {
        if (_components.Remove(component))
        {
            // 通知组件已从实体中移除
            component.OnRemovedFromEntity();
        }
    }

    /// <summary>
    /// 销毁此实体并将其从场景中移除。
    /// </summary>
    public void Destroy()
    {
        // 标记为已销毁
        IsDestroyed = true;
        // 从所属场景中移除自身
        Scene.RemoveEntity(this);
    }

    /// <summary>
    /// 每帧更新逻辑。遍历所有已启用的组件进行更新。
    /// 如果实体被禁用（Enabled == false），则跳过更新。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照。</param>
    public virtual void Update(GameTime gameTime)
    {
        // 实体禁用时跳过更新
        if (!Enabled) return;

        // 更新所有已启用的组件
        foreach (var component in _components)
        {
            if (component.Enabled)
            {
                component.Update(gameTime);
            }
        }
    }

    /// <summary>
    /// 每帧绘制逻辑。遍历所有已启用的组件进行绘制。
    /// 如果实体被禁用（Enabled == false），则跳过绘制。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照。</param>
    public virtual void Draw(GameTime gameTime)
    {
        // 实体禁用时跳过绘制
        if (!Enabled) return;

        // 绘制所有已启用的组件
        foreach (var component in _components)
        {
            if (component.Enabled)
            {
                component.Draw(gameTime);
            }
        }
    }

    /// <summary>
    /// 当实体从场景中移除或被销毁时调用。
    /// 通知所有组件已从实体移除，并清空组件列表。
    /// </summary>
    public void OnRemovedFromScene()
    {
        // 通知所有组件从实体中移除
        foreach (var component in _components)
        {
            component.OnRemovedFromEntity();
        }
        // 清空组件列表
        _components.Clear();
    }
}
