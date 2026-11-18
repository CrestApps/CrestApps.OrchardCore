using CrestApps.Core.Models;
using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// How checklists saved before the shared conditions said when a question shows: after another was answered a certain
/// way. Still read from those checklists (<see cref="ChecklistQuestion.GetRule"/>); new ones keep
/// <see cref="ChecklistQuestion.Rule"/>.
/// </summary>
public sealed class ChecklistCondition
{
    /// <summary>
    /// Gets or sets the question it depends on.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the answer that shows it.
    /// </summary>
    public string EqualsValue { get; set; }
}

/// <summary>
/// A question on a checklist.
/// </summary>
public sealed class ChecklistQuestion
{
    /// <summary>
    /// Gets or sets the question identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the section it is in.
    /// </summary>
    public string Section { get; set; }

    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind of answer.
    /// </summary>
    public ChecklistQuestionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it must be answered.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets help text.
    /// </summary>
    public string Help { get; set; }

    /// <summary>
    /// Gets or sets the lowest number accepted.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the highest number accepted.
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// Gets or sets the unit of a number.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets the choices.
    /// </summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets how many photos it needs.
    /// </summary>
    public int MinPhotos { get; set; }

    /// <summary>
    /// Gets or sets the asset meter a reading is for.
    /// </summary>
    public string Meter { get; set; }

    /// <summary>
    /// Gets or sets when it shows, or <see langword="null"/> for always. Its conditions read other questions by their
    /// <see cref="Id"/>.
    /// </summary>
    public ConditionRule Rule { get; set; }

    /// <summary>
    /// Gets or sets when it shows, as checklists saved before <see cref="Rule"/> said it. Read through
    /// <see cref="GetRule"/>; cleared when the question is saved again.
    /// </summary>
    public ChecklistCondition ShowWhen { get; set; }

    /// <summary>
    /// Gets when the question shows: its <see cref="Rule"/>, or the older <see cref="ShowWhen"/> as a rule.
    /// </summary>
    /// <returns>The rule, or <see langword="null"/> for always.</returns>
    public ConditionRule GetRule()
    {
        if (!ConditionRule.IsEmpty(Rule))
        {
            return Rule;
        }

        return string.IsNullOrEmpty(ShowWhen?.QuestionId)
            ? null
            : new ConditionRule
            {
                Conditions =
                [
                    new Condition { Source = ShowWhen.QuestionId, Operator = ConditionOperator.Is, Values = [ShowWhen.EqualsValue ?? string.Empty] },
                ],
            };
    }
}

/// <summary>
/// A checklist to fill in on a job, such as an inspection.
/// </summary>
public sealed class ChecklistTemplate : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the questions, in order.
    /// </summary>
    public List<ChecklistQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// A finer status inside one, such as Waiting for parts inside On hold.
/// </summary>
public sealed class WorkOrderSubStatus : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the status it belongs to.
    /// </summary>
    public WorkOrderStatus ParentStatus { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the service level clock stops in it.
    /// </summary>
    public bool PausesSla { get; set; }
}
