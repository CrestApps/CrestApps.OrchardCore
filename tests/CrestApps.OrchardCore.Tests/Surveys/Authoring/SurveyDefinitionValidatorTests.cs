using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed partial class SurveyDefinitionValidatorTests
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

    // R11-L4: an ending's message is restricted Liquid, so a filter of Orchard Core's full surface is an error the author
    // sees, and the four values and the restricted filters are fine.
    [Theory]
    [InlineData("Thanks {{ Answers.name | upcase }}, your score is {{ Score }}.", false)]
    [InlineData("{{ \"**done**\" | markdownify }}", true)]
    [InlineData("{% if Response.Passed %}Well done{% endif", true)]
    public async Task ValidateAsync_AnEndingsLiquid_IsCheckedAgainstTheRestrictedSet(string message, bool error)
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Endings[0].Message = message;

        Assert.Equal(error, (await CodesAsync(definition)).Contains("ending-liquid"));
    }

    [Fact]
    public async Task ValidateAsync_ATranslatedEndingsLiquid_IsCheckedToo()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Translations["fr"] = new Dictionary<string, string>
        {
            [CrestApps.OrchardCore.Surveys.Core.Translations.SurveyTextKeys.EndingMessage("e1")] = "{{ \"merci\" | markdownify }}",
        };

        Assert.Contains("ending-liquid", await CodesAsync(definition));
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

    [Theory]
    [InlineData(BranchTargetKind.Page, true)]
    [InlineData(BranchTargetKind.Ending, false)]
    public async Task ValidateAsync_ShuffledPagesWithAPageBranch_WarnsThatTheOrderIsKept(BranchTargetKind targetKind, bool warns)
    {
        // Arrange: five pages set to shuffle; the first page branches to the fourth page, or to an ending.
        var definition = AuthoringDefinitions.TwoPages();

        for (var number = 3; number <= 5; number++)
        {
            definition.Pages.Add(new SurveyPage
            {
                Id = "p" + number,
                Name = "page_" + number,
                Questions = [new SurveyQuestion { Id = "q" + (number + 1), Name = "text_" + number, ControlType = AuthoringSurveyHarness.TextControl, Title = "Text" }],
            });
        }

        definition.Presentation.PageOrder = QuestionOrder.Shuffle;
        definition.Pages[0].Branches.Add(new SurveyBranch { Id = "b1", TargetKind = targetKind, TargetId = targetKind == BranchTargetKind.Page ? "p4" : "e1" });

        // Act
        var context = await Validator().ValidateAsync(null, definition, TestContext.Current.CancellationToken);

        // Assert: a warning, never an error, and only for a branch to a page.
        Assert.DoesNotContain(context.Messages, message => message.IsError);
        Assert.Equal(warns, context.Messages.Any(message => message.Code == "shuffle-with-branches" && !message.IsError));
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

    [Fact]
    public async Task ValidateAsync_PageFormulaReadingALaterPage_IsAnError()
    {
        // Arrange: page 1 shows by a formula on the answer to q3, on page 2 (R6-L1).
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Visibility = new SurveyVisibility { Mode = VisibilityMode.HideWhen, Formula = "[why] = 'x'" };

        // Act & Assert
        Assert.Contains("invalid-formula-source", await CodesAsync(definition, new ReferencingFormulas()));
    }

    [Fact]
    public async Task ValidateAsync_PageFormulaReadingItsOwnPage_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[1].Visibility = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Formula = "[why] = 'x'" };

        Assert.Contains("invalid-formula-source", await CodesAsync(definition, new ReferencingFormulas()));
    }

    [Fact]
    public async Task ValidateAsync_BranchFormulaReadingALaterPage_IsAnError()
    {
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Branches.Add(new SurveyBranch { Id = "b1", Formula = "[why] = 'x'", TargetKind = BranchTargetKind.Ending, TargetId = "e1" });

        Assert.Contains("invalid-formula-source", await CodesAsync(definition, new ReferencingFormulas()));
    }

    [Fact]
    public async Task ValidateAsync_QuestionFormulaReadingThroughACalculatedVariableALaterPage_IsAnError()
    {
        // Arrange: q2 (page 1) hides by a formula on a calculated variable that copies q3 (page 2).
        var definition = AuthoringDefinitions.TwoPages();
        definition.Variables.Add(new SurveyVariable { Name = "late", Kind = VariableKind.Calculated, Formula = "[why]" });
        definition.Pages[0].Questions[1].Visibility = new SurveyVisibility { Mode = VisibilityMode.HideWhen, Formula = "[late] = 'x'" };

        // Act & Assert
        Assert.Contains("invalid-formula-source", await CodesAsync(definition, new ReferencingFormulas()));
    }

    [Fact]
    public async Task ValidateAsync_RuleReadingACalculatedVariableOfALaterPage_IsAnError()
    {
        // Arrange: a rule reads the variable, which reads q3 on page 2: as late as q3 itself.
        var definition = AuthoringDefinitions.TwoPages();
        definition.Variables.Add(new SurveyVariable { Name = "late", Kind = VariableKind.Calculated, Formula = "[why]" });
        definition.Pages[0].Questions[1].Visibility = ShowWhen("var:late", ConditionOperator.IsNotEmpty);

        Assert.Contains("invalid-rule-source", await CodesAsync(definition, new ReferencingFormulas()));
    }

    [Fact]
    public async Task ValidateAsync_FormulasReadingEarlierQuestions_AreFine()
    {
        // Arrange: page 2 and its question read page 1, and a branch of page 1 reads its own page.
        var definition = AuthoringDefinitions.TwoPages();
        definition.Variables.Add(new SurveyVariable { Name = "early", Kind = VariableKind.Calculated, Formula = "[color]" });
        definition.Pages[1].Visibility = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Formula = "[early] = 'red'" };
        definition.Pages[1].Questions[0].Visibility = new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Formula = "[comment] <> ''" };
        definition.Pages[0].Branches.Add(new SurveyBranch { Id = "b1", Formula = "[color] = 'blue'", TargetKind = BranchTargetKind.Ending, TargetId = "e1" });

        // Act & Assert
        Assert.Empty(await CodesAsync(definition, new ReferencingFormulas()));
    }

    private static SurveyVisibility ShowWhen(string source, ConditionOperator op, params string[] values)
        => new()
        {
            Mode = VisibilityMode.ShowWhen,
            Rule = new ConditionRule { Conditions = [new Condition { Source = source, Operator = op, Values = values.ToList() }] },
        };

    // Accepts every formula and reads the fields between its brackets, as the formula compiler does.
    private sealed partial class ReferencingFormulas : ISurveyFormulaValidator
    {
        public bool TryCompile(SurveyDefinition definition, string formula, out string error)
        {
            error = null;

            return true;
        }

        public IReadOnlyCollection<string> GetReferences(SurveyDefinition definition, string formula)
            => [.. Field().Matches(formula ?? string.Empty).Select(match => match.Groups[1].Value)];

        [System.Text.RegularExpressions.GeneratedRegex(@"\[([^\]]+)\]")]
        private static partial System.Text.RegularExpressions.Regex Field();
    }

    private sealed class RejectingFormulas : ISurveyFormulaValidator
    {
        public bool TryCompile(SurveyDefinition definition, string formula, out string error)
        {
            error = "syntax";

            return false;
        }
    }
}
