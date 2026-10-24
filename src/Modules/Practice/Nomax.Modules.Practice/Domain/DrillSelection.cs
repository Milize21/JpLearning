namespace Nomax.Modules.Practice.Domain;

/// <summary>Settings of docs/04 §12 (<c>Practice:Drill:*</c>).</summary>
public sealed record DrillSettings(int BatchSize, int IdleMinutes, int WeakFocusMinCandidates)
{
    public static DrillSettings Default { get; } = new(10, 30, 5);
}

/// <summary>A pool question in weak-focus mode with the mastery of its item.</summary>
public sealed record WeakCandidate(Guid QuestionId, decimal Mastery, DateTimeOffset LastSeenAt);

/// <summary>Result of picking a batch: <see cref="FellBack"/> when weak focus had too few candidates (BR-DRILL-04).</summary>
public sealed record DrillBatch(IReadOnlyList<Guid> QuestionIds, bool FellBack);

/// <summary>Question order of a drilling session (BR-DRILL-03, BR-DRILL-04) as pure functions.</summary>
public static class DrillSelection
{
    /// <summary>
    /// BR-DRILL-03: a random order without repeats until the pool is used up, then a new shuffle. The order of cycle
    /// <c>k</c> is a permutation seeded by the session id and <c>k</c>, so no state beyond the answer count is needed:
    /// <paramref name="cursor"/> is the number of answers in the session.
    /// </summary>
    public static IReadOnlyList<Guid> NormalBatch(IReadOnlyList<Guid> pool, Guid sessionId, int cursor, int count)
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (pool.Count == 0 || count <= 0)
        {
            return [];
        }

        var ordered = pool.Distinct().Order().ToList();
        var batch = new List<Guid>(Math.Min(count, ordered.Count));
        var position = Math.Max(cursor, 0);
        while (batch.Count < count && batch.Count < ordered.Count)
        {
            var cycle = position / ordered.Count;
            var permutation = Permutation(ordered, sessionId, cycle);
            var next = permutation[position % ordered.Count];
            // Across a cycle boundary the new shuffle may start with an item of this batch; skip it.
            if (!batch.Contains(next))
            {
                batch.Add(next);
            }

            position++;
        }

        return batch;
    }

    /// <summary>
    /// BR-DRILL-04 weight: <c>(1 − mastery) + 0.1</c>, plus <c>0.2</c> when the item was last seen more than 7 days ago.
    /// </summary>
    public static double Weight(WeakCandidate candidate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var weight = (double)(1m - candidate.Mastery) + 0.1;
        return now - candidate.LastSeenAt > TimeSpan.FromDays(7) ? weight + 0.2 : weight;
    }

    /// <summary>BR-DRILL-04: weighted random pick without replacement within a batch.</summary>
    public static IReadOnlyList<Guid> WeakFocusBatch(IReadOnlyList<WeakCandidate> candidates, int count, DateTimeOffset now, Random random)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(random);
        var remaining = candidates.DistinctBy(candidate => candidate.QuestionId).Select(candidate => (candidate.QuestionId, Weight: Weight(candidate, now))).ToList();
        var batch = new List<Guid>(Math.Min(count, remaining.Count));
        while (batch.Count < count && remaining.Count > 0)
        {
            var total = remaining.Sum(item => item.Weight);
            var target = random.NextDouble() * total;
            var index = 0;
            for (var cumulative = remaining[0].Weight; cumulative < target && index < remaining.Count - 1; cumulative += remaining[index].Weight)
            {
                index++;
            }

            batch.Add(remaining[index].QuestionId);
            remaining.RemoveAt(index);
        }

        return batch;
    }

    private static List<Guid> Permutation(List<Guid> ordered, Guid sessionId, int cycle)
    {
        var random = new Random(SafeQuestions.StableSeed(sessionId, new Guid(cycle, 0, 0, new byte[8])));
        var permutation = new List<Guid>(ordered);
        for (var i = permutation.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        return permutation;
    }
}
