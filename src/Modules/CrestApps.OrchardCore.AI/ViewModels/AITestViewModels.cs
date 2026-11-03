using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// What the AI test case list can do to the selected tests.
/// </summary>
public enum AITestBulkAction
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// Run each once.
    /// </summary>
    Run,

    /// <summary>
    /// Run each five times, for a pass rate.
    /// </summary>
    RunFiveTimes,

    /// <summary>
    /// Delete them.
    /// </summary>
    Delete,
}

/// <summary>
/// The AI test case list.
/// </summary>
public class AITestCasesIndexViewModel
{
    /// <summary>
    /// Gets or sets the search and the bulk actions.
    /// </summary>
    public CatalogEntryOptions<AITestBulkAction> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether there are tests to list, or a search to show.
    /// </summary>
    [BindNever]
    public bool HasTests { get; set; }

    /// <summary>
    /// Gets or sets the tests for the admin list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// The runs of a test, a batch, or every test.
/// </summary>
public class AITestRunsViewModel
{
    /// <summary>
    /// Gets or sets the test the runs belong to, if one.
    /// </summary>
    public AITestCase TestCase { get; set; }

    /// <summary>
    /// Gets or sets the batch the runs belong to, if one.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the share of finished runs that passed.
    /// </summary>
    public double? PassRate { get; set; }

    /// <summary>
    /// Gets or sets how many runs are still queued or running.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the runs for the admin list.
    /// </summary>
    public AdminListContext List { get; set; }
}

/// <summary>
/// The editor of an AI test case.
/// </summary>
public class AITestCaseViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the profile under test.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version under test; empty for the current one.
    /// </summary>
    public int? ProfileVersion { get; set; }

    /// <summary>
    /// Gets or sets the simulated customer.
    /// </summary>
    public string Scenario { get; set; }

    /// <summary>
    /// Gets or sets the criteria, one per line.
    /// </summary>
    public string Criteria { get; set; }

    /// <summary>
    /// Gets or sets the variables, one <c>name: value</c> per line.
    /// </summary>
    public string Variables { get; set; }

    /// <summary>
    /// Gets or sets the mocked tools, one <c>tool name: result</c> per line.
    /// </summary>
    public string MockedTools { get; set; }

    /// <summary>
    /// Gets or sets the most customer turns.
    /// </summary>
    public int MaxTurns { get; set; }

    /// <summary>
    /// Gets or sets the passing score.
    /// </summary>
    public int PassingScore { get; set; }

    /// <summary>
    /// Gets or sets the deployment that plays the customer.
    /// </summary>
    public string CustomerDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges.
    /// </summary>
    public string JudgeDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the profiles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the deployments to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Deployments { get; set; } = [];
}
