using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// Represents a game object within a scene.
/// Entities are composed of Components via AddComponent.
/// </summary>
public class Entity
{
    private readonly List<Component> _components = new();

    /// <summary>
    /// Unique name identifier for this entity within its scene.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The scene this entity belongs to.
    /// </summary>
    public Scene Scene { get; }

    /// <summary>
    /// World position of this entity.
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Whether this entity is active (updated and drawn).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this entity has been destroyed.
    /// </summary>
    public bool IsDestroyed { get; private set; }

    /// <summary>
    /// Read-only view of all components.
    /// </summary>
    public IReadOnlyList<Component> Components => _components;

    public Entity(string name, Scene scene)
    {
        Name = name;
        Scene = scene;
    }

    /// <summary>
    /// Add a component to this entity. Returns this entity for fluent chaining.
    /// </summary>
    public Entity AddComponent(Component component)
    {
        _components.Add(component);
        component.OnAddedToEntity(this);
        return this;
    }

    /// <summary>
    /// Get the first component of the specified type.
    /// </summary>
    public T? GetComponent<T>() where T : Component
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Remove a component from this entity.
    /// </summary>
    public void RemoveComponent(Component component)
    {
        if (_components.Remove(component))
        {
            component.OnRemovedFromEntity();
        }
    }

    /// <summary>
    /// Destroy this entity and remove it from the scene.
    /// </summary>
    public void Destroy()
    {
        IsDestroyed = true;
        Scene.RemoveEntity(this);
    }

    public virtual void Update(GameTime gameTime)
    {
        if (!Enabled) return;

        foreach (var component in _components)
        {
            if (component.Enabled)
            {
                component.Update(gameTime);
            }
        }
    }

    public virtual void Draw(GameTime gameTime)
    {
        if (!Enabled) return;

        foreach (var component in _components)
        {
            if (component.Enabled)
            {
                component.Draw(gameTime);
            }
        }
    }

    /// <summary>
    /// Called when removed from scene or destroyed.
    /// </summary>
    public void OnRemovedFromScene()
    {
        foreach (var component in _components)
        {
            component.OnRemovedFromEntity();
        }
        _components.Clear();
    }
}
