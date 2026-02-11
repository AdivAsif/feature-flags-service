using System.Text;

namespace Infrastructure;

public static class CursorHelper
{
    public static string EncodeCursor(Guid id, DateTimeOffset createdAt)
    {
        var cursorData = $"{id}|{createdAt:O}";
        var bytes = Encoding.UTF8.GetBytes(cursorData);
        return Convert.ToBase64String(bytes);
    }

    public static (Guid Id, DateTimeOffset CreatedAt) DecodeCursor(string cursor)
    {
        var bytes = Convert.FromBase64String(cursor);
        var cursorData = Encoding.UTF8.GetString(bytes);
        var parts = cursorData.Split('|');

        return parts.Length != 2
            ? throw new ArgumentException("Invalid cursor format")
            : (Guid.Parse(parts[0]), DateTimeOffset.Parse(parts[1]));
    }

    public static bool TryDecodeCursor(string? cursor, out Guid id, out DateTimeOffset createdAt)
    {
        id = Guid.Empty;
        createdAt = DateTimeOffset.MinValue;

        if (string.IsNullOrWhiteSpace(cursor))
            return false;

        try
        {
            (id, createdAt) = DecodeCursor(cursor);
            return true;
        }
        catch
        {
            return false;
        }
    }
}