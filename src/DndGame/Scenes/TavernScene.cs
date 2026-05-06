using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using DndGame.Core;
using DndGame.UI;
using DndGame.UI.Widgets;
using DndGame.Systems.Character;
using DndGame.Systems.Combat;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace DndGame.Scenes;

/// <summary>
/// 酒馆主场景 —— 招募、任务、队伍管理。
/// </summary>
public class TavernScene : Scene
{
    private RecruitmentManager? _recruitment;
    private QuestBoardManager? _questBoard;
    private Texture2D? _pixelTexture;
    private string _currentPanel = "main";
    private DynamicSpriteFont? _font;
    private DynamicSpriteFont? _fontSmall;
    private KeyboardState _prevKeyState;

    public override void Initialize()
    {
        _recruitment = new RecruitmentManager();
        _questBoard = new QuestBoardManager();

        _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        if (ServiceLocator.TryGet<IFontService>(out var fontService))
        {
            _font = fontService!.GetFont(24);
            _fontSmall = fontService.GetFont(16);
        }

        LoadTestCharacters();
        LoadTestAdventures();

        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        var state = Keyboard.GetState();

        if (state.IsKeyDown(Keys.R) && _prevKeyState.IsKeyUp(Keys.R))
            _currentPanel = "recruit";
        else if (state.IsKeyDown(Keys.Q) && _prevKeyState.IsKeyUp(Keys.Q))
            _currentPanel = "quest";
        else if (state.IsKeyDown(Keys.P) && _prevKeyState.IsKeyUp(Keys.P))
            _currentPanel = "party";
        else if (state.IsKeyDown(Keys.Escape) && _prevKeyState.IsKeyUp(Keys.Escape))
            _currentPanel = "main";
        else if (state.IsKeyDown(Keys.E) && _prevKeyState.IsKeyUp(Keys.E) && _currentPanel == "main")
        {
            if (_recruitment?.Party.Count > 0)
                GameRoot.Instance.StartSceneTransition(new AdventureScene());
        }

        _prevKeyState = state;
        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        if (_pixelTexture == null || _font == null) return;

        var sb = Game.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        sb.Draw(_pixelTexture, new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            new Color(40, 30, 50));

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

    private void LoadTestAdventures()
    {
        _questBoard?.LoadAdventures([
            new AdventureTemplate
            {
                AdventureId = "adv_cellar",
                Title = "地窖清剿",
                Description = "清除酒馆地窖中的鼠群",
                RecommendedLevel = 1,
                EstimatedDuration = "30分钟",
                EnemyTypes = ["巨型老鼠", "鼠人"],
                NodeCount = 5
            },
            new AdventureTemplate
            {
                AdventureId = "adv_caravan",
                Title = "马车护卫",
                Description = "护送商队穿越危险的森林",
                RecommendedLevel = 1,
                EstimatedDuration = "45分钟",
                EnemyTypes = ["哥布林", "狼"],
                NodeCount = 6
            },
            new AdventureTemplate
            {
                AdventureId = "adv_ruins",
                Title = "废墟探险",
                Description = "探索古代遗迹的秘密",
                RecommendedLevel = 2,
                EstimatedDuration = "1小时",
                EnemyTypes = ["骷髅", "陷阱"],
                NodeCount = 8
            }
        ]);
    }

    private void DrawMainMenu(SpriteBatch sb)
    {
        _font!.DrawText(sb, "酒馆与命运", new Vector2(100, 50), PixelTheme.Gold);
        _fontSmall!.DrawText(sb, "[R] 招募板  [Q] 任务板  [P] 队伍管理  [E] 出发冒险", new Vector2(100, 120), PixelTheme.PrimaryText);
        _fontSmall.DrawText(sb, $"队伍: {_recruitment?.Party.Count ?? 0}/{RecruitmentManager.MaxPartySize}", new Vector2(100, 160), PixelTheme.PrimaryText);

        if (_recruitment?.Party.Count > 0)
        {
            _fontSmall.DrawText(sb, "按 E 进入冒险", new Vector2(100, 220), new Color(100, 255, 100));
        }
        else
        {
            _fontSmall.DrawText(sb, "先招募队员再出发", new Vector2(100, 220), PixelTheme.HintText);
        }
    }

    private void DrawRecruitPanel(SpriteBatch sb)
    {
        _font!.DrawText(sb, "招募板", new Vector2(100, 30), PixelTheme.Gold);
        _fontSmall!.DrawText(sb, "[Esc] 返回", new Vector2(100, 60), PixelTheme.PrimaryText);

        if (_recruitment == null) return;

        var y = 100;
        foreach (var charData in _recruitment.AvailableCharacters)
        {
             var vm = new CharacterPanelViewModel(charData);
             _fontSmall.DrawText(sb, $"{vm.Name} - {vm.Race} {vm.Level}级", new Vector2(120, y), PixelTheme.PrimaryText);
             _fontSmall.DrawText(sb, $"HP:{vm.FormatHP()} AC:{vm.AC}", new Vector2(400, y), PixelTheme.SecondaryText);
             y += 30;
        }

        _fontSmall.DrawText(sb, $"队伍: {_recruitment.Party.Count}/{RecruitmentManager.MaxPartySize}", new Vector2(100, y + 20), PixelTheme.PrimaryText);
    }

    private void DrawQuestPanel(SpriteBatch sb)
    {
        _font!.DrawText(sb, "任务板", new Vector2(100, 30), PixelTheme.Gold);
        _fontSmall!.DrawText(sb, "[Esc] 返回", new Vector2(100, 60), PixelTheme.PrimaryText);

        if (_questBoard == null) return;

        var y = 100;
        foreach (var adventure in _questBoard.Adventures)
        {
             _fontSmall.DrawText(sb, $"{adventure.Title} (Lv{adventure.RecommendedLevel})", new Vector2(120, y), PixelTheme.PrimaryText);
             _fontSmall.DrawText(sb, adventure.Description, new Vector2(120, y + 20), PixelTheme.SecondaryText);
             y += 50;
        }
    }

    private void DrawPartyPanel(SpriteBatch sb)
    {
        _font!.DrawText(sb, "队伍管理", new Vector2(100, 30), PixelTheme.Gold);
        _fontSmall!.DrawText(sb, "[Esc] 返回", new Vector2(100, 60), PixelTheme.PrimaryText);

        if (_recruitment == null) return;

        var y = 100;
        foreach (var charData in _recruitment.Party)
        {
             var vm = new CharacterPanelViewModel(charData);
             _fontSmall.DrawText(sb, $"{vm.Name} - {vm.Race} {vm.Level}级", new Vector2(120, y), PixelTheme.PrimaryText);
             _fontSmall.DrawText(sb, $"HP:{vm.FormatHP()} AC:{vm.AC}", new Vector2(400, y), PixelTheme.SecondaryText);
             _fontSmall.DrawText(sb, vm.FormatAbility(Ability.Str), new Vector2(550, y), PixelTheme.SecondaryText);
             y += 40;
        }

        if (_recruitment.Party.Count == 0)
        {
            _fontSmall.DrawText(sb, "队伍为空，请先招募队员", new Vector2(120, y + 20), PixelTheme.HintText);
        }
    }
}
