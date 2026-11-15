using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Imports pay profiles, matched by id and then by staff member and employer. A staff member can be named by
/// <c>EmployeeNumber</c>, an employer by <c>EmployerName</c> and a schedule by <c>PayScheduleName</c>. A tax id in clear
/// (<c>TaxId</c>) is protected with this site's keys.
/// </summary>
internal sealed class PayProfilesRecipeStep : NamedRecipeStepHandler
{
    public const string EntriesName = "Profiles";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IPayProfileManager _manager;
    private readonly IPayProfileService _service;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly IStaffMemberManager _staff;

    public PayProfilesRecipeStep(IPayProfileManager manager, IPayProfileService service, EmployerStore employers, IPayScheduleManager schedules, IStaffMemberManager staff)
        : base(PayrollConstants.PayProfilesRecipeStepName)
    {
        _manager = manager;
        _service = service;
        _employers = employers;
        _schedules = schedules;
        _staff = staff;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step[EntriesName] is not JsonArray entries)
        {
            return;
        }

        var schedules = await _schedules.GetAllAsync();

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var data = (JsonObject)entry.DeepClone();
            var taxId = data["TaxId"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

            data.Remove("TaxId");
            data.Remove("TaxIdHash");

            var incoming = data.Deserialize<EmployeePayProfile>(_options) ?? new EmployeePayProfile();

            if (string.IsNullOrEmpty(incoming.StaffMemberId) && data["EmployeeNumber"]?.GetValue<string>() is { } number)
            {
                incoming.StaffMemberId = (await _staff.FindByEmployeeNumberAsync(number))?.ItemId;
            }

            if (string.IsNullOrEmpty(incoming.EmployerId) && data["EmployerName"]?.GetValue<string>() is { } employerName)
            {
                incoming.EmployerId = (await _employers.FindByNameAsync(employerName))?.ItemId;
            }

            if (string.IsNullOrEmpty(incoming.PayScheduleId) && data["PayScheduleName"]?.GetValue<string>() is { } scheduleName)
            {
                incoming.PayScheduleId = schedules.FirstOrDefault(schedule => string.Equals(schedule.Name, scheduleName, StringComparison.OrdinalIgnoreCase))?.ItemId;
            }

            var existing = (!string.IsNullOrEmpty(incoming.ItemId) ? await _manager.FindByIdAsync(incoming.ItemId) : null)
                ?? (string.IsNullOrEmpty(incoming.StaffMemberId) || string.IsNullOrEmpty(incoming.EmployerId) ? null : await _manager.FindAsync(incoming.StaffMemberId, incoming.EmployerId));
            var profile = existing ?? await _manager.NewAsync();

            if (existing is null && !string.IsNullOrEmpty(incoming.ItemId))
            {
                profile.ItemId = incoming.ItemId;
            }

            profile.StaffMemberId = incoming.StaffMemberId;
            profile.EmployerId = incoming.EmployerId;
            profile.PayScheduleId = incoming.PayScheduleId;
            profile.WorkerType = incoming.WorkerType;
            profile.OvertimeStatus = incoming.OvertimeStatus;
            profile.Tipped = incoming.Tipped;
            profile.BackupWithholding = incoming.BackupWithholding;
            profile.Status = incoming.Status;
            profile.LegalName = incoming.LegalName;
            profile.HomeAddress = incoming.HomeAddress;
            profile.WorkSiteTeamId = incoming.WorkSiteTeamId;
            profile.Elections = incoming.Elections ?? [];
            profile.Distribution = incoming.Distribution ?? [];
            profile.DefaultAllocation = incoming.DefaultAllocation ?? new PayInputAllocation();
            profile.RetroPayFrom = incoming.RetroPayFrom;

            if (taxId is not null)
            {
                await _service.SetTaxIdAsync(profile, taxId);
            }

            var validation = await _manager.ValidateAsync(profile);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"The pay profile of staff member '{profile.StaffMemberId}' with employer '{profile.EmployerId}' was not imported: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (existing is null)
            {
                await _manager.CreateAsync(profile);
            }
            else
            {
                await _manager.UpdateAsync(profile);
            }
        }
    }
}

/// <summary>
/// Imports pay items, matched by id; a pay code can be named by <c>PayCode</c>.
/// </summary>
internal sealed class PayItemsRecipeStep : NamedRecipeStepHandler
{
    public const string EntriesName = "Items";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IPayItemManager _manager;
    private readonly IPayCodeManager _codes;

    public PayItemsRecipeStep(IPayItemManager manager, IPayCodeManager codes)
        : base(PayrollConstants.PayItemsRecipeStepName)
    {
        _manager = manager;
        _codes = codes;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step[EntriesName] is not JsonArray entries)
        {
            return;
        }

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var incoming = entry.Deserialize<EmployeePayItem>(_options) ?? new EmployeePayItem();

            if (string.IsNullOrEmpty(incoming.PayCodeId) && entry["PayCode"]?.GetValue<string>() is { } code)
            {
                incoming.PayCodeId = (await _codes.FindByCodeAsync(code))?.ItemId;
            }

            var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await _manager.FindByIdAsync(incoming.ItemId);
            var item = existing ?? await _manager.NewAsync();

            if (existing is null && !string.IsNullOrEmpty(incoming.ItemId))
            {
                item.ItemId = incoming.ItemId;
            }

            item.ProfileId = incoming.ProfileId;
            item.PayCodeId = incoming.PayCodeId;
            item.Amount = incoming.Amount;
            item.Percent = incoming.Percent;
            item.StartDate = incoming.StartDate;
            item.EndDate = incoming.EndDate;
            item.EmployerMatchPercent = incoming.EmployerMatchPercent;
            item.EmployerMatchLimitPercent = incoming.EmployerMatchLimitPercent;
            item.Goal = incoming.Goal;
            item.PaidTowardGoal = incoming.PaidTowardGoal;
            item.PeriodMaximum = incoming.PeriodMaximum;
            item.YearMaximum = incoming.YearMaximum;
            item.ArrearsBalance = incoming.ArrearsBalance;
            item.Note = incoming.Note;

            var validation = await _manager.ValidateAsync(item);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"The pay item '{item.ItemId}' was not imported: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (existing is null)
            {
                await _manager.CreateAsync(item);
            }
            else
            {
                await _manager.UpdateAsync(item);
            }
        }
    }
}
