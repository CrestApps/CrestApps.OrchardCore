using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The page of a question bank: its questions and the filters.
/// </summary>
public class QuestionBankViewModel
{
    /// <summary>
    /// Gets or sets the bank.
    /// </summary>
    public QuestionBank Bank { get; set; }

    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public BankQuestionFilterOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the questions of the page.
    /// </summary>
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the question types a question may be added with: name, display name, category and icon.
    /// </summary>
    public IReadOnlyList<(string Name, string DisplayName, string Category, string Icon)> Controls { get; set; } = [];

    /// <summary>
    /// Gets or sets the other banks questions may move to.
    /// </summary>
    public IReadOnlyList<QuestionBank> OtherBanks { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the user may change the bank.
    /// </summary>
    public bool CanManage { get; set; }
}
