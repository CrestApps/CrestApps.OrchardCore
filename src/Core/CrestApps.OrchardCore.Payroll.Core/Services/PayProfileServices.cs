using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.SensitiveData;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Stores pay profiles. A profile with pay items is not deleted.
/// </summary>
public sealed class PayProfileStore : DocumentCatalog<EmployeePayProfile, PayProfileIndex>
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayProfileStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayProfileStore(ISession session, IStringLocalizer<PayProfileStore> stringLocalizer)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
        S = stringLocalizer;
    }

    /// <summary>
    /// Finds the profile of a staff member with an employer.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="employerId">The employer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The profile, or <see langword="null"/>.</returns>
    public Task<EmployeePayProfile> FindAsync(string staffMemberId, string employerId, CancellationToken cancellationToken = default)
        => Session.Query<EmployeePayProfile, PayProfileIndex>(index => index.StaffMemberId == staffMemberId && index.EmployerId == employerId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lists the profiles matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The profiles.</returns>
    public async Task<IReadOnlyList<EmployeePayProfile>> ListAsync(System.Linq.Expressions.Expression<Func<PayProfileIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await (filter is null ? Session.Query<EmployeePayProfile, PayProfileIndex>(collection: CollectionName) : Session.Query<EmployeePayProfile, PayProfileIndex>(filter, collection: CollectionName)).ListAsync(cancellationToken)).ToList();

    /// <summary>
    /// Counts the profiles matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count.</returns>
    public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<PayProfileIndex, bool>> filter, CancellationToken cancellationToken = default)
        => Session.QueryIndex<PayProfileIndex>(filter, collection: CollectionName).CountAsync(cancellationToken);

    /// <inheritdoc />
    protected override async ValueTask DeletingAsync(EmployeePayProfile entry)
    {
        var items = await Session.QueryIndex<PayItemIndex>(index => index.ProfileId == entry.ItemId, collection: CollectionName).CountAsync();

        if (items > 0)
        {
            throw new InvalidOperationException(S["The pay profile has pay items: remove them first, or end the profile instead."]);
        }
    }
}

/// <summary>
/// Stores pay items.
/// </summary>
public sealed class PayItemStore : DocumentCatalog<EmployeePayItem, PayItemIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayItemStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PayItemStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <summary>
    /// Lists the items matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    public async Task<IReadOnlyList<EmployeePayItem>> ListAsync(System.Linq.Expressions.Expression<Func<PayItemIndex, bool>> filter, CancellationToken cancellationToken = default)
        => (await Session.Query<EmployeePayItem, PayItemIndex>(filter, collection: CollectionName).ListAsync(cancellationToken)).ToList();

    /// <summary>
    /// Counts the items matching a filter on the index.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count.</returns>
    public Task<int> CountAsync(System.Linq.Expressions.Expression<Func<PayItemIndex, bool>> filter, CancellationToken cancellationToken = default)
        => Session.QueryIndex<PayItemIndex>(filter, collection: CollectionName).CountAsync(cancellationToken);
}

