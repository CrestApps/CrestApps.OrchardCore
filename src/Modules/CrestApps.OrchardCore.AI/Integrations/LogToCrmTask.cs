using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// Finds or creates the customer in HubSpot or Salesforce by phone number, updates the fields you map, and logs the
/// call with its summary and outcome on them. Put it after AI Chat Session Closed or AI Conversation Analyzed.
/// </summary>
public sealed class LogToCrmTask : TaskActivity<LogToCrmTask>
{
    private readonly IAIIntegrationConnectionStore _store;
    private readonly AIIntegrationClients _clients;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogToCrmTask"/> class.
    /// </summary>
    /// <param name="store">Finds the connection.</param>
    /// <param name="clients">Makes the CRM client.</param>
    /// <param name="expressionEvaluator">Renders the Liquid fields.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LogToCrmTask(
        IAIIntegrationConnectionStore store,
        AIIntegrationClients clients,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IClock clock,
        ILogger<LogToCrmTask> logger,
        IStringLocalizer<LogToCrmTask> stringLocalizer)
    {
        _store = store;
        _clients = clients;
        _expressionEvaluator = expressionEvaluator;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Log to CRM"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the CRM connection.
    /// </summary>
    public string ConnectionId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the customer's phone number, which finds them.
    /// </summary>
    public string Phone
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets <c>field: value</c> lines (Liquid) written to the contact, such as <c>email: {{ ... }}</c>.
    /// <c>firstname</c>, <c>lastname</c> and <c>email</c> map to the contact's own fields on either CRM.
    /// </summary>
    public string Fields
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the logged call's title.
    /// </summary>
    public string Title
    {
        get => GetProperty(() => "AI call");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the logged call's notes, such as the summary and the outcome.
    /// </summary>
    public string Notes
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a customer who is not in the CRM is added.
    /// </summary>
    public bool CreateIfMissing
    {
        get => GetProperty(() => true);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Not found"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var connection = string.IsNullOrEmpty(ConnectionId) ? null : await _store.FindByIdAsync(ConnectionId);
        var phone = await EvaluateAsync(Phone, workflowContext);

        if (connection is null || !connection.IsConnected || string.IsNullOrWhiteSpace(phone))
        {
            _logger.LogWarning("The Log to CRM task needs a signed-in CRM connection and a phone number.");

            return Outcome("Failed");
        }

        try
        {
            var crm = await _clients.CreateCrmAsync(connection);
            var contact = await crm.FindByPhoneAsync(phone);

            if (contact is null && !CreateIfMissing)
            {
                return Outcome("Not found");
            }

            contact ??= new AICrmContact { Phone = phone };
            ApplyFields(contact, ParseFields(await EvaluateAsync(Fields, workflowContext)));

            var contactId = await crm.SaveAsync(contact);
            var activityId = await crm.LogCallAsync(contactId, new AICrmActivity
            {
                Title = await EvaluateAsync(Title, workflowContext) ?? "AI call",
                Notes = await EvaluateAsync(Notes, workflowContext),
                OccurredUtc = _clock.UtcNow,
                Inbound = false,
            });

            workflowContext.Output["CrmContactId"] = contactId;
            workflowContext.Output["CrmActivityId"] = activityId;
            workflowContext.LastResult = contactId;

            return Outcome("Done");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            _logger.LogError(ex, "The Log to CRM task could not reach the '{Connection}' connection.", connection.Name);

            return Outcome("Failed");
        }
    }

    /// <summary>
    /// Reads <c>field: value</c> lines.
    /// </summary>
    /// <param name="text">The rendered lines.</param>
    /// <returns>The fields.</returns>
    internal static Dictionary<string, string> ParseFields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var separator = line.IndexOf(':');

            if (separator > 0 && line[(separator + 1)..].Trim() is { Length: > 0 } value)
            {
                fields[line[..separator].Trim()] = value;
            }
        }

        return fields;
    }

    /// <summary>
    /// Writes fields to a contact: the name and email to its own properties, the rest by the CRM's field names.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="fields">The fields.</param>
    internal static void ApplyFields(AICrmContact contact, Dictionary<string, string> fields)
    {
        foreach (var (name, value) in fields)
        {
            switch (name.ToLowerInvariant())
            {
                case "firstname" or "first_name":
                    contact.FirstName = value;
                    break;
                case "lastname" or "last_name":
                    contact.LastName = value;
                    break;
                case "email":
                    contact.Email = value;
                    break;
                default:
                    contact.Fields[name] = value;
                    break;
            }
        }
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
