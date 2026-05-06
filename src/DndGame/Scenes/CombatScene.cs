using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using DndGame.Core;
using DndGame.Systems.Combat;
using DndGame.Systems.Character;

namespace DndGame.Scenes;

/// <summary>
/// 战斗场景 —— 演示战斗系统（FSM + ActionResolver + AI）。
/// </summary>
public class CombatScene : Scene
{
    private CombatFSM? _fsm;
    private ActionResolver? _resolver;
    private ConditionSystem? _conditions;
    private AISystem? _ai;
    private Texture2D? _pixelTexture;
    private DynamicSpriteFont? _font;
    private DynamicSpriteFont? _fontSmall;
    private readonly List<string> _log = new();
    private KeyboardState _prevKeyState;

    private class DummyCombatant : ICombatant
    {
        public string CombatantId { get; set; } = "";
        public int ArmorClass { get; set; } = 10;
        public int MaxHp { get; set; } = 20;
        public int CurrentHp { get; set; } = 20;
        public int TempHp { get; set; }
        public int ProficiencyBonus { get; set; } = 2;
        public bool IsConcentrating { get; set; }
        private readonly Dictionary<Ability, int> _mods = new();
        public void SetMod(Ability a, int v) => _mods[a] = v;
        public int GetAbilityModifier(Ability a) => _mods.TryGetValue(a, out var v) ? v : 0;
        public ResistanceType GetResistance(DamageType t) => ResistanceType.Normal;
    }

    public override void Initialize()
    {
        _fsm = new CombatFSM();
        _resolver = new ActionResolver();
        _conditions = new ConditionSystem();
        _ai = new AISystem();

        _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        if (ServiceLocator.TryGet<IFontService>(out var fontService))
        {
            _font = fontService!.GetFont(20);
            _fontSmall = fontService.GetFont(14);
        }

        SetupFSM();
        _log.Add("战斗开始！按 Space 进行攻击");

        base.Initialize();
    }

    public override void Update(GameTime gameTime)
    {
        var state = Keyboard.GetState();

        if (state.IsKeyDown(Keys.Space) && _prevKeyState.IsKeyUp(Keys.Space))
        {
            SimulateCombatRound();
        }

        if (state.IsKeyDown(Keys.T) && _prevKeyState.IsKeyUp(Keys.T))
            GameRoot.Instance.StartSceneTransition(new TavernScene());

        if (state.IsKeyDown(Keys.A) && _prevKeyState.IsKeyUp(Keys.A))
            GameRoot.Instance.StartSceneTransition(new AdventureScene());

        _prevKeyState = state;
        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        if (_pixelTexture == null || _fsm == null || _font == null) return;

        var sb = Game.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        sb.Draw(_pixelTexture, new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            new Color(20, 20, 40));

        _font.DrawText(sb, "战斗演示", new Vector2(10, 10), new Color(255, 215, 0));
        _fontSmall!.DrawText(sb, $"状态: {_fsm.CurrentState}", new Vector2(10, 40), Color.White);
        _fontSmall.DrawText(sb, "Space 攻击 | A 冒险地图 | T 酒馆", new Vector2(10, 70), Color.LightGray);

        var logY = 100;
        var startIndex = Math.Max(0, _log.Count - 15);
        for (int i = startIndex; i < _log.Count; i++)
        {
            _fontSmall.DrawText(sb, _log[i], new Vector2(10, logY), Color.White);
            logY += 20;
        }

        sb.End();
        base.Draw(gameTime);
    }

    public override void End()
    {
        _pixelTexture?.Dispose();
        base.End();
    }

    private void SimulateCombatRound()
    {
        if (_fsm == null || _resolver == null) return;

        if (_fsm.CurrentState == CombatState.Initialization)
        {
            _fsm.Transition(CombatState.RollInitiative);
            _log.Add(">> 先攻检定完成");
            _fsm.Transition(CombatState.RoundStart);
            _log.Add(">> 回合开始");
            _fsm.Transition(CombatState.SimultaneousSelection);
            _fsm.Transition(CombatState.ActionPhase);
            _log.Add(">> 行动阶段");
            return;
        }

        var attacker = new DummyCombatant { CombatantId = "战士", ArmorClass = 15, CurrentHp = 20, MaxHp = 20 };
        attacker.SetMod(Ability.Str, 3);
        var target = new DummyCombatant { CombatantId = "哥布林", ArmorClass = 10, CurrentHp = 10, MaxHp = 10 };

        var weapon = new WeaponData { Name = "长剑", DamageDice = "1d8", DamageType = DamageType.Slashing, ScalingAbility = Ability.Str };
        var result = _resolver.ResolveAttack(attacker, target, weapon);

        foreach (var entry in result.LogEntries)
            _log.Add(entry);

        if (target.CurrentHp <= 0)
        {
            _log.Add(">> 哥布林被击败！");
            _fsm.Transition(CombatState.TurnEnd);
            _fsm.Transition(CombatState.RoundEnd);
            _fsm.Transition(CombatState.Victory);
            _log.Add(">> 战斗胜利！按 T 返回酒馆");
        }
    }

    private void SetupFSM()
    {
        if (_fsm == null) return;

        _fsm.RegisterGuard(CombatState.Initialization, CombatState.RollInitiative, () => true);
        _fsm.RegisterGuard(CombatState.RollInitiative, CombatState.RoundStart, () => true);
        _fsm.RegisterGuard(CombatState.RoundStart, CombatState.SimultaneousSelection, () => true);
        _fsm.RegisterGuard(CombatState.SimultaneousSelection, CombatState.ActionPhase, () => true);
        _fsm.RegisterGuard(CombatState.ActionPhase, CombatState.BonusActionPhase, () => true);
        _fsm.RegisterGuard(CombatState.ActionPhase, CombatState.TurnEnd, () => true);
        _fsm.RegisterGuard(CombatState.BonusActionPhase, CombatState.MovementPhase, () => true);
        _fsm.RegisterGuard(CombatState.BonusActionPhase, CombatState.TurnEnd, () => true);
        _fsm.RegisterGuard(CombatState.MovementPhase, CombatState.ReactionWindow, () => true);
        _fsm.RegisterGuard(CombatState.MovementPhase, CombatState.TurnEnd, () => true);
        _fsm.RegisterGuard(CombatState.ReactionWindow, CombatState.TurnEnd, () => true);
        _fsm.RegisterGuard(CombatState.TurnEnd, CombatState.RoundStart, () => true);
        _fsm.RegisterGuard(CombatState.TurnEnd, CombatState.RoundEnd, () => true);
        _fsm.RegisterGuard(CombatState.RoundEnd, CombatState.RollInitiative, () => true);
        _fsm.RegisterGuard(CombatState.RoundEnd, CombatState.Victory, () => true);
        _fsm.RegisterGuard(CombatState.RoundEnd, CombatState.Defeat, () => true);
    }
}
