using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// The fields survey formulas may read: <c>[name]</c> and <c>[name.row]</c> for answers, <c>[name]</c> or
/// <c>[var:name]</c> for variables, <c>[score]</c>, <c>[percent]</c> and <c>[score:category]</c>. Each field has a
/// slot of the row the formula reads.
/// </summary>
public sealed class SurveyFormulaScope : IExpressionScope
{
    private readonly Dictionary<string, SurveyFormulaField> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SurveyFormulaField> _slots = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyFormulaScope"/> class.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="registry">The control registry.</param>
    public SurveyFormulaScope(SurveyDefinition definition, ISurveyControlRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);

        foreach (var question in (definition.Pages ?? []).SelectMany(page => page.Questions ?? []))
        {
            if (string.IsNullOrEmpty(question?.Name) || registry.GetHandler(question.ControlType) is not { } handler)
            {
                continue;
            }

            Add(question.Name, handler.GetFormulaType(question, null), SurveyFormulaFieldKind.Question, question.Id, null);

            foreach (var row in question.Rows ?? [])
            {
                if (!string.IsNullOrEmpty(row?.Name))
                {
                    Add(question.Name + "." + row.Name, handler.GetFormulaType(question, row.Id), SurveyFormulaFieldKind.Question, question.Id, row.Id);
                }
            }
        }

        foreach (var variable in definition.Variables ?? [])
        {
            if (string.IsNullOrEmpty(variable?.Name))
            {
                continue;
            }

            var field = Add(variable.Name, variable.DataType, SurveyFormulaFieldKind.Variable, variable.Name, null);

            if (field is not null)
            {
                _fields["var:" + variable.Name] = field;
            }
        }

        Add("score", DataFieldType.Decimal, SurveyFormulaFieldKind.Score, null, null);
        Add("percent", DataFieldType.Decimal, SurveyFormulaFieldKind.Percent, null, null);

        foreach (var category in definition.ScoreCategories ?? [])
        {
            if (!string.IsNullOrEmpty(category?.Id))
            {
                Add("score:" + (string.IsNullOrEmpty(category.Name) ? category.Id : category.Name), DataFieldType.Decimal, SurveyFormulaFieldKind.CategoryScore, category.Id, null);
            }
        }
    }

    /// <summary>
    /// Gets the fields in slot order.
    /// </summary>
    public IReadOnlyList<SurveyFormulaField> Slots => _slots;

    /// <summary>
    /// Finds a field by its key.
    /// </summary>
    /// <param name="key">The key, as written between brackets.</param>
    /// <returns>The field, or <see langword="null"/>.</returns>
    public SurveyFormulaField Find(string key)
        => !string.IsNullOrEmpty(key) && _fields.TryGetValue(key.Trim(), out var field) ? field : null;

    /// <inheritdoc />
    public bool TryResolve(string key, out ExpressionFieldBinding binding)
    {
        var field = Find(key);

        if (field is null)
        {
            binding = null;

            return false;
        }

        binding = new ExpressionFieldBinding
        {
            Key = field.Key,
            DataType = field.Type,
            Index = field.Slot,
        };

        return true;
    }

    private SurveyFormulaField Add(string key, DataFieldType type, SurveyFormulaFieldKind kind, string id, string rowId)
    {
        if (_fields.ContainsKey(key))
        {
            return null;
        }

        var field = new SurveyFormulaField
        {
            Key = key,
            Type = type,
            Kind = kind,
            Id = id,
            RowId = rowId,
            Slot = _slots.Count,
        };

        _slots.Add(field);
        _fields[key] = field;

        return field;
    }
}
