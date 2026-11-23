using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Localization;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Indexes e-file submissions.
/// </summary>
public sealed class EFileSubmissionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the kind of document filed.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the document filed.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the channel is asked for the agency's answer: the submission was sent
    /// through a channel that is not manual and has no answer yet.
    /// </summary>
    public bool AwaitsAnswer { get; set; }

    /// <summary>
    /// Gets or sets when the channel was last asked for the answer.
    /// </summary>
    public DateTime? LastCheckedUtc { get; set; }
}

/// <summary>
/// Maps e-file submissions to <see cref="EFileSubmissionIndex"/> rows.
/// </summary>
public sealed class EFileSubmissionIndexProvider : IndexProvider<EFileSubmission>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EFileSubmissionIndexProvider"/> class.
    /// </summary>
    public EFileSubmissionIndexProvider()
    {
        CollectionName = TaxationConstants.TaxReturnsCollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<EFileSubmission> context)
        => context
            .For<EFileSubmissionIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(submission => new EFileSubmissionIndex
            {
                ItemId = submission.ItemId,
                TargetType = submission.TargetType,
                TargetId = submission.TargetId,
                Status = submission.Status.ToString(),
                AwaitsAnswer = submission.Status == EFileSubmissionStatus.Submitted && !submission.IsManual,
                LastCheckedUtc = submission.LastCheckedUtc,
            });
}

/// <summary>
/// Creates the table of e-file submissions.
/// </summary>
public static class EFileSubmissionIndexSchema
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<EFileSubmissionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TargetType", column => column.Nullable().WithLength(50))
            .Column<string>("TargetId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<bool>("AwaitsAnswer")
            .Column<DateTime>("LastCheckedUtc", column => column.Nullable()),
            collection: TaxationConstants.TaxReturnsCollectionName);

        await schemaBuilder.AlterIndexTableAsync<EFileSubmissionIndex>(table => table
            .CreateIndex("IDX_EFileSubmissionIndex_Target", "TargetType", "TargetId"),
            collection: TaxationConstants.TaxReturnsCollectionName);

        await schemaBuilder.AlterIndexTableAsync<EFileSubmissionIndex>(table => table
            .CreateIndex("IDX_EFileSubmissionIndex_AwaitsAnswer", "AwaitsAnswer", "LastCheckedUtc"),
            collection: TaxationConstants.TaxReturnsCollectionName);
    }
}

/// <summary>
/// Stores e-file submissions in the Tax Returns collection.
/// </summary>
public sealed class EFileSubmissionStore : DocumentCatalog<EFileSubmission, EFileSubmissionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EFileSubmissionStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public EFileSubmissionStore(ISession session)
        : base(session)
    {
        CollectionName = TaxationConstants.TaxReturnsCollectionName;
    }

    /// <summary>
    /// Lists the submissions of a document.
    /// </summary>
    /// <param name="targetType">The kind of document.</param>
    /// <param name="targetId">The document.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions.</returns>
    public async Task<IReadOnlyList<EFileSubmission>> ListAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
        => [.. await Session.Query<EFileSubmission, EFileSubmissionIndex>(index => index.TargetType == targetType && index.TargetId == targetId, collection: CollectionName).ListAsync(cancellationToken)];

    /// <summary>
    /// Lists the submissions waiting for the agency's answer, the ones asked longest ago first.
    /// </summary>
    /// <param name="count">The most to list.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The submissions.</returns>
    public async Task<IReadOnlyList<EFileSubmission>> ListPendingAsync(int count, CancellationToken cancellationToken = default)
        => [.. await Session.Query<EFileSubmission, EFileSubmissionIndex>(index => index.AwaitsAnswer, collection: CollectionName)
            .OrderBy(index => index.LastCheckedUtc)
            .Take(count)
            .ListAsync(cancellationToken)];
}

/// <summary>
/// The default <see cref="IEFileSubmissionManager"/>.
/// </summary>
public sealed class EFileSubmissionManager : CatalogManager<EFileSubmission>, IEFileSubmissionManager
{
    private readonly EFileSubmissionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="EFileSubmissionManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public EFileSubmissionManager(EFileSubmissionStore store, IEnumerable<ICatalogEntryHandler<EFileSubmission>> handlers, ILogger<CatalogManager<EFileSubmission>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EFileSubmission>> ListAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
        => [.. (await _store.ListAsync(targetType, targetId, cancellationToken)).OrderByDescending(submission => submission.CreatedUtc)];

