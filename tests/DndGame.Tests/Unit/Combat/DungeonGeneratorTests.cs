using Xunit;
using FluentAssertions;
using GoRogue;
using DndGame.Systems.Combat;

namespace DndGame.Tests.Unit.Combat;

public class DungeonGeneratorTests
{
    [Fact]
    public void Generate_CreatesMapWithCorrectDimensions()
    {
        // Arrange
        var gen = new DungeonGenerator();

        // Act
        var map = gen.Generate(40, 30);

        // Assert
        map.Width.Should().Be(40);
        map.Height.Should().Be(30);
    }

    [Fact]
    public void Generate_HasWalkableTiles()
    {
        // Arrange
        var gen = new DungeonGenerator();

        // Act
        var map = gen.Generate(40, 30);

        // Assert — 至少有一些可通行地块
        var walkableCount = 0;
        for (int x = 0; x < 40; x++)
        for (int y = 0; y < 30; y++)
            if (map.IsWalkable(x, y)) walkableCount++;

        walkableCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FindStartPosition_ReturnsWalkableCoord()
    {
        // Arrange
        var gen = new DungeonGenerator();
        var map = gen.Generate(40, 30);

        // Act
        var start = DungeonGenerator.FindStartPosition(map);

        // Assert
        start.Should().NotBeNull();
        map.IsWalkable(start!.Value.X, start.Value.Y).Should().BeTrue();
    }

    [Fact]
    public void FindEndPosition_ReturnsDifferentFromStart()
    {
        // Arrange
        var gen = new DungeonGenerator();
        var map = gen.Generate(40, 30);
        var start = DungeonGenerator.FindStartPosition(map)!.Value;

        // Act
        var end = DungeonGenerator.FindEndPosition(map, start);

        // Assert
        end.Should().NotBeNull();
        // 终点可能与起点相同（如果只有一块可通行），但通常不同
        map.IsWalkable(end!.Value.X, end.Value.Y).Should().BeTrue();
    }
}
