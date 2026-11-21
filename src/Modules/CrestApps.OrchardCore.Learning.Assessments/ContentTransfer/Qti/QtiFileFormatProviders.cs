using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;

/// <summary>
/// Reads QTI files for ContentTransfer: each assessment item becomes a row with the question columns, of the question
/// type that fits it. It imports question types only and never writes a file.
/// </summary>
public abstract class QtiFileFormatProvider : IContentTransferFileFormatProvider
{
    private readonly QuestionTypeOptions _questionTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="QtiFileFormatProvider"/> class.
    /// </summary>
    /// <param name="questionTypes">The question types.</param>
    /// <param name="stringLocalizer">The localizer of the row errors.</param>
    protected QtiFileFormatProvider(IOptions<QuestionTypeOptions> questionTypes, IStringLocalizer<QtiFileFormatProvider> stringLocalizer)
    {
        _questionTypes = questionTypes.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public abstract string ContentType { get; }

    /// <inheritdoc />
    public bool CanExport => false;

    /// <inheritdoc />
    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName ?? string.Empty).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool CanImport(string contentType)
        => _questionTypes.IsQuestion(contentType);

    /// <inheritdoc />
    public IContentTransferFileReader CreateReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var result = QtiReader.Read(stream, "file" + FileExtension);

        return new InMemoryContentTransferFileReader(QtiQuestionRows.Columns, QtiQuestionRows.Write(result, _questionTypes, S));
    }

    /// <inheritdoc />
    public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName)
        => throw new NotSupportedException("QTI files are imported only.");

    /// <inheritdoc />
    public string DescribeImport(ContentTypeDefinition contentType)
        => contentType is not null && CanImport(contentType.Name) ? Describe() : null;

    /// <summary>
    /// Says what files of this format hold.
    /// </summary>
    protected abstract string Describe();
}

/// <summary>
/// A QTI 3.0 or QTI 2.1 content package: a zip archive with <c>imsmanifest.xml</c>.
/// </summary>
public sealed class QtiPackageFileFormatProvider : QtiFileFormatProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QtiPackageFileFormatProvider"/> class.
    /// </summary>
    public QtiPackageFileFormatProvider(IOptions<QuestionTypeOptions> questionTypes, IStringLocalizer<QtiFileFormatProvider> stringLocalizer)
        : base(questionTypes, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string FileExtension => ".zip";

    /// <inheritdoc />
    public override string ContentType => "application/zip";

    /// <inheritdoc />
    protected override string Describe()
        => S["A QTI 3.0 or QTI 2.1 content package (.zip with imsmanifest.xml at its root): each assessment item becomes the kind of question that fits it, whichever question type you import, and an item that cannot be imported is reported as a failed row."];
}

/// <summary>
/// A single QTI 3.0 or QTI 2.1 assessment item file.
/// </summary>
public sealed class QtiItemFileFormatProvider : QtiFileFormatProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QtiItemFileFormatProvider"/> class.
    /// </summary>
    public QtiItemFileFormatProvider(IOptions<QuestionTypeOptions> questionTypes, IStringLocalizer<QtiFileFormatProvider> stringLocalizer)
        : base(questionTypes, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string FileExtension => ".xml";

    /// <inheritdoc />
    public override string ContentType => "application/xml";

    /// <inheritdoc />
    protected override string Describe()
        => S["A single QTI 3.0 or QTI 2.1 assessment item (.xml), which becomes the kind of question that fits it. An item file has no package around it, so an item with images is imported from a package only."];
}
