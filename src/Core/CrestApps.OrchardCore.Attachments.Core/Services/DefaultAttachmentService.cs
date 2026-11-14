using CrestApps.Core;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Attachments.Core.Services;

/// <summary>
/// The default <see cref="IAttachmentService"/>: the upload policy of <see cref="AttachmentOptions"/>, the registered
/// <see cref="IAttachmentScanner"/>s, the records in <see cref="AttachmentStore"/> and the bytes in
/// <see cref="IAttachmentFileStore"/>.
/// </summary>
public sealed class DefaultAttachmentService : IAttachmentService
{
    /// <summary>
    /// The purpose of the signed download links.
    /// </summary>
    public const string SignedLinkPurpose = "CrestApps.Attachments.Download";

    private readonly AttachmentStore _store;
    private readonly IAttachmentFileStore _files;
    private readonly IEnumerable<IAttachmentScanner> _scanners;
    private readonly IOptions<AttachmentOptions> _options;
    private readonly SignedTokenService _signedTokens;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAttachmentService"/> class.
    /// </summary>
    /// <param name="store">The attachment records.</param>
    /// <param name="files">The file store.</param>
    /// <param name="scanners">The file scanners.</param>
    /// <param name="options">The upload policy.</param>
    /// <param name="signedTokens">The signed token service, for download links.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the tenant's path base in links.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultAttachmentService(
        AttachmentStore store,
        IAttachmentFileStore files,
        IEnumerable<IAttachmentScanner> scanners,
        IOptions<AttachmentOptions> options,
        SignedTokenService signedTokens,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<DefaultAttachmentService> stringLocalizer)
    {
        _store = store;
        _files = files;
        _scanners = scanners;
        _options = options;
        _signedTokens = signedTokens;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<AttachmentResult> AddAsync(IReadOnlyList<AttachmentUpload> uploads, CancellationToken cancellationToken = default)
    {
        var files = uploads?.Where(upload => upload is not null && !upload.Content.IsEmpty).ToList() ?? [];

        if (files.Count == 0)
        {
            return new AttachmentResult();
        }

        var options = _options.Value;
        var formats = options.GetAllowedFormats();

        if (files.Count > options.MaxFilesPerUpload)
        {
            return Refuse(S["Attach at most {0} files at a time.", options.MaxFilesPerUpload]);
        }

        // Every file is checked before any is stored, so a refusal never leaves the ones before it behind.
        var accepted = new List<(Attachment Attachment, AttachmentUpload Upload)>();

        foreach (var upload in files)
        {
            var fileName = Path.GetFileName(upload.FileName ?? string.Empty);

            if (upload.Owner.IsEmpty)
            {
                return Refuse(S["Say which record the file is attached to."]);
            }

            if (upload.Content.Length > options.MaxFileBytes)
            {
                return Refuse(S["{0} is too large. The limit is {1} MB.", fileName, Math.Max(1, options.MaxFileBytes / (1024 * 1024))]);
            }

            var format = FileFormats.Detect(upload.Content.Span, fileName, upload.DeclaredContentType, formats);

            if (format is null)
            {
                return Refuse(S["{0} can't be attached. Accepted: {1}.", fileName, string.Join(", ", formats.Select(item => item.Name))]);
            }

            foreach (var scanner in _scanners)
            {
                if (await scanner.ScanAsync(upload, format.ContentType, cancellationToken) is { Length: > 0 } reason)
                {
                    return Refuse(S["{0} was refused: {1}", fileName, reason]);
                }
            }

            accepted.Add((new Attachment
            {
                ItemId = UniqueId.GenerateId(),
                Owner = upload.Owner,
                FileName = string.IsNullOrWhiteSpace(fileName) ? "file" + format.PreferredExtension : fileName,
                ContentType = format.ContentType,
                Length = upload.Content.Length,
                IsImage = format.IsImage,
                UploadedBy = upload.UploadedBy,
                UploadedUtc = _clock.UtcNow,
            }, upload));
        }

        var written = new List<string>();

        try
        {
            foreach (var (attachment, upload) in accepted)
            {
                await _files.WriteAsync(attachment.ItemId, upload.Content, cancellationToken);
                written.Add(attachment.ItemId);
                await _store.CreateAsync(attachment, cancellationToken);
            }
        }
        catch
        {
            // A write that fails part way leaves no file behind: the records roll back with the request.
            foreach (var attachmentId in written)
            {
                await _files.DeleteAsync(attachmentId, CancellationToken.None);
            }

            throw;
        }

        return new AttachmentResult { Attachments = [.. accepted.Select(item => item.Attachment)] };
    }

    /// <inheritdoc/>
    public async Task<Attachment> FindAsync(string attachmentId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(attachmentId) ? null : await _store.FindByIdAsync(attachmentId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Attachment>> GetAsync(IEnumerable<string> attachmentIds, CancellationToken cancellationToken = default)
    {
        var ids = attachmentIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList() ?? [];

        if (ids.Count == 0)
        {
            return [];
        }

        var found = (await _store.GetAsync(ids, cancellationToken)).ToDictionary(attachment => attachment.ItemId, StringComparer.Ordinal);

        return [.. ids.Where(found.ContainsKey).Select(id => found[id])];
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Attachment>> ListAsync(RecordRef owner, CancellationToken cancellationToken = default)
        => _store.ListByOwnerAsync(owner, cancellationToken);

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return _files.ReadAsync(attachment.ItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await FindAsync(attachmentId, cancellationToken);

        if (attachment is null)
        {
            return false;
        }

        await _store.DeleteAsync(attachment, cancellationToken);
        await _files.DeleteAsync(attachment.ItemId, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public string CreateSignedPath(string attachmentId, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentId);

        // The tenant's path base is part of the link, so it reaches this tenant from an e-mail or a page alike: the
        // request's when there is one, else the tenant's URL prefix (such as for an e-mail sent in the background).
        var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value;

        var prefix = ShellScope.Context?.Settings?.RequestUrlPrefix;

        if (string.IsNullOrEmpty(pathBase) && !string.IsNullOrEmpty(prefix))
        {
            pathBase = "/" + prefix.Trim('/');
        }

        return $"{pathBase}/attachments/{attachmentId}?token={Uri.EscapeDataString(_signedTokens.Protect(SignedLinkPurpose, attachmentId, lifetime))}";
    }

    private static AttachmentResult Refuse(string error)
        => new() { Errors = [error] };
}
