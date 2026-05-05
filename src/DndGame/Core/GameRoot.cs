using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DndGame.Core;

/// <summary>
/// Root game class — manages scenes, global systems, and the main game loop.
/// Inherits from MonoGame's Game class directly, with a scene-based architecture.
/// </summary>
public class GameRoot : Game
{
    private static GameRoot? _instance;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Scene? _currentScene;
    private Scene? _nextScene;

    /// <summary>
    /// Global singleton accessor.
    /// </summary>
    public static GameRoot Instance => _instance ?? throw new InvalidOperationException("GameRoot not initialized");

    /// <summary>
    /// Global SpriteBatch shared across all scenes.
    /// </summary>
    public SpriteBatch SpriteBatch => _spriteBatch ?? throw new InvalidOperationException("SpriteBatch not initialized");

    /// <summary>
    /// Current active scene.
    /// </summary>
    public Scene? CurrentScene => _currentScene;

    /// <summary>
    /// Window width in pixels.
    /// </summary>
    public const int DESIGN_WIDTH = 1280;

    /// <summary>
    /// Window height in pixels.
    /// </summary>
    public const int DESIGN_HEIGHT = 720;

    public GameRoot()
    {
        _instance = this;

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = DESIGN_WIDTH,
            PreferredBackBufferHeight = DESIGN_HEIGHT,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "酒馆与命运";
    }

    /// <summary>
    /// Switch to a new scene with an optional transition effect.
    /// The actual switch happens at the start of the next update cycle.
    /// </summary>
    public void StartSceneTransition(Scene nextScene)
    {
        _nextScene = nextScene;
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        // Process pending scene transition
        if (_nextScene != null)
        {
            _currentScene?.End();
            _currentScene = _nextScene;
            _nextScene = null;
            _currentScene.Initialize();
            _currentScene.Begin();
        }

        _currentScene?.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        _currentScene?.Draw(gameTime);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentScene?.End();
            _spriteBatch?.Dispose();
        }
        base.Dispose(disposing);
    }
}
