using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task that finds the numbers an agent calls and texts from: the phone number whose line names them and the
/// SMS number whose line names them, each falling back to the default number chosen under Settings &gt; Contact Center.
/// It writes them to the workflow output, so a follow-up text to a customer the agent spoke to is sent from the number
/// the agent texts from.
/// </summary>
public sealed class FindAgentNumbersTask : TaskActivity<FindAgentNumbersTask>
{
    /// <summary>
    /// The output the phone number is written to.
    /// </summary>
    public const string PhoneNumberOutput = "AgentPhoneNumber";

    /// <summary>
    /// The output the SMS number is written to.
    /// </summary>
    public const string SmsNumberOutput = "AgentSmsNumber";

    private readonly IAgentAddressResolver _addressResolver;
    private readonly UserManager<IUser> _userManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindAgentNumbersTask"/> class.
    /// </summary>
    /// <param name="addressResolver">The resolver of an agent's numbers.</param>
    /// <param name="userManager">The user manager, to find the user by name.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FindAgentNumbersTask(
        IAgentAddressResolver addressResolver,
        UserManager<IUser> userManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<FindAgentNumbersTask> logger,
        IStringLocalizer<FindAgentNumbersTask> stringLocalizer)
    {
        _addressResolver = addressResolver;
        _userManager = userManager;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Find Agent Numbers"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the agent's user name. A user identifier or an email address is
    /// accepted too.
    /// </summary>
    public string UserName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Done"]),
            new Outcome(S["NotFound"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var name = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(UserName), workflowContext, null))?.Trim();

        // An unknown or empty user still gets the default numbers: the follow-up can be sent from the business's
        // number even when nobody in particular placed the call.
        var user = string.IsNullOrEmpty(name) ? null : await FindUserAsync(name);
        var userId = user is null ? null : await _userManager.GetUserIdAsync(user);

        if (!string.IsNullOrEmpty(name) && user is null)
        {
            _logger.LogWarning("The Find Agent Numbers task found no user named '{UserName}'; the default numbers are used.", name.SanitizeLogValue());
        }

        var addresses = await _addressResolver.ResolveAsync(userId);

        workflowContext.Output[PhoneNumberOutput] = addresses.PhoneAddress?.Value;
        workflowContext.Output[SmsNumberOutput] = addresses.SmsAddress?.Value;

        return addresses.PhoneAddress is null && addresses.SmsAddress is null
            ? WorkflowOutcomeResults.From("NotFound")
            : WorkflowOutcomeResults.From("Done");
    }

    private async Task<IUser> FindUserAsync(string name)
        => await _userManager.FindByNameAsync(name)
            ?? await _userManager.FindByIdAsync(name)
            ?? (name.Contains('@') ? await _userManager.FindByEmailAsync(name) : null);
}
