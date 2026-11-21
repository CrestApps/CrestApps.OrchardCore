using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// The exam form posts one field per answer; the reader keeps only values the item offers, so a forged option id or
/// matching answer is dropped.
/// </summary>
public sealed class AttemptFormReaderTests
{
    [Fact]
    public void Choices_KeepOnlyOfferedOptions()
    {
        var item = new AttemptItem { Index = 2, QuestionType = AssessmentsConstants.QuestionTypes.MultipleChoice, Choices = [new() { Id = "a" }, new() { Id = "b" }] };
        var form = Form(("a2", ["a", "forged", "b", "a"]));

        Assert.Equal(["a", "b"], AttemptFormReader.Read(form, item).ChoiceIds);
        Assert.True(AttemptFormReader.HasAnswer(form, item));
        Assert.False(AttemptFormReader.HasAnswer(form, new AttemptItem { Index = 3 }));
    }

    [Fact]
    public void BlanksMatchesOrderAndNumbers_AreReadFromTheirFields()
    {
        var blanks = new AttemptItem { Index = 0, QuestionType = AssessmentsConstants.QuestionTypes.FillInBlanks, BlankCount = 2 };
        var matching = new AttemptItem
        {
            Index = 1,
            QuestionType = AssessmentsConstants.QuestionTypes.Matching,
            Choices = [new() { Id = "p1" }, new() { Id = "p2" }],
            Answers = [new() { Id = "m0" }, new() { Id = "m1" }],
        };
        var ordering = new AttemptItem { Index = 2, QuestionType = AssessmentsConstants.QuestionTypes.Ordering, Choices = [new() { Id = "x" }, new() { Id = "y" }] };
        var numeric = new AttemptItem { Index = 3, QuestionType = AssessmentsConstants.QuestionTypes.Numeric };
        var form = Form(("a0_b0", ["blue"]), ("a0_b1", ["green"]), ("a1_pp1", ["m1"]), ("a1_pp2", ["evil"]), ("a2_o0", ["y"]), ("a2_o1", ["x"]), ("a3", ["9.81"]));

        Assert.Equal(["blue", "green"], AttemptFormReader.Read(form, blanks).Texts);
        Assert.Equal(new Dictionary<string, string> { ["p1"] = "m1" }, AttemptFormReader.Read(form, matching).Pairs);
        Assert.Equal(["y", "x"], AttemptFormReader.Read(form, ordering).ChoiceIds);
        Assert.Equal(9.81m, AttemptFormReader.Read(form, numeric).Number);
    }

    private static FormCollection Form(params (string Key, string[] Values)[] fields)
        => new(fields.ToDictionary(field => field.Key, field => new StringValues(field.Values)));
}
