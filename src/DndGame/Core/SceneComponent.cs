using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// Base class for scene-level components (renderers, systems).
/// SceneComponents are like systems that operate at the scene level,
/// not attached to individual entities.
/// </summary>
public abstract class SceneComponent
{
    /// <summary>
    /// The scene this component belongs to.
    /// </summary>
    public Scene? Scene { get; private set; }

    /// <summary>
    /// Whether this component is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Called when the component is added to a scene.
    /// </summary>
    public virtual void OnAddedToScene(Scene scene)
    {
        Scene = scene;
    }

    /// <summary>
    /// Called when the scene is removed or the component is detached.
    /// </summary>
    public virtual void OnRemovedFromScene()
    {
        Scene = null;
    }

    /// <summary>
    /// Called every frame. Override for system logic.
    /// </summary>
    public virtual void Update(GameTime gameTime) { }

    /// <summary>
    /// Called every frame. Override for rendering.
    /// </summary>
    public virtual void Draw(GameTime gameTime) { }
}
