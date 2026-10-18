using System.Text;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Tests.Domain;

public sealed class IcsAndTemplatesTests
{
    private static readonly Guid SessionId = Guid.Parse("0192f1e2-7c1a-7b3e-9a40-5d2c8e1f0a11");
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static IcsEvent Event(bool cancelled = false, string topic = "Bab 3 – Partikel を", string? note = "Siapkan buku bab 3") =>
        new(SessionId, 2, new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 21, 13, 30, 0, TimeSpan.Zero),
            "N5 Sabtu Pagi", topic, "Zoom", "https://zoom.us/j/123", note, cancelled);

    [Fact]
    public void BR_JADWAL_03_event_matches_the_documented_example()
    {
        // Assert on the unfolded text: long lines are folded (CRLF + space) as RFC 5545 requires.
        var ics = IcsCalendar.Write([Event()], Now, "NomaX JP Learn").Replace("\r\n ", string.Empty, StringComparison.Ordinal);

        ics.ShouldContain("BEGIN:VCALENDAR\r\n");
        ics.ShouldContain($"UID:{SessionId}@nomax\r\n");
        ics.ShouldContain("SEQUENCE:2\r\n");
        ics.ShouldContain("DTSTAMP:20261018T120000Z\r\n");
        ics.ShouldContain("DTSTART:20261021T120000Z\r\n");
        ics.ShouldContain("DTEND:20261021T133000Z\r\n");
        ics.ShouldContain("SUMMARY:[N5 Sabtu Pagi] Bab 3 – Partikel を\r\n");
        ics.ShouldContain("DESCRIPTION:Platform: Zoom\\nLink: https://zoom.us/j/123\\nCatatan: Siapkan buku bab 3\r\n");
        ics.ShouldContain("URL:https://zoom.us/j/123\r\n");
        ics.ShouldContain("STATUS:CONFIRMED\r\n");
        ics.ShouldEndWith("END:VCALENDAR\r\n");
    }

    [Fact]
    public void BR_JADWAL_03_cancelled_session_is_marked_cancelled()
    {
        IcsCalendar.Write([Event(cancelled: true)], Now, "NomaX").ShouldContain("STATUS:CANCELLED\r\n");
    }

    [Fact]
    public void BR_JADWAL_03_text_is_escaped_and_long_lines_are_folded_at_75_octets()
    {
        var ics = IcsCalendar.Write([Event(topic: "Kosakata; angka, waktu \\ " + new string('あ', 40), note: null)], Now, "NomaX");

        ics.ShouldContain("Kosakata\\; angka\\, waktu \\\\ ");
        foreach (var line in ics.Split("\r\n"))
        {
            Encoding.UTF8.GetByteCount(line).ShouldBeLessThanOrEqualTo(75);
        }

        // Unfolding (CRLF + space) restores the summary.
        ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal).ShouldContain(new string('あ', 40));
    }

    [Fact]
    public void BR_JADWAL_07_times_use_the_recipient_zone_and_its_abbreviation()
    {
        var start = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var end = start.AddMinutes(90);

        NotificationTemplates.FormatRange(start, end, "Asia/Jakarta", english: false).ShouldBe("Rabu, 21 Oktober 2026 19.00–20.30 WIB");
        NotificationTemplates.FormatRange(start, end, "Asia/Makassar", english: true).ShouldBe("Wednesday 21 October 2026, 20:00–21:30 WITA");
        NotificationTemplates.Abbreviation("Asia/Jayapura", start).ShouldBe("WIT");
        NotificationTemplates.Abbreviation("Asia/Tokyo", start).ShouldBe("UTC+09:00");
    }

    [Fact]
    public void BR_JADWAL_07_messages_are_in_the_recipient_language()
    {
        var data = new SessionMessageData("Kelas Demo", "Baris ka", Now.AddDays(1), Now.AddDays(1).AddHours(1), "zoom", "https://zoom.us/j/1", null, "http://localhost:5173/murid/jadwal");

        var indonesian = NotificationTemplates.Render(NotificationTypes.SessionReminder1h, "id", "Asia/Jakarta", data);
        var english = NotificationTemplates.Render(NotificationTypes.SessionReminder1h, "en", "Asia/Jakarta", data);

        indonesian.Subject.ShouldBe("[NomaX] Satu jam lagi: Kelas Demo");
        english.Subject.ShouldBe("[NomaX] In one hour: Kelas Demo");
        indonesian.Text.ShouldContain("Link: https://zoom.us/j/1");
        english.Html.ShouldContain("Open schedule");
    }

    [Fact]
    public void BR_JADWAL_05_update_message_lists_what_changed()
    {
        var previous = new SessionSnapshot("Baris ka", Now.AddDays(1), Now.AddDays(1).AddHours(1), null);
        var data = new SessionMessageData("Kelas Demo", "Baris sa", Now.AddDays(2), Now.AddDays(2).AddHours(1), "discord", null, previous, "http://x");

        var message = NotificationTemplates.Render(NotificationTypes.SessionUpdated, "id", "Asia/Jakarta", data);

        message.Text.ShouldContain("Topik: Baris ka → Baris sa");
        message.Text.ShouldContain("Waktu: Senin, 19 Oktober 2026 19.00–20.00 WIB → Selasa, 20 Oktober 2026 19.00–20.00 WIB");
    }
}
