using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Stores employers in the Payroll collection. An employer a module still uses is not deleted.
/// </summary>
public sealed class EmployerStore : DocumentCatalog<Employer, EmployerIndex>
{
    private readonly IEnumerable<IEmployerUsage> _usages;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployerStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="usages">What uses employers.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmployerStore(ISession session, IEnumerable<IEmployerUsage> usages, IStringLocalizer<EmployerStore> stringLocalizer)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
        _usages = usages;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the employers by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The employers.</returns>
    public async Task<IReadOnlyList<Employer>> ListAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<Employer, EmployerIndex>(collection: CollectionName).OrderBy(index => index.Name).ListAsync(cancellationToken)).ToList();

    /// <summary>
    /// Finds an employer by name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The employer, or <see langword="null"/>.</returns>
    public Task<Employer> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name?.Trim();

        return Session.Query<Employer, EmployerIndex>(index => index.Name == trimmed, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask DeletingAsync(Employer entry)
    {
        foreach (var usage in _usages)
        {
            if ((await usage.GetUsagesAsync(entry)) is { Count: > 0 } uses)
            {
                throw new InvalidOperationException(S["The employer is in use: {0}", string.Join(", ", uses)]);
            }
        }
    }
}

/// <summary>
/// Manages employers.
/// </summary>
public sealed class EmployerManager : CatalogManager<Employer>, IEmployerManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmployerManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public EmployerManager(EmployerStore store, IEnumerable<ICatalogEntryHandler<Employer>> handlers, ILogger<CatalogManager<Employer>> logger)
        : base(store, handlers, logger)
    {
    }
}

/// <summary>
/// Stamps employers and checks them: a unique name, a three-letter currency, registrations with a jurisdiction and
/// rates in range.
/// </summary>
public sealed class EmployerHandler : CatalogEntryHandlerBase<Employer>
{
    private readonly EmployerStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployerHandler"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmployerHandler(EmployerStore store, IClock clock, IStringLocalizer<EmployerHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<Employer> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<Employer> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<Employer> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    // Trims and upper-cases what a page, a recipe or a deployment plan typed.
    private static void Normalize(Employer item)
    {
        item.Name = item.Name?.Trim();
        item.LegalName = string.IsNullOrWhiteSpace(item.LegalName) ? null : item.LegalName.Trim();
        item.TaxId = string.IsNullOrWhiteSpace(item.TaxId) ? null : item.TaxId.Trim();
        item.Currency = string.IsNullOrWhiteSpace(item.Currency) ? null : item.Currency.Trim().ToUpperInvariant();
        item.CountryPack = string.IsNullOrWhiteSpace(item.CountryPack) ? null : item.CountryPack.Trim().ToUpperInvariant();
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<Employer> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        Normalize(item);

        if (string.IsNullOrEmpty(item.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(Employer.Name)]));
        }
        else if (await _store.FindByNameAsync(item.Name, cancellationToken) is { } other && other.ItemId != item.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another employer is named {0}.", item.Name], [nameof(Employer.Name)]));
        }

        if (item.Currency is not null && (item.Currency.Length != 3 || !item.Currency.All(char.IsAsciiLetter)))
        {
            context.Result.Fail(new ValidationResult(S["The currency is a three-letter code, such as USD."], [nameof(Employer.Currency)]));
        }

        foreach (var registration in item.Registrations ?? [])
        {
            registration.Jurisdiction = registration.Jurisdiction?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(registration.Jurisdiction))
            {
                context.Result.Fail(new ValidationResult(S["Each tax registration needs a jurisdiction, such as US or US-CA."], [nameof(Employer.Registrations)]));
            }

            if (registration.Rates.Any(rate => rate.Rate is < 0 or > 100))
            {
                context.Result.Fail(new ValidationResult(S["A registration's rate is a percent from 0 to 100."], [nameof(Employer.Registrations)]));
            }

            if (registration.Rates.Any(rate => rate.EffectiveFrom == default))
            {
                context.Result.Fail(new ValidationResult(S["A registration's rate needs the day it starts."], [nameof(Employer.Registrations)]));
            }
        }

        // The T4's box 45 takes one of the CRA's five dental benefits codes.
        if (item.GetCanadaSettings().DentalBenefitsCode is { } dental && !CanadaEmployerSettings.IsDentalBenefitsCode(dental))
        {
            context.Result.Fail(new ValidationResult(S["Choose one of the five dental benefits codes."], [nameof(CanadaEmployerSettings.DentalBenefitsCode)]));
        }
    }
}

/// <summary>
/// Answers which employers a user may act on with a permission: every one with the global permission, otherwise the
/// employers of the per-employer permissions they hold.
/// </summary>
public sealed class PayrollScopeService : IPayrollScopeService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly EmployerStore _employers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollScopeService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="employers">The employers.</param>
    public PayrollScopeService(IAuthorizationService authorizationService, EmployerStore employers)
    {
        _authorizationService = authorizationService;
        _employers = employers;
    }

    /// <inheritdoc />
    public async Task<PayrollScope> GetScopeAsync(ClaimsPrincipal user, Permission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (user is null)
        {
            return PayrollScope.None;
        }

        if (await _authorizationService.AuthorizeAsync(user, permission))
        {
            return PayrollScope.Everyone;
        }

        var allowed = new List<string>();

        foreach (var employer in await _employers.ListAsync(cancellationToken))
        {
            if (await _authorizationService.AuthorizeAsync(user, PayrollPermissionTemplates.For(permission, employer.ItemId, employer.Name)))
            {
                allowed.Add(employer.ItemId);
            }
        }

        return new PayrollScope(false, allowed);
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Permission permission, string employerId)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (user is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, permission))
        {
            return true;
        }

        return !string.IsNullOrEmpty(employerId) && await _authorizationService.AuthorizeAsync(user, PayrollPermissionTemplates.For(permission, employerId));
    }
}

/// <summary>
/// Finds employers as parties, such as the payer of a check.
/// </summary>
public sealed class EmployerPartySource : IPartySource
{
    private readonly EmployerStore _employers;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmployerPartySource"/> class.
    /// </summary>
    /// <param name="employers">The employers.</param>
    public EmployerPartySource(EmployerStore employers)
    {
        _employers = employers;
    }

    /// <inheritdoc />
    public string Kind => PayrollConstants.EmployerPartyKind;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var search = query?.Trim();

        return [.. (await _employers.ListAsync(cancellationToken))
            .Where(employer => string.IsNullOrEmpty(search) || employer.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(count)
            .Select(employer => new PartyOption(employer.ItemId, employer.Name))];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return (await _employers.GetAsync([.. ids], cancellationToken)).ToDictionary(employer => employer.ItemId, employer => employer.Name, StringComparer.Ordinal);
    }
}

/// <summary>
/// Knows no employee until pay profiles answer: data limited to some employers is then empty, never too wide.
/// </summary>
public sealed class EmptyPayrollEmployeeDirectory : IPayrollEmployeeDirectory
{
    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetStaffMemberIdsAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
}
