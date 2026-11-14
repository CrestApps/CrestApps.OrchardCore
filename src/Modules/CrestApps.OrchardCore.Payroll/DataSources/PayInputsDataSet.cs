using System.Globalization;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>
/// Payroll's data source of Orchard Core, for data pipelines and reports, such as the export to an outside payroll
/// system.
/// </summary>
public sealed class PayrollDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public PayrollDataSource(IEnumerable<IPayrollDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the data source's technical name.
    /// </summary>
    public static string Name => PayrollConstants.DataSourceName;

    /// <summary>
    /// Gets the data source's name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PayrollDataSource>("Payroll");

    /// <summary>
    /// Gets what the data source holds.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<PayrollDataSource>("Payroll data: the hours and amounts other modules hand to payroll, such as locked timesheets.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// A data set of Payroll's data source.
/// </summary>
public interface IPayrollDataSet : IRecordDataSet
{
}

/// <summary>
/// A row of the Pay Inputs data set.
/// </summary>
/// <param name="Input">The pay input.</param>
/// <param name="Code">Its pay code, or <see langword="null"/>.</param>
/// <param name="Schedule">Its pay schedule, or <see langword="null"/>.</param>
public sealed record PayInputRow(PayInput Input, PayCode Code, PaySchedule Schedule)
{
    /// <summary>
    /// Gets what the input pays, not rounded: its own amount, or the hours times the code's premium, or times the rate
    /// and the code's multiplier. Unpaid time pays nothing; without a rate it is <see langword="null"/>.
    /// </summary>
    public decimal? Amount => PayInputsDataSet.AmountOf(Input, Code);
}

/// <summary>
/// The pay inputs of every source: one row per line a source hands to payroll, with the staff member, the pay code,
/// the period, the hours, the rate and the amount. By default it holds the inputs no pay run or export took yet.
/// </summary>
public sealed class PayInputsDataSet : RecordDataSet<PayInputRow>, IPayrollDataSet
{
    /// <summary>
    /// The parameter that includes the inputs already taken: <c>IncludeExported=true</c>.
    /// </summary>
    public const string IncludeExportedParameter = "IncludeExported";

    private readonly IPayInputService _inputs;
    private readonly IPayCodeManager _codes;
    private readonly IPayScheduleManager _schedules;
    private readonly IAuthorizationService _authorizationService;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayInputsDataSet"/> class.
    /// </summary>
    /// <param name="inputs">The pay inputs.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="schedules">The pay schedules.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="services">The services, for the payroll scope when the Payroll feature is on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayInputsDataSet(
        IPayInputService inputs,
        IPayCodeManager codes,
        IPayScheduleManager schedules,
        IAuthorizationService authorizationService,
        IServiceProvider services,
        IStringLocalizer<PayInputsDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            PayrollConstants.PayInputsDataSetName,
            stringLocalizer["Pay inputs"],
            stringLocalizer["One row per line handed to payroll, such as a locked timesheet's hours under a pay code on a day: who, the period, the code, the hours, the rate and the amount. Holds the lines not exported or paid yet; the parameter IncludeExported=true adds the others."]))
    {
        _inputs = inputs;
        _codes = codes;
        _schedules = schedules;
        _authorizationService = authorizationService;
        _services = services;

        var S = stringLocalizer;
        var person = S["Staff member"].Value;
        var period = S["Period"].Value;
        var pay = S["Pay"].Value;
        var source = S["Source"].Value;

        AddField("StaffMemberId", S["Staff member ID"], DataFieldType.Text, row => row.Input.StaffMemberId, person);
        AddField("PayrollId", S["Payroll ID"], DataFieldType.Text, row => row.Input.PayrollId, person).Description = S["The id an outside payroll system knows the person by, or the employee number."].Value;
        AddField("EmployeeNumber", S["Employee number"], DataFieldType.Text, row => row.Input.EmployeeNumber, person);
        AddField("StaffMemberName", S["Name"], DataFieldType.Text, row => row.Input.StaffMemberName, person);
        AddField("PayScheduleId", S["Pay schedule ID"], DataFieldType.Text, row => row.Input.PayScheduleId, period);
        AddField("PayScheduleName", S["Pay schedule"], DataFieldType.Text, row => row.Schedule?.Name, period);
        AddField("PeriodStart", S["Period start"], DataFieldType.Date, row => row.Input.PeriodStart.ToDateTime(TimeOnly.MinValue), period);
        AddField("PeriodEnd", S["Period end"], DataFieldType.Date, row => row.Input.PeriodEnd.ToDateTime(TimeOnly.MinValue), period);
        AddField("PayCodeId", S["Pay code ID"], DataFieldType.Text, row => row.Input.PayCodeId, pay);
        AddField("PayCode", S["Pay code"], DataFieldType.Text, row => row.Code?.PayrollCode, pay).Description = S["The code's export code, or the code."].Value;
        AddField("PayCodeName", S["Pay code name"], DataFieldType.Text, row => row.Code?.Name, pay);
        AddField("PayCodeKind", S["Pays for"], DataFieldType.Text, row => row.Code?.Kind, pay);
        AddField("Date", S["Day"], DataFieldType.Date, row => row.Input.Date?.ToDateTime(TimeOnly.MinValue), pay);
        AddField("Hours", S["Hours"], DataFieldType.Decimal, row => row.Input.Hours, pay);
        AddField("Rate", S["Rate"], DataFieldType.Decimal, row => row.Input.Rate, pay).Description = S["The rate an hour on the day, from the staff member's pay."].Value;
        AddField("Amount", S["Amount"], DataFieldType.Decimal, row => row.Amount, pay).Description = S["Not rounded: the input's own amount, or the hours times the code's premium, or times the rate and the code's multiplier. Round it after summing."].Value;
        AddField("ActivityCode", S["Activity"], DataFieldType.Text, row => row.Input.Allocation?.ActivityCode, pay);
        AddField("Job", S["Job"], DataFieldType.Text, row => row.Input.Allocation?.Job, pay);
        AddField("DepartmentTeamId", S["Department team ID"], DataFieldType.Text, row => row.Input.Allocation?.DepartmentTeamId, pay);
        AddField("SiteTeamId", S["Site team ID"], DataFieldType.Text, row => row.Input.Allocation?.SiteTeamId, pay);
        AddField("SourceKind", S["Source kind"], DataFieldType.Text, row => row.Input.SourceKind, source).Description = S["What the line comes from, such as Timesheet. The Mark pay inputs exported step reads it."].Value;
        AddField("SourceId", S["Source ID"], DataFieldType.Text, row => row.Input.SourceId, source);
        AddField("Exported", S["Exported or paid"], DataFieldType.Boolean, row => row.Input.Consumed, source);

        Descriptor.DefaultDateField = "PeriodStart";
    }

    /// <summary>
    /// Works out what an input pays, not rounded.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="code">Its pay code.</param>
    /// <returns>The amount, or <see langword="null"/> when there is no rate.</returns>
    public static decimal? AmountOf(PayInput input, PayCode code)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Amount is { } amount)
        {
            return amount;
        }

        if (code is null)
        {
            return null;
        }

        if (code.Kind == PayCodeKinds.Unpaid)
        {
            return 0;
        }

        var hours = input.Hours ?? 0;

        if (code.PremiumPerHour is > 0)
        {
            return hours * code.PremiumPerHour.Value;
        }

        return input.Rate is { } rate ? hours * rate * code.Multiplier : null;
    }

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await GetScopeAsync(context?.User, cancellationToken)).IsEmpty;

    /// <summary>
    /// Gets the employers a reader may export the data of: every one with Export payroll data, otherwise those of the
    /// per-employer permissions they hold when the Payroll feature is on.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope.</returns>
    internal async Task<PayrollScope> GetScopeAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return PayrollScope.None;
        }

        if (_services.GetService(typeof(IPayrollScopeService)) is IPayrollScopeService scope)
        {
            return await scope.GetScopeAsync(user, PayrollPermissions.ExportPayrollData, cancellationToken);
        }

        return await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ExportPayrollData) ? PayrollScope.Everyone : PayrollScope.None;
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<PayInputRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var conditions = query.Conditions ?? [];
        var schedules = DataConditions.Keys(conditions, "PayScheduleId")?.Select(key => key?.ToString()).ToHashSet(StringComparer.Ordinal);

        if (schedules is { Count: 0 })
        {
            yield break;
        }

        // A reader of some employers reads the inputs of their people only.
        var scope = await GetScopeAsync(query.Context?.User, cancellationToken);
        IReadOnlyCollection<string> staff = [];

        if (scope.IsEmpty)
        {
            yield break;
        }

        if (!scope.All)
        {
            staff = _services.GetService(typeof(IPayrollEmployeeDirectory)) is IPayrollEmployeeDirectory directory
                ? await directory.GetStaffMemberIdsAsync([.. scope.EmployerIds], cancellationToken)
                : [];

            if (staff.Count == 0)
            {
                yield break;
            }
        }

        var (from, to) = DataConditions.DateRange(conditions, "PeriodStart");
        var inputQuery = new PayInputQuery
        {
            StaffMemberIds = staff,
            PayScheduleId = schedules is { Count: 1 } ? schedules.First() : null,
            PeriodStart = from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
            PeriodEnd = to.HasValue ? DateOnly.FromDateTime(to.Value) : null,
            IncludeConsumed = IncludesExported(query.Parameters),
        };

        var codes = (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var allSchedules = (await _schedules.GetAllAsync(cancellationToken)).ToDictionary(schedule => schedule.ItemId, StringComparer.Ordinal);

        foreach (var input in await _inputs.GetInputsAsync(inputQuery, cancellationToken))
        {
            if (schedules is not null && !schedules.Contains(input.PayScheduleId))
            {
                continue;
            }

            yield return new PayInputRow(
                input,
                input.PayCodeId is null ? null : codes.GetValueOrDefault(input.PayCodeId),
                input.PayScheduleId is null ? null : allSchedules.GetValueOrDefault(input.PayScheduleId));
        }
    }

    private static bool IncludesExported(IDictionary<string, object> parameters)
        => parameters is not null
            && parameters.TryGetValue(IncludeExportedParameter, out var value)
            && bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var include)
            && include;
}
