using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Keeps the files of responses with Attachments: stores the files a page posted forward once it passes validation,
/// removes the files an update released (an unticked file, a replaced signature) and every file of a deleted response.
/// Without Attachments nothing is stored and nothing is removed.
/// </summary>
public sealed class SurveyResponseFiles
{
    private readonly IAttachmentService _attachments;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseFiles"/> class.
    /// </summary>
    /// <param name="attachments">The attachment service, when Attachments is on.</param>
    /// <param name="logger">The logger.</param>
    public SurveyResponseFiles(IEnumerable<IAttachmentService> attachments, ILogger<SurveyResponseFiles> logger)
    {
        _attachments = attachments.FirstOrDefault();
        _logger = logger;
    }

    /// <summary>
    /// Gets the record that owns the files of a response.
    /// </summary>
    /// <param name="responseId">The response's id.</param>
    /// <returns>The record.</returns>
    public static RecordRef OwnerOf(string responseId)
        => new(SurveysConstants.ResponseRecordKind, responseId);

    /// <summary>
    /// Gets the ids of the files the answers of a response refer to, pending files left out.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The ids.</returns>
    public static HashSet<string> ReferencedIds(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Answers.Values
            .SelectMany(answer => answer?.AttachmentIds ?? [])
            .Where(id => !string.IsNullOrEmpty(id) && !IsPending(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets whether an attachment id is the temporary id of a file not stored yet.
    /// </summary>
    /// <param name="attachmentId">The id.</param>
    /// <returns>Whether it is pending.</returns>
    public static bool IsPending(string attachmentId)
        => attachmentId?.StartsWith(SurveyPendingFile.IdPrefix, StringComparison.Ordinal) == true;

    /// <summary>
    /// Drops the pending files from the answers of a response, for a page that was refused; an answer left with no
    /// file is removed.
    /// </summary>
    /// <param name="response">The response.</param>
    public static void DiscardPending(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        foreach (var (questionId, answer) in response.Answers.ToList())
        {
            if (answer?.AttachmentIds is not { Count: > 0 } ids || ids.RemoveAll(IsPending) == 0)
            {
                continue;
            }

            if (ids.Count == 0)
            {
                response.Answers.Remove(questionId);
            }
        }
    }

    /// <summary>
    /// Stores the pending files of a page that passed validation and puts the stored files' ids in place of their
    /// temporary ids. The files of each question are stored together; when Attachments refuses one, none of that
    /// question's files are stored, its answer drops them and the refusal is returned for the question.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="pending">The pending files by question id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refusals by question id; empty when every file was stored.</returns>
    public async Task<Dictionary<string, List<string>>> StorePendingAsync(SurveyResponse response, IReadOnlyDictionary<string, List<SurveyPendingFile>> pending, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (questionId, files) in pending ?? new Dictionary<string, List<SurveyPendingFile>>())
        {
            if (files is not { Count: > 0 } || !response.Answers.TryGetValue(questionId, out var answer) || answer?.AttachmentIds is not { Count: > 0 } ids)
            {
                continue;
            }

            // Only the files the answer still refers to are stored: one dropped since it was read is not.
            var kept = files.Where(file => ids.Contains(file.Id, StringComparer.Ordinal)).ToList();

            if (kept.Count == 0)
            {
                continue;
            }

            if (_attachments is null)
            {
                ids.RemoveAll(IsPending);
            }
            else
            {
                var uploads = kept.Select(file => new AttachmentUpload
                {
                    Owner = OwnerOf(response.ItemId),
                    FileName = file.FileName,
                    DeclaredContentType = file.ContentType,
                    Content = file.Content,
                    UploadedBy = response.Respondent ?? default,
                }).ToList();

                var result = await _attachments.AddAsync(uploads, cancellationToken);

                if (result.Succeeded && result.Attachments.Count == kept.Count)
                {
                    for (var i = 0; i < kept.Count; i++)
                    {
                        var index = ids.IndexOf(kept[i].Id);

                        if (index >= 0)
                        {
                            ids[index] = result.Attachments[i].ItemId;
                        }
                    }
                }
                else
                {
                    errors[questionId] = [.. result.Errors];
                }

                ids.RemoveAll(IsPending);
            }

            if (ids.Count == 0)
            {
                response.Answers.Remove(questionId);
            }
        }

        return errors;
    }

    /// <summary>
    /// Removes the files a response referred to before an update that it no longer refers to, such as an unticked file
    /// or a replaced signature. Call it after the update is committed, so a failed update never loses a file.
    /// </summary>
    /// <param name="before">The ids the response referred to before the update (<see cref="ReferencedIds"/>).</param>
    /// <param name="after">The response as committed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many files were removed.</returns>
    public async Task<int> DeleteReleasedAsync(IReadOnlySet<string> before, SurveyResponse after, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);

        if (_attachments is null || before is not { Count: > 0 })
        {
            return 0;
        }

        var current = ReferencedIds(after);
        var removed = 0;

        foreach (var attachmentId in before.Where(id => !current.Contains(id)))
        {
            removed += await DeleteAsync(attachmentId, cancellationToken) ? 1 : 0;
        }

        return removed;
    }

    /// <summary>
    /// Removes every file of a response, those its answers no longer refer to included.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many files were removed.</returns>
    public async Task<int> DeleteAllAsync(SurveyResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (_attachments is null || string.IsNullOrEmpty(response.ItemId))
        {
            return 0;
        }

        var ids = ReferencedIds(response);

        foreach (var attachment in await _attachments.ListAsync(OwnerOf(response.ItemId), cancellationToken))
        {
            ids.Add(attachment.ItemId);
        }

        var removed = 0;

        foreach (var attachmentId in ids)
        {
            removed += await DeleteAsync(attachmentId, cancellationToken) ? 1 : 0;
        }

        return removed;
    }

    private async Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            return await _attachments.DeleteAsync(attachmentId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The orphan sweep removes a file that could not be removed now.
            _logger.LogWarning(exception, "The survey response file {AttachmentId} could not be removed.", attachmentId);

            return false;
        }
    }
}
