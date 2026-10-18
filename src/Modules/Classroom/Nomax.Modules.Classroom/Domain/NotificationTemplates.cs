using System.Globalization;
using System.Net;
using System.Text;

namespace Nomax.Modules.Classroom.Domain;

/// <summary>What a template needs about the session, already resolved for one recipient.</summary>
public sealed record SessionMessageData(
    string ClassName,
    string Topic,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Platform,
    string? MeetingUrl,
    SessionSnapshot? Previous,
    string ScheduleUrl);

public sealed record RenderedMessage(string Subject, string Text, string Html, string PushTitle, string PushBody);

/// <summary>
/// Email and push texts of docs/05 §10 in the recipient's UI language (id/en, without Japanese companion; docs/06 §10),
/// with times in the recipient's time zone and its abbreviation (BR-JADWAL-07, e.g. WIB).
/// </summary>
public static class NotificationTemplates
{
    public static RenderedMessage Render(string type, string uiLocale, string timeZone, SessionMessageData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var english = uiLocale == "en";
        var when = FormatRange(data.StartsAt, data.EndsAt, timeZone, english);
        var platform = PlatformName(data.Platform);
        var lines = new List<string>();
        string headline;

        switch (type)
        {
            case NotificationTypes.SessionCreated:
                headline = english ? $"New session: {data.ClassName}" : $"Pertemuan baru: {data.ClassName}";
                lines.Add(english ? $"Topic: {data.Topic}" : $"Topik: {data.Topic}");
                lines.Add(english ? $"When: {when}" : $"Waktu: {when}");
                lines.Add($"Platform: {platform}");
                break;
            case NotificationTypes.SessionUpdated:
                headline = english ? $"Session changed: {data.ClassName}" : $"Pertemuan diubah: {data.ClassName}";
                if (data.Previous is { } previous)
                {
                    if (previous.Topic != data.Topic)
                    {
                        lines.Add(english ? $"Topic: {previous.Topic} → {data.Topic}" : $"Topik: {previous.Topic} → {data.Topic}");
                    }

                    if (previous.StartsAt != data.StartsAt || previous.EndsAt != data.EndsAt)
                    {
                        var before = FormatRange(previous.StartsAt, previous.EndsAt, timeZone, english);
                        lines.Add(english ? $"When: {before} → {when}" : $"Waktu: {before} → {when}");
                    }

                    if (previous.MeetingUrl != data.MeetingUrl)
                    {
                        lines.Add(english ? "The meeting link changed." : "Link pertemuan berubah.");
                    }
                }

                lines.Add(english ? $"Topic: {data.Topic}" : $"Topik: {data.Topic}");
                lines.Add(english ? $"When: {when}" : $"Waktu: {when}");
                break;
            case NotificationTypes.SessionCancelled:
                headline = english ? $"Session cancelled: {data.ClassName}" : $"Pertemuan dibatalkan: {data.ClassName}";
                lines.Add(english ? $"Topic: {data.Topic}" : $"Topik: {data.Topic}");
                lines.Add(english ? $"Was planned for: {when}" : $"Semula: {when}");
                break;
            case NotificationTypes.SessionReminder1d:
                headline = english ? $"Tomorrow: {data.ClassName}" : $"Besok: {data.ClassName}";
                lines.Add(english ? $"Topic: {data.Topic}" : $"Topik: {data.Topic}");
                lines.Add(english ? $"When: {when}" : $"Waktu: {when}");
                lines.Add($"Platform: {platform}");
                break;
            case NotificationTypes.SessionReminder1h:
                headline = english ? $"In one hour: {data.ClassName}" : $"Satu jam lagi: {data.ClassName}";
                lines.Add(english ? $"Topic: {data.Topic}" : $"Topik: {data.Topic}");
                lines.Add(english ? $"When: {when}" : $"Waktu: {when}");
                lines.Add($"Platform: {platform}");
                if (data.MeetingUrl is not null)
                {
                    lines.Add($"Link: {data.MeetingUrl}");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type.");
        }

        var footer = english ? $"Schedule: {data.ScheduleUrl}" : $"Jadwal: {data.ScheduleUrl}";
        var text = new StringBuilder().AppendLine(headline).AppendLine();
        lines.ForEach(line => text.AppendLine(line));
        text.AppendLine().AppendLine(footer);

        return new RenderedMessage(
            Subject: $"[NomaX] {headline}",
            Text: text.ToString(),
            Html: Html(headline, lines, data.ScheduleUrl, english),
            PushTitle: headline,
            PushBody: string.Join(" · ", lines.Take(2)));
    }

    /// <summary>"Rabu, 21 Oktober 2026 19.00–20.30 WIB" / "Wednesday 21 October 2026, 19:00–20:30 WIB".</summary>
    public static string FormatRange(DateTimeOffset startsAt, DateTimeOffset endsAt, string timeZone, bool english)
    {
        var zone = FindZone(timeZone);
        var start = TimeZoneInfo.ConvertTime(startsAt, zone);
        var end = TimeZoneInfo.ConvertTime(endsAt, zone);
        var culture = english ? CultureInfo.GetCultureInfo("en-GB") : CultureInfo.GetCultureInfo("id-ID");
        var date = start.ToString(english ? "dddd d MMMM yyyy" : "dddd, d MMMM yyyy", culture);
        var time = english ? "HH:mm" : "HH.mm";
        var range = start.Date == end.Date
            ? $"{start.ToString(time, culture)}–{end.ToString(time, culture)}"
            : $"{start.ToString(time, culture)} – {end.ToString(english ? "d MMM, HH:mm" : "d MMM, HH.mm", culture)}";
        return english ? $"{date}, {range} {Abbreviation(timeZone, startsAt)}" : $"{date} {range} {Abbreviation(timeZone, startsAt)}";
    }

    /// <summary>Indonesian zones by their usual names; elsewhere the UTC offset.</summary>
    public static string Abbreviation(string timeZone, DateTimeOffset instant) => timeZone switch
    {
        "Asia/Jakarta" or "Asia/Pontianak" => "WIB",
        "Asia/Makassar" => "WITA",
        "Asia/Jayapura" => "WIT",
        _ => Offset(FindZone(timeZone).GetUtcOffset(instant)),
    };

    public static TimeZoneInfo FindZone(string timeZone) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    private static string Offset(TimeSpan offset) =>
        offset == TimeSpan.Zero ? "UTC" : $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";

    private static string PlatformName(string platform) => platform switch
    {
        "zoom" => "Zoom",
        "discord" => "Discord",
        _ => platform == "other" ? "Online" : platform,
    };

    private static string Html(string headline, IEnumerable<string> lines, string scheduleUrl, bool english)
    {
        // Brand colours from design/tokens.css (aka-900 brand line, aka-700 action, sumi-900 text); emails need inline styles.
        var body = new StringBuilder();
        body.Append("<!doctype html><html><body style=\"margin:0;background:#ffffff;font-family:'Plus Jakarta Sans',Arial,sans-serif;color:#1a1a1a\">");
        body.Append("<div style=\"border-top:4px solid #780000;max-width:560px;margin:0 auto;padding:24px\">");
        body.Append("<p style=\"font-weight:700;margin:0 0 4px\">NomaX JP Learn</p>");
        body.Append("<h1 style=\"font-size:20px;margin:16px 0\">").Append(WebUtility.HtmlEncode(headline)).Append("</h1>");
        foreach (var line in lines)
        {
            body.Append("<p style=\"margin:4px 0\">").Append(WebUtility.HtmlEncode(line)).Append("</p>");
        }

        body.Append("<p style=\"margin:24px 0\"><a href=\"").Append(WebUtility.HtmlEncode(scheduleUrl))
            .Append("\" style=\"background:#b9050e;color:#ffffff;padding:10px 16px;border-radius:6px;text-decoration:none;font-weight:600\">")
            .Append(english ? "Open schedule" : "Buka jadwal")
            .Append("</a></p></div></body></html>");
        return body.ToString();
    }
}
