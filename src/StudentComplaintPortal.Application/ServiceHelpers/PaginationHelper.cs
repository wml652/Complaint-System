using StudentComplaintPortal.Application.DTOs;

namespace StudentComplaintPortal.Application.ServiceHelper;

public static class PaginationHelper
{
    public static PagedResult<T> PaginateByPage<T>(IEnumerable<T> source, int pageNumber, int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var totalCount = source.Count();

        var items = source
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public static CursorResult<T> PaginateByCursorTimestamp<T>(
    IEnumerable<T> source,
    Func<T, DateTime> timestampSelector,
    string? cursor,
    int pageSize,
    bool moveForward = true)
    {
        if (pageSize < 1) pageSize = 10;

        var ordered = source.OrderByDescending(timestampSelector).ToList();

        IEnumerable<T> filtered = ordered;

        var decodedCursor = DecodeCursor(cursor);
        if (!string.IsNullOrEmpty(decodedCursor) && DateTime.TryParse(decodedCursor, out var cursorTimestamp))
        {
            filtered = moveForward
                ? ordered.Where(x => timestampSelector(x) < cursorTimestamp)
                : ordered.Where(x => timestampSelector(x) > cursorTimestamp);
        }

        var items = filtered.Take(pageSize + 1).ToList();
        var hasMore = items.Count > pageSize;
        if (hasMore) items = items.Take(pageSize).ToList();

        string? nextCursor = hasMore ? EncodeCursor(timestampSelector(items.Last()).ToString("o")) : null;
        string? previousCursor = items.Count > 0 ? EncodeCursor(timestampSelector(items.First()).ToString("o")) : null;

        return new CursorResult<T>
        {
            Items = items,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            HasMore = hasMore,
            PageSize = pageSize
        };
    }

    public static CursorResult<T> PaginateByCursorId<T>(
    IEnumerable<T> source,
    Func<T, int> idSelector,
    string? cursor,
    int pageSize,
    bool moveForward = true)
    {
        if (pageSize < 1) pageSize = 10;

        var ordered = source.OrderByDescending(idSelector).ToList();

        IEnumerable<T> filtered = ordered;

        var decodedCursor = DecodeCursor(cursor);
        if (!string.IsNullOrEmpty(decodedCursor) && int.TryParse(decodedCursor, out var cursorId))
        {
            filtered = moveForward
                ? ordered.Where(x => idSelector(x) < cursorId)
                : ordered.Where(x => idSelector(x) > cursorId);
        }

        var items = filtered.Take(pageSize + 1).ToList();
        var hasMore = items.Count > pageSize;
        if (hasMore) items = items.Take(pageSize).ToList();

        string? nextCursor = hasMore ? EncodeCursor(idSelector(items.Last()).ToString()) : null;
        string? previousCursor = items.Count > 0 ? EncodeCursor(idSelector(items.First()).ToString()) : null;

        return new CursorResult<T>
        {
            Items = items,
            NextCursor = nextCursor,
            PreviousCursor = previousCursor,
            HasMore = hasMore,
            PageSize = pageSize
        };
    }

    private static string EncodeCursor(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes);
    }

    private static string? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null; // invalid/tampered cursor, treat as no cursor
        }
    }

    public static int? DecodeIdCursor(string? cursor)
    {
        var decoded = DecodeCursor(cursor);
        if (!string.IsNullOrEmpty(decoded) && int.TryParse(decoded, out var id))
        {
            return id;
        }
        return null;
    }

    public static string EncodeIdCursor(int id)
    {
        return EncodeCursor(id.ToString());
    }

    public static DateTime? DecodeTimestampCursor(string? cursor)
    {
        var decoded = DecodeCursor(cursor);
        if (!string.IsNullOrEmpty(decoded) && DateTime.TryParse(decoded, out var timestamp))
        {
            return timestamp;
        }
        return null;
    }

    public static string EncodeTimestampCursor(DateTime timestamp)
    {
        return EncodeCursor(timestamp.ToString("o"));
    }
}