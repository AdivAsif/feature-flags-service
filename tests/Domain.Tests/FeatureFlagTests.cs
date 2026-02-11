using FluentAssertions;

namespace Domain.Tests;

public class FeatureFlagTests
{
    [Fact]
    public void FeatureFlag_DefaultValues_ShouldBeInitialized()
    {
        // Act
        var featureFlag = new FeatureFlag();

        // Assert
        featureFlag.Version.Should().Be(1);
        featureFlag.Key.Should().BeEmpty();
        featureFlag.Description.Should().BeEmpty();
        featureFlag.Enabled.Should().BeFalse();
        featureFlag.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void FeatureFlag_SetProperties_ShouldRetainValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var key = "test-feature";
        var description = "Test feature description";
        var parameters = new[]
        {
            new FeatureFlagParameters
            {
                RuleType = RuleType.Percentage,
                RuleValue = "50"
            }
        };

        // Act
        var featureFlag = new FeatureFlag
        {
            Id = id,
            Key = key,
            Description = description,
            Enabled = true,
            Version = 2,
            Parameters = parameters
        };

        // Assert
        featureFlag.Id.Should().Be(id);
        featureFlag.Key.Should().Be(key);
        featureFlag.Description.Should().Be(description);
        featureFlag.Enabled.Should().BeTrue();
        featureFlag.Version.Should().Be(2);
        featureFlag.Parameters.Should().HaveCount(1);
        featureFlag.Parameters[0].RuleType.Should().Be(RuleType.Percentage);
        featureFlag.Parameters[0].RuleValue.Should().Be("50");
    }

    [Theory]
    [InlineData(RuleType.Percentage)]
    [InlineData(RuleType.Group)]
    [InlineData(RuleType.User)]
    public void RuleType_AllValues_ShouldBeValid(RuleType ruleType)
    {
        // Arrange
        var parameter = new FeatureFlagParameters
        {
            RuleType = ruleType,
            RuleValue = "test-value"
        };

        // Assert
        parameter.RuleType.Should().Be(ruleType);
    }

    [Fact]
    public void FeatureFlagParameters_DefaultValues_ShouldBeInitialized()
    {
        // Act
        var parameter = new FeatureFlagParameters();

        // Assert
        parameter.RuleType.Should().Be(RuleType.Percentage);
        parameter.RuleValue.Should().BeEmpty();
    }
}