using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// Checklist questions say when they show with the shared rules, and checklists saved the older way still read the same.
/// </summary>
public sealed class ChecklistRuleTests
{
    private static ChecklistQuestion Question(string id, ChecklistQuestionKind kind = ChecklistQuestionKind.YesNo)
        => new() { Id = id, Text = id, Kind = kind };

    [Fact]
    public void OlderShowWhen_ReadsAsARule()
    {
        // Arrange: a question saved before the shared rules, as it sits on work orders.
        var checklist = new WorkOrderChecklist
        {
            Questions =
            [
                Question("leak"),
                new ChecklistQuestion { Id = "where", Text = "Where?", ShowWhen = new ChecklistCondition { QuestionId = "leak", EqualsValue = "Yes" } },
            ],
        };

        // Act
        var whenNo = WorkOrderService.IsShown(checklist, checklist.Questions[1]);
        checklist.Answers.Add(new ChecklistAnswer { QuestionId = "leak", Value = "yes" });
        var whenYes = WorkOrderService.IsShown(checklist, checklist.Questions[1]);

        // Assert
        Assert.False(whenNo);
        Assert.True(whenYes);
    }

    [Fact]
    public void QuestionUnderAHiddenOne_IsHiddenToo()
    {
        // Arrange: leak -> where (leak is Yes) -> photo of the meter (where is Meter). "where" keeps an earlier answer.
        var checklist = new WorkOrderChecklist
        {
            Questions =
            [
                Question("leak"),
                new ChecklistQuestion { Id = "where", Text = "Where?", Kind = ChecklistQuestionKind.Choice, Choices = ["Meter", "Pipe"], Rule = Rule("leak", "Yes") },
                new ChecklistQuestion { Id = "meter", Text = "Meter reading", Kind = ChecklistQuestionKind.Number, Required = true, Rule = Rule("where", "Meter") },
            ],
            Answers =
            [
                new ChecklistAnswer { QuestionId = "leak", Value = "No" },
                new ChecklistAnswer { QuestionId = "where", Value = "Meter" },
            ],
        };

        // Act
        var hidden = WorkOrderService.GetHiddenQuestions(checklist);

        // Assert: the required meter reading isn't asked for once the leak is gone.
        Assert.Equal(["meter", "where"], hidden.Order());
    }

    [Fact]
    public void MultipleChoiceAnswer_MatchesAnyChosenOption()
    {
        // Arrange
        var checklist = new WorkOrderChecklist
        {
            Questions =
            [
                Question("issues", ChecklistQuestionKind.MultiChoice),
                new ChecklistQuestion { Id = "noise", Text = "Describe the noise", Rule = Rule("issues", "Noise") },
            ],
            Answers = [new ChecklistAnswer { QuestionId = "issues", Value = "Smell\nNoise" }],
        };

        // Act & Assert
        Assert.True(WorkOrderService.IsShown(checklist, checklist.Questions[1]));
    }

    [Fact]
    public async Task Upgrade_KeepsOlderConditionsAsRulesOnce()
    {
        // Arrange
        var templates = new InMemoryCatalog<ChecklistTemplate>();
        templates.Entries.Add(new ChecklistTemplate
        {
            ItemId = "boiler",
            Name = "Boiler",
            Questions =
            [
                Question("leak"),
                new ChecklistQuestion { Id = "where", Text = "Where?", ShowWhen = new ChecklistCondition { QuestionId = "leak", EqualsValue = "Yes" } },
            ],
        });
        templates.Entries.Add(new ChecklistTemplate { ItemId = "plain", Name = "Plain", Questions = [Question("ok")] });

        // Act
        var first = await ChecklistRuleUpgrade.ConvertAsync(templates, null);
        var second = await ChecklistRuleUpgrade.ConvertAsync(templates, null);

        // Assert
        var where = templates.Entries[0].Questions[1];
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Null(where.ShowWhen);
        var condition = Assert.Single(where.Rule.Conditions);
        Assert.Equal("leak", condition.Source);
        Assert.Equal(ConditionOperator.Is, condition.Operator);
        Assert.Equal(["Yes"], condition.Values);
        Assert.Null(templates.Entries[0].Questions[0].Rule);
    }

    private static ConditionRule Rule(string source, string value)
        => new() { Conditions = [new Condition { Source = source, Operator = ConditionOperator.Is, Values = [value] }] };
}