/// <summary>
/// Manages pay profiles.
/// </summary>
public sealed class PayProfileManager : CatalogManager<EmployeePayProfile>, IPayProfileManager
{
    private readonly PayProfileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayProfileManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayProfileManager(PayProfileStore store, IEnumerable<ICatalogEntryHandler<EmployeePayProfile>> handlers, ILogger<CatalogManager<EmployeePayProfile>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<EmployeePayProfile> FindAsync(string staffMemberId, string employerId, CancellationToken cancellationToken = default)
        => _store.FindAsync(staffMemberId, employerId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<EmployeePayProfile>> ListByStaffMemberAsync(string staffMemberId, CancellationToken cancellationToken = default)
        => _store.ListAsync(index => index.StaffMemberId == staffMemberId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<EmployeePayProfile>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
    {
        if (employerIds is null)
        {
            return _store.ListAsync(null, cancellationToken);
        }

        if (employerIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<EmployeePayProfile>>([]);
        }

        var ids = employerIds.ToArray();

        return _store.ListAsync(index => index.EmployerId.IsIn(ids), cancellationToken);
    }
}

/// <summary>
/// Manages pay items.
/// </summary>
public sealed class PayItemManager : CatalogManager<EmployeePayItem>, IPayItemManager
{
    private readonly PayItemStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayItemManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayItemManager(PayItemStore store, IEnumerable<ICatalogEntryHandler<EmployeePayItem>> handlers, ILogger<CatalogManager<EmployeePayItem>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeePayItem>> ListByProfileAsync(string profileId, CancellationToken cancellationToken = default)
        => [.. (await _store.ListAsync(index => index.ProfileId == profileId, cancellationToken)).OrderBy(item => item.StartDate)];
}

/// <summary>
/// Checks pay profiles: a staff member, an employer and a schedule that exist, one profile per staff member and
/// employer, elections with a jurisdiction, and a net pay split that adds up.
/// </summary>
public sealed class PayProfileHandler : CatalogEntryHandlerBase<EmployeePayProfile>
{
    private readonly PayProfileStore _store;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly IStaffDirectory _staff;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayProfileHandler"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="schedules">The pay schedules.</param>
    /// <param name="staff">The staff directory.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayProfileHandler(PayProfileStore store, EmployerStore employers, IPayScheduleManager schedules, IStaffDirectory staff, IClock clock, IStringLocalizer<PayProfileHandler> stringLocalizer)
    {
        _store = store;
        _employers = employers;
        _schedules = schedules;
        _staff = staff;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<EmployeePayProfile> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        item.LegalName = string.IsNullOrWhiteSpace(item.LegalName) ? null : item.LegalName.Trim();

        if (string.IsNullOrEmpty(item.StaffMemberId) || !(await _staff.GetAsync([item.StaffMemberId], cancellationToken)).ContainsKey(item.StaffMemberId))
        {
            context.Result.Fail(new ValidationResult(S["Choose the staff member paid."], [nameof(EmployeePayProfile.StaffMemberId)]));
        }

        if (string.IsNullOrEmpty(item.EmployerId) || await _employers.FindByIdAsync(item.EmployerId, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["Choose the employer."], [nameof(EmployeePayProfile.EmployerId)]));
        }
        else if (await _store.FindAsync(item.StaffMemberId, item.EmployerId, cancellationToken) is { } other && other.ItemId != item.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["The staff member already has a pay profile with this employer."], [nameof(EmployeePayProfile.EmployerId)]));
        }

        if (!string.IsNullOrEmpty(item.PayScheduleId) && await _schedules.FindByIdAsync(item.PayScheduleId, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["The pay schedule no longer exists."], [nameof(EmployeePayProfile.PayScheduleId)]));
        }

        foreach (var election in item.Elections)
        {
            election.Jurisdiction = election.Jurisdiction?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(election.Jurisdiction))
            {
                context.Result.Fail(new ValidationResult(S["Each election needs a jurisdiction."], [nameof(EmployeePayProfile.Elections)]));
            }
        }

        foreach (var message in DistributionProblems(item.Distribution))
        {
            context.Result.Fail(new ValidationResult(message, [nameof(EmployeePayProfile.Distribution)]));
        }
    }

    /// <summary>
    /// Finds what is wrong with a net pay split: amounts and percents in range, percents up to 100, a bank account on
    /// each deposit, and exactly one remainder row, last.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The problems.</returns>
    public IEnumerable<string> DistributionProblems(IList<NetPayRow> rows)
    {
        if (rows is null || rows.Count == 0)
        {
            yield break;
        }

        if (rows.Any(row => row.Split == NetPaySplit.Amount && row.Amount is not > 0))
        {
            yield return S["An amount row needs an amount above zero."];
        }

        if (rows.Any(row => row.Split == NetPaySplit.Percent && row.Percent is not (> 0 and <= 100)))
        {
            yield return S["A percent row needs a percent from 0 to 100."];
        }

        if (rows.Where(row => row.Split == NetPaySplit.Percent).Sum(row => row.Percent ?? 0) > 100)
        {
            yield return S["The percents add up to more than 100."];
        }

        if (rows.Any(row => row.Method == NetPayMethod.BankAccount && string.IsNullOrEmpty(row.BankAccountId)))
        {
            yield return S["A deposit needs a bank account."];
        }

        if (rows.Count(row => row.Split == NetPaySplit.Remainder) != 1 || rows[^1].Split != NetPaySplit.Remainder)
        {
            yield return S["The last row takes what is left, and only the last row does."];
        }
    }
}

