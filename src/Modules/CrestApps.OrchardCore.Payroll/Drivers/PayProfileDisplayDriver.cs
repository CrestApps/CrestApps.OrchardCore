using System.Globalization;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the pay profile list and the pay profile editor: the profile, the tax id (only its last characters show),
/// the home address, the election history with a new election, and the net pay split.
/// </summary>
internal sealed class PayProfileDisplayDriver : DisplayDriver<EmployeePayProfile>
{
    private const int BlankDistributionRows = 2;

    private readonly IPayProfileService _profiles;
    private readonly IPayrollScopeService _scope;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly IStaffDirectory _staff;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IServiceProvider _services;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PayrollElectionOptions _elections;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly PayProfileEditTracker _tracker;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public PayProfileDisplayDriver(
        IPayProfileService profiles,
        IPayrollScopeService scope,
        EmployerStore employers,
        IPayScheduleManager schedules,
        IStaffDirectory staff,
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        IServiceProvider services,
        IHttpContextAccessor httpContextAccessor,
        IOptions<PayrollElectionOptions> elections,
        IStringLocalizerFactory localizerFactory,
        PayProfileEditTracker tracker,
        IClock clock,
        IStringLocalizer<PayProfileDisplayDriver> stringLocalizer)
    {
        _profiles = profiles;
        _scope = scope;
        _employers = employers;
        _schedules = schedules;
        _staff = staff;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        _services = services;
        _httpContextAccessor = httpContextAccessor;
        _elections = elections.Value;
        _localizerFactory = localizerFactory;
        _tracker = tracker;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(EmployeePayProfile profile, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("EmployeePayProfile_Fields_SummaryAdmin", profile)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("EmployeePayProfile_Tags_SummaryAdmin", profile)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("EmployeePayProfile_Buttons_SummaryAdmin", profile).Location("Actions:5"),
            View("EmployeePayProfile_ActionsMenu_SummaryAdmin", profile).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(EmployeePayProfile profile, BuildEditorContext context)
    {
        // My Pay: the person's own elections and net pay split, nothing else of the profile.
        if (context.GroupId == PayrollConstants.MyPayEditorGroup)
        {
            return Combine(
                (await ElectionsAsync(profile, new PayProfileElectionsViewModel { NewEffectiveFrom = await TodayAsync() })).OnGroup(PayrollConstants.MyPayEditorGroup),
                (await DistributionAsync(profile, null)).OnGroup(PayrollConstants.MyPayEditorGroup));
        }

        return Combine(
            await MainAsync(profile, context.IsNew),
            await AddressAsync(await _addressEditor.BuildAsync(profile.HomeAddress, AddressOptions(profile.HomeAddress?.Country))),
            await ElectionsAsync(profile, new PayProfileElectionsViewModel()),
            await DistributionAsync(profile, null));
    }

    public override async Task<IDisplayResult> UpdateAsync(EmployeePayProfile profile, UpdateEditorContext context)
    {
        if (context.GroupId == PayrollConstants.MyPayEditorGroup)
        {
            var (typedElections, typedRows) = await UpdateElectionsAndDistributionAsync(profile, context);

            return Combine(
                (await ElectionsAsync(profile, typedElections)).OnGroup(PayrollConstants.MyPayEditorGroup),
                (await DistributionAsync(profile, typedRows)).OnGroup(PayrollConstants.MyPayEditorGroup));
        }

        var model = new PayProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            profile.StaffMemberId = model.StaffMemberId;
        }

        profile.EmployerId = model.EmployerId;
        profile.PayScheduleId = string.IsNullOrEmpty(model.PayScheduleId) ? null : model.PayScheduleId;
        profile.WorkerType = Enum.TryParse<PayWorkerType>(model.WorkerType, out var workerType) ? workerType : PayWorkerType.Employee;
        profile.OvertimeStatus = Enum.TryParse<OvertimeStatus>(model.OvertimeStatus, out var overtime) ? overtime : OvertimeStatus.NonExempt;
        profile.Tipped = model.Tipped;
        profile.BackupWithholding = model.BackupWithholding;
        profile.Status = Enum.TryParse<PayProfileStatus>(model.Status, out var status) ? status : PayProfileStatus.Active;
        profile.LegalName = model.LegalName;
        profile.WorkSiteTeamId = string.IsNullOrEmpty(model.WorkSiteTeamId) ? null : model.WorkSiteTeamId;
        profile.RetroPayFrom = model.RetroPayFrom;
        profile.DefaultAllocation = new PayInputAllocation
        {
            DepartmentTeamId = string.IsNullOrEmpty(model.DepartmentTeamId) ? null : model.DepartmentTeamId,
            SiteTeamId = profile.WorkSiteTeamId,
            ActivityCode = string.IsNullOrWhiteSpace(model.ActivityCode) ? null : model.ActivityCode.Trim(),
            Job = string.IsNullOrWhiteSpace(model.Job) ? null : model.Job.Trim(),
        };

        if (model.RemoveTaxId)
        {
            await _profiles.SetTaxIdAsync(profile, null);
            _tracker.Changes.Add(PayProfileChangeKind.TaxIdChanged);
        }
        else if (!string.IsNullOrWhiteSpace(model.TaxId))
        {
            await _profiles.SetTaxIdAsync(profile, model.TaxId);
            _tracker.Changes.Add(PayProfileChangeKind.TaxIdChanged);
        }

        var address = new PayProfileAddressViewModel();

        await context.Updater.TryUpdateModelAsync(address, Prefix + "Address");

        address.Address ??= new AddressEditorViewModel();

        if (!address.Address.HasAnyValue())
        {
            profile.HomeAddress = null;
        }
        else
        {
            var result = await _addressEditor.ValidateAsync(address.Address, context.Updater.ModelState, $"{Prefix}Address.{nameof(address.Address)}", AddressOptions(address.Address.Country));

            if (result.IsValid)
            {
                profile.HomeAddress = result.Address;
            }
        }

        await _addressEditor.PopulateAsync(address.Address, AddressOptions(address.Address.Country));

        var (elections, typedDistribution) = await UpdateElectionsAndDistributionAsync(profile, context);

        return Combine(
            await MainAsync(profile, context.IsNew),
            await AddressAsync(address.Address),
            await ElectionsAsync(profile, elections),
            await DistributionAsync(profile, typedDistribution));
    }

    // Reads a new election and the net pay split, shared by the profile page and My Pay. Returns what to show again.
    private async Task<(PayProfileElectionsViewModel Elections, IList<NetPayRowViewModel> Rows)> UpdateElectionsAndDistributionAsync(EmployeePayProfile profile, UpdateEditorContext context)
    {
        var elections = new PayProfileElectionsViewModel();

        await context.Updater.TryUpdateModelAsync(elections, Prefix + "Elections");

        if (!string.IsNullOrWhiteSpace(elections.NewJurisdiction))
        {
            if (elections.NewEffectiveFrom is { } from)
            {
                var jurisdiction = elections.NewJurisdiction.Trim().ToUpperInvariant();
                var fields = _elections.For(jurisdiction)?.Fields ?? [];
                var errors = context.Updater.ModelState.ErrorCount;
                var values = ReadElectionValues(fields, elections.NewValues, context);

                // A value that can't be read keeps the election out, and the typed values on the form.
                if (context.Updater.ModelState.ErrorCount == errors)
                {
                    profile.Elections.Add(new TaxElection
                    {
                        Jurisdiction = jurisdiction,
                        EffectiveFrom = from,
                        Values = values,
                        EnteredBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name,
                        EnteredUtc = _clock.UtcNow,
                    });
                    _tracker.Changes.Add(PayProfileChangeKind.ElectionAdded);
                    elections = new PayProfileElectionsViewModel();
                }
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix + "Elections", nameof(elections.NewEffectiveFrom), S["Give the day the election starts."]);
            }
        }

        var distribution = new PayProfileDistributionViewModel();

        await context.Updater.TryUpdateModelAsync(distribution, Prefix + "Distribution");

        var rows = ReadDistribution(distribution.Rows, context);

        if (!rows.SequenceEqual(profile.Distribution, NetPayRowComparer.Instance))
        {
            _tracker.Changes.Add(PayProfileChangeKind.DistributionChanged);
        }

        profile.Distribution = rows;

        return (elections, distribution.Rows);
    }

