using CrestApps.OrchardCore.ContentTransfer;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;

/// <summary>
/// Makes the question types one group of row types, so one import (a QTI package, or a spreadsheet with a
/// <c>ContentType</c> column) creates questions of every kind.
/// </summary>
internal sealed class QuestionRowTypeProvider : IContentImportRowTypeProvider
{
    private readonly QuestionTypeOptions _questionTypes;

    public QuestionRowTypeProvider(IOptions<QuestionTypeOptions> questionTypes)
    {
        _questionTypes = questionTypes.Value;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetRowContentTypes(ContentTypeDefinition contentType)
        => contentType is not null && _questionTypes.IsQuestion(contentType.Name)
            ? _questionTypes.Types.Select(type => type.ContentType).Where(type => !string.IsNullOrEmpty(type))
            : [];
}
