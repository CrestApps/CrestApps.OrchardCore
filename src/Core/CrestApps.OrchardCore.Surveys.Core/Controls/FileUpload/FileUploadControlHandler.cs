using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// File uploads, kept by Attachments with the response as owner. The input is <c>q.{questionId}</c> (files); files
/// uploaded before are kept when their ids are posted back as <c>q.{questionId}.keep</c>. New files are read only in
/// <see cref="SurveyAnswerReadMode.Commit"/> mode and handed to the runtime as pending files, which it stores once the
/// page passes validation.
/// </summary>
public sealed class FileUploadControlHandler : SurveyControlHandlerBase<FileUploadSettings>
{
    private const int DefaultMaxMegabytes = 10;
    private const int MaxFilesLimit = 20;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public FileUploadControlHandler(IStringLocalizer<FileUploadControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.FileUpload;

    /// <inheritdoc />
    public override async Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<FileUploadSettings>();
        var maxFiles = Math.Clamp(settings.MaxFiles, 1, MaxFilesLimit);
        var existing = (context.Existing?.AttachmentIds ?? []).ToHashSet(StringComparer.Ordinal);
        var kept = ReadValues(context, context.Prefix + ".keep").Where(existing.Contains).Distinct(StringComparer.Ordinal).ToList();
        var ids = new List<string>(kept);

        // New files are only read when the page is posted forward, and are stored by the runtime once the page passes
        // validation: an evaluation, an autosave, a check or going back never keeps a file.
        if (context.Mode == SurveyAnswerReadMode.Commit && context.Form?.Files is { Count: > 0 } files && context.Response is not null)
        {
            var maxBytes = (long)(settings.MaxMegabytes is > 0 ? settings.MaxMegabytes.Value : DefaultMaxMegabytes) * 1024 * 1024;
            var added = 0;

            foreach (var file in files.Where(file => string.Equals(file.Name, context.Prefix, StringComparison.Ordinal) && file.Length > 0))
            {
                if (ids.Count + added >= maxFiles)
                {
                    context.Errors.Add(S["Upload at most {0} files.", maxFiles]);

                    break;
                }

                if (file.Length > maxBytes)
                {
                    context.Errors.Add(S["The file {0} is larger than {1} MB.", file.FileName, maxBytes / 1024 / 1024]);

                    continue;
                }

                if (!IsAllowed(file.FileName, settings))
                {
                    context.Errors.Add(S["The file {0} is not of an allowed type.", file.FileName]);

                    continue;
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, context.CancellationToken);

                var pending = new SurveyPendingFile
                {
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
                    Content = stream.ToArray(),
                };

                context.PendingFiles.Add(pending);
                ids.Add(pending.Id);
                added++;
            }
        }

        context.Answer = ids.Count == 0 ? null : new SurveyAnswer { AttachmentIds = ids };
    }

    /// <inheritdoc />
    public override bool IsEmpty(SurveyQuestion question, SurveyAnswer answer)
        => !(answer?.AttachmentIds?.Count > 0);

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Integer;

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => (long)(answer?.AttachmentIds?.Count ?? 0);

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        var count = answer?.AttachmentIds?.Count ?? 0;

        return count == 0 ? string.Empty : context?.UseValues == true ? string.Join(", ", answer.AttachmentIds) : S["Files: {0}", count];
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        if (answer?.AttachmentIds?.Count > 0)
        {
            yield return new SurveyAnswerIndexRow { NumberValue = answer.AttachmentIds.Count };
        }
    }

    private static bool IsAllowed(string fileName, FileUploadSettings settings)
    {
        if (settings.AllowedFormats is not { Count: > 0 })
        {
            return true;
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);

        return settings.AllowedFormats.Any(format => string.Equals(format?.Trim().TrimStart('.'), extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }
}