    // Today in the site's or the user's time zone: the default first day of an election typed in My Pay.
    private async Task<DateOnly> TodayAsync()
        => _services.GetService<ILocalClock>() is { } localClock
            ? DateOnly.FromDateTime((await localClock.GetLocalNowAsync()).DateTime)
            : DateOnly.FromDateTime(_clock.UtcNow);

    /// <summary>
    /// Reads the typed values of an election: whole numbers and amounts with a dot for decimals, yes or no, a choice.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <param name="typed">The typed values.</param>
    /// <param name="context">The editor context, for errors; or <see langword="null"/>.</param>
    /// <returns>The values to keep.</returns>
    internal Dictionary<string, string> ReadElectionValues(IEnumerable<ElectionField> fields, IDictionary<string, string> typed, UpdateEditorContext context)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            var value = typed is not null && typed.TryGetValue(field.Name, out var raw) ? raw?.Trim() : null;

            switch (field.Kind)
            {
                case ElectionFieldKind.Boolean:
                    values[field.Name] = (value is "true" or "on" or "True").ToString(CultureInfo.InvariantCulture).ToLowerInvariant();
                    continue;
                case ElectionFieldKind.Integer when !string.IsNullOrEmpty(value) && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _):
                case ElectionFieldKind.Amount when !string.IsNullOrEmpty(value) && !decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _):
                    context?.Updater.ModelState.AddModelError(Prefix + "Elections", nameof(PayProfileElectionsViewModel.NewValues), S["{0} needs a number, with a dot for decimals.", _localizerFactory.Localize(field.Label).Value]);
                    continue;
                case ElectionFieldKind.Choice when !string.IsNullOrEmpty(value) && field.Choices.All(choice => choice.Value != value):
                    context?.Updater.ModelState.AddModelError(Prefix + "Elections", nameof(PayProfileElectionsViewModel.NewValues), S["Choose one of the choices of {0}.", _localizerFactory.Localize(field.Label).Value]);
                    continue;
            }

            if (!string.IsNullOrEmpty(value))
            {
                values[field.Name] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Reads the typed rows of the net pay split; an empty row is dropped.
    /// </summary>
    /// <param name="rows">The typed rows.</param>
    /// <param name="context">The editor context, for errors; or <see langword="null"/>.</param>
    /// <returns>The rows.</returns>
    internal List<NetPayRow> ReadDistribution(IEnumerable<NetPayRowViewModel> rows, UpdateEditorContext context)
    {
        var result = new List<NetPayRow>();

        foreach (var row in rows ?? [])
        {
            if (row is null || (string.IsNullOrEmpty(row.Method) && string.IsNullOrEmpty(row.BankAccountId)))
            {
                continue;
            }

            var method = Enum.TryParse<NetPayMethod>(row.Method, out var parsed) ? parsed : NetPayMethod.BankAccount;
            var split = Enum.TryParse<NetPaySplit>(row.Split, out var parsedSplit) ? parsedSplit : NetPaySplit.Remainder;
            var item = new NetPayRow
            {
                Method = method,
                BankAccountId = method == NetPayMethod.BankAccount ? row.BankAccountId : null,
                Split = split,
            };

            if (split != NetPaySplit.Remainder)
            {
                if (decimal.TryParse(row.Value?.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                {
                    if (split == NetPaySplit.Amount)
                    {
                        item.Amount = number;
                    }
                    else
                    {
                        item.Percent = number;
                    }
                }
                else
                {
                    context?.Updater.ModelState.AddModelError(Prefix + "Distribution", nameof(PayProfileDistributionViewModel.Rows), S["An amount or a percent row needs a number, with a dot for decimals."]);
                }
            }

            result.Add(item);
        }

        return result;
    }

    private async Task<ShapeResult> MainAsync(EmployeePayProfile profile, bool isNew)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var manage = await _scope.GetScopeAsync(user, PayrollPermissions.ManagePayProfiles);
        var employers = (await _employers.ListAsync()).Where(employer => manage.Allows(employer.ItemId) || employer.ItemId == profile.EmployerId).ToList();
        var schedules = await _schedules.GetAllAsync();
        var teams = _services.GetService<PayrollTeamsFeature>() is not null && _services.GetService<ITeamManager>() is { } teamManager ? (await teamManager.ListTreeAsync()).Select(item => item.Team).ToList() : [];
        IReadOnlyDictionary<string, string> staffNames = string.IsNullOrEmpty(profile.StaffMemberId) ? new Dictionary<string, string>() : await _staff.GetNamesAsync([profile.StaffMemberId]);
        var blockers = isNew ? [] : await _profiles.GetBlockersAsync(profile);
        var same = isNew ? [] : await _profiles.FindSameTaxIdAsync(profile);
        IReadOnlyDictionary<string, string> sameNames = same.Count == 0 ? new Dictionary<string, string>() : await _staff.GetNamesAsync(same.Select(item => item.StaffMemberId));
        var canReveal = !isNew && profile.TaxId is { HasValue: true } && await _scope.AuthorizeAsync(user, PayrollPermissions.ViewTaxIds, profile.EmployerId);
        IList<SelectListItem> staffOptions = [];

        if (isNew && string.IsNullOrEmpty(profile.StaffMemberId))
        {
            staffOptions = [.. (await _staff.SearchAsync(null, take: 500)).OrderBy(staff => staff.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(staff => new SelectListItem(staff.DisplayName, staff.ItemId))];
        }

        return Initialize<PayProfileViewModel>("PayProfileFields_Edit", model =>
        {
            model.IsNew = isNew;
            model.ProfileId = profile.ItemId;
            model.StaffMemberId = profile.StaffMemberId;
            model.StaffName = staffNames.GetValueOrDefault(profile.StaffMemberId ?? string.Empty);
            model.StaffMembers = staffOptions;
            model.EmployerId = profile.EmployerId;
            model.PayScheduleId = profile.PayScheduleId;
            model.WorkerType = profile.WorkerType.ToString();
            model.OvertimeStatus = profile.OvertimeStatus.ToString();
            model.Tipped = profile.Tipped;
            model.BackupWithholding = profile.BackupWithholding;
            model.Status = profile.Status.ToString();
            model.LegalName = profile.LegalName;
            model.WorkSiteTeamId = profile.WorkSiteTeamId;
            model.RetroPayFrom = profile.RetroPayFrom;
            model.DepartmentTeamId = profile.DefaultAllocation?.DepartmentTeamId;
            model.ActivityCode = profile.DefaultAllocation?.ActivityCode;
            model.Job = profile.DefaultAllocation?.Job;
            model.TaxIdHint = profile.TaxId is { HasValue: true } ? profile.TaxId.ToString() : null;
            model.CanRevealTaxId = canReveal;
            model.SameTaxIdStaff = [.. same.Select(item => sameNames.GetValueOrDefault(item.StaffMemberId, item.StaffMemberId))];
            model.Employers = [.. employers.Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == profile.EmployerId))];
            model.Schedules = [.. schedules.Where(schedule => schedule.Enabled || schedule.ItemId == profile.PayScheduleId).Select(schedule => new SelectListItem(schedule.Name, schedule.ItemId, schedule.ItemId == profile.PayScheduleId))];
            model.Teams = [.. teams.Select(team => new SelectListItem(team.Name, team.ItemId))];
            model.Blockers = blockers;
        }).Location("Content:1");
    }

    private async Task<ShapeResult> AddressAsync(AddressEditorViewModel address)
    {
        var editor = await _shapeFactory.CreateAddressEditorAsync(address, $"{Prefix}Address.{nameof(PayProfileAddressViewModel.Address)}");

        return Initialize<PayProfileAddressViewModel>("PayProfileAddress_Edit", model =>
        {
            model.Address = address;
            model.AddressEditor = editor;
        }).Location("Content:3");
    }

    private async Task<ShapeResult> ElectionsAsync(EmployeePayProfile profile, PayProfileElectionsViewModel typed)
    {
        var employer = string.IsNullOrEmpty(profile.EmployerId) ? null : await _employers.FindByIdAsync(profile.EmployerId);
        var jurisdictions = (employer?.Registrations ?? []).Select(registration => registration.Jurisdiction)
            .Concat(profile.Elections.Select(election => election.Jurisdiction))
            .Where(jurisdiction => !string.IsNullOrEmpty(jurisdiction))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fieldSet = _elections.For(typed.NewJurisdiction ?? jurisdictions.FirstOrDefault() ?? "*");
        var labels = _elections.FieldSets.SelectMany(set => set.Fields)
            .GroupBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => _localizerFactory.Localize(group.First().Label).Value, StringComparer.Ordinal);

        return Initialize<PayProfileElectionsViewModel>("PayProfileElections_Edit", model =>
        {
            model.NewJurisdiction = typed.NewJurisdiction;
            model.NewEffectiveFrom = typed.NewEffectiveFrom;
            model.NewValues = typed.NewValues ?? new(StringComparer.Ordinal);
            model.History = [.. profile.Elections.OrderBy(election => election.Jurisdiction).ThenByDescending(election => election.EffectiveFrom).ThenByDescending(election => election.EnteredUtc)];
            model.Jurisdictions = jurisdictions;
            model.Fields = fieldSet?.Fields ?? [];
            model.FieldLabels = labels;
        }).Prefix(Prefix + "Elections").Location("Content:5");
    }

    private async Task<ShapeResult> DistributionAsync(EmployeePayProfile profile, IList<NetPayRowViewModel> typed)
    {
        IList<SelectListItem> accounts = [];

        if (!string.IsNullOrEmpty(profile.StaffMemberId) && _services.GetService<IBankAccountManager>() is { } bankAccounts)
        {
            accounts = [.. (await bankAccounts.ListByOwnersAsync([new PartyRef(StaffConstants.PartyKind, profile.StaffMemberId)]))
                .Select(account => new SelectListItem($"{account.Name} ({account.Status})", account.ItemId))];
        }

        return Initialize<PayProfileDistributionViewModel>("PayProfileDistribution_Edit", model =>
        {
            model.BankAccounts = accounts;
            model.Rows = typed ?? [.. profile.Distribution.Select(row => new NetPayRowViewModel
            {
                Method = row.Method.ToString(),
                BankAccountId = row.BankAccountId,
                Split = row.Split.ToString(),
                Value = (row.Split == NetPaySplit.Amount ? row.Amount : row.Percent)?.ToString("0.##", CultureInfo.InvariantCulture),
            })];

            for (var i = 0; i < BlankDistributionRows; i++)
            {
                model.Rows.Add(new NetPayRowViewModel());
            }
        }).Prefix(Prefix + "Distribution").Location("Content:7");
    }

    private static AddressEditorOptions AddressOptions(string country)
        => new()
        {
            ShowName = false,
            ShowCompany = false,
            DefaultCountry = string.IsNullOrWhiteSpace(country) ? null : country,
        };

    private sealed class NetPayRowComparer : IEqualityComparer<NetPayRow>
    {
        public static NetPayRowComparer Instance { get; } = new();

        public bool Equals(NetPayRow x, NetPayRow y)
            => x?.Method == y?.Method && x?.BankAccountId == y?.BankAccountId && x?.Split == y?.Split && x?.Amount == y?.Amount && x?.Percent == y?.Percent;

        public int GetHashCode(NetPayRow obj)
            => HashCode.Combine(obj.Method, obj.BankAccountId, obj.Split, obj.Amount, obj.Percent);
    }
}
