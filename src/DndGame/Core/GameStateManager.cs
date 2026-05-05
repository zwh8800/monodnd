namespace DndGame.Core;

/// <summary>
/// 游戏状态管理器接口，定义场景切换和场景间数据传输的契约。
/// 负责跟踪当前场景标识，并提供场景过渡时跨场景传递数据的上下文机制。
/// </summary>
public interface IGameStateManager
{
    /// <summary>
    /// 获取当前所在的场景标识。
    /// </summary>
    SceneId CurrentScene { get; }

    /// <summary>
    /// 获取或设置场景过渡上下文对象。
    /// 用于在场景切换时将数据从一个场景传递到下一个场景。
    /// </summary>
    object? TransitionContext { get; set; }

    /// <summary>
    /// 设置当前场景标识，触发场景切换。
    /// 场景管理系统应据此执行实际的场景加载和过渡流程。
    /// </summary>
    /// <param name="sceneId">要切换到的目标场景标识。</param>
    void SetCurrentScene(SceneId sceneId);
}

/// <summary>
/// 游戏场景标识枚举，定义游戏中所有可切换的场景。
/// 每个值对应一个独立的游戏场景，涵盖菜单导航、核心玩法、战斗与对话等完整流程。
/// </summary>
public enum SceneId
{
    /// <summary>
    /// 主菜单场景，游戏启动时的初始界面。
    /// 包含开始游戏、加载存档、设置等选项。
    /// </summary>
    MainMenu,

    /// <summary>
    /// 酒馆场景，玩家招募冒险者、接受任务、休息补给的核心社交场所。
    /// </summary>
    Tavern,

    /// <summary>
    /// 冒险地图场景，显示世界地图和行进路线的宏观界面。
    /// 玩家在此规划路线、遭遇随机事件。
    /// </summary>
    AdventureMap,

    /// <summary>
    /// 战斗场景，进入回合制战斗时的专用场景。
    /// 管理战斗网格、角色行动序列和伤害结算。
    /// </summary>
    Combat,

    /// <summary>
    /// 加载场景，在场景切换时显示加载进度和提示信息。
    /// 作为过渡场景使用，资源加载完成后自动跳转至目标场景。
    /// </summary>
    Loading,

    /// <summary>
    /// 据点场景，管理玩家定居点和资源的界面。
    /// 包含建筑升级、资源收集、NPC 管理等据点经营功能。
    /// </summary>
    Settlement,

    /// <summary>
    /// 对话场景，显示 NPC 对话和事件选择的界面。
    /// 支持分支对话树和基于玩家选择的剧情走向。
    /// </summary>
    Dialogue
}

/// <summary>
/// 游戏状态管理器的默认实现，维护当前场景标识和过渡上下文。
/// 场景状态通过 CurrentScene 属性追踪。TransitionContext 用于在
/// 场景切换时传递数据，实现场景间的松耦合通信。
/// </summary>
public class GameStateManager : IGameStateManager
{
    /// <summary>
    /// 获取当前场景标识。默认初始化为 <see cref="SceneId.MainMenu"/>。
    /// setter 为 private，确保场景切换必须通过 <see cref="SetCurrentScene"/> 方法进行，
    /// 保持状态变更的可追踪性。
    /// </summary>
    public SceneId CurrentScene { get; private set; } = SceneId.MainMenu;

    /// <summary>
    /// 获取或设置场景过渡上下文对象。
    /// TransitionContext 用于场景间传递数据，典型使用场景：
    /// 例如从冒险地图进入战斗时传递遭遇配置和敌人信息，
    /// 或从酒馆进入冒险地图时传递队伍组成和任务列表。
    /// 使用方法：源场景在切换场景前设置此属性，目标场景在激活后读取并消费数据。
    /// 该属性为 object? 类型，调用方负责类型转换。
    /// </summary>
    public object? TransitionContext { get; set; }

    /// <summary>
    /// 设置当前场景标识为指定的场景。
    /// 场景管理系统应监听此变更（如通过事件总线）并据此执行实际的场景加载和过渡流程。
    /// 若需要传递数据，调用前应先设置 <see cref="TransitionContext"/>。
    /// </summary>
    /// <param name="sceneId">要切换到的目标场景标识。</param>
    public void SetCurrentScene(SceneId sceneId)
    {
        CurrentScene = sceneId;
    }
}
