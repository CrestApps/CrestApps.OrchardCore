using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;
using CrestApps.OrchardCore.Reports.Designer.Workflows.ViewModels;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Drivers;

/// <summary>
/// The editor of a report event: the reports that start the workflow, all of them when none is chosen.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public sealed class ReportEventDisplayDriver<TEvent> : ActivityDisplayDriver<TEvent, ReportEventViewModel>
    where TEvent : ReportEventActivity
{
    private readonly ReportDesignService _designService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportEventDisplayDriver{TEvent}"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    public ReportEventDisplayDriver(ReportDesignService designService)
    {
        _designService = designService;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(TEvent activity, ReportEventViewModel model)
    {
        model.ReportIds = activity.ReportIds ?? [];
        model.Reports = await ReportWorkflowDisplay.ReportsAsync(_designService);
    }

    /// <inheritdoc/>
    protected override void UpdateActivity(ReportEventViewModel model, TEvent activity)
    {
        activity.ReportIds = (model.ReportIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// The editor of a report task: the report, its filters and the output name, then the task's own fields.
/// </summary>
/// <typeparam name="TTask">The task type.</typeparam>
/// <typeparam name="TModel">The view model of the task.</typeparam>
public abstract class ReportTaskDisplayDriver<TTask, TModel> : ActivityDisplayDriver<TTask, TModel>
    where TTask : ReportTaskActivity<TTask>, ITask
    where TModel : ReportTaskViewModel, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportTaskDisplayDriver{TTask, TModel}"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager, to validate expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected ReportTaskDisplayDriver(
        ReportDesignService designService,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer stringLocalizer)
    {
        DesignService = designService;
        LiquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the designed reports.
    /// </summary>
    protected ReportDesignService DesignService { get; }

    /// <summary>
    /// Gets the Liquid template manager.
    /// </summary>
    protected ILiquidTemplateManager LiquidTemplateManager { get; }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <summary>
    /// Gets the output name the task uses when none is set.
    /// </summary>
    protected abstract string DefaultOutputName { get; }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(TTask activity, TModel model)
    {
        model.ReportId = activity.ReportId;
        model.ReportIdExpression = activity.ReportIdExpression;
        model.Filters = activity.Filters;
        model.OutputName = activity.OutputName;
        model.DefaultOutputName = DefaultOutputName;
        model.HasOutput = !string.IsNullOrEmpty(DefaultOutputName);
        model.Reports = await ReportWorkflowDisplay.ReportsAsync(DesignService);

        await EditTaskAsync(activity, model);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TTask activity, UpdateEditorContext context)
    {
        var model = new TModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ReportId) && string.IsNullOrWhiteSpace(model.ReportIdExpression))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ReportId), S["Choose a report, or enter a Liquid expression that gives its ID."]);
        }

        ValidateLiquid(model.ReportIdExpression, nameof(model.ReportIdExpression), context);
        ValidateLiquid(model.Filters, nameof(model.Filters), context);

        activity.ReportId = model.ReportId?.Trim();
        activity.ReportIdExpression = model.ReportIdExpression?.Trim();
        activity.Filters = model.Filters;
        activity.OutputName = model.OutputName?.Trim();

        UpdateTask(model, activity, context);

        return Edit(activity, context);
    }

    /// <summary>
    /// Fills the task's own fields of the editor.
    /// </summary>
    /// <param name="activity">The task.</param>
    /// <param name="model">The editor.</param>
    /// <returns>A task that completes when the editor is filled.</returns>
    protected abstract ValueTask EditTaskAsync(TTask activity, TModel model);

    /// <summary>
    /// Validates the task's own fields and stores them.
    /// </summary>
    /// <param name="model">The editor.</param>
    /// <param name="activity">The task.</param>
    /// <param name="context">The update context, for validation errors.</param>
    protected abstract void UpdateTask(TModel model, TTask activity, UpdateEditorContext context);

    /// <summary>
    /// Adds an error when a value is not a valid Liquid template.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="member">The name of the field.</param>
    /// <param name="context">The update context.</param>
    protected void ValidateLiquid(string value, string member, UpdateEditorContext context)
    {
        if (!string.IsNullOrWhiteSpace(value) && !LiquidTemplateManager.Validate(value, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, member, S["The Liquid expression is invalid."]);
        }
    }
}

