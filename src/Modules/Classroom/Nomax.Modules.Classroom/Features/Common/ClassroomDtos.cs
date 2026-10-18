using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;

namespace Nomax.Modules.Classroom.Features.Common;

// Response shapes of /api/classroom (docs/05 §6). Other users appear with id and display name only (docs/01 §4).

public sealed record TeacherDto(Guid Id, string DisplayName, bool IsPrimary);

/// <param name="MyRole"><c>teacher</c> or <c>student</c> for the caller; null for admins who do not teach the class.</param>
public sealed record ClassDto(
    Guid Id,
    string Name,
    Guid LevelId,
    Guid BookId,
    ClassStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    int StudentCount,
    IReadOnlyList<TeacherDto> Teachers,
    string? MyRole);

/// <param name="Username">Shown to teachers so they can match accounts; never the email (docs/01 §4).</param>
public sealed record ClassStudentDto(Guid StudentId, string Username, string DisplayName, MemberStatus Status, DateTimeOffset JoinedAt, bool IsActive);

public sealed record ClassUnlockDto(Guid ChapterId, DateTimeOffset UnlockedAt, Guid UnlockedBy);
