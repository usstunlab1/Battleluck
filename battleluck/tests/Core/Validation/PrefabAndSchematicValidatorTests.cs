using BattleLuck.Core.Validation;
using BattleLuck.Models;
using FluentAssertions;
using Xunit;

namespace BattleLuck.Tests.Core.Validation;

public class PrefabValidatorTests
{
    [Fact]
    public void Validate_ReturnsIssues_WhenPrefabGuidAndNameAreMissing()
    {
        // Arrange
        var config = new ModeConfig
        {
            Prefabs = new[] { new PrefabConfig { Name = "BrokenPrefab", Guid = 0, PrefabName = "" } }
        };

        // Act
        var issues = PrefabValidator.Validate("test_mode", config);

        // Assert
        issues.Should().Contain(i => i.Contains("Prefab 'BrokenPrefab' is missing both guid and prefabName"));
    }

    [Fact]
    public void Validate_ReturnsIssues_WhenZoneReferenceIsZero()
    {
        // Arrange
        var config = new ModeConfig
        {
            Zones = new ZonesConfig
            {
                Zones = new List<ZoneDefinition>
                {
                    new ZoneDefinition
                    {
                        Name = "TestZone",
                        Boundary = new BoundaryConfig
                        {
                            Walls = new WallBoundaryConfig
                            {
                                Enabled = true,
                                SpawnWalls = true,
                                WallPrefab = "0"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var issues = PrefabValidator.Validate("test_mode", config);

        // Assert
        issues.Should().Contain(i => i.Contains("Invalid zone 'TestZone' wall prefab '0'"));
    }
}

public class SchematicValidatorTests
{
    [Fact]
    public void Validate_ReturnsIssues_WhenSchematicHasNoStructures()
    {
        // Arrange
        var config = new ModeConfig
        {
            Schematics = new[] { new SchematicConfig { EventName = "EmptySchematic", Structures = new List<SchematicStructure>() } }
        };

        // Act
        var issues = SchematicValidator.Validate("test_mode", config);

        // Assert
        issues.Should().Contain(i => i.Contains("Schematic 'EmptySchematic' has no structures"));
    }

    [Fact]
    public void Validate_ReturnsIssues_WhenArenaSchematicMissingPositions()
    {
        // Arrange
        var config = new ModeConfig
        {
            Schematics = new[] 
            { 
                new SchematicConfig 
                { 
                    EventName = "ArenaSchematic", 
                    Structures = new List<SchematicStructure> { new SchematicStructure { Prefab = "Wall" } },
                    ChestPositions = new List<Vec3Config>(), // Missing
                    CornerPositions = new List<Vec3Config>() // Missing
                } 
            }
        };

        // Act
        var issues = SchematicValidator.Validate("arena_mode", config);

        // Assert
        issues.Should().Contain(i => i.Contains("Arena schematic 'ArenaSchematic' is missing 'chestPositions'"));
        issues.Should().Contain(i => i.Contains("Arena schematic 'ArenaSchematic' is missing 'cornerPositions'"));
    }

    [Fact]
    public void Validate_ReturnsIssues_WhenStructureMissingPrefab()
    {
        // Arrange
        var config = new ModeConfig
        {
            Schematics = new[] 
            { 
                new SchematicConfig 
                { 
                    EventName = "TestSchematic", 
                    Structures = new List<SchematicStructure> { new SchematicStructure { Prefab = "", PrefabGuid = null } }
                } 
            }
        };

        // Act
        var issues = SchematicValidator.Validate("test_mode", config);

        // Assert
        issues.Should().Contain(i => i.Contains("has structure missing prefab and prefabGuid"));
    }
}