/// <summary>
/// The editor of the Run Report task.
/// </summary>
public sealed class RunReportTaskDisplayDriver : ReportTaskDisplayDriver<RunReportTask, RunReportTaskViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RunReportTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RunReportTaskDisplayDriver(
        ReportDesignService designService,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<RunReportTaskDisplayDriver> stringLocalizer)
        : base(designService, liquidTemplateManager, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override string DefaultOutputName => RunReportTask.DefaultOutputName;

    /// <inheritdoc/>
    protected override ValueTask EditTaskAsync(RunReportTask activity, RunReportTaskViewModel model) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    protected override void UpdateTask(RunReportTaskViewModel model, RunReportTask activity, UpdateEditorContext context)
    {
    }
}

/// <summary>
/// The editor of the Export Report task.
/// </summary>
public sealed class ExportReportTaskDisplayDriver : ReportTaskDisplayDriver<ExportReportTask, ExportReportTaskViewModel>
{
    private readonly IReportExportManager _exportManager;
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportReportTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    /// <param name="exportManager">The export formats.</param>
    /// <param name="localizerFactory">The localizer factory, for the names of the export formats.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExportReportTaskDisplayDriver(
        ReportDesignService designService,
        IReportExportManager exportManager,
        IStringLocalizerFactory localizerFactory,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<ExportReportTaskDisplayDriver> stringLocalizer)
        : base(designService, liquidTemplateManager, stringLocalizer)
    {
        _exportManager = exportManager;
        _localizerFactory = localizerFactory;
    }

    /// <inheritdoc/>
    protected override string DefaultOutputName => ExportReportTask.DefaultOutputName;

    /// <inheritdoc/>
    protected override ValueTask EditTaskAsync(ExportReportTask activity, ExportReportTaskViewModel model)
    {
        model.Format = activity.Format;
        model.MediaFolder = activity.MediaFolder;
        model.Formats = ReportWorkflowDisplay.Formats(_exportManager, _localizerFactory);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void UpdateTask(ExportReportTaskViewModel model, ExportReportTask activity, UpdateEditorContext context)
    {
        if (_exportManager.GetDescriptor(model.Format) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Format), S["Choose an export format."]);
        }

        ValidateLiquid(model.MediaFolder, nameof(model.MediaFolder), context);

        activity.Format = model.Format;
        activity.MediaFolder = model.MediaFolder?.Trim();
    }
}

/// <summary>
/// The editor of the Email Report task.
/// </summary>
public sealed class EmailReportTaskDisplayDriver : ReportTaskDisplayDriver<EmailReportTask, EmailReportTaskViewModel>
{
    private readonly IReportExportManager _exportManager;
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailReportTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    /// <param name="exportManager">The export formats.</param>
    /// <param name="localizerFactory">The localizer factory, for the names of the export formats.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailReportTaskDisplayDriver(
        ReportDesignService designService,
        IReportExportManager exportManager,
        IStringLocalizerFactory localizerFactory,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<EmailReportTaskDisplayDriver> stringLocalizer)
        : base(designService, liquidTemplateManager, stringLocalizer)
    {
        _exportManager = exportManager;
        _localizerFactory = localizerFactory;
    }

    /// <inheritdoc/>
    protected override string DefaultOutputName => null;

    /// <inheritdoc/>
    protected override ValueTask EditTaskAsync(EmailReportTask activity, EmailReportTaskViewModel model)
    {
        model.To = activity.To;
        model.Cc = activity.Cc;
        model.Bcc = activity.Bcc;
        model.Subject = activity.Subject;
        model.Body = activity.Body;
        model.AttachmentFormat = activity.AttachmentFormat;
        model.IncludeReport = activity.IncludeReport;
        model.Formats = ReportWorkflowDisplay.Formats(_exportManager, _localizerFactory);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void UpdateTask(EmailReportTaskViewModel model, EmailReportTask activity, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(model.To))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.To), S["Enter at least one recipient."]);
        }

        if (!string.IsNullOrWhiteSpace(model.AttachmentFormat) && _exportManager.GetDescriptor(model.AttachmentFormat) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AttachmentFormat), S["Choose an attachment format."]);
        }

        if (string.IsNullOrWhiteSpace(model.AttachmentFormat) && !model.IncludeReport)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IncludeReport), S["Attach the report, show it in the message, or both."]);
        }

        ValidateLiquid(model.To, nameof(model.To), context);
        ValidateLiquid(model.Cc, nameof(model.Cc), context);
        ValidateLiquid(model.Bcc, nameof(model.Bcc), context);
        ValidateLiquid(model.Subject, nameof(model.Subject), context);
        ValidateLiquid(model.Body, nameof(model.Body), context);

        activity.To = model.To?.Trim();
        activity.Cc = model.Cc?.Trim();
        activity.Bcc = model.Bcc?.Trim();
        activity.Subject = model.Subject?.Trim();
        activity.Body = model.Body;
        activity.AttachmentFormat = model.AttachmentFormat ?? string.Empty;
        activity.IncludeReport = model.IncludeReport;
    }
}

