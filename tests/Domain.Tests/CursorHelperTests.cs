using System.Text;
using FluentAssertions;
using SharedKernel;

namespace Domain.Tests;

public class CursorHelperTests
{
    [Fact]
    public void EncodeCursor_ShouldReturnBase64EncodedString()
    {
        // Arrange
        var id = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var createdAt = new DateTimeOffset(2026, 1, 19, 0, 0, 0, TimeSpan.Zero);

        // Act
        var cursor = CursorHelper.EncodeCursor(id, createdAt);

        // Assert
        cursor.Should().NotBeNullOrEmpty();
        cursor.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$"); // Valid Base64
    }

    [Fact]
    public void DecodeCursor_ShouldReturnOriginalIdAndTimestamp()
    {
        // Arrange
        var originalId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        var originalCreatedAt = new DateTimeOffset(2026, 1, 19, 0, 0, 0, TimeSpan.Zero);
        var cursor = CursorHelper.EncodeCursor(originalId, originalCreatedAt);

        // Act
        var (decodedId, decodedCreatedAt) = CursorHelper.DecodeCursor(cursor);

        // Assert
        decodedId.Should().Be(originalId);
        decodedCreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public void TryDecodeCursor_WithValidCursor_ShouldReturnTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var cursor = CursorHelper.EncodeCursor(id, createdAt);

        // Act
        var result = CursorHelper.TryDecodeCursor(cursor, out var decodedId, out var decodedCreatedAt);

        // Assert
        result.Should().BeTrue();
        decodedId.Should().Be(id);
        decodedCreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void TryDecodeCursor_WithNullCursor_ShouldReturnFalse()
    {
        // Act
        var result = CursorHelper.TryDecodeCursor(null, out var id, out var createdAt);

        // Assert
        result.Should().BeFalse();
        id.Should().Be(Guid.Empty);
        createdAt.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public void TryDecodeCursor_WithInvalidCursor_ShouldReturnFalse()
    {
        // Act
        var result = CursorHelper.TryDecodeCursor("invalid-cursor", out var id, out var createdAt);

        // Assert
        result.Should().BeFalse();
        id.Should().Be(Guid.Empty);
        createdAt.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public void DecodeCursor_WithInvalidFormat_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid"));

        // Act & Assert
        var act = () => CursorHelper.DecodeCursor(invalidCursor);
        act.Should().Throw<ArgumentException>();
    }
}