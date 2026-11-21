using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Translations;

/// <summary>
/// Lists the translatable texts of a survey definition in designed order, and reads or writes them by key.
/// </summary>
public static class SurveyTextCatalog
{
    /// <summary>
    /// Lists every translatable text that is not empty.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The texts.</returns>
    public static IReadOnlyList<SurveyTextEntry> Describe(SurveyDefinition definition)
    {
        var entries = new List<SurveyTextEntry>();

        if (definition is null)
        {
            return entries;
        }

        Add(entries, SurveyTextKeys.SurveyTitle, "survey", null, definition.Title, false);
        Add(entries, SurveyTextKeys.SurveyIntroduction, "survey", null, definition.Introduction, true);

        foreach (var page in definition.Pages ?? [])
        {
            if (string.IsNullOrEmpty(page?.Id))
            {
                continue;
            }

            Add(entries, SurveyTextKeys.PageTitle(page.Id), "page", page.Name, page.Title, false);
            Add(entries, SurveyTextKeys.PageDescription(page.Id), "page", page.Name, page.Description, true);

            foreach (var question in page.Questions ?? [])
            {
                if (string.IsNullOrEmpty(question?.Id))
                {
                    continue;
                }

                Add(entries, SurveyTextKeys.QuestionTitle(question.Id), "question", question.Name, question.Title, false);
                Add(entries, SurveyTextKeys.QuestionDescription(question.Id), "question", question.Name, question.Description, true);
                Add(entries, SurveyTextKeys.QuestionPlaceholder(question.Id), "question", question.Name, question.Placeholder, false);

                foreach (var choice in question.Choices ?? [])
                {
                    if (!string.IsNullOrEmpty(choice?.Id))
                    {
                        Add(entries, SurveyTextKeys.Choice(question.Id, choice.Id), "choice", question.Name + " / " + (choice.Value ?? choice.Id), choice.Text, false);
                    }
                }

                foreach (var row in question.Rows ?? [])
                {
                    if (!string.IsNullOrEmpty(row?.Id))
                    {
                        Add(entries, SurveyTextKeys.Row(question.Id, row.Id), "row", question.Name + " / " + (row.Name ?? row.Id), row.Text, false);
                    }
                }
            }
        }

        foreach (var ending in definition.Endings ?? [])
        {
            if (string.IsNullOrEmpty(ending?.Id))
            {
                continue;
            }

            Add(entries, SurveyTextKeys.EndingTitle(ending.Id), "ending", ending.Name, ending.Title, false);
            Add(entries, SurveyTextKeys.EndingMessage(ending.Id), "ending", ending.Name, ending.Message, true);
        }

        return entries;
    }

    /// <summary>
    /// Gets the keys of every item that can be translated, empty texts included.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlySet<string> KnownKeys(SurveyDefinition definition)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            SurveyTextKeys.SurveyTitle,
            SurveyTextKeys.SurveyIntroduction,
        };

        foreach (var page in definition?.Pages ?? [])
        {
            if (string.IsNullOrEmpty(page?.Id))
            {
                continue;
            }

            keys.Add(SurveyTextKeys.PageTitle(page.Id));
            keys.Add(SurveyTextKeys.PageDescription(page.Id));

            foreach (var question in page.Questions ?? [])
            {
                if (string.IsNullOrEmpty(question?.Id))
                {
                    continue;
                }

                keys.Add(SurveyTextKeys.QuestionTitle(question.Id));
                keys.Add(SurveyTextKeys.QuestionDescription(question.Id));
                keys.Add(SurveyTextKeys.QuestionPlaceholder(question.Id));

                foreach (var choice in question.Choices ?? [])
                {
                    if (!string.IsNullOrEmpty(choice?.Id))
                    {
                        keys.Add(SurveyTextKeys.Choice(question.Id, choice.Id));
                    }
                }

                foreach (var row in question.Rows ?? [])
                {
                    if (!string.IsNullOrEmpty(row?.Id))
                    {
                        keys.Add(SurveyTextKeys.Row(question.Id, row.Id));
                    }
                }
            }
        }

        foreach (var ending in definition?.Endings ?? [])
        {
            if (!string.IsNullOrEmpty(ending?.Id))
            {
                keys.Add(SurveyTextKeys.EndingTitle(ending.Id));
                keys.Add(SurveyTextKeys.EndingMessage(ending.Id));
            }
        }

        return keys;
    }

    private static void Add(List<SurveyTextEntry> entries, string key, string group, string label, string text, bool isHtml)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        entries.Add(new SurveyTextEntry
        {
            Key = key,
            Group = group,
            Label = label,
            Text = text,
            IsHtml = isHtml,
        });
    }
}
