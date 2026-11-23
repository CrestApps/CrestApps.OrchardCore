using System.Collections.Concurrent;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Keeps attachments in memory, so a test can see which files were stored and removed.
/// </summary>
internal sealed class SurveyFilesFakeAttachmentService : IAttachmentService
{
    private int _next;

    public ConcurrentDictionary<string, Attachment> Stored { get; } = new(StringComparer.Ordinal);

    public int AddCalls { get; private set; }

    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether uploads are refused, as a scanner or a type check would.
    /// </summary>
    public bool Refuse { get; set; }

    public Task<AttachmentResult> AddAsync(IReadOnlyList<AttachmentUpload> uploads, CancellationToken cancellationToken = default)
    {
        AddCalls++;

        if (Refuse)
        {
            return Task.FromResult(new AttachmentResult { Errors = ["The file was refused."] });
        }

        var added = uploads.Select(upload => new Attachment
        {
            ItemId = "att" + Interlocked.Increment(ref _next),
            Owner = upload.Owner,
            FileName = upload.FileName,
            ContentType = upload.DeclaredContentType,
            Length = upload.Content.Length,
            UploadedUtc = Clock(),
        }).ToList();

        foreach (var attachment in added)
        {
            Stored[attachment.ItemId] = attachment;
        }

        return Task.FromResult(new AttachmentResult { Attachments = added });
    }

    public Attachment Seed(RecordRef owner, DateTime uploadedUtc)
    {
        var attachment = new Attachment { ItemId = "seed" + Interlocked.Increment(ref _next), Owner = owner, FileName = "f.txt", UploadedUtc = uploadedUtc };
        Stored[attachment.ItemId] = attachment;

        return attachment;
    }

    public Task<Attachment> FindAsync(string attachmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(Stored.GetValueOrDefault(attachmentId));

    public Task<IReadOnlyList<Attachment>> GetAsync(IEnumerable<string> attachmentIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attachment>>([.. attachmentIds.Where(Stored.ContainsKey).Select(id => Stored[id])]);

    public Task<IReadOnlyList<Attachment>> ListAsync(RecordRef owner, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attachment>>([.. Stored.Values.Where(attachment => attachment.Owner == owner).OrderByDescending(attachment => attachment.UploadedUtc)]);

    public Task<IReadOnlyList<Attachment>> ListUploadedBeforeAsync(IReadOnlyCollection<string> ownerKinds, DateTime uploadedBeforeUtc, DateTime? uploadedFromUtc, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attachment>>([.. Stored.Values
            .Where(attachment => ownerKinds.Contains(attachment.Owner.Kind) && attachment.UploadedUtc < uploadedBeforeUtc && (uploadedFromUtc is null || attachment.UploadedUtc >= uploadedFromUtc))
            .OrderBy(attachment => attachment.UploadedUtc)
            .Take(take)]);

    public Task<byte[]> ReadAsync(Attachment attachment, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]>([]);

    public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(Stored.TryRemove(attachmentId, out _));

    public string CreateSignedPath(string attachmentId, TimeSpan lifetime)
        => "/attachments/" + attachmentId;
}
