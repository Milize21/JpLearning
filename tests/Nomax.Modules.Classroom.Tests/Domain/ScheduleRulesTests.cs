using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Tests.Domain;

public sealed class ScheduleRulesTests
{
    // WIB = UTC+7. Monday 2026-10-19 … Thursday 2026-10-22.
    private static readonly TimeSpan Wib = TimeSpan.FromHours(7);

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, Wib);

    private static IReadOnlyList<(string Type, DateTimeOffset SendAt)> Plan(DateTimeOffset startsAt, DateTimeOffset now) =>
        [.. ScheduleRules.PlanReminders(startsAt, now).Select(reminder => (reminder.Type, reminder.SendAt))];

    [Fact]
    public void BR_JADWAL_06_case1_monday_for_wednesday_19_gives_tuesday_19_and_wednesday_18()
    {
        Plan(At(21, 19), now: At(19, 10)).ShouldBe(
        [
            (NotificationTypes.SessionReminder1d, At(20, 19)),
            (NotificationTypes.SessionReminder1h, At(21, 18)),
        ]);
    }

    [Fact]
    public void BR_JADWAL_06_case2_wednesday_8_for_wednesday_19_gives_only_the_one_hour_reminder()
    {
        Plan(At(21, 19), now: At(21, 8)).ShouldBe([(NotificationTypes.SessionReminder1h, At(21, 18))]);
    }

    [Fact]
    public void BR_JADWAL_06_case3_wednesday_18_30_for_wednesday_19_gives_no_reminder()
    {
        Plan(At(21, 19), now: At(21, 18, 30)).ShouldBeEmpty();
    }

    [Fact]
    public void BR_JADWAL_06_case4_moving_to_thursday_19_on_monday_plans_wednesday_19_and_thursday_18()
    {
        // The old reminders are cancelled by the planner; the new plan only depends on the new start.
        Plan(At(22, 19), now: At(19, 12)).ShouldBe(
        [
            (NotificationTypes.SessionReminder1d, At(21, 19)),
            (NotificationTypes.SessionReminder1h, At(22, 18)),
        ]);
    }

    [Fact]
    public void BR_JADWAL_06_case5_reminders_of_a_changed_session_get_new_dedupe_keys()
    {
        // Case 5 (cancel) and case 4 cancel pending reminders; new ones must not collide with the cancelled rows.
        var session = Guid.Parse("01929000-0000-7000-8000-000000000001");
        var user = Guid.Parse("a0000000-0000-4000-8000-000000000003");

        var before = ScheduleRules.DedupeKey(session, NotificationTypes.SessionReminder1d, 0, user, NotificationChannel.Email);
        var after = ScheduleRules.DedupeKey(session, NotificationTypes.SessionReminder1d, 1, user, NotificationChannel.Email);

        before.ShouldNotBe(after);
        before.ShouldStartWith($"session:{session}:session_reminder_1d:");
        ScheduleRules.DedupeKey(session, NotificationTypes.SessionCreated, 3, user, NotificationChannel.WebPush)
            .ShouldBe($"session:{session}:session_created:{user}:web_push");
    }

    [Fact]
    public void BR_JADWAL_06_offsets_come_from_configuration()
    {
        Plan(At(21, 19), now: At(19, 10)).Count.ShouldBe(2);
        ScheduleRules.PlanReminders(At(21, 19), At(19, 10), [TimeSpan.FromHours(2)])
            .ShouldBe([new PlannedReminder(NotificationTypes.SessionReminder1d, At(21, 17))]);
    }

    [Fact]
    public void BR_JADWAL_01_end_must_be_after_start()
    {
        ScheduleRules.Validate(At(21, 19), At(21, 19), null, ClassStatus.Active)!.Value.Field.ShouldBe("endsAt");
        ScheduleRules.Validate(At(21, 19), At(21, 20), null, ClassStatus.Active).ShouldBeNull();
    }

    [Fact]
    public void BR_JADWAL_01_a_session_lasts_at_most_8_hours()
    {
        ScheduleRules.Validate(At(21, 9), At(21, 17), null, ClassStatus.Active).ShouldBeNull();
        ScheduleRules.Validate(At(21, 9), At(21, 17, 1), null, ClassStatus.Active)!.Value.Field.ShouldBe("endsAt");
    }

    [Theory]
    [InlineData("https://zoom.us/j/123", true)]
    [InlineData("http://zoom.us/j/123", false)]
    [InlineData("zoom.us/j/123", false)]
    public void BR_JADWAL_01_meeting_link_is_https_only(string url, bool valid)
    {
        (ScheduleRules.Validate(At(21, 19), At(21, 20), url, ClassStatus.Active) is null).ShouldBe(valid);
    }

    [Theory]
    [InlineData(ClassStatus.Draft)]
    [InlineData(ClassStatus.Archived)]
    public void BR_JADWAL_01_only_active_classes_get_sessions(ClassStatus status)
    {
        ScheduleRules.Validate(At(21, 19), At(21, 20), null, status)!.Value.Field.ShouldBe("classId");
    }
}
