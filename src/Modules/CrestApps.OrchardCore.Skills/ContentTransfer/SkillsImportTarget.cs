using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Skills.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.ContentTransfer;

/// <summary>
/// Imports skills, with competency frameworks as their categories, from a file through ContentTransfer: a Moodle
/// competency framework export (the framework row names the category of the competencies under it) or a plain file
/// with Name, Category, Description and Enabled columns. Skills are matched by name; see <see cref="SkillImporter"/>.
/// </summary>
internal sealed class SkillsImportTarget : IContentTransferRecordImporter
{
    /// <summary>
    /// The name imports of skills are filed under.
    /// </summary>
    public const string TargetName = "Skills.Skill";

    // The framework of a Moodle export, kept on the import between batches and across a resume.
    private const string FrameworkProperty = "SkillsImportFramework";

    private readonly SkillImporter _importer;

    internal readonly IStringLocalizer S;

    public SkillsImportTarget(SkillImporter importer, IStringLocalizer<SkillsImportTarget> stringLocalizer)
    {
        _importer = importer;
        S = stringLocalizer;
    }

    public string Name => TargetName;

    public string DisplayName => S["Skills and competency frameworks"];

    public bool IsContentType => false;

    public Task<bool> AuthorizeAsync(ClaimsPrincipal user, IAuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);

        return authorizationService.AuthorizeAsync(user, SkillsPermissions.ManageSkills);
    }

    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>(
        [
            new ImportColumn
            {
                Name = "Name",
                IsRequired = true,
                AdditionalNames = ["shortname"],
                Description = S["The skill, or competency, name. A skill with this name is updated; another name adds a skill. A Moodle competency framework export is read as it is: each competency's short name (or ID number) is the name, and the framework row names the category."],
            },
            new ImportColumn
            {
                Name = "Category",
                Description = S["The competency framework (skill category), such as Customer Service. A name another category already holds is left out."],
            },
            new ImportColumn
            {
                Name = "Description",
                Description = S["What the skill means, as plain text. The HTML of a Moodle description is turned into plain text."],
            },
            new ImportColumn
            {
                Name = "Enabled",
                Description = S["Whether the skill can be given to people. Empty keeps a skill as it is and enables a new one."],
                ValidValues = ["yes", "no"],
            },
        ]);

    public async Task ImportAsync(ContentTransferImportBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var reader = new SkillImportRowReader
        {
            Framework = batch.Entry.Properties[FrameworkProperty]?.GetValue<string>(),
        };
        var rowsByLine = new Dictionary<int, ContentTransferImportRow>();
        var skills = new List<SkillImportRow>();

        foreach (var row in batch.Rows)
        {
            var cells = row.Columns.ToDictionary(column => column, column => row.Get(column), StringComparer.OrdinalIgnoreCase);
            var read = reader.Read(row.RowIndex, cells);

            if (read.Error is not null)
            {
                row.Fail(read.Error, S["Could not be read"]);

                continue;
            }

            if (read.Skill is not null)
            {
                rowsByLine[row.RowIndex] = row;
                skills.Add(read.Skill);
            }
        }

        batch.Entry.Properties[FrameworkProperty] = reader.Framework is null ? null : JsonValue.Create(reader.Framework);

        var result = await _importer.ImportAsync(skills, cancellationToken);

        foreach (var (line, error) in result.Failures)
        {
            if (rowsByLine.TryGetValue(line, out var row))
            {
                row.Fail(error, S["Not imported"]);
            }
        }
    }
}
