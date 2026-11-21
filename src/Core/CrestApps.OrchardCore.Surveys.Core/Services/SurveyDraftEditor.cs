using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Applies the designer outline commands to a draft: add, move, duplicate and delete pages and questions.
/// </summary>
public sealed class SurveyDraftEditor
{
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDraftEditor"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyDraftEditor(
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyDraftEditor> stringLocalizer)
    {
        _registry = registry;
        S = stringLocalizer;
    }

    /// <summary>
    /// Makes a new question of a type or preset, with its handler's defaults.
    /// </summary>
    /// <param name="definition">The definition the question joins, for a unique code.</param>
    /// <param name="controlType">The question type, or <see langword="null"/> when a preset is given.</param>
    /// <param name="preset">The preset, or <see langword="null"/>.</param>
    /// <returns>The question, or <see langword="null"/> when the type is not registered.</returns>
    public SurveyQuestion NewQuestion(SurveyDefinition definition, string controlType, string preset = null)
    {
        var presetDescriptor = _registry.GetPreset(preset);
        var type = presetDescriptor?.BaseControl ?? controlType;
        var descriptor = _registry.GetControl(type);

        if (descriptor is null)
        {
            return null;
        }

        var question = new SurveyQuestion
        {
            Id = IdGenerator.GenerateId(),
            ControlType = descriptor.Name,
            Name = SurveyDefinitions.NextQuestionName(definition),
            Title = S["New question"],
        };

        _registry.GetHandler(descriptor.Name)?.Initialize(question);
        presetDescriptor?.Initialize?.Invoke(question);

        foreach (var choice in question.Choices.Where(choice => string.IsNullOrEmpty(choice.Id)))
        {
            choice.Id = IdGenerator.GenerateId();
        }

        foreach (var row in question.Rows.Where(row => string.IsNullOrEmpty(row.Id)))
        {
            row.Id = IdGenerator.GenerateId();
        }

        return question;
    }

    /// <summary>
    /// Applies a command to a draft.
    /// </summary>
    /// <param name="definition">The draft, changed in place.</param>
    /// <param name="command">The command.</param>
    /// <returns>The result.</returns>
    public SurveyDraftCommandResult Apply(SurveyDefinition definition, SurveyDraftCommand command)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(command);

        return command.Command switch
        {
            "addPage" => AddPage(definition, command),
            "addQuestion" => AddQuestion(definition, command),
            "move" => MoveQuestion(definition, command),
            "movePage" => MovePage(definition, command),
            "delete" => DeleteQuestion(definition, command),
            "deletePage" => DeletePage(definition, command),
            "duplicate" => DuplicateQuestion(definition, command),
            "duplicatePage" => DuplicatePage(definition, command),
            _ => Fail(S["The command is not known."]),
        };
    }

    private static SurveyDraftCommandResult AddPage(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var page = SurveyDefinitions.NewPage(NextPageNumber(definition));
        definition.Pages.Insert(ClampIndex(command.Index, definition.Pages.Count), page);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = page.Id };
    }

    private SurveyDraftCommandResult AddQuestion(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var page = FindPage(definition, command.PageId) ?? definition.Pages.LastOrDefault();

        if (page is null)
        {
            page = SurveyDefinitions.NewPage(1);
            definition.Pages.Add(page);
        }

        var question = NewQuestion(definition, command.ControlType, command.Preset);

        if (question is null)
        {
            return Fail(S["The question type is not available."]);
        }

        page.Questions.Insert(ClampIndex(command.Index, page.Questions.Count), question);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = question.Id };
    }

    private SurveyDraftCommandResult MoveQuestion(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var (page, question) = FindQuestion(definition, command.QuestionId);
        var target = FindPage(definition, command.ToPageId ?? command.PageId) ?? page;

        if (question is null || target is null)
        {
            return Fail(S["The question was not found."]);
        }

        page.Questions.Remove(question);

        if (target != page && !string.IsNullOrEmpty(question.PoolId))
        {
            question.PoolId = null;
        }

        target.Questions.Insert(ClampIndex(command.Index, target.Questions.Count), question);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = question.Id };
    }

    private SurveyDraftCommandResult MovePage(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var page = FindPage(definition, command.PageId);

        if (page is null)
        {
            return Fail(S["The page was not found."]);
        }

        definition.Pages.Remove(page);
        definition.Pages.Insert(ClampIndex(command.Index, definition.Pages.Count), page);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = page.Id };
    }

    private SurveyDraftCommandResult DeleteQuestion(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var (page, question) = FindQuestion(definition, command.QuestionId);

        if (question is null)
        {
            return Fail(S["The question was not found."]);
        }

        page.Questions.Remove(question);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = question.Id };
    }

    private SurveyDraftCommandResult DeletePage(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var page = FindPage(definition, command.PageId);

        if (page is null)
        {
            return Fail(S["The page was not found."]);
        }

        if (definition.Pages.Count == 1)
        {
            return Fail(S["A survey needs at least one page."]);
        }

        definition.Pages.Remove(page);

        foreach (var other in definition.Pages)
        {
            other.Branches.RemoveAll(branch => branch.TargetKind == BranchTargetKind.Page && branch.TargetId == page.Id);
        }

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = page.Id };
    }

    private SurveyDraftCommandResult DuplicateQuestion(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var (page, question) = FindQuestion(definition, command.QuestionId);

        if (question is null)
        {
            return Fail(S["The question was not found."]);
        }

        var copy = SurveyDefinitionCloner.Clone(question);
        SurveyDefinitionCloner.RegenerateIds(copy);
        copy.Name = SurveyDefinitions.NextQuestionName(definition, (question.Name ?? "q") + "_copy");
        page.Questions.Insert(page.Questions.IndexOf(question) + 1, copy);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = copy.Id };
    }

    private SurveyDraftCommandResult DuplicatePage(SurveyDefinition definition, SurveyDraftCommand command)
    {
        var page = FindPage(definition, command.PageId);

        if (page is null)
        {
            return Fail(S["The page was not found."]);
        }

        var single = new SurveyDefinition { Pages = [page] };
        var copyDefinition = SurveyDefinitionCloner.Clone(single);
        SurveyDefinitionCloner.RegenerateIds(copyDefinition);

        var copy = copyDefinition.Pages[0];
        copy.Name = $"page_{NextPageNumber(definition)}";
        copy.Branches.Clear();

        foreach (var question in copy.Questions)
        {
            question.Name = SurveyDefinitions.NextQuestionName(definition, (question.Name ?? "q") + "_copy");
            definition.Pages.Add(new SurveyPage { Id = "pending", Questions = [question] });
        }

        definition.Pages.RemoveAll(item => item.Id == "pending");
        definition.Pages.Insert(definition.Pages.IndexOf(page) + 1, copy);

        return new SurveyDraftCommandResult { Succeeded = true, ItemId = copy.Id };
    }

    private static int NextPageNumber(SurveyDefinition definition)
    {
        var used = definition.Pages.Select(page => page.Name).Where(name => name is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = definition.Pages.Count + 1; ; i++)
        {
            if (!used.Contains($"page_{i}"))
            {
                return i;
            }
        }
    }

    private static SurveyPage FindPage(SurveyDefinition definition, string pageId)
        => string.IsNullOrEmpty(pageId) ? null : definition.Pages.FirstOrDefault(page => page.Id == pageId);

    private static (SurveyPage Page, SurveyQuestion Question) FindQuestion(SurveyDefinition definition, string questionId)
    {
        foreach (var page in definition.Pages)
        {
            var question = page.Questions.FirstOrDefault(item => item.Id == questionId);

            if (question is not null)
            {
                return (page, question);
            }
        }

        return (null, null);
    }

    private static int ClampIndex(int? index, int count)
        => index is { } value ? Math.Clamp(value, 0, count) : count;

    private static SurveyDraftCommandResult Fail(string error)
        => new() { Error = error };
}
