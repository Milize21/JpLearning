namespace Nomax.SharedKernel.Events;

/// <summary>
/// An integration event between modules, delivered through the outbox (docs/02 §6.3, catalog in docs/05 §8).
/// The records live in the producer's Contracts project so consumers can read them without a module reference.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Versioned name stored in <c>outbox_messages.type</c>, e.g. <c>ChapterUnlocked.v1</c>.</summary>
    static abstract string EventType { get; }
}
