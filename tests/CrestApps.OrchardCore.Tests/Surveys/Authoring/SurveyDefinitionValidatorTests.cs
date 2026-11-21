using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyDefinitionValidatorTests
{
    private static SurveyDefinitionValidator Validator(ISurveyFormulaValidator formulas = null)
        => new(AuthoringSurveyHarness.CreateRegistry(), [], formulas ?? new NullSurveyFormulaValidator(), new AuthoringLocalizer<SurveyDefinitionValidator>());

    private static async Task<IReadOnlyList<string>> CodesAsync(SurveyDefinition definition, ISurveyFormulaValidator formulas = null)
    {
        var context = await Validator(formulas).ValidateAsync(null, definition, TestContext.Current.CancellationToken);

        return context.Messages.Where(message => message.IsError).Select(message => message.Code).ToList();
    }

    [Fact]
    public async Task ValidateAsync_ValidDefinition_HasNoErrors()
        => Assert.Empty(await CodesAsync(AuthoringDefinitions.TwoPages()));

    [Fact]
    public async Task ValidateAsync_DuplicateNames_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Questions[0].Name = "COLOR";

        Assert.Contains("duplicate-name", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_DuplicateIds_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Questions[0].Id = "q1";

        Assert.Contains("duplicate-id", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_NoAnswerableQuestion_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions.Clear();
        definition.Pages[1].Questions[0].ControlType = AuthoringSurveyHarness.BlockControl;
        definition.Pages[1].Questions[0].Title = "Intro";

        Assert.Contains("no-questions", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_UnknownControl_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[1].ControlType = "Missing";

        Assert.Contains("unknown-control", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_RuleReadingALaterPage_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[1].Visibility = ShowWhen("q3", ConditionOperator.IsNotEmpty);

        Assert.Contains("invalid-rule-source", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_PageRuleReadingTheSamePage_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Visibility = ShowWhen("q3", ConditionOperator.IsNotEmpty);

        Assert.Contains("invalid-rule-source", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_RuleWithUnknownChoice_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Questions[0].Visibility = ShowWhen("q1", ConditionOperator.Is, "green");

        Assert.Contains("unknown-rule-choice", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_CycleOfRules_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[0].Visibility = ShowWhen("q2", ConditionOperator.IsNotEmpty);
        definition.Pages[0].Questions[1].Visibility = ShowWhen("q1", ConditionOperator.IsNotEmpty);

        Assert.Contains("logic-cycle", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_BranchToAnEarlierPage_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Branches.Add(new SurveyBranch { Id = "b1", TargetKind = BranchTargetKind.Page, TargetId = "p1" });

        Assert.Contains("branch-backward", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_BranchToMissingEnding_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Branches.Add(new SurveyBranch { Id = "b1", TargetKind = BranchTargetKind.Ending, TargetId = "nope" });

        Assert.Contains("branch-ending", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_TwoDefaultEndings_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Endings.Add(new SurveyEnding { Id = "e2", Name = "other", IsDefault = true });

        Assert.Contains("default-ending", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_InvalidRegex_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[1].Validation.Add(new SurveyValidationRule { Kind = SurveyValidationKind.Regex, Value = "([a-z" });

        Assert.Contains("invalid-regex", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_PoolDrawingMoreThanItHas_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Pools.Add(new QuestionPool { Id = "pool", Name = "pool", DrawCount = 3 });
        definition.Pages[0].Questions[0].PoolId = "pool";

        Assert.Contains("pool-draw", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_PipingALaterQuestion_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[1].Title = "About {why}";

        Assert.Contains("late-pipe", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_PipingAnUnknownName_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Questions[0].Title = "About {nothing}";

        Assert.Contains("unknown-pipe", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_LiquidInEndingMessage_IsNotPiping()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Endings[0].Message = "{{ Survey.Title }} {color}";

        Assert.Empty(await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_KeyNamingMissingChoice_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Scoring.Enabled = true;
        definition.Pages[0].Questions[0].Scoring.CorrectChoiceIds = ["purple"];

        Assert.Contains("unknown-key-choice", await CodesAsync(definition));
    }

    [Fact]
    public async Task ValidateAsync_FormulaThatDoesNotCompile_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Variables.Add(new SurveyVariable { Name = "total", Kind = VariableKind.Calculated, Formula = "[color] +" });

        Assert.Contains("invalid-formula", await CodesAsync(definition, new RejectingFormulas()));
    }

    private static SurveyVisibility ShowWhen(string source, ConditionOperator op, params string[] values)
        => new()
        {
            Mode = VisibilityMode.ShowWhen,
            Rule = new ConditionRule { Conditions = [new Condition { Source = source, Operator = op, Values = values.ToList() }] },
        };

    private sealed class RejectingFormulas : ISurveyFormulaValidator
    {
        public bool TryCompile(SurveyDefinition definition, string formula, out string error)
        {
            error = "syntax";

            return false;
        }
    }
}
