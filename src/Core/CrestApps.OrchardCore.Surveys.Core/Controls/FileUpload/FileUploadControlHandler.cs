using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;

/// <summary>
/// File uploads, kept by Attachments with the response as owner. The input is <c>q.{questionId}</c> (files); files
/// uploaded before are kept when their ids are posted back as <c>q.{questionId}.keep</c>.
/// </summary>
public sealed class FileUploadControlHandler : SurveyControlHandlerBase<FileUploadSettings>
{
    private const int DefaultMaxMegabytes = 10;
    private const int MaxFilesLimit = 20;

    private readonly IAttachmentService _attachmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadControlHandler"/> class.
    /// </summary>
    /// <param name="attachmentService">The attachment service.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public FileUploadControlHandler(
        IAttachmentService attachmentService,
        IStringLocalizer<FileUploadControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
        _attachmentService = attachmentService;
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

        if (context.Form?.Files is { Count: > 0 } files && context.Response is not null)
        {
            var maxBytes = (long)(settings.MaxMegabytes is > 0 ? settings.MaxMegabytes.Value : DefaultMaxMegabytes) * 1024 * 1024;
            var uploads = new List<AttachmentUpload>();

            foreach (var file in files.Where(file => string.Equals(file.Name, context.Prefix, StringComparison.Ordinal) && file.Length > 0))
            {
                if (ids.Count + uploads.Count >= maxFiles)
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

                uploads.Add(new AttachmentUpload
                {
                    Owner = new RecordRef(SurveysConstants.ResponseRecordKind, context.Response.ItemId),
                    FileName = Path.GetFileName(file.FileName),
                    DeclaredContentType = file.ContentType,
                    Content = stream.ToArray(),
                    UploadedBy = context.Response.Respondent ?? default,
                });
            }

            if (uploads.Count > 0)
            {
                var result = await _attachmentService.AddAsync(uploads, context.CancellationToken);

                context.Errors.AddRange(result.Errors);
                ids.AddRange(result.Attachments.Select(attachment => attachment.ItemId));
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
