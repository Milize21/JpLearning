using Nomax.SharedKernel.Events;

namespace Nomax.Modules.Insight.Contracts;

// Integration events published by Insight (docs/05 §8).

/// <summary>A season was closed and its successor opened (BR-SEASON-02); the hub tells the boards.</summary>
public sealed record SeasonClosed(Guid SeasonId, Guid LevelId, Guid NewSeasonId, DateTimeOffset ClosedAt) : IIntegrationEvent
{
    public static string EventType => "SeasonClosed.v1";
}
