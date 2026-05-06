using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DndGame.Core;
using DndGame.UI;
using DndGame.UI.Widgets;
using DndGame.Systems.Character;
using DndGame.Systems.Combat;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DndGame.Scenes;

/// <summary>
/// 酒馆主场景，整合招募板、任务板、角色面板和出发按钮。
/// </summary>
public class TavernScene : Scene
{
    private RecruitmentManager? _recruitment;
    private QuestBoardManager? _questBoard;
    private Texture2D? _pixelTexture;
    private string _currentPanel = "main"; // main, recruit, quest, party

    public override void Initialize()
    {
        _recruitment = new RecruitmentManager();
        _questBoard = new QuestBoardManager();

        // 创建单像素纹理
        _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // 加载测试角色
        LoadTestCharacters();

        base.Initialize();
    }

    public override void Draw(GameTime gameTime)
    {
        if (_pixelTexture == null) return;

        var sb = Game.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        // 绘制酒馆背景
        sb.Draw(_pixelTexture, new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            new Color(40, 30, 50));

        // 绘制当前面板
        switch (_currentPanel)
        {
            case "recruit":
                DrawRecruitPanel(sb);
                break;
            case "quest":
                DrawQuestPanel(sb);
                break;
            case "party":
                DrawPartyPanel(sb);
                break;
            default:
                DrawMainMenu(sb);
                break;
        }

        sb.End();

        base.Draw(gameTime);
    }

    public override void Update(GameTime gameTime)
    {
        var state = Microsoft.Xna.Framework.Input.Keyboard.GetState();

        // 简单按键切换面板
        if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R))
            _currentPanel = "recruit";
        else if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Q))
            _currentPanel = "quest";
        else if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.P))
            _currentPanel = "party";
        else if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            _currentPanel = "main";

        base.Update(gameTime);
    }

    public override void End()
    {
        _pixelTexture?.Dispose();
        base.End();
    }

    private void LoadTestCharacters()
    {
        var gen = new CharacterGenerator();
        gen.LoadRaces([
            new RaceConfig { RaceId = "human", Name = "人类", Speed = 30, AbilityIncreases = new Dictionary<string, int>
            {
                ["str"] = 1, ["dex"] = 1, ["con"] = 1, ["int"] = 1, ["wis"] = 1, ["cha"] = 1
            }},
            new RaceConfig { RaceId = "elf", Name = "精灵", Speed = 30, AbilityIncreases = new Dictionary<string, int>
            {
                ["dex"] = 2
            }},
            new RaceConfig { RaceId = "dwarf", Name = "矮人", Speed = 25, AbilityIncreases = new Dictionary<string, int>
            {
                ["con"] = 2
            }}
        ]);
        gen.LoadClasses([
            new ClassConfig { ClassId = "fighter", Name = "战士", HitDie = 10, PrimaryAbility = "str" },
            new ClassConfig { ClassId = "wizard", Name = "法师", HitDie = 6, PrimaryAbility = "int" },
            new ClassConfig { ClassId = "rogue", Name = "盗贼", HitDie = 8, PrimaryAbility = "dex" }
        ]);

        var characters = new List<CharacterData>
        {
            gen.Generate("human", "fighter"),
            gen.Generate("elf", "wizard"),
            gen.Generate("dwarf", "fighter"),
            gen.Generate("human", "rogue"),
            gen.Generate("elf", "rogue"),
            gen.Generate("dwarf", "wizard")
        };

        _recruitment?.LoadCharacters(characters);
    }

    private void DrawMainMenu(SpriteBatch sb)
    {
        DrawText(sb, "酒馆与命运", 100, 50, PixelTheme.Gold);
        DrawText(sb, "[R] 招募板  [Q] 任务板  [P] 队伍管理", 100, 120, PixelTheme.PrimaryText);
        DrawText(sb, $"队伍: {_recruitment?.Party.Count ?? 0}/{RecruitmentManager.MaxPartySize}", 100, 160, PixelTheme.PrimaryText);
    }

    private void DrawRecruitPanel(SpriteBatch sb)
    {
        DrawText(sb, "招募板", 100, 30, PixelTheme.Gold);
        DrawText(sb, "[Esc] 返回", 100, 60, PixelTheme.PrimaryText);

        if (_recruitment == null) return;

        var y = 100;
        foreach (var charData in _recruitment.AvailableCharacters)
        {
            var vm = new CharacterPanelViewModel(charData);
            DrawText(sb, $"{vm.Name} - {vm.Race} {vm.Level}级", 120, y, PixelTheme.PrimaryText);
            DrawText(sb, $"HP:{vm.FormatHP()} AC:{vm.AC}", 400, y, Color.Gray);
            y += 30;
        }
    }

    private void DrawQuestPanel(SpriteBatch sb)
    {
        DrawText(sb, "任务板", 100, 30, PixelTheme.Gold);
        DrawText(sb, "[Esc] 返回", 100, 60, PixelTheme.PrimaryText);

        if (_questBoard == null) return;

        var y = 100;
        foreach (var adventure in _questBoard.Adventures)
        {
            DrawText(sb, $"{adventure.Title} (Lv{adventure.RecommendedLevel})", 120, y, PixelTheme.PrimaryText);
            DrawText(sb, adventure.Description, 120, y + 20, Color.Gray);
            y += 50;
        }
    }

    private void DrawPartyPanel(SpriteBatch sb)
    {
        DrawText(sb, "队伍管理", 100, 30, PixelTheme.Gold);
        DrawText(sb, "[Esc] 返回", 100, 60, PixelTheme.PrimaryText);

        if (_recruitment == null) return;

        var y = 100;
        foreach (var charData in _recruitment.Party)
        {
            var vm = new CharacterPanelViewModel(charData);
            DrawText(sb, $"{vm.Name} - {vm.Race} {vm.Level}级", 120, y, PixelTheme.PrimaryText);
            DrawText(sb, $"HP:{vm.FormatHP()} AC:{vm.AC}", 400, y, Color.Gray);
            DrawText(sb, vm.FormatAbility(Ability.Str), 550, y, Color.LightGray);
            y += 40;
        }
    }

    private static void DrawText(SpriteBatch sb, string text, int x, int y, Color color)
    {
        // 简单文本绘制 — 使用单像素模拟（后续集成 FontStashSharp）
        // 当前仅占位，实际需要字体支持
    }
}
