namespace DndGame.Core;

public interface IGameStateManager
{
    SceneId CurrentScene { get; }
    object? TransitionContext { get; set; }
    void SetCurrentScene(SceneId sceneId);
}

public enum SceneId
{
    MainMenu,
    Tavern,
    AdventureMap,
    Combat,
    Loading,
    Settlement,
    Dialogue
}

public class GameStateManager : IGameStateManager
{
    public SceneId CurrentScene { get; private set; } = SceneId.MainMenu;
    public object? TransitionContext { get; set; }

    public void SetCurrentScene(SceneId sceneId)
    {
        CurrentScene = sceneId;
    }
}
