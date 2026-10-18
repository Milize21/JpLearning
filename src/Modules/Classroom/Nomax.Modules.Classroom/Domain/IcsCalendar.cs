using System.Globalization;
using System.Text;

namespace Nomax.Modules.Classroom.Domain;

public sealed record IcsEvent(
    Guid SessionId,
    int Sequence,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string ClassName,
    string Topic,
    string Platform,
    string? MeetingUrl,
    string? Note,
    bool Cancelled);

/// <summary>
/// BR-JADWAL-03 feed (RFC 5545): <c>UID = {sessionId}@nomax</c>, <c>SEQUENCE</c> from the session, cancelled sessions with
/// <c>STATUS:CANCELLED</c>, <c>SUMMARY = [class] topic</c>, times in UTC. Lines end with CRLF and are folded at 75 octets.
/// </summary>
public static class IcsCalendar
{
    public static string Write(IEnumerable<IcsEvent> events, DateTimeOffset now, string calendarName)
    {
        var builder = new StringBuilder();
        Line(builder, "BEGIN:VCALENDAR");
        Line(builder, "VERSION:2.0");
        Line(builder, "PRODID:-//NomaX JP Learn//Jadwal//ID");
        Line(builder, "CALSCALE:GREGORIAN");
        Line(builder, "METHOD:PUBLISH");
        Line(builder, $"X-WR-CALNAME:{Escape(calendarName)}");
        foreach (var item in events ?? [])
        {
            Line(builder, "BEGIN:VEVENT");
            Line(builder, $"UID:{item.SessionId}@nomax");
            Line(builder, $"SEQUENCE:{item.Sequence.ToString(CultureInfo.InvariantCulture)}");
            Line(builder, $"DTSTAMP:{Utc(now)}");
            Line(builder, $"DTSTART:{Utc(item.StartsAt)}");
            Line(builder, $"DTEND:{Utc(item.EndsAt)}");
            Line(builder, $"SUMMARY:{Escape($"[{item.ClassName}] {item.Topic}")}");
            Line(builder, $"DESCRIPTION:{Escape(Description(item))}");
            if (item.MeetingUrl is not null)
            {
                Line(builder, $"URL:{item.MeetingUrl}");
            }

            Line(builder, item.Cancelled ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
            Line(builder, "END:VEVENT");
        }

        Line(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    public static string Utc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Description(IcsEvent item)
    {
        var lines = new List<string> { $"Platform: {item.Platform}" };
        if (item.MeetingUrl is not null)
        {
            lines.Add($"Link: {item.MeetingUrl}");
        }

        if (!string.IsNullOrWhiteSpace(item.Note))
        {
            lines.Add($"Catatan: {item.Note}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>TEXT escaping of RFC 5545 §3.3.11.</summary>
    public static string Escape(string text) =>
        (text ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Writes one content line, folded so no physical line exceeds 75 UTF-8 octets (RFC 5545 §3.1).</summary>
    private static void Line(StringBuilder builder, string line)
    {
        var limit = 75;
        var octets = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(line);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var size = Encoding.UTF8.GetByteCount(element);
            if (octets + size > limit)
            {
                builder.Append("\r\n ");
                octets = 1;
                limit = 75;
            }

            builder.Append(element);
            octets += size;
        }

        builder.Append("\r\n");
    }
}
