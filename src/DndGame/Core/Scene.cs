using Microsoft.Xna.Framework;

namespace DndGame.Core;

/// <summary>
/// Base class for all game scenes.
/// Each scene owns its entities and systems (SceneComponents).
/// </summary>
public abstract class Scene
{
    private readonly List<Entity> _entities = new();
    private readonly List<SceneComponent> _sceneComponents = new();

    /// <summary>
    /// Reference to the GameRoot for accessing global services.
    /// </summary>
    protected GameRoot Game => GameRoot.Instance;

    /// <summary>
    /// Read-only view of all entities in this scene.
    /// </summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>
    /// All scene-level components (renderers, systems).
    /// </summary>
    public IReadOnlyList<SceneComponent> SceneComponents => _sceneComponents;

    /// <summary>
    /// Create a new entity attached to this scene.
    /// </summary>
    public Entity CreateEntity(string name)
    {
        var entity = new Entity(name, this);
        _entities.Add(entity);
        return entity;
    }

    /// <summary>
    /// Remove an entity from this scene.
    /// </summary>
    public void RemoveEntity(Entity entity)
    {
        if (_entities.Remove(entity))
        {
            entity.OnRemovedFromScene();
        }
    }

    /// <summary>
    /// Add a scene-level component (renderer, system).
    /// </summary>
    public T AddSceneComponent<T>(T component) where T : SceneComponent
    {
        _sceneComponents.Add(component);
        component.OnAddedToScene(this);
        return component;
    }

    /// <summary>
    /// Called once after the scene is set as active. Initialize entities and systems here.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    /// Called every frame after Initialize and before the first Update.
    /// </summary>
    public virtual void Begin() { }

    /// <summary>
    /// Called every frame before Draw. Update game logic here.
    /// </summary>
    public virtual void Update(GameTime gameTime)
    {
        foreach (var entity in _entities)
        {
            entity.Update(gameTime);
        }

        foreach (var sc in _sceneComponents)
        {
            sc.Update(gameTime);
        }
    }

    /// <summary>
    /// Called every frame after Update. Render everything here.
    /// </summary>
    public virtual void Draw(GameTime gameTime)
    {
        foreach (var sc in _sceneComponents)
        {
            sc.Draw(gameTime);
        }

        foreach (var entity in _entities)
        {
            entity.Draw(gameTime);
        }
    }

    /// <summary>
    /// Called when the scene is being replaced. Clean up resources here.
    /// </summary>
    public virtual void End()
    {
        foreach (var entity in _entities)
        {
            entity.OnRemovedFromScene();
        }
        _entities.Clear();

        foreach (var sc in _sceneComponents)
        {
            sc.OnRemovedFromScene();
        }
        _sceneComponents.Clear();
    }
}