/// <summary>
/// Checks pay items: a profile and a pay code that exist, an amount or a percent, the dates in order and the limits in
/// range. The staff member and the employer are copied from the profile.
/// </summary>
public sealed class PayItemHandler : CatalogEntryHandlerBase<EmployeePayItem>
{
    private readonly PayProfileStore _profiles;
    private readonly IPayCodeManager _codes;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayItemHandler"/> class.
    /// </summary>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for who changes an item.</param>
    public PayItemHandler(PayProfileStore profiles, IPayCodeManager codes, IClock clock, IStringLocalizer<PayItemHandler> stringLocalizer, IHttpContextAccessor httpContextAccessor = null)
    {
        _profiles = profiles;
        _codes = codes;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<EmployeePayItem> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        context.Model.ChangedById = ChangedBy() ?? context.Model.ChangedById;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<EmployeePayItem> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;
        context.Model.ChangedById = ChangedBy() ?? context.Model.ChangedById;

        return Task.CompletedTask;
    }

    // Who changes an item on a page; a recipe or a background job keeps who changed it last.
    private string ChangedBy()
        => _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<EmployeePayItem> context, CancellationToken cancellationToken = default)
    {
        var item = context.Model;

        if (string.IsNullOrEmpty(item.ProfileId) || await _profiles.FindByIdAsync(item.ProfileId, cancellationToken) is not { } profile)
        {
            context.Result.Fail(new ValidationResult(S["The pay profile no longer exists."], [nameof(EmployeePayItem.ProfileId)]));
        }
        else
        {
            item.StaffMemberId = profile.StaffMemberId;
            item.EmployerId = profile.EmployerId;
        }

        if (string.IsNullOrEmpty(item.PayCodeId) || await _codes.FindByIdAsync(item.PayCodeId, cancellationToken) is null)
        {
            context.Result.Fail(new ValidationResult(S["Choose the pay code."], [nameof(EmployeePayItem.PayCodeId)]));
        }

        if (item.Amount.HasValue == item.Percent.HasValue)
        {
            context.Result.Fail(new ValidationResult(S["Give an amount or a percent, not both."], [nameof(EmployeePayItem.Amount)]));
        }

        if (item.Amount is < 0 || item.Percent is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["An amount can't be negative, and a percent is from 0 to 100."], [nameof(EmployeePayItem.Amount)]));
        }

        if (item.EndDate is { } end && end < item.StartDate)
        {
            context.Result.Fail(new ValidationResult(S["The last day can't be before the first."], [nameof(EmployeePayItem.EndDate)]));
        }

        if (item.Goal is < 0 || item.PeriodMaximum is < 0 || item.YearMaximum is < 0 || item.EmployerMatchPercent is < 0 or > 100 || item.EmployerMatchLimitPercent is < 0 or > 100)
        {
            context.Result.Fail(new ValidationResult(S["Limits can't be negative, and percents are from 0 to 100."], [nameof(EmployeePayItem.Goal)]));
        }
    }
}

/// <summary>
/// The keyed hash of tax ids: an HMAC with a random key kept once per tenant, protected by the tenant's data protection
/// keys, so a number used twice is found without keeping it in clear, and a database backup alone can't turn the hashes
/// back into numbers. The key is made once, under a lock; a key kept in clear by an earlier version is protected in
/// place, with the same value, so the hashes already stored still match.
/// </summary>
public sealed class PayrollTaxIdHasher
{
    /// <summary>The data protection purpose of the key.</summary>
    public const string KeyPurpose = "CrestApps.OrchardCore.Payroll.TaxIdHashKey";

    private readonly IStore _store;
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtectionProvider _dataProtection;
    private readonly global::OrchardCore.Locking.Distributed.IDistributedLock _locks;
    private byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxIdHasher"/> class.
    /// </summary>
    /// <param name="store">The store, for a session of its own that reads and saves the key.</param>
    /// <param name="dataProtection">The data protection provider that protects the key.</param>
    /// <param name="locks">The distributed lock, so the key is made once.</param>
    public PayrollTaxIdHasher(IStore store, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtection, global::OrchardCore.Locking.Distributed.IDistributedLock locks)
    {
        _store = store;
        _dataProtection = dataProtection;
        _locks = locks;
    }