    /// <inheritdoc />
    public Task<IReadOnlyList<EFileSubmission>> ListPendingAsync(int count, CancellationToken cancellationToken = default)
        => _store.ListPendingAsync(count, cancellationToken);
}

/// <summary>
/// Stamps e-file submissions when they are added and changed.
/// </summary>
public sealed class EFileSubmissionHandler : CatalogEntryHandlerBase<EFileSubmission>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EFileSubmissionHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public EFileSubmissionHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<EFileSubmission> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<EFileSubmission> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<EFileSubmission> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}

/// <summary>
/// The default <see cref="IEFilingService"/>.
/// </summary>
public sealed class EFilingService : IEFilingService
{
    private readonly IEFileSubmissionManager _submissions;
    private readonly IEnumerable<IEFileTargetProvider> _targets;
    private readonly IEnumerable<IEFileFormat> _formats;
    private readonly IEnumerable<IEFileTransport> _transports;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EFilingService"/> class.
    /// </summary>
    /// <param name="submissions">The submissions.</param>
    /// <param name="targets">The target providers.</param>
    /// <param name="formats">The formats.</param>
    /// <param name="transports">The filing channels.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EFilingService(
        IEFileSubmissionManager submissions,
        IEnumerable<IEFileTargetProvider> targets,
        IEnumerable<IEFileFormat> formats,
        IEnumerable<IEFileTransport> transports,
        IClock clock,
        ILogger<EFilingService> logger,
        IStringLocalizer<EFilingService> stringLocalizer)
    {
        _submissions = submissions;
        _targets = targets;
        _formats = formats;
        _transports = transports;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<EFileTarget> FindTargetAsync(string targetType, string targetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(targetType) || string.IsNullOrEmpty(targetId) || ProviderOf(targetType) is not { } provider)
        {
            return null;
        }

        return await provider.FindAsync(targetId, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<(IEFileFormat Format, IEFileTransport Transport)> GetOptions(EFileTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return [.. _formats
            .Where(format => format.CanWrite(target))
            .SelectMany(format => _transports
                .Where(transport => transport.CanSend(target, format.Name))
                .Select(transport => (format, transport)))];
    }

    /// <inheritdoc />
    public IEFileTransport GetTransport(string name)
        => _transports.FirstOrDefault(transport => transport.Name == name);

    /// <inheritdoc />
    public async Task<EFilingResult> SubmitAsync(EFileTarget target, string format, string transport, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var result = new EFilingResult();

        if (!string.IsNullOrEmpty(target.NotFileableReason))
        {
            result.Errors.Add(target.NotFileableReason);

            return result;
        }

        var option = GetOptions(target).FirstOrDefault(candidate => candidate.Format.Name == format && candidate.Transport.Name == transport);

        if (option.Format is null)
        {
            result.Errors.Add(S["The format and the filing channel chosen are not available for this document."]);

            return result;
        }

        var earlier = await _submissions.ListAsync(target.Type, target.Id, cancellationToken);

        if (earlier.Any(submission => submission.Status == EFileSubmissionStatus.Accepted))
        {
            result.Errors.Add(S["The document was accepted already."]);

            return result;
        }

        if (earlier.Any(submission => submission.Status == EFileSubmissionStatus.Submitted))
        {
            result.Errors.Add(S["A submission of the document is waiting for the agency's answer: record it, or wait for it, first."]);

            return result;
        }

        var payload = await option.Format.WriteAsync(target, cancellationToken);

        if (payload.Problems.Count > 0)
        {
            foreach (var problem in payload.Problems)
            {
                result.Errors.Add(problem);
            }

            return result;
        }

        var submission = await _submissions.NewAsync(cancellationToken: cancellationToken);

        submission.TargetType = target.Type;
        submission.TargetId = target.Id;
        submission.TargetDisplayText = target.DisplayText;
        submission.Format = option.Format.Name;
        submission.Transport = option.Transport.Name;
        submission.IsManual = option.Transport.IsManual;
        submission.Status = EFileSubmissionStatus.Created;
        submission.FileName = payload.FileName;
        submission.ContentType = payload.ContentType;
        submission.Content = Convert.ToBase64String(payload.Content ?? []);
        submission.SubmittedBy = user?.Identity?.IsAuthenticated == true ? user.Identity.Name : null;

        EFileTransportResult sent;

        try
        {
            sent = await option.Transport.SendAsync(submission, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The filing channel '{Transport}' failed to send the submission of '{TargetType}' '{TargetId}'.", option.Transport.Name, target.Type, target.Id);

            sent = new EFileTransportResult { Status = EFileSubmissionStatus.Failed };
            sent.Messages.Add(S["The filing channel could not send the file. Try again later."]);
        }

        submission.SubmittedUtc = sent.Status == EFileSubmissionStatus.Failed ? null : _clock.UtcNow;
        result.Submission = submission;

        var errors = await ApplyAsync(submission, target, sent, user, cancellationToken);

        await _submissions.CreateAsync(submission, cancellationToken);

        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<EFilingResult> CheckAsync(EFileSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var result = new EFilingResult { Submission = submission };

        if (submission.Status != EFileSubmissionStatus.Submitted || submission.IsManual)
        {
            result.Errors.Add(S["The submission is not waiting for an answer from its filing channel."]);

            return result;
        }

        var transport = GetTransport(submission.Transport);

        if (transport is null)
        {
            result.Errors.Add(S["The filing channel {0} is not available: turn on the feature that adds it.", submission.Transport ?? string.Empty]);

            return result;
        }

        EFileTransportResult answer;

        try
        {
            answer = await transport.CheckAsync(submission, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The filing channel '{Transport}' failed to check the submission '{SubmissionId}'.", transport.Name, submission.ItemId);

            answer = new EFileTransportResult { Status = EFileSubmissionStatus.Submitted };
        }

        submission.LastCheckedUtc = _clock.UtcNow;

        var target = answer.Status == EFileSubmissionStatus.Accepted
            ? await FindTargetAsync(submission.TargetType, submission.TargetId, cancellationToken)
            : null;
        var errors = await ApplyAsync(submission, target, answer, user: null, cancellationToken);

        await _submissions.UpdateAsync(submission, cancellationToken: cancellationToken);

        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<EFilingResult> RecordAnswerAsync(EFileSubmission submission, bool accepted, string confirmationNumber, DateOnly filedOn, string message, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var result = new EFilingResult { Submission = submission };

        if (submission.Status != EFileSubmissionStatus.Submitted)
        {
            result.Errors.Add(S["The submission is not waiting for an answer."]);

            return result;
        }

        if (accepted && filedOn == default)
        {
            result.Errors.Add(S["Give the day the document was filed."]);

            return result;
        }

        var answer = new EFileTransportResult
        {
            Status = accepted ? EFileSubmissionStatus.Accepted : EFileSubmissionStatus.Rejected,
            ConfirmationNumber = string.IsNullOrWhiteSpace(confirmationNumber) ? null : confirmationNumber.Trim(),
            FiledOn = accepted ? filedOn : null,
        };

        if (!string.IsNullOrWhiteSpace(message))
        {
            answer.Messages.Add(message.Trim());
        }

        var target = accepted ? await FindTargetAsync(submission.TargetType, submission.TargetId, cancellationToken) : null;
        var errors = await ApplyAsync(submission, target, answer, user, cancellationToken);

        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }

        if (result.Succeeded)
        {
            await _submissions.UpdateAsync(submission, cancellationToken: cancellationToken);
        }

        return result;
    }

    private IEFileTargetProvider ProviderOf(string targetType)
        => _targets.FirstOrDefault(provider => provider.TargetType == targetType);

    // Records what the channel or the person said; an accepted document is marked as filed through its provider, and
    // stays submitted when that fails, so the acceptance can be recorded again.
    private async Task<IReadOnlyList<string>> ApplyAsync(EFileSubmission submission, EFileTarget target, EFileTransportResult answer, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        foreach (var message in answer.Messages)
        {
            submission.Messages.Add(message);
        }

        submission.ExternalId = answer.ExternalId ?? submission.ExternalId;

        if (answer.Status == EFileSubmissionStatus.Accepted)
        {
            if (target is null || ProviderOf(submission.TargetType) is not { } provider)
            {
                submission.Status = EFileSubmissionStatus.Submitted;

                return [S["The document filed was not found, so it was not marked as filed."]];
            }

            submission.ConfirmationNumber = answer.ConfirmationNumber ?? submission.ConfirmationNumber;

            var filedOn = answer.FiledOn ?? DateOnly.FromDateTime(_clock.UtcNow);
            var errors = await provider.AcceptedAsync(target, submission, filedOn, user, cancellationToken);

            if (errors.Count > 0)
            {
                submission.Status = EFileSubmissionStatus.Submitted;

                return errors;
            }
        }

        submission.Status = answer.Status;

        if (answer.Status is EFileSubmissionStatus.Accepted or EFileSubmissionStatus.Rejected)
        {
            submission.AcknowledgedUtc = _clock.UtcNow;
        }

        return [];
    }
}

/// <summary>
/// The manual filing channel: it hands over the file to download and file on the agency's site, and the person records
/// the agency's answer and confirmation number.
/// </summary>
public sealed class ManualEFileTransport : IEFileTransport
{
    /// <summary>
    /// The stored name.
    /// </summary>
    public const string TransportName = "Manual";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<ManualEFileTransport>("Download and file by hand");

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManualEFileTransport"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ManualEFileTransport(IStringLocalizer<ManualEFileTransport> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name
        => TransportName;

    /// <inheritdoc />
    public LocalizationSource DisplayName
        => _displayName;

    /// <inheritdoc />
    public bool IsManual
        => true;

    /// <inheritdoc />
    public bool CanSend(EFileTarget target, string format)
        => true;

    /// <inheritdoc />
    public Task<EFileTransportResult> SendAsync(EFileSubmission submission, EFilePayload payload, CancellationToken cancellationToken = default)
    {
        var result = new EFileTransportResult { Status = EFileSubmissionStatus.Submitted };

        result.Messages.Add(S["Download the file, file it on the agency's site, then record the agency's answer."]);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<EFileTransportResult> CheckAsync(EFileSubmission submission, CancellationToken cancellationToken = default)
        => Task.FromResult(new EFileTransportResult { Status = EFileSubmissionStatus.Submitted });
}

/// <summary>
/// Tax returns as e-file targets: a return ready to file can be filed, and one the agency accepted is marked as filed.
/// </summary>
public sealed class TaxReturnEFileTargetProvider : IEFileTargetProvider
{
    private readonly ITaxReturnManager _returns;
    private readonly IServiceProvider _services;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnEFileTargetProvider"/> class.
    /// </summary>
    /// <param name="returns">The returns.</param>
    /// <param name="services">The services, for the return service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxReturnEFileTargetProvider(
        ITaxReturnManager returns,
        IServiceProvider services,
        IAuthorizationService authorizationService,
        IStringLocalizer<TaxReturnEFileTargetProvider> stringLocalizer)
    {
        _returns = returns;
        _services = services;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string TargetType
        => TaxationConstants.TaxReturnEFileTargetType;

    /// <inheritdoc />
    public async Task<EFileTarget> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id) || await _returns.FindByIdAsync(id, cancellationToken) is not { } taxReturn)
        {
            return null;
        }

        return new EFileTarget
        {
            Type = TargetType,
            Id = taxReturn.ItemId,
            DisplayText = string.IsNullOrEmpty(taxReturn.JurisdictionId)
                ? $"{taxReturn.Form} {taxReturn.PeriodKey}"
                : $"{taxReturn.Form} {taxReturn.PeriodKey} ({taxReturn.JurisdictionId})",
            JurisdictionId = taxReturn.JurisdictionId,
            Form = taxReturn.Form,
            NotFileableReason = taxReturn.Status switch
            {
                TaxReturnStatus.Draft => S["Mark the return ready to file first."],
                TaxReturnStatus.Ready => null,
                _ => S["The return is filed already."],
            },
            Document = taxReturn,
        };
    }

    /// <inheritdoc />
    public Task<bool> CanFileAsync(ClaimsPrincipal user, EFileTarget target)
        => _authorizationService.AuthorizeAsync(user, TaxationPermissions.FileTaxReturns);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> AcceptedAsync(EFileTarget target, EFileSubmission submission, DateOnly filedOn, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(submission);

        if (target.Document is not TaxReturn taxReturn)
        {
            return [S["The document filed is not a tax return."]];
        }

        var result = await _services.GetRequiredService<ITaxReturnService>().MarkFiledAsync(taxReturn, filedOn, submission.ConfirmationNumber, submission.Transport, user, cancellationToken);

        return [.. result.Errors];
    }
}

/// <summary>
/// Writes any tax return as a JSON file: its registration, period, boxes, amount due and lines. It suits an agency or an
/// accountant that takes the figures, and is the file the manual channel hands over until a jurisdiction's own format is
/// added.
/// </summary>
public sealed class TaxReturnJsonEFileFormat : IEFileFormat
{
    /// <summary>
    /// The stored name.
    /// </summary>
    public const string FormatName = "TaxReturnJson";

    private static readonly LocalizationSource _displayName = LocalizationSource.Create<TaxReturnJsonEFileFormat>("Tax return (JSON)");

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxReturnJsonEFileFormat"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxReturnJsonEFileFormat(IStringLocalizer<TaxReturnJsonEFileFormat> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name
        => FormatName;

    /// <inheritdoc />
    public LocalizationSource DisplayName
        => _displayName;

    /// <inheritdoc />
    public bool CanWrite(EFileTarget target)
        => target?.Document is TaxReturn;

    /// <inheritdoc />
    public Task<EFilePayload> WriteAsync(EFileTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var payload = new EFilePayload
        {
            ContentType = "application/json",
        };

        if (target.Document is not TaxReturn taxReturn)
        {
            payload.Problems.Add(S["The document is not a tax return."]);

            return Task.FromResult(payload);
        }

        if (string.IsNullOrWhiteSpace(taxReturn.RegistrationNumber))
        {
            payload.Problems.Add(S["The registration has no registration number: add the number the agency gave."]);
        }

        payload.FileName = $"{taxReturn.Form}-{taxReturn.PeriodKey}{(taxReturn.Amendment > 0 ? $"-a{taxReturn.Amendment}" : string.Empty)}.json";
        payload.Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            taxReturn.Form,
            Jurisdiction = taxReturn.JurisdictionId,
            taxReturn.TaxType,
            taxReturn.RegistrationNumber,
            Period = new
            {
                Key = taxReturn.PeriodKey,
                Start = taxReturn.PeriodStart,
                End = taxReturn.PeriodEnd,
            },
            taxReturn.DueDate,
            taxReturn.IsAnnualReconciliation,
            taxReturn.Amendment,
            taxReturn.Currency,
            taxReturn.Boxes,
            taxReturn.AmountDue,
            Lines = taxReturn.Lines.Select(line => new
            {
                line.Kind,
                Jurisdiction = line.JurisdictionId,
                line.TaxType,
                line.FormLine,
                line.Category,
                line.Description,
                line.Amount,
                line.TaxAmount,
            }),
        }, _options));

        return Task.FromResult(payload);
    }
}

/// <summary>
/// Registers E-Filing's services.
/// </summary>
public static class EFilingCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the submissions, the e-filing service, the manual channel, and tax returns as targets with their JSON
    /// format. Calling it twice adds nothing twice.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEFilingCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<EFileSubmissionStore>();
        services.TryAddScoped<IEFileSubmissionManager, EFileSubmissionManager>();
        services.TryAddScoped<ICatalogManager<EFileSubmission>>(serviceProvider => serviceProvider.GetRequiredService<IEFileSubmissionManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EFileSubmission>, EFileSubmissionHandler>());
        services.TryAddScoped<IEFilingService, EFilingService>();
        services.AddEFileTransport<ManualEFileTransport>();
        services.AddEFileTargetProvider<TaxReturnEFileTargetProvider>();
        services.AddEFileFormat<TaxReturnJsonEFileFormat>();

        return services;
    }
}
