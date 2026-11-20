using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;

/// <summary>
/// The limits of the Moodle import, bound from <c>CrestApps:Learning:MoodleImport</c>.
/// </summary>
public sealed class MoodleImportOptions
{
    /// <summary>
    /// Gets or sets the most entries a backup may hold.
    /// </summary>
    public int MaxEntries { get; set; } = new MoodleBackupLimits().MaxEntries;

    /// <summary>
    /// Gets or sets the most bytes a backup may expand to.
    /// </summary>
    public long MaxExpandedBytes { get; set; } = new MoodleBackupLimits().MaxExpandedBytes;

    /// <summary>
    /// Gets or sets the most characters an XML file of a backup may hold.
    /// </summary>
    public long MaxXmlCharacters { get; set; } = new MoodleBackupLimits().MaxXmlCharacters;

    internal MoodleBackupLimits ToLimits()
        => new()
        {
            MaxEntries = MaxEntries,
            MaxExpandedBytes = MaxExpandedBytes,
            MaxXmlCharacters = MaxXmlCharacters,
        };
}

/// <summary>
/// Reads a Moodle course backup (<c>.mbz</c>) for ContentTransfer: one row for the course, one for each activity the
/// site has an item for (a SCORM package, a quiz, a file, a link, a page) and one failed row for each thing it does not
/// import. The course is imported from the import page of the Course type and the file is never written.
/// </summary>
internal sealed class MoodleFileFormatProvider : IContentTransferFileFormatProvider
{
    private readonly ITempDirectoryProvider _tempDirectoryProvider;
    private readonly MoodleWorkspaces _workspaces;
    private readonly MoodleImportOptions _options;
    private readonly QuestionTypeOptions _questionTypes;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public MoodleFileFormatProvider(
        ITempDirectoryProvider tempDirectoryProvider,
        MoodleWorkspaces workspaces,
        IOptions<MoodleImportOptions> options,
        IOptions<QuestionTypeOptions> questionTypes,
        ILogger<MoodleFileFormatProvider> logger,
        IStringLocalizer<MoodleFileFormatProvider> stringLocalizer)
    {
        _tempDirectoryProvider = tempDirectoryProvider;
        _workspaces = workspaces;
        _options = options.Value;
        _questionTypes = questionTypes.Value;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string FileExtension => ".mbz";

    /// <inheritdoc />
    public string ContentType => "application/vnd.moodle.backup";

    /// <inheritdoc />
    public bool CanExport => false;

    /// <inheritdoc />
    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName ?? string.Empty).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool CanImport(string contentType)
        => contentType == LearningConstants.ContentTypes.Course;

    /// <inheritdoc />
    public IContentTransferFileReader CreateReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var folder = _tempDirectoryProvider.CreateTempSubdirectory("moodle-import-");
        MoodleBackupReadResult result;

        try
        {
            // ContentTransfer reads a file synchronously, so the backup is extracted and read synchronously.
            result = MoodleBackupReader.ExtractAndRead(stream, folder, _options.ToLimits());
        }
        catch
        {
            MoodleWorkspaces.Delete(folder, _logger);

            throw;
        }

        if (!result.Succeeded)
        {
            MoodleWorkspaces.Delete(folder, _logger);

            return new MoodleImportReader(null, _workspaces, [MoodleCourseRows.FileError(result, S)]);
        }

        var token = _workspaces.Add(new MoodleWorkspace(folder, result.Backup));

        return new MoodleImportReader(token, _workspaces, MoodleCourseRows.Write(result.Backup, token, _questionTypes, S));
    }

    /// <inheritdoc />
    public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName)
        => throw new NotSupportedException("Moodle backups are imported only.");

    /// <inheritdoc />
    public string DescribeImport(ContentTypeDefinition contentType)
        => contentType is not null && CanImport(contentType.Name)
            ? S["A Moodle course backup (.mbz, as a zip or a tar.gz archive): the course with its category, dates, enrolment and completion, its sections, and an item for each SCORM package, quiz (with its questions in a question bank), file, link and page. Labels join their section's summary. Every activity, question or setting that is not imported is reported as a failed row, and importing the same course again updates it."]
            : null;
}

/// <summary>
/// The rows of a Moodle backup. Disposing it removes the extracted backup.
/// </summary>
internal sealed class MoodleImportReader : IContentTransferFileReader
{
    private readonly string _token;
    private readonly MoodleWorkspaces _workspaces;
    private readonly IReadOnlyList<string[]> _rows;

    public MoodleImportReader(string token, MoodleWorkspaces workspaces, IReadOnlyList<string[]> rows)
    {
        _token = token;
        _workspaces = workspaces;
        _rows = rows;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetColumnNames()
        => MoodleCourseRows.Columns;

    /// <inheritdoc />
    public int GetRowCount()
        => _rows.Count;

    /// <inheritdoc />
    public IEnumerable<string[]> ReadRows()
        => _rows;

    /// <inheritdoc />
    public void Dispose()
        => _workspaces.Remove(_token);
}
