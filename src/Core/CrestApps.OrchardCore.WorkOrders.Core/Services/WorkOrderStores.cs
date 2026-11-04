using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Stores work orders; two changes to one work order at once can't both be kept.
/// </summary>
public sealed class WorkOrderStore : DocumentCatalog<WorkOrder, WorkOrderIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public WorkOrderStore(ISession session)
        : base(session)
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;
}

/// <summary>
/// Stores one kind of work order settings record.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
/// <typeparam name="TIndex">The record type's index.</typeparam>
public sealed class WorkOrderSettingStore<T, TIndex> : DocumentCatalog<T, TIndex>
    where T : CatalogItem, new()
    where TIndex : WorkOrderSettingIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSettingStore{T, TIndex}"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public WorkOrderSettingStore(ISession session)
        : base(session)
    {
        CollectionName = WorkOrdersConstants.CollectionName;
    }
}

/// <summary>
/// Manages one kind of work order settings record.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public sealed class WorkOrderSettingManager<T> : CatalogManager<T>
    where T : CatalogItem, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSettingManager{T}"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public WorkOrderSettingManager(ICatalog<T> store, IEnumerable<ICatalogEntryHandler<T>> handlers, ILogger<CatalogManager<T>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Checks and stamps work order types.
/// </summary>
internal sealed class WorkOrderTypeHandler : CatalogEntryHandlerBase<WorkOrderType>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderTypeHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderTypeHandler(IClock clock, IStringLocalizer<WorkOrderTypeHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<WorkOrderType> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<WorkOrderType> context, CancellationToken cancellationToken = default)
    {
        var type = context.Model;

        if (string.IsNullOrWhiteSpace(type.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(WorkOrderType.Name)]));
        }

        if (type.DefaultDurationMinutes < 1 || type.CrewSize < 1 || type.RequiredPhotos < 0)
        {
            context.Result.Fail(new ValidationResult(S["The duration and crew size must be at least 1."], [nameof(WorkOrderType.DefaultDurationMinutes)]));
        }

        if (type.BillingMode == WorkOrderBillingMode.FixedPrice && type.FixedPrice is not > 0)
        {
            context.Result.Fail(new ValidationResult(S["Enter the fixed price."], [nameof(WorkOrderType.FixedPrice)]));
        }

        foreach (var line in type.DefaultLines)
        {
            line.Id ??= CrestApps.Core.UniqueId.GenerateId();
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Checks and stamps checklist templates.
/// </summary>
internal sealed class ChecklistTemplateHandler : CatalogEntryHandlerBase<ChecklistTemplate>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecklistTemplateHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChecklistTemplateHandler(IClock clock, IStringLocalizer<ChecklistTemplateHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ChecklistTemplate> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ChecklistTemplate> context, CancellationToken cancellationToken = default)
    {
        var template = context.Model;

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ChecklistTemplate.Name)]));
        }

        template.Questions = [.. template.Questions.Where(question => !string.IsNullOrWhiteSpace(question?.Text))];

        foreach (var question in template.Questions)
        {
            question.Id ??= CrestApps.Core.UniqueId.GenerateId();
            question.Choices = [.. question.Choices.Where(choice => !string.IsNullOrWhiteSpace(choice)).Select(choice => choice.Trim())];

            if (question.Kind is ChecklistQuestionKind.Choice or ChecklistQuestionKind.MultiChoice && question.Choices.Count < 2)
            {
                context.Result.Fail(new ValidationResult(S["The question '{0}' needs at least two choices.", question.Text], [nameof(ChecklistTemplate.Questions)]));
            }

            if (question.Min > question.Max)
            {
                context.Result.Fail(new ValidationResult(S["The question '{0}' has a lowest number above its highest.", question.Text], [nameof(ChecklistTemplate.Questions)]));
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Checks and stamps service level policies.
/// </summary>
internal sealed class ServiceLevelPolicyHandler : CatalogEntryHandlerBase<ServiceLevelPolicy>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelPolicyHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceLevelPolicyHandler(IClock clock, IStringLocalizer<ServiceLevelPolicyHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        // A new policy stops the clock while work orders are on hold.
        context.Model.PauseStatuses = [WorkOrderStatus.OnHold];

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ServiceLevelPolicy.Name)]));
        }

        if (context.Model.WarningPercent is < 1 or > 99)
        {
            context.Result.Fail(new ValidationResult(S["The warning must come between 1 and 99 percent of the way."], [nameof(ServiceLevelPolicy.WarningPercent)]));
        }

        if (context.Model.Targets.Any(target => target.ResponseMinutes < 0 || target.ResolveMinutes < 0))
        {
            context.Result.Fail(new ValidationResult(S["Targets can't be negative."], [nameof(ServiceLevelPolicy.Targets)]));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Checks sub-statuses.
/// </summary>
internal sealed class WorkOrderSubStatusHandler : CatalogEntryHandlerBase<WorkOrderSubStatus>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSubStatusHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrderSubStatusHandler(IStringLocalizer<WorkOrderSubStatusHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<WorkOrderSubStatus> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(WorkOrderSubStatus.Name)]));
        }

        return Task.CompletedTask;
    }
}