/// <summary>
/// The editor of the Create Report Share Link task.
/// </summary>
public sealed class CreateReportShareLinkTaskDisplayDriver : ReportTaskDisplayDriver<CreateReportShareLinkTask, CreateReportShareLinkTaskViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateReportShareLinkTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="designService">The designed reports.</param>
    /// <param name="liquidTemplateManager">The Liquid template manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateReportShareLinkTaskDisplayDriver(
        ReportDesignService designService,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<CreateReportShareLinkTaskDisplayDriver> stringLocalizer)
        : base(designService, liquidTemplateManager, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    protected override string DefaultOutputName => CreateReportShareLinkTask.DefaultOutputName;

    /// <inheritdoc/>
    protected override ValueTask EditTaskAsync(CreateReportShareLinkTask activity, CreateReportShareLinkTaskViewModel model)
    {
        model.Recipients = activity.Recipients;
        model.Note = activity.Note;
        model.ExpiresAfterDays = activity.ExpiresAfterDays;
        model.MaxUses = activity.MaxUses;
        model.AllowExport = activity.AllowExport;
        model.NotifyByEmail = activity.NotifyByEmail;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void UpdateTask(CreateReportShareLinkTaskViewModel model, CreateReportShareLinkTask activity, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(model.Recipients))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Recipients), S["Enter the user names or emails of the people the link is for."]);
        }

        if (model.ExpiresAfterDays < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ExpiresAfterDays), S["Enter the number of days the link works, 1 at least."]);
        }

        if (model.MaxUses < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxUses), S["Enter 0 for no limit, or the most times the link may be used."]);
        }

        ValidateLiquid(model.Recipients, nameof(model.Recipients), context);
        ValidateLiquid(model.Note, nameof(model.Note), context);

        activity.Recipients = model.Recipients?.Trim();
        activity.Note = model.Note?.Trim();
        activity.ExpiresAfterDays = Math.Max(1, model.ExpiresAfterDays);
        activity.MaxUses = Math.Max(0, model.MaxUses);
        activity.AllowExport = model.AllowExport;
        activity.NotifyByEmail = model.NotifyByEmail;
    }
}

internal static class ReportWorkflowDisplay
{
    public static async Task<IList<SelectListItem>> ReportsAsync(ReportDesignService designService)
    {
        return (await designService.GetAllAsync())
            .OrderBy(design => design.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .Select(design => new SelectListItem(design.DisplayText, design.ItemId))
            .ToList();
    }

    public static IList<SelectListItem> Formats(IReportExportManager exportManager, IStringLocalizerFactory localizerFactory)
    {
        return exportManager.GetFormats()
            .Select(format => new SelectListItem(localizerFactory.Localize(format.DisplayName).Value, format.Name))
            .ToList();
    }
}
