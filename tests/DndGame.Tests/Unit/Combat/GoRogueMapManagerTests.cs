using Xunit;
using FluentAssertions;
using GoRogue;
using DndGame.Systems.Combat;

namespace DndGame.Tests.Unit.Combat;

public class GoRogueMapManagerTests
{
    [Fact]
    public void Constructor_SetsDimensions()
    {
        // Arrange & Act
        var map = new GoRogueMapManager(20, 15);

        // Assert
        map.Width.Should().Be(20);
        map.Height.Should().Be(15);
    }

    [Fact]
    public void SetTile_AndIsWalkable_ReturnsCorrectValue()
    {
        // Arrange
        var map = new GoRogueMapManager(10, 10);

        // Act
        map.SetTile(5, 5, TileType.Floor, walkable: true, transparent: true);
        map.SetTile(6, 6, TileType.Wall, walkable: false, transparent: false);

        // Assert
        map.IsWalkable(5, 5).Should().BeTrue();
        map.IsWalkable(6, 6).Should().BeFalse();
        map.GetTileType(5, 5).Should().Be(TileType.Floor);
        map.GetTileType(6, 6).Should().Be(TileType.Wall);
    }

    [Fact]
    public void CalculateFOV_ReturnsVisibleCoords()
    {
        // Arrange
        var map = new GoRogueMapManager(10, 10);

        // Act
        var visible = map.CalculateFOV(5, 5, 3).ToList();

        // Assert
        visible.Should().NotBeEmpty();
        visible.Should().Contain(c => c.X == 5 && c.Y == 5);
        visible.Should().Contain(c => c.X == 5 && c.Y == 6);
        map.IsVisible(5, 5).Should().BeTrue();
        map.IsVisible(0, 0).Should().BeFalse();
    }

    [Fact]
    public void CalculateFOV_WallsBlockVision()
    {
        // Arrange
        var map = new GoRogueMapManager(5, 5);
        map.SetTile(2, 1, TileType.Wall, walkable: false, transparent: false);

        // Act
        map.CalculateFOV(2, 3, 5);

        // Assert
        // Wall tile itself is visible (you see the wall), but tiles behind it are blocked
        map.IsVisible(2, 1).Should().BeTrue();
        map.IsVisible(2, 0).Should().BeFalse();
        map.IsVisible(1, 1).Should().BeTrue();
    }

    [Fact]
    public void IsWalkable_OutOfBounds_ReturnsFalse()
    {
        // Arrange
        var map = new GoRogueMapManager(10, 10);

        // Act & Assert
        map.IsWalkable(-1, 5).Should().BeFalse();
        map.IsWalkable(5, -1).Should().BeFalse();
        map.IsWalkable(10, 5).Should().BeFalse();
        map.IsWalkable(5, 10).Should().BeFalse();
    }
}
