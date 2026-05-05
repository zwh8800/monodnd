using DndGame.Core;
using DndGame.Scenes;

namespace DndGame;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var game = new GameRoot();
        game.StartSceneTransition(new MainMenuScene());
        game.Run();
    }
}
