using System.ComponentModel.DataAnnotations;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Infrastructure.Notifications;

/// <summary>Configuration section <c>Schedule</c> with the defaults of docs/04 §12.</summary>
public sealed class ScheduleOptions
{
    public const string SectionName = "Schedule";

    /// <summary>BR-JADWAL-06: the first offset is the 1-day reminder, the second the 1-hour reminder.</summary>
    public List<TimeSpan> ReminderOffsets { get; set; } = [.. ScheduleRules.DefaultReminderOffsets];

    /// <summary>BR-JADWAL-03 feed window.</summary>
    [Range(0, 3650)]
    public int IcsPastDays { get; set; } = 30;

    [Range(1, 3650)]
    public int IcsFutureDays { get; set; } = 180;
}

/// <summary>Configuration section <c>App</c>: the public address users open (links in emails, push and the ICS URL).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required]
    [Url]
    public string PublicUrl { get; set; } = "http://localhost:5173";
}

/// <summary>Configuration section <c>Email</c> (docs/02 §14). Development sends to Mailpit.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = true;

    public string SmtpHost { get; set; } = "localhost";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 1025;

    public bool UseSsl { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = "NomaX JP Learn <no-reply@nomax.test>";
}

/// <summary>Configuration section <c>WebPush</c>: VAPID keys (base64url, P-256). Without them web push is off.</summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>Uncompressed public key (65 bytes), also given to browsers as applicationServerKey.</summary>
    public string? PublicKey { get; set; }

    /// <summary>Private scalar (32 bytes). A secret: user-secrets or environment only.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>Contact for push services, e.g. <c>mailto:admin@example.org</c>.</summary>
    public string Subject { get; set; } = "mailto:admin@nomax.test";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
