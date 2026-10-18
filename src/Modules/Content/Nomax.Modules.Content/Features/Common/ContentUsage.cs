using Nomax.Modules.Content.Contracts;

namespace Nomax.Modules.Content.Features.Common;

/// <summary>Asks every module that stores content references (BR-KONTEN-03; docs/02 §6.2 <see cref="IContentUsageProbe"/>).</summary>
internal sealed class ContentUsage(IEnumerable<IContentUsageProbe> probes)
{
    public async Task<bool> IsUsedElsewhereAsync(ContentUsageTarget target, Guid id, CancellationToken cancellationToken)
    {
        foreach (var probe in probes)
        {
            if (await probe.IsUsedAsync(target, id, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IReadOnlySet<Guid>> FilesUsedElsewhereAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken)
    {
        var used = new HashSet<Guid>();
        foreach (var probe in probes)
        {
            used.UnionWith(await probe.GetUsedFileIdsAsync(fileIds, cancellationToken));
        }

        return used;
    }
}
