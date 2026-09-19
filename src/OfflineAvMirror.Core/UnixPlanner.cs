namespace OfflineAvMirror.Core;

/// <summary>
/// Plans a Unix-line mirror: <c>&lt;line dir&gt;/version2.lst</c> plus every file it lists.
/// </summary>
/// <remarks>
/// The result is the layout the vendor's own mirror tool produces, so the
/// protected machine takes it with <c>drweb-ctl update --from &lt;folder&gt;</c>.
/// </remarks>
public static class UnixPlanner
{
    public static async Task<MirrorPlan> PlanAsync(MirrorClient client, Zone zone, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(zone);

        var (baseUrl, raw) = await MirrorPlan.FetchFirstAsync(client, zone, UnixIndex.FileName, ct).ConfigureAwait(false);
        var index = UnixIndex.Parse(raw);

        // All files come from the mirror that served the index: mixing mirrors
        // within one set could combine two different releases.
        var files = index.Entries
            .Select(e => new PlannedFile(
                MirrorPlan.CheckRelativePath(e.Name), MirrorPlan.Join(baseUrl, e.Name), e.Sha256, e.Size))
            .ToList();

        return new MirrorPlan
        {
            RootDirectory = MirrorPlan.CheckRelativePath(zone.DirName),
            Indexes = [(UnixIndex.FileName, raw)],
            Files = files,
            Timestamp = index.Timestamp,
        };
    }
}