    /// <summary>
    /// Hashes a tax id: only its letters and digits count, in upper case.
    /// </summary>
    /// <param name="taxId">The tax id in clear.</param>
    /// <returns>The hash, or <see langword="null"/> for none.</returns>
    public async Task<string> HashAsync(string taxId)
    {
        var normalized = Normalize(taxId);

        if (normalized.Length == 0)
        {
            return null;
        }

        return Convert.ToHexString(HMACSHA256.HashData(await GetKeyAsync(), Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>
    /// Keeps only a tax id's letters and digits, in upper case.
    /// </summary>
    /// <param name="taxId">The tax id.</param>
    /// <returns>The normalized tax id.</returns>
    public static string Normalize(string taxId)
        => new([.. (taxId ?? string.Empty).Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant)]);

    private async Task<byte[]> GetKeyAsync()
    {
        if (_key is not null)
        {
            return _key;
        }

        var protector = global::Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.CreateProtector(_dataProtection, KeyPurpose);

        // Read first without the lock: once the key is made and protected, every later read is just this.
        await using (var reader = _store.CreateSession())
        {
            if (await reader.Query<PayrollKeys>(collection: PayrollConstants.CollectionName).FirstOrDefaultAsync() is { ProtectedTaxIdHashKey.Length: > 0 } ready)
            {
                return _key = Convert.FromBase64String(global::Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Unprotect(protector, ready.ProtectedTaxIdHashKey));
            }
        }

        var (locker, locked) = await _locks.TryAcquireLockAsync("Payroll:TaxIdHashKey", TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

        if (!locked)
        {
            throw new InvalidOperationException("The payroll tax id key is being made: try again in a moment.");
        }

        await using (locker)
        {
            // In a session of its own, committed before the lock is let go: another one waiting reads the key made.
            await using var session = _store.CreateSession();
            var stored = await session.Query<PayrollKeys>(collection: PayrollConstants.CollectionName).FirstOrDefaultAsync();
            string key;

            if (stored is { ProtectedTaxIdHashKey.Length: > 0 })
            {
                return _key = Convert.FromBase64String(global::Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Unprotect(protector, stored.ProtectedTaxIdHashKey));
            }

            // A key kept in clear by an earlier version keeps its value, so the hashes already stored still match.
            key = stored is { TaxIdHashKey.Length: > 0 } ? stored.TaxIdHashKey : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            stored ??= new PayrollKeys();
            stored.ProtectedTaxIdHashKey = global::Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Protect(protector, key);
            stored.TaxIdHashKey = null;

            await session.SaveAsync(stored, false, PayrollConstants.CollectionName);
            await session.SaveChangesAsync();

            return _key = Convert.FromBase64String(key);
        }
    }
}

/// <summary>
/// The tenant's payroll keys.
/// </summary>
public sealed class PayrollKeys
{
    /// <summary>
    /// Gets or sets the key of the tax id hash as an earlier version kept it, in clear; protected and cleared the first
    /// time the key is read.
    /// </summary>
    public string TaxIdHashKey { get; set; }

    /// <summary>
    /// Gets or sets the key of the tax id hash, protected by the tenant's data protection keys.
    /// </summary>
    public string ProtectedTaxIdHashKey { get; set; }
}

/// <summary>
/// Protects tax ids, finds a number used twice, reads one in full with a record of who read it, and lists what keeps a
/// profile from being paid.
/// </summary>
public sealed class DefaultPayProfileService : IPayProfileService
{
    private readonly ISensitiveValueProtector _protector;
    private readonly PayrollTaxIdHasher _hasher;
    private readonly PayProfileStore _store;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly IStaffDirectory _staff;
    private readonly IServiceProvider _services;
    private readonly IEnumerable<IPayProfileBlockerProvider> _blockerProviders;
    private readonly IEnumerable<IPayProfileObserver> _observers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPayProfileService"/> class.
    /// </summary>
    /// <param name="protector">The protector of sensitive values.</param>
    /// <param name="hasher">The tax id hasher.</param>
    /// <param name="store">The pay profiles.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="schedules">The pay schedules.</param>
    /// <param name="staff">The staff directory.</param>
    /// <param name="services">The services, for the optional ones.</param>
    /// <param name="blockerProviders">What adds blockers.</param>
    /// <param name="observers">What is told of changes.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultPayProfileService(
        ISensitiveValueProtector protector,
        PayrollTaxIdHasher hasher,
        PayProfileStore store,
        EmployerStore employers,
        IPayScheduleManager schedules,
        IStaffDirectory staff,
        IServiceProvider services,
        IEnumerable<IPayProfileBlockerProvider> blockerProviders,
        IEnumerable<IPayProfileObserver> observers,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        ILogger<DefaultPayProfileService> logger,
        IStringLocalizer<DefaultPayProfileService> stringLocalizer)
    {
        _protector = protector;
        _hasher = hasher;
        _store = store;
        _employers = employers;
        _schedules = schedules;
        _staff = staff;
        _services = services;
        _blockerProviders = blockerProviders;
        _observers = observers;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task SetTaxIdAsync(EmployeePayProfile profile, string taxId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(taxId))
        {
            profile.TaxId = null;
            profile.TaxIdHash = null;

            return;
        }

        var trimmed = taxId.Trim();

        profile.TaxId = _protector.Protect(PayrollConstants.TaxIdPurpose, trimmed, 4);
        profile.TaxIdHash = await _hasher.HashAsync(trimmed);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmployeePayProfile>> FindSameTaxIdAsync(EmployeePayProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrEmpty(profile.TaxIdHash))
        {
            return [];
        }

        var hash = profile.TaxIdHash;
        var id = profile.ItemId ?? string.Empty;

        return await _store.ListAsync(index => index.TaxIdHash == hash && index.ItemId != id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> RevealTaxIdAsync(EmployeePayProfile profile, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.TaxId is not { HasValue: true } || !_protector.TryUnprotect(PayrollConstants.TaxIdPurpose, profile.TaxId, out var clearText))
        {
            return null;
        }

        await TellAsync(profile, PayProfileChangeKind.TaxIdRevealed, reason, cancellationToken);

        return clearText;
    }

    /// <summary>
    /// Records a change: in the log, never with a number, and to the observers, such as the Audit Trail.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="kind">What happened.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task TellAsync(EmployeePayProfile profile, PayProfileChangeKind kind, string reason, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var change = new PayProfileChange
        {
            Profile = profile,
            Kind = kind,
            Actor = user?.Identity?.Name,
            ActorId = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            Reason = reason,
        };

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Pay profile {ProfileId}: {Change} by {Actor} ({ActorId}): {Reason}", profile.ItemId, kind, change.Actor ?? "the system", change.ActorId, reason);
        }

        foreach (var observer in _observers)
        {
            await observer.ChangedAsync(change, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayBlocker>> GetBlockersAsync(EmployeePayProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var blockers = new List<PayBlocker>();
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var employer = string.IsNullOrEmpty(profile.EmployerId) ? null : await _employers.FindByIdAsync(profile.EmployerId, cancellationToken);

        if (employer is null)
        {
            blockers.Add(new PayBlocker("NoEmployer", S["The employer no longer exists."]));
        }
        else if (employer.Status != EmployerStatus.Active)
        {
            blockers.Add(new PayBlocker("EmployerInactive", S["The employer doesn't run payroll."]));
        }

        if (string.IsNullOrEmpty(profile.PayScheduleId) || await _schedules.FindByIdAsync(profile.PayScheduleId, cancellationToken) is not { Enabled: true })
        {
            blockers.Add(new PayBlocker("NoPaySchedule", S["Choose a pay schedule in use."]));
        }

        if (profile.WorkerType == PayWorkerType.Employee && profile.TaxId is not { HasValue: true })
        {
            blockers.Add(new PayBlocker("NoTaxId", S["Enter the tax id."]));
        }

        if (profile.WorkerType == PayWorkerType.Employee)
        {
            foreach (var jurisdiction in await JurisdictionsAsync(profile, employer, cancellationToken))
            {
                if (profile.ElectionOn(jurisdiction, today) is null)
                {
                    blockers.Add(new PayBlocker("NoElection", S["Enter the tax election for {0}.", jurisdiction]));
                }
            }
        }

        if (profile.Distribution.Count == 0)
        {
            blockers.Add(new PayBlocker("NoDistribution", S["Choose how net pay is paid: a bank account or a check."]));
        }

        if ((await _staff.GetAsync([profile.StaffMemberId], cancellationToken)).GetValueOrDefault(profile.StaffMemberId) is { } staff)
        {
            var pay = staff.TryGet<StaffPayRates>(out var rates) ? rates : new StaffPayRates();

            // A contractor is paid the amounts of their invoices: no rate is needed.
            if (profile.WorkerType != PayWorkerType.Contractor && (pay.PayBasis == StaffPayBasis.Salary ? pay.SalaryOn(today) is null : pay.RateOn(today) is null))
            {
                blockers.Add(new PayBlocker("NoPayRate", pay.PayBasis == StaffPayBasis.Salary
                    ? S["Enter the salary in the staff member's Pay card."]
                    : S["Enter a pay rate in the staff member's Pay card."]));
            }
        }
        else
        {
            blockers.Add(new PayBlocker("NoStaffMember", S["The staff member no longer exists."]));
        }

        foreach (var provider in _blockerProviders)
        {
            await provider.AddBlockersAsync(profile, blockers, cancellationToken);
        }

        return blockers;
    }

    /// <inheritdoc />
    public async Task<bool> MayChangePayOfAsync(ClaimsPrincipal user, string staffMemberId, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || await _staff.FindByUserIdAsync(userId, cancellationToken) is not { } self || self.ItemId != staffMemberId)
        {
            return true;
        }

        return _services.GetService<ISiteService>() is { } site && (await site.GetSettingsAsync<PayrollSettings>())?.AllowChangingOwnPay == true;
    }

    // The jurisdictions a person needs an election for: the employer's registrations and the work site's.
    private async Task<IReadOnlyList<string>> JurisdictionsAsync(EmployeePayProfile profile, Employer employer, CancellationToken cancellationToken)
    {
        var jurisdictions = new List<string>();

        foreach (var registration in employer?.Registrations ?? [])
        {
            if (!string.IsNullOrEmpty(registration.Jurisdiction) && !jurisdictions.Contains(registration.Jurisdiction, StringComparer.OrdinalIgnoreCase))
            {
                jurisdictions.Add(registration.Jurisdiction);
            }
        }

        if (!string.IsNullOrEmpty(profile.WorkSiteTeamId) && _services.GetService<PayrollTeamsFeature>() is not null && _services.GetService<ITeamManager>() is { } teams && await teams.FindByIdAsync(profile.WorkSiteTeamId, cancellationToken) is { } site
            && site.TryGet<PayrollTeamSettings>(out var settings))
        {
            foreach (var jurisdiction in settings.Jurisdictions.Where(jurisdiction => !jurisdictions.Contains(jurisdiction, StringComparer.OrdinalIgnoreCase)))
            {
                jurisdictions.Add(jurisdiction);
            }
        }

        return jurisdictions;
    }
}

/// <summary>
/// Finds the staff members an employer pays from their pay profiles.
/// </summary>
public sealed class PayProfileEmployeeDirectory : IPayrollEmployeeDirectory
{
    private readonly PayProfileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayProfileEmployeeDirectory"/> class.
    /// </summary>
    /// <param name="store">The pay profiles.</param>
    public PayProfileEmployeeDirectory(PayProfileStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetStaffMemberIdsAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
    {
        if (employerIds is null || employerIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = employerIds.ToArray();

        return (await _store.ListAsync(index => index.EmployerId.IsIn(ids), cancellationToken)).Select(profile => profile.StaffMemberId).ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>
/// Keeps an employer, a pay schedule and a pay code that pay profiles or pay items use.
/// </summary>
public sealed class PayProfileUsage : IEmployerUsage, IPaySetupUsage
{
    private readonly PayProfileStore _profiles;
    private readonly PayItemStore _items;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayProfileUsage"/> class.
    /// </summary>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="items">The pay items.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayProfileUsage(PayProfileStore profiles, PayItemStore items, IStringLocalizer<PayProfileUsage> stringLocalizer)
    {
        _profiles = profiles;
        _items = items;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUsagesAsync(Employer employer, CancellationToken cancellationToken = default)
    {
        var id = employer.ItemId;
        var count = await _profiles.CountAsync(index => index.EmployerId == id, cancellationToken);

        return count == 0 ? [] : [S.Plural(count, "one pay profile", "{0} pay profiles").Value];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUsagesAsync(PaySchedule schedule, CancellationToken cancellationToken = default)
    {
        var id = schedule.ItemId;
        var count = await _profiles.CountAsync(index => index.PayScheduleId == id, cancellationToken);

        return count == 0 ? [] : [S.Plural(count, "one pay profile", "{0} pay profiles").Value];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUsagesAsync(PayCode code, CancellationToken cancellationToken = default)
    {
        var id = code.ItemId;
        var count = await _items.CountAsync(index => index.PayCodeId == id, cancellationToken);

        return count == 0 ? [] : [S.Plural(count, "one pay item", "{0} pay items").Value];
    }
}
