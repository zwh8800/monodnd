using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DndGame.Core;
using DndGame.Systems.Combat;
using DndGame.Systems.Character;

namespace DndGame.Scenes;

/// <summary>
/// 战斗场景，管理回合制战斗流程、UI 渲染和玩家输入。
/// </summary>
public class CombatScene : Scene
{
    private CombatFSM? _fsm;
    private ActionResolver? _resolver;
    private ConditionSystem? _conditions;
    private AISystem? _ai;
    private Texture2D? _pixelTexture;
    private readonly List<CombatLogEntry> _log = new();

    private record CombatLogEntry(string Message);

    /// <summary>
    /// 初始化战斗场景。
    /// </summary>
    public override void Initialize()
    {
        _fsm = new CombatFSM();
        _resolver = new ActionResolver();
        _conditions = new ConditionSystem();
        _ai = new AISystem();

        // 创建单像素纹理
        _pixelTexture = new Texture2D(Game.GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // 注册 FSM 转换
        SetupFSM();

        base.Initialize();
    }

    /// <summary>
    /// 绘制战斗 UI：回合指示器、HP 条、战斗日志。
    /// </summary>
    public override void Draw(GameTime gameTime)
    {
        if (_pixelTexture == null || _fsm == null) return;

        var sb = Game.SpriteBatch;
        sb.Begin(samplerState: SamplerState.PointClamp);

        // 绘制背景
        sb.Draw(_pixelTexture, new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            new Color(20, 20, 40));

        // 绘制状态指示
        DrawText(sb, $"战斗状态: {_fsm.CurrentState}", 10, 10, Color.White);

        // 绘制战斗日志
        for (int i = 0; i < Math.Min(_log.Count, 10); i++)
        {
            DrawText(sb, _log[^(i + 1)].Message, 10, 40 + i * 20, Color.LightGray);
        }

        sb.End();

        base.Draw(gameTime);
    }

    public override void End()
    {
        _pixelTexture?.Dispose();
        base.End();
    }

    private void SetupFSM()
    {
        if (_fsm == null) return;

        // 注册基本转换路径
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

    private static void DrawText(SpriteBatch sb, string text, int x, int y, Color color)
    {
        // 简单文本绘制 — 使用单像素模拟（后续集成 FontStashSharp）
        // 当前仅占位，实际需要字体支持
    }
}
