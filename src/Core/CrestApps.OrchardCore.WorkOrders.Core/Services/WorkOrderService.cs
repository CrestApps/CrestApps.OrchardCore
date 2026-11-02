using System.Globalization;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// The result of a change to a work order.
/// </summary>
public sealed class WorkOrderResult
{
    /// <summary>
    /// Gets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; init; }

    /// <summary>
    /// Gets why it was refused; empty when it succeeded.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether it succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    internal static WorkOrderResult Fail(params string[] errors) => new() { Errors = errors };
}

/// <summary>
/// Creates, changes, completes, bills and follows up work orders, keeping their service level clock.
/// </summary>
public sealed class WorkOrderService
{
    private readonly ISession _session;
    private readonly WorkOrderStore _store;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderSettingManager<ChecklistTemplate> _checklists;
    private readonly WorkOrderSettingManager<ServiceLevelPolicy> _policies;
    private readonly WorkOrderSettingManager<WorkOrderSubStatus> _subStatuses;
    private readonly SlaClock _slaClock;
    private readonly IEnumerable<IWorkOrderHandler> _handlers;
    private readonly IEnumerable<IWorkOrderObserver> _observers;
    private readonly IEnumerable<IWorkOrderPartsService> _parts;
    private readonly IEnumerable<IWorkOrderBilling> _billing;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="store">The work order store.</param>
    /// <param name="types">The work order types.</param>
    /// <param name="checklists">The checklist templates.</param>
    /// <param name="policies">The service level policies.</param>
    /// <param name="subStatuses">The sub-statuses.</param>
    /// <param name="slaClock">The service level clock.</param>
    /// <param name="handlers">The work order handlers.</param>
    /// <param name="observers">The work order observers.</param>
    /// <param name="parts">The parts service, when Inventory is on.</param>
    /// <param name="billing">The billing service, when Transactions is on.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderService(
        ISession session,
        WorkOrderStore store,
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderSettingManager<ChecklistTemplate> checklists,
        WorkOrderSettingManager<ServiceLevelPolicy> policies,
        WorkOrderSettingManager<WorkOrderSubStatus> subStatuses,
        SlaClock slaClock,
        IEnumerable<IWorkOrderHandler> handlers,
        IEnumerable<IWorkOrderObserver> observers,
        IEnumerable<IWorkOrderPartsService> parts,
        IEnumerable<IWorkOrderBilling> billing,
        IClock clock,
        IStringLocalizer<WorkOrderService> stringLocalizer)
    {
        _session = session;
        _store = store;
        _types = types;
        _checklists = checklists;
        _policies = policies;
        _subStatuses = subStatuses;
        _slaClock = slaClock;
        _handlers = handlers;
        _observers = observers;
        _parts = parts;
        _billing = billing;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets a value indicating whether parts used come off Inventory.
    /// </summary>
    public bool TracksParts => _parts.Any();

    /// <summary>
    /// Gets a value indicating whether completed work orders are invoiced.
    /// </summary>
    public bool Bills => _billing.Any();

    /// <summary>
    /// Makes a new work order with its type's defaults: priority, duration, skills, lines, checklists and policy.
    /// </summary>
    /// <param name="typeId">The type.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work order, not saved, or <see langword="null"/> when the type doesn't exist.</returns>
    public async Task<WorkOrder> NewAsync(string typeId, CancellationToken cancellationToken = default)
    {
        var type = string.IsNullOrEmpty(typeId) ? null : await _types.FindByIdAsync(typeId, cancellationToken);

        if (type is null)
        {
            return null;
        }

        var workOrder = new WorkOrder
        {
            ItemId = CrestApps.Core.UniqueId.GenerateId(),
            TypeId = type.ItemId,
            Priority = type.DefaultPriority,
            EstimatedMinutes = type.DefaultDurationMinutes,
            RequiredSkills = [.. type.RequiredSkills.Select(skill => new RequiredSkill { SkillId = skill.SkillId, MinProficiency = skill.MinProficiency })],
            Lines = [.. type.DefaultLines.Select(line => Copy(line))],
            Sla = new WorkOrderSla { PolicyId = type.ServiceLevelPolicyId },
        };

        foreach (var templateId in type.ChecklistTemplateIds)
        {
            if (await _checklists.FindByIdAsync(templateId, cancellationToken) is { } template)
            {
                workOrder.Checklists.Add(new WorkOrderChecklist { TemplateId = template.ItemId, Name = template.Name, Questions = [.. template.Questions] });
            }
        }

        return workOrder;
    }

    /// <summary>
    /// Saves a new work order: its number, its service level due times and its first note.
    /// </summary>
    /// <param name="workOrder">The work order, from <see cref="NewAsync"/>.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> CreateAsync(WorkOrder workOrder, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        var errors = await ValidateAsync(workOrder, cancellationToken);

        if (errors.Count > 0)
        {
            return new WorkOrderResult { WorkOrder = workOrder, Errors = errors };
        }

        var now = _clock.UtcNow;

        workOrder.ItemId ??= CrestApps.Core.UniqueId.GenerateId();
        workOrder.Status = WorkOrderStatus.New;
        workOrder.CreatedUtc = now;
        workOrder.Number ??= await NextNumberAsync(cancellationToken);

        foreach (var line in workOrder.Lines)
        {
            line.Id ??= CrestApps.Core.UniqueId.GenerateId();
        }

        await ScheduleSlaAsync(workOrder, now, cancellationToken);

        foreach (var handler in _handlers)
        {
            await handler.CreatingAsync(workOrder, cancellationToken);
        }

        Note(workOrder, actor, S["Created."]);
        await _store.CreateAsync(workOrder, cancellationToken);
        await NotifyAsync(workOrder, WorkOrdersConstants.Events.Created, null, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Saves the details of a work order: what, where, who for, its lines' plans.
    /// </summary>
    /// <param name="workOrder">The work order, loaded and changed.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> UpdateAsync(WorkOrder workOrder, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workOrder);

        var errors = await ValidateAsync(workOrder, cancellationToken);

        if (errors.Count > 0)
        {
            return new WorkOrderResult { WorkOrder = workOrder, Errors = errors };
        }

        foreach (var handler in _handlers)
        {
            await handler.UpdatingAsync(workOrder, cancellationToken);
        }

        Note(workOrder, actor, S["Details changed."]);
        await _store.UpdateAsync(workOrder, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Moves a work order to another status, keeping the service level clock: the first move out of New is the
    /// response, and the clock stops while it waits.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="status">The new status; Completed only through <see cref="CompleteAsync"/>.</param>
    /// <param name="subStatusId">The sub-status, or <see langword="null"/>.</param>
    /// <param name="note">Why, or <see langword="null"/>.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> ChangeStatusAsync(string id, WorkOrderStatus status, string subStatusId, string note, string actor, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(id, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderResult.Fail(S["The work order doesn't exist."]);
        }

        if (status == WorkOrderStatus.Completed)
        {
            return WorkOrderResult.Fail(S["Complete a work order with Complete, which checks it is finished."]);
        }

        if (status == WorkOrderStatus.Canceled && string.IsNullOrWhiteSpace(note))
        {
            return WorkOrderResult.Fail(S["Say why the work order is canceled."]);
        }

        var subStatus = string.IsNullOrEmpty(subStatusId) ? null : await _subStatuses.FindByIdAsync(subStatusId, cancellationToken);

        if (subStatus is not null && subStatus.ParentStatus != status)
        {
            return WorkOrderResult.Fail(S["{0} belongs to {1}, not {2}.", subStatus.Name, subStatus.ParentStatus.Text(), status.Text()]);
        }

        if (status == workOrder.Status && subStatus?.ItemId == workOrder.SubStatusId)
        {
            return new WorkOrderResult { WorkOrder = workOrder };
        }

        if (status != workOrder.Status && !WorkOrderStatusRules.CanMove(workOrder.Status, status))
        {
            return WorkOrderResult.Fail(S["A work order can't move from {0} to {1}.", workOrder.Status.Text(), status.Text()]);
        }

        var previous = workOrder.Status;

        await MoveAsync(workOrder, status, subStatus, cancellationToken);

        if (status == WorkOrderStatus.Canceled)
        {
            workOrder.CancelReason = note.Trim();
        }

        if (status == WorkOrderStatus.Closed)
        {
            workOrder.ClosedUtc = _clock.UtcNow;
            workOrder.ClosedBy = actor;
        }

        Note(workOrder, actor, string.IsNullOrWhiteSpace(note) ? S["{0} to {1}.", previous.Text(), Label(status, subStatus)] : S["{0} to {1}: {2}", previous.Text(), Label(status, subStatus), note.Trim()]);
        await _store.UpdateAsync(workOrder, cancellationToken);
        await NotifyAsync(workOrder, WorkOrdersConstants.Events.StatusChanged, new Dictionary<string, object> { ["PreviousStatus"] = previous.ToString() }, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Records parts used from a product line, taking them off the stock location when Inventory is on.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="lineId">The line.</param>
    /// <param name="quantity">How many.</param>
    /// <param name="locationId">The stock location, such as the van's.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> UsePartAsync(string id, string lineId, decimal quantity, string locationId, string actor, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(id, cancellationToken);
        var line = workOrder?.Lines.FirstOrDefault(item => item.Id == lineId);

        if (line is null)
        {
            return WorkOrderResult.Fail(S["The line doesn't exist."]);
        }

        if (quantity <= 0)
        {
            return WorkOrderResult.Fail(S["Enter how many were used."]);
        }

        if (!WorkOrderStatusRules.IsOpen(workOrder.Status))
        {
            return WorkOrderResult.Fail(S["Parts can't be added to a {0} work order.", workOrder.Status.Text().ToLowerInvariant()]);
        }

        if (line.Kind == WorkOrderLineKind.Product && !string.IsNullOrEmpty(line.StockItemId))
        {
            if (string.IsNullOrEmpty(locationId))
            {
                return WorkOrderResult.Fail(S["Choose where the parts came from."]);
            }

            line.LocationId = locationId;

            foreach (var parts in _parts)
            {
                if (await parts.UseAsync(workOrder, line, quantity, actor, cancellationToken) is { } refused)
                {
                    return WorkOrderResult.Fail(refused);
                }
            }
        }

        line.QuantityUsed += quantity;
        Note(workOrder, actor, S["Used {0} × {1}.", quantity.ToString("0.##", CultureInfo.CurrentCulture), line.Description]);
        await _store.UpdateAsync(workOrder, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Answers a checklist question, checking the answer fits it.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="checklistIndex">The checklist, by position.</param>
    /// <param name="questionId">The question.</param>
    /// <param name="value">The answer.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> AnswerAsync(string id, int checklistIndex, string questionId, string value, string actor, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(id, cancellationToken);
        var checklist = workOrder is not null && checklistIndex >= 0 && checklistIndex < workOrder.Checklists.Count ? workOrder.Checklists[checklistIndex] : null;
        var question = checklist?.Questions.FirstOrDefault(item => item.Id == questionId);

        if (question is null)
        {
            return WorkOrderResult.Fail(S["The question doesn't exist."]);
        }

        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        if (value is not null && Check(question, value) is { } refused)
        {
            return WorkOrderResult.Fail(refused);
        }

        checklist.Answers.RemoveAll(answer => answer.QuestionId == questionId);

        if (value is not null)
        {
            checklist.Answers.Add(new ChecklistAnswer { QuestionId = questionId, Value = value, Utc = _clock.UtcNow, By = actor });
        }

        await _store.UpdateAsync(workOrder, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Records the customer's signature.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="name">Who signed.</param>
    /// <param name="imagePath">The signature image in the media library, or <see langword="null"/>.</param>
    /// <param name="actor">Who took it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> SignAsync(string id, string name, string imagePath, string actor, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(id, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderResult.Fail(S["The work order doesn't exist."]);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return WorkOrderResult.Fail(S["Enter the name of who signed."]);
        }

        workOrder.Signatures.Add(new WorkOrderSignature { Name = name.Trim(), ImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath.Trim(), Utc = _clock.UtcNow });
        Note(workOrder, actor, S["Signed by {0}.", name.Trim()]);
        await _store.UpdateAsync(workOrder, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Adds a note.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="text">The note.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> AddNoteAsync(string id, string text, string actor, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(id, cancellationToken);

        if (workOrder is null || string.IsNullOrWhiteSpace(text))
        {
            return WorkOrderResult.Fail(S["Write the note."]);
        }

        Note(workOrder, actor, text.Trim());
        await _store.UpdateAsync(workOrder, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Completes a work order once its required answers, signature and photos are in, and invoices it when it is
    /// billable and Transactions is on.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<WorkOrderResult> CompleteAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        var workOrder = await _store.FindByIdAsync(id, cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderResult.Fail(S["The work order doesn't exist."]);
        }

        if (!WorkOrderStatusRules.CanMove(workOrder.Status, WorkOrderStatus.Completed))
        {
            return WorkOrderResult.Fail(S["Only a work order in progress can be completed."]);
        }

        var type = await _types.FindByIdAsync(workOrder.TypeId, cancellationToken);
        var missing = new List<string>();

        foreach (var checklist in workOrder.Checklists)
        {
            foreach (var question in checklist.Questions.Where(question => question.Required && IsShown(checklist, question)))
            {
                if (!checklist.Answers.Any(answer => answer.QuestionId == question.Id))
                {
                    missing.Add(S["Answer \"{0}\" on {1}.", question.Text, checklist.Name]);
                }
            }
        }

        if (type?.RequiresSignature == true && workOrder.Signatures.Count == 0)
        {
            missing.Add(S["The customer must sign."]);
        }

        if (type?.RequiredPhotos > 0 && workOrder.Attachments.Count < type.RequiredPhotos)
        {
            missing.Add(S["Add at least {0} photos.", type.RequiredPhotos]);
        }

        if (missing.Count > 0)
        {
            return new WorkOrderResult { WorkOrder = workOrder, Errors = missing };
        }

        await MoveAsync(workOrder, WorkOrderStatus.Completed, null, cancellationToken);
        workOrder.CompletedUtc = _clock.UtcNow;

        if (type is not null && (type.BillingMode != WorkOrderBillingMode.NotBillable || workOrder.AgreedPrice is not null) && string.IsNullOrEmpty(workOrder.InvoiceTransactionId))
        {
            var amount = BillableAmount(workOrder, type);

            foreach (var billing in _billing)
            {
                if (amount > 0)
                {
                    workOrder.InvoiceTransactionId = await billing.InvoiceAsync(workOrder, amount, type.Currency ?? "USD", cancellationToken);
                }

                break;
            }
        }

        Note(workOrder, actor, S["Completed."]);
        await _store.UpdateAsync(workOrder, cancellationToken);
        await NotifyAsync(workOrder, WorkOrdersConstants.Events.Completed, null, cancellationToken);

        return new WorkOrderResult { WorkOrder = workOrder };
    }

    /// <summary>
    /// Makes a follow-up work order for the same customer, place and assets.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="summary">What the follow-up is for.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result, with the follow-up.</returns>
    public async Task<WorkOrderResult> FollowUpAsync(string id, string summary, string actor, CancellationToken cancellationToken = default)
    {
        var parent = await _store.FindByIdAsync(id, cancellationToken);

        if (parent is null)
        {
            return WorkOrderResult.Fail(S["The work order doesn't exist."]);
        }

        var followUp = await NewAsync(parent.TypeId, cancellationToken);

        if (followUp is null)
        {
            return WorkOrderResult.Fail(S["The work order's type doesn't exist anymore."]);
        }

        followUp.Summary = string.IsNullOrWhiteSpace(summary) ? S["Follow-up to {0}", parent.Number] : summary.Trim();
        followUp.Description = parent.Description;
        followUp.CustomerId = parent.CustomerId;
        followUp.CompanyId = parent.CompanyId;
        followUp.ContactId = parent.ContactId;
        followUp.Contact = parent.Contact;
        followUp.ServiceAddress = parent.ServiceAddress;
        followUp.AccessNotes = parent.AccessNotes;
        followUp.AssetIds = [.. parent.AssetIds];
        followUp.ParentWorkOrderId = parent.ItemId;
        followUp.Source = parent.Source;
        followUp.SourceId = parent.SourceId;

        return await CreateAsync(followUp, actor, cancellationToken);
    }

    /// <summary>
    /// Gives a work order by its id.
    /// </summary>
    /// <param name="id">The work order.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The work order, or <see langword="null"/>.</returns>
    public ValueTask<WorkOrder> FindAsync(string id, CancellationToken cancellationToken = default) => _store.FindByIdAsync(id, cancellationToken);

    /// <summary>
    /// Gives what a work order bills: its fixed price, or its billable lines at the quantities used (the estimate for
    /// services, labor and expenses with nothing recorded yet). Pure.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="type">Its type.</param>
    /// <returns>The amount.</returns>
    public static decimal BillableAmount(WorkOrder workOrder, WorkOrderType type)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(type);

        if (workOrder.AgreedPrice is { } agreed)
        {
            return agreed;
        }

        return type.BillingMode switch
        {
            WorkOrderBillingMode.FixedPrice => type.FixedPrice ?? 0,
            WorkOrderBillingMode.TimeAndMaterials => workOrder.Lines.Where(line => line.Billable).Sum(line => line.UnitPrice * (line.QuantityUsed > 0 || line.Kind == WorkOrderLineKind.Product ? line.QuantityUsed : line.QuantityEstimated)),
            _ => 0,
        };
    }

    /// <summary>
    /// Tells whether a question shows, given the answers so far. Pure.
    /// </summary>
    /// <param name="checklist">The checklist.</param>
    /// <param name="question">The question.</param>
    /// <returns><see langword="true"/> when it shows.</returns>
    public static bool IsShown(WorkOrderChecklist checklist, ChecklistQuestion question)
    {
        ArgumentNullException.ThrowIfNull(checklist);
        ArgumentNullException.ThrowIfNull(question);

        if (question.ShowWhen is null || string.IsNullOrEmpty(question.ShowWhen.QuestionId))
        {
            return true;
        }

        var answer = checklist.Answers.FirstOrDefault(item => item.QuestionId == question.ShowWhen.QuestionId)?.Value;

        return string.Equals(answer, question.ShowWhen.EqualsValue, StringComparison.OrdinalIgnoreCase);
    }

    private string Check(ChecklistQuestion question, string value)
    {
        switch (question.Kind)
        {
            case ChecklistQuestionKind.YesNo when value is not ("Yes" or "No"):
                return S["Answer \"{0}\" with Yes or No.", question.Text];
            case ChecklistQuestionKind.PassFailNA when value is not ("Pass" or "Fail" or "N/A"):
                return S["Answer \"{0}\" with Pass, Fail or N/A.", question.Text];
            case ChecklistQuestionKind.Number or ChecklistQuestionKind.MeterReading:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    return S["Answer \"{0}\" with a number.", question.Text];
                }

                if (number < question.Min || number > question.Max)
                {
                    return S["\"{0}\" must be from {1} to {2}.", question.Text, question.Min?.ToString(CultureInfo.CurrentCulture) ?? "…", question.Max?.ToString(CultureInfo.CurrentCulture) ?? "…"];
                }

                break;
            case ChecklistQuestionKind.Choice when !question.Choices.Contains(value, StringComparer.OrdinalIgnoreCase):
                return S["Choose an answer to \"{0}\" from its list.", question.Text];
            case ChecklistQuestionKind.MultiChoice when value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(item => !question.Choices.Contains(item, StringComparer.OrdinalIgnoreCase)):
                return S["Choose answers to \"{0}\" from its list.", question.Text];
            case ChecklistQuestionKind.Date when !DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _):
                return S["Answer \"{0}\" with a date.", question.Text];
        }

        return null;
    }

    private async Task MoveAsync(WorkOrder workOrder, WorkOrderStatus status, WorkOrderSubStatus subStatus, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var policy = string.IsNullOrEmpty(workOrder.Sla.PolicyId) ? null : await _policies.FindByIdAsync(workOrder.Sla.PolicyId, cancellationToken);
        var pauses = policy is not null && (policy.PauseStatuses.Contains(status) || subStatus?.PausesSla == true);

        if (workOrder.Sla.FirstResponseUtc is null && WorkOrderStatusRules.IsResponse(status))
        {
            workOrder.Sla.FirstResponseUtc = now;
        }

        // The clock stops while the work order waits, and its open targets move by the time it stopped.
        if (workOrder.Sla.PausedSinceUtc is { } since && !pauses)
        {
            var stopped = now - since;

            workOrder.Sla.PausedSeconds += (long)stopped.TotalSeconds;
            workOrder.Sla.PausedSinceUtc = null;

            if (workOrder.Sla.FirstResponseUtc is null && workOrder.Sla.RespondByUtc is { } respondBy)
            {
                workOrder.Sla.RespondByUtc = respondBy + stopped;
            }

            if (workOrder.Sla.ResolveByUtc is { } resolveBy)
            {
                workOrder.Sla.ResolveByUtc = resolveBy + stopped;
            }
        }
        else if (pauses && workOrder.Sla.PausedSinceUtc is null)
        {
            workOrder.Sla.PausedSinceUtc = now;
        }

        workOrder.Status = status;
        workOrder.SubStatusId = subStatus?.ItemId;
    }

    private async Task ScheduleSlaAsync(WorkOrder workOrder, DateTime now, CancellationToken cancellationToken)
    {
        var policy = string.IsNullOrEmpty(workOrder.Sla?.PolicyId) ? null : await _policies.FindByIdAsync(workOrder.Sla.PolicyId, cancellationToken);
        var target = SlaClock.TargetOf(policy, workOrder.Priority);

        workOrder.Sla ??= new WorkOrderSla();
        workOrder.Sla.RespondByUtc = target?.ResponseMinutes > 0 ? await _slaClock.DueAsync(now, target.ResponseMinutes, policy, cancellationToken) : null;
        workOrder.Sla.ResolveByUtc = target?.ResolveMinutes > 0 ? await _slaClock.DueAsync(now, target.ResolveMinutes, policy, cancellationToken) : null;
    }

    private async Task<IReadOnlyList<string>> ValidateAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(workOrder.Summary))
        {
            errors.Add(S["Say what the work is."]);
        }

        if (string.IsNullOrEmpty(workOrder.TypeId) || await _types.FindByIdAsync(workOrder.TypeId, cancellationToken) is null)
        {
            errors.Add(S["Choose the work order's type."]);
        }

        if (workOrder.EarliestUtc > workOrder.LatestUtc)
        {
            errors.Add(S["The latest visit can't come before the earliest."]);
        }

        if (workOrder.Lines.Any(line => string.IsNullOrWhiteSpace(line.Description) || line.QuantityEstimated < 0 || line.UnitPrice < 0))
        {
            errors.Add(S["Each line needs a description, and its quantity and price can't be negative."]);
        }

        return errors;
    }

    private async Task<string> NextNumberAsync(CancellationToken cancellationToken)
    {
        var numbers = await _session.QueryIndex<WorkOrderIndex>(index => index.Number.StartsWith(WorkOrdersConstants.NumberPrefix), collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
        var highest = numbers
            .Select(index => int.TryParse(index.Number.AsSpan(WorkOrdersConstants.NumberPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return string.Create(CultureInfo.InvariantCulture, $"{WorkOrdersConstants.NumberPrefix}{highest + 1:000000}");
    }

    private async Task NotifyAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            await observer.OnEventAsync(workOrder, eventName, detail ?? new Dictionary<string, object>(), cancellationToken);
        }
    }

    private void Note(WorkOrder workOrder, string actor, string text)
        => workOrder.Notes.Add(new WorkOrderNote { Utc = _clock.UtcNow, By = actor, Text = text });

    private static string Label(WorkOrderStatus status, WorkOrderSubStatus subStatus)
        => subStatus is null ? status.Text() : $"{status.Text()} ({subStatus.Name})";

    private static WorkOrderLine Copy(WorkOrderLine line) => new()
    {
        Id = CrestApps.Core.UniqueId.GenerateId(),
        Kind = line.Kind,
        ProductId = line.ProductId,
        StockItemId = line.StockItemId,
        Description = line.Description,
        QuantityEstimated = line.QuantityEstimated,
        UnitPrice = line.UnitPrice,
        Billable = line.Billable,
    };
}
