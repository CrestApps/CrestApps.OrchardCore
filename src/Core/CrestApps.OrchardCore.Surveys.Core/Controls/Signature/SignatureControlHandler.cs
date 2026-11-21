using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Signature;

/// <summary>
/// A drawn signature, posted as a PNG data URL in <c>q.{questionId}</c>. It is kept as a file of the response when
/// Attachments is on, else in the answer's extra data (up to 256 KB).
/// </summary>
public sealed class SignatureControlHandler : SurveyControlHandlerBase<SignatureSettings>
{
    /// <summary>
    /// The key of the data URL in the answer's extra data.
    /// </summary>
    public const string DataUrlKey = "DataUrl";

    private const string Prefix = "data:image/png;base64,";
    private const int MaxBytes = 256 * 1024;

    private readonly IAttachmentService _attachmentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureControlHandler"/> class.
    /// </summary>
    /// <param name="attachmentServices">The attachment service, when Attachments is on.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SignatureControlHandler(
        IEnumerable<IAttachmentService> attachmentServices,
        IStringLocalizer<SignatureControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
        _attachmentService = attachmentServices.FirstOrDefault();
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Signature;

    /// <inheritdoc />
    public override async Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = ReadValue(context, context.Prefix);

        if (string.IsNullOrEmpty(value))
        {
            // An unchanged signature posts nothing: keep the one saved before.
            context.Answer = context.Existing is not null && !IsEmpty(context.Question, context.Existing) ? context.Existing : null;

            return;
        }

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            context.Errors.Add(S["The signature could not be read."]);

            return;
        }

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(value.Substring(Prefix.Length));
        }
        catch (FormatException)
        {
            context.Errors.Add(S["The signature could not be read."]);

            return;
        }

        if (bytes.Length > MaxBytes)
        {
            context.Errors.Add(S["The signature is too large."]);

            return;
        }

        if (_attachmentService is not null && context.Response is not null)
        {
            var result = await _attachmentService.AddAsync(
            [
                new AttachmentUpload
                {
                    Owner = new RecordRef(SurveysConstants.ResponseRecordKind, context.Response.ItemId),
                    FileName = "signature.png",
                    DeclaredContentType = "image/png",
                    Content = bytes,
                    UploadedBy = context.Response.Respondent ?? default,
                },
            ], context.CancellationToken);

            context.Errors.AddRange(result.Errors);
            context.Answer = result.Attachments.Count > 0 ? new SurveyAnswer { AttachmentIds = [result.Attachments[0].ItemId] } : null;

            return;
        }

        var answer = new SurveyAnswer();
        answer.Extra[DataUrlKey] = value;
        context.Answer = answer;
    }

    /// <inheritdoc />
    public override bool IsEmpty(SurveyQuestion question, SurveyAnswer answer)
        => answer is null || (!(answer.AttachmentIds?.Count > 0) && answer.Extra?[DataUrlKey] is null);

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Boolean;

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => !IsEmpty(question, answer);

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => IsEmpty(question, answer) ? string.Empty : context?.UseValues == true ? "true" : S["Signed"];

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
        => [];
}
