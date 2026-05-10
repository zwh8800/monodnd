namespace DndGame.Core;

/// <summary>
/// 音频管理器接口，管理游戏中背景音乐和音效的播放、停止与音量控制。
/// </summary>
public interface IAudioManager
{
    /// <summary>
    /// 播放指定名称的背景音乐（单曲循环）。
    /// 若已有 BGM 在播放，则淡入淡出切换。
    /// </summary>
    /// <param name="name">BGM 资源名称（不含路径与扩展名）。</param>
    /// <param name="volume">播放音量（0.0 ~ 1.0）。</param>
    void PlayBGM(string name, float volume = 1.0f);

    /// <summary>
    /// 停止当前背景音乐的播放。
    /// </summary>
    /// <param name="fadeDurationMs">淡出持续时间（毫秒），0 为即刻停止。</param>
    void StopBGM(int fadeDurationMs = 500);

    /// <summary>
    /// 播放指定名称的音效（一次性，不循环）。
    /// </summary>
    /// <param name="name">SFX 资源名称（不含路径与扩展名）。</param>
    /// <param name="volume">播放音量（0.0 ~ 1.0）。</param>
    void PlaySFX(string name, float volume = 1.0f);

    /// <summary>
    /// 设置全局主音量。
    /// </summary>
    /// <param name="volume">主音量值（0.0 ~ 1.0）。</param>
    void SetMasterVolume(float volume);

    /// <summary>
    /// 获取当前全局主音量。
    /// </summary>
    float GetMasterVolume();
}
