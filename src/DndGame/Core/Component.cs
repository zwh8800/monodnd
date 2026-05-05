using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// Base class for entity-level components.
/// Components contain the behavior and data for entities.
/// </summary>
public abstract class Component
{
    /// <summary>
    /// The entity this component is attached to.
    /// </summary>
    public Entity? Entity { get; private set; }

    /// <summary>
    /// Whether this component is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Called when the component is added to an entity.
    /// Use this for initialization — not the constructor.
    /// </summary>
    public virtual void OnAddedToEntity(Entity entity)
    {
        Entity = entity;
    }

    /// <summary>
    /// Called when the component is removed from an entity.
    /// </summary>
    public virtual void OnRemovedFromEntity()
    {
        Entity = null;
    }

    /// <summary>
    /// Called every frame. Override for game logic.
    /// </summary>
    public virtual void Update(GameTime gameTime) { }

    /// <summary>
    /// Called every frame. Override for rendering.
    /// </summary>
    public virtual void Draw(GameTime gameTime) { }
}
