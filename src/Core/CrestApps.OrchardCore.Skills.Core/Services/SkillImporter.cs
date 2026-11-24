using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Saves the skills read from an import file, matched to the catalog by name in any case: a new name adds a skill, a
/// known one in the same category (or with none yet) is updated, and a known one in another category is left alone,
/// since two Moodle frameworks may hold competencies of the same name and a skill name is unique. A file can be
/// imported again: a skill that already matches is counted as unchanged.
/// </summary>
public sealed class SkillImporter
{
    private readonly ISkillManager _skillManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillImporter"/> class.
    /// </summary>
    /// <param name="skillManager">The skill manager.</param>
    public SkillImporter(ISkillManager skillManager)
    {
        _skillManager = skillManager;
    }

    /// <summary>
    /// Adds or updates the skills of the rows.
    /// </summary>
    /// <param name="rows">The rows read from the file.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was added, changed and unchanged, and the rows left out by line.</returns>
    public async Task<SkillImportResult> ImportAsync(IEnumerable<SkillImportRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var result = new SkillImportResult();
        var seen = new Dictionary<string, SkillImportRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(row?.Name))
            {
                continue;
            }

            var name = row.Name.Trim();

            if (seen.TryGetValue(name, out var earlier))
            {
                result.Failures[row.Line] = $"'{name}' is already on line {earlier.Line} of the file.";

                continue;
            }

            seen[name] = row;

            var existing = await _skillManager.FindByNameAsync(name, cancellationToken);

            if (existing is null)
            {
                var skill = await _skillManager.NewAsync(cancellationToken: cancellationToken);

                skill.Name = name;
                skill.Category = row.Category;
                skill.Description = row.Description;
                skill.Enabled = row.Enabled ?? true;

                if (await SaveAsync(skill, isNew: true, row, result, cancellationToken))
                {
                    result.Created++;
                }

                continue;
            }

            if (!string.IsNullOrEmpty(existing.Category) && !string.IsNullOrEmpty(row.Category) && !string.Equals(existing.Category, row.Category, StringComparison.OrdinalIgnoreCase))
            {
                result.Failures[row.Line] = $"A skill named '{existing.Name}' is already in '{existing.Category}'.";

                continue;
            }

            var changed = false;

            changed |= Set(existing.Category, row.Category, value => existing.Category = value);
            changed |= Set(existing.Description, row.Description, value => existing.Description = value);

            if (row.Enabled is { } enabled && existing.Enabled != enabled)
            {
                existing.Enabled = enabled;
                changed = true;
            }

            if (!changed)
            {
                result.Unchanged++;
            }
            else if (await SaveAsync(existing, isNew: false, row, result, cancellationToken))
            {
                result.Updated++;
            }
        }

        return result;
    }

    private async Task<bool> SaveAsync(Skill skill, bool isNew, SkillImportRow row, SkillImportResult result, CancellationToken cancellationToken)
    {
        var validation = await _skillManager.ValidateAsync(skill, cancellationToken: cancellationToken);

        if (!validation.Succeeded)
        {
            result.Failures[row.Line] = string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));

            return false;
        }

        if (isNew)
        {
            await _skillManager.CreateAsync(skill, cancellationToken: cancellationToken);
        }
        else
        {
            await _skillManager.UpdateAsync(skill, cancellationToken: cancellationToken);
        }

        return true;
    }

    private static bool Set(string current, string incoming, Action<string> set)
    {
        // An empty cell keeps what the skill holds.
        if (string.IsNullOrWhiteSpace(incoming) || string.Equals(current, incoming, StringComparison.Ordinal))
        {
            return false;
        }

        set(incoming);

        return true;
    }
}
