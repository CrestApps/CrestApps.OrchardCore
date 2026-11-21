using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Compares two survey definitions by stable ids and flags the changes that affect existing answers or comparisons.
/// </summary>
public sealed class SurveyDefinitionDiffer : ISurveyDefinitionDiffer
{
    /// <summary>
    /// The kind of item: a page.
    /// </summary>
    public const string PageTarget = "page";

    /// <summary>
    /// The kind of item: a question.
    /// </summary>
    public const string QuestionTarget = "question";

    /// <summary>
    /// The kind of item: a choice.
    /// </summary>
    public const string ChoiceTarget = "choice";

    /// <summary>
    /// The kind of item: a row.
    /// </summary>
    public const string RowTarget = "row";

    /// <summary>
    /// The kind of item: an ending.
    /// </summary>
    public const string EndingTarget = "ending";

    /// <summary>
    /// The kind of item: a variable.
    /// </summary>
    public const string VariableTarget = "variable";

    /// <inheritdoc />
    public SurveyDefinitionDiff Compare(SurveyDefinition before, SurveyDefinition after)
    {
        ArgumentNullException.ThrowIfNull(after);

        var diff = new SurveyDefinitionDiff();

        if (before is null)
        {
            return diff;
        }

        ComparePages(diff, before, after);
        CompareQuestions(diff, before, after);
        CompareList(diff, EndingTarget, before.Endings, after.Endings, ending => ending.Id, ending => ending.Title ?? ending.Name, (a, b) => Json(a) != Json(b));
        CompareList(diff, VariableTarget, before.Variables, after.Variables, variable => variable.Name, variable => variable.Name, (a, b) => Json(a) != Json(b));

        return diff;
    }

    private static void ComparePages(SurveyDefinitionDiff diff, SurveyDefinition before, SurveyDefinition after)
    {
        var oldPages = before.Pages.Select((page, index) => (page, index)).Where(item => item.page.Id is not null).ToDictionary(item => item.page.Id, StringComparer.Ordinal);

        for (var i = 0; i < after.Pages.Count; i++)
        {
            var page = after.Pages[i];

            if (page.Id is null || !oldPages.TryGetValue(page.Id, out var old))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Added, Target = PageTarget, Id = page.Id, Name = page.Title ?? page.Name });

                continue;
            }

            if (old.index != i)
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Moved, Target = PageTarget, Id = page.Id, Name = page.Title ?? page.Name });
            }

            if (page.Title != old.page.Title || page.Description != old.page.Description || Json(page.Visibility) != Json(old.page.Visibility) || Json(page.Branches) != Json(old.page.Branches) || Json(page.Pools) != Json(old.page.Pools))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = PageTarget, Id = page.Id, Name = page.Title ?? page.Name });
            }
        }

        var newIds = after.Pages.Select(page => page.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var page in before.Pages.Where(page => !newIds.Contains(page.Id)))
        {
            diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Removed, Target = PageTarget, Id = page.Id, Name = page.Title ?? page.Name });
        }
    }

    private static void CompareQuestions(SurveyDefinitionDiff diff, SurveyDefinition before, SurveyDefinition after)
    {
        var oldQuestions = SurveyDefinitions.Questions(before).Where(item => item.Question.Id is not null).GroupBy(item => item.Question.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var newIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (page, _, question) in SurveyDefinitions.Questions(after))
        {
            if (question.Id is null)
            {
                continue;
            }

            newIds.Add(question.Id);

            if (!oldQuestions.TryGetValue(question.Id, out var old))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Added, Target = QuestionTarget, Id = question.Id, Name = Label(question) });

                continue;
            }

            var previous = old.Question;

            if (!string.Equals(previous.ControlType, question.ControlType, StringComparison.OrdinalIgnoreCase))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = QuestionTarget, Id = question.Id, Name = Label(question), Detail = "controlType", Destructive = true });
            }

            if (question.Required && !previous.Required)
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = QuestionTarget, Id = question.Id, Name = Label(question), Detail = "required", Destructive = true });
            }

            if (Json(question.Scoring) != Json(previous.Scoring))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = QuestionTarget, Id = question.Id, Name = Label(question), Detail = "scoring", Destructive = true });
            }

            if (!string.Equals(question.Name, previous.Name, StringComparison.Ordinal))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = QuestionTarget, Id = question.Id, Name = Label(question), Detail = "name" });
            }

            if (question.Title != previous.Title
                || question.Description != previous.Description
                || Json(question.Visibility) != Json(previous.Visibility)
                || Json(question.Settings) != Json(previous.Settings)
                || Json(question.Validation) != Json(previous.Validation))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = QuestionTarget, Id = question.Id, Name = Label(question), Detail = "content" });
            }

            if (old.Page.Id != page.Id)
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Moved, Target = QuestionTarget, Id = question.Id, Name = Label(question), Detail = "page" });
            }

            CompareChildren(diff, ChoiceTarget, question, previous.Choices, question.Choices, choice => choice.Id, choice => choice.Text, choice => choice.Retired);
            CompareChildren(diff, RowTarget, question, previous.Rows, question.Rows, row => row.Id, row => row.Text, row => row.Retired);
        }

        foreach (var (id, old) in oldQuestions.Where(pair => !newIds.Contains(pair.Key)))
        {
            diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Removed, Target = QuestionTarget, Id = id, Name = Label(old.Question), Destructive = true });
        }
    }

    private static void CompareChildren<T>(
        SurveyDefinitionDiff diff,
        string target,
        SurveyQuestion question,
        IEnumerable<T> before,
        IEnumerable<T> after,
        Func<T, string> id,
        Func<T, string> text,
        Func<T, bool> retired)
    {
        var old = before.Where(item => id(item) is not null).GroupBy(id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in after)
        {
            var key = id(item);

            if (key is null)
            {
                continue;
            }

            seen.Add(key);

            if (!old.TryGetValue(key, out var previous))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Added, Target = target, Id = key, Name = text(item), QuestionId = question.Id });

                continue;
            }

            if (retired(item) && !retired(previous))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = target, Id = key, Name = text(item), QuestionId = question.Id, Detail = "retired", Destructive = true });
            }
            else if (Json(item) != Json(previous))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = target, Id = key, Name = text(item), QuestionId = question.Id, Detail = "content" });
            }
        }

        foreach (var (key, item) in old.Where(pair => !seen.Contains(pair.Key)))
        {
            diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Removed, Target = target, Id = key, Name = text(item), QuestionId = question.Id, Destructive = true });
        }
    }

    private static void CompareList<T>(SurveyDefinitionDiff diff, string target, IEnumerable<T> before, IEnumerable<T> after, Func<T, string> id, Func<T, string> name, Func<T, T, bool> changed)
    {
        var old = before.Where(item => id(item) is not null).GroupBy(id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in after)
        {
            var key = id(item);

            if (key is null)
            {
                continue;
            }

            seen.Add(key);

            if (!old.TryGetValue(key, out var previous))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Added, Target = target, Id = key, Name = name(item) });
            }
            else if (changed(item, previous))
            {
                diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Changed, Target = target, Id = key, Name = name(item) });
            }
        }

        foreach (var (key, item) in old.Where(pair => !seen.Contains(pair.Key)))
        {
            diff.Changes.Add(new SurveyDefinitionChange { Kind = SurveyChangeKind.Removed, Target = target, Id = key, Name = name(item) });
        }
    }

    private static string Label(SurveyQuestion question)
        => string.IsNullOrWhiteSpace(question.Title) ? question.Name : question.Title;

    private static string Json<T>(T value)
        => value is null ? string.Empty : JsonSerializer.Serialize(value);
}
