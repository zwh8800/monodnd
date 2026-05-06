using DndGame.Systems.Character;

namespace DndGame.UI.Widgets;

/// <summary>
/// 招募管理器，管理可用角色池和队伍槽位。
/// </summary>
public class RecruitmentManager
{
    private readonly List<CharacterData> _availableCharacters = new();
    private readonly List<CharacterData> _party = new();
    public const int MaxPartySize = 4;

    public IReadOnlyList<CharacterData> AvailableCharacters => _availableCharacters;
    public IReadOnlyList<CharacterData> Party => _party;

    /// <summary>
    /// 加载可用角色池。
    /// </summary>
    public void LoadCharacters(IEnumerable<CharacterData> characters)
    {
        _availableCharacters.Clear();
        _availableCharacters.AddRange(characters);
    }

    /// <summary>
    /// 招募角色加入队伍。队伍满时返回 false。
    /// </summary>
    public bool Recruit(string characterId)
    {
        if (_party.Count >= MaxPartySize)
            return false;

        var character = _availableCharacters.FirstOrDefault(c => c.CharacterId == characterId);
        if (character == null)
            return false;

        _availableCharacters.Remove(character);
        _party.Add(character);
        return true;
    }

    /// <summary>
    /// 从队伍中移除角色。
    /// </summary>
    public bool Dismiss(string characterId)
    {
        var character = _party.FirstOrDefault(c => c.CharacterId == characterId);
        if (character == null)
            return false;

        _party.Remove(character);
        _availableCharacters.Add(character);
        return true;
    }

    /// <summary>
    /// 检查队伍是否已满。
    /// </summary>
    public bool IsPartyFull => _party.Count >= MaxPartySize;
}
