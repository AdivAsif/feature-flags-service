using Application.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Web.Api.Extensions;

namespace Web.Api.Tests;

public class ETagExtensionsTests
{
    [Fact]
    public void GenerateETag_WithFeatureFlag_ShouldReturnValidETag()
    {
        // Arrange
        var featureFlag = new FeatureFlagDto
        {
            Id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            Version = 1,
            Key = "test-feature",
            Description = "Test",
            Enabled = true
        };

        // Act
        var etag = featureFlag.GenerateETag();

        // Assert
        etag.Should().NotBeNullOrEmpty();
        etag.Should().StartWith("\"");
        etag.Should().EndWith("\"");
        etag.Length.Should().BeGreaterThan(2); // More than just quotes
    }

    [Fact]
    public void GenerateETag_SameFeatureFlagVersions_ShouldProduceSameETag()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flag1 = new FeatureFlagDto { Id = id, Version = 1 };
        var flag2 = new FeatureFlagDto { Id = id, Version = 1 };

        // Act
        var etag1 = flag1.GenerateETag();
        var etag2 = flag2.GenerateETag();

        // Assert
        etag1.Should().Be(etag2);
    }

    [Fact]
    public void GenerateETag_DifferentVersions_ShouldProduceDifferentETags()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flag1 = new FeatureFlagDto { Id = id, Version = 1 };
        var flag2 = new FeatureFlagDto { Id = id, Version = 2 };

        // Act
        var etag1 = flag1.GenerateETag();
        var etag2 = flag2.GenerateETag();

        // Assert
        etag1.Should().NotBe(etag2);
    }

    [Fact]
    public void ValidateETag_WithMatchingETag_ShouldReturnTrue()
    {
        // Arrange
        var featureFlag = new FeatureFlagDto
        {
            Id = Guid.NewGuid(),
            Version = 1
        };
        var expectedETag = featureFlag.GenerateETag();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-Match"] = expectedETag;

        // Act
        var result = httpContext.Request.ValidateETag(expectedETag);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateETag_WithMismatchedETag_ShouldReturnFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-Match"] = "\"different-etag\"";

        // Act
        var result = httpContext.Request.ValidateETag("\"expected-etag\"");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateETag_WithoutIfMatchHeader_ShouldReturnFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = httpContext.Request.ValidateETag("\"some-etag\"");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasIfMatchHeader_WithHeader_ShouldReturnTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-Match"] = "\"some-etag\"";

        // Act
        var result = httpContext.Request.HasIfMatchHeader();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasIfMatchHeader_WithoutHeader_ShouldReturnFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = httpContext.Request.HasIfMatchHeader();

        // Assert
        result.Should().BeFalse();
    }
}