using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// A row of pay inputs to save, as text: the run's number, the person's employee number, the pay code, and hours or an
/// amount (rate, date and note are optional).
/// </summary>
public sealed class PayRunInputRow
{
    /// <summary>Gets or sets the run's number.</summary>
    public string PayRun { get; set; }

    /// <summary>Gets or sets the person's employee number.</summary>
    public string EmployeeNumber { get; set; }

    /// <summary>Gets or sets the pay code.</summary>
    public string PayCode { get; set; }

    /// <summary>Gets or sets the hours, with a dot for decimals.</summary>
    public string Hours { get; set; }

    /// <summary>Gets or sets the amount, with a dot for decimals.</summary>
    public string Amount { get; set; }

    /// <summary>Gets or sets the rate, with a dot for decimals.</summary>
    public string Rate { get; set; }

    /// <summary>Gets or sets the day, such as 2026-11-20.</summary>
    public string Date { get; set; }

    /// <summary>Gets or sets a note.</summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets what identifies the row where it comes from (a key column, or a workflow and activity), so saving it
    /// again updates the input it made; empty to identify the row by its values.
    /// </summary>
    public string Key { get; set; }
}

/// <summary>
/// Saves rows as the inputs of pay runs, one at a time, keeping what it looked up; the Save pay inputs pipeline step
/// and workflow tasks use it. A row is refused with the reason when its run is missing, processed or of an employer
/// the user may not prepare runs for, when its person or pay code is unknown, or when a number can't be read. Runs
/// already calculated are calculated again, at the end, for the people whose inputs changed.
/// </summary>
public sealed class PayRunInputImporter
{
    private readonly IPayRunManager _runs;
    private readonly IPayRunInputManager _inputs;
    private readonly IPayRunService _service;
    private readonly IPayCodeManager _codes;
    private readonly IPayrollScopeService _scope;
    private readonly IPayProfileManager _profiles;
    private readonly IEmployerManager _employers;
    private readonly IStaffDirectory _staff;
    private readonly IPayProfileService _profileService;
    private readonly Dictionary<string, List<PayRunInput>> _inputsByRun = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PayRun> _runsByNumber = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _staffByEmployer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _changed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _mayPrepare = new(StringComparer.Ordinal);
    private Dictionary<string, PayCode> _codesByCode;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayRunInputImporter"/> class.
    /// </summary>
    /// <param name="runs">The runs.</param>
    /// <param name="inputs">The run inputs.</param>
    /// <param name="service">The run service.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="scope">The payroll scope.</param>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="staff">The staff directory.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="profileService">The pay profile service, for the rule that nobody enters their own pay.</param>
    public PayRunInputImporter(
        IPayRunManager runs,
        IPayRunInputManager inputs,
        IPayRunService service,
        IPayCodeManager codes,
        IPayrollScopeService scope,
        IPayProfileManager profiles,
        IEmployerManager employers,
        IStaffDirectory staff,
        IStringLocalizer<PayRunInputImporter> stringLocalizer,
        IPayProfileService profileService)
    {
        _profileService = profileService;
        _runs = runs;
        _inputs = inputs;
        _service = service;
        _codes = codes;
        _scope = scope;
        _profiles = profiles;
        _employers = employers;
        _staff = staff;
        S = stringLocalizer;
    }

    /// <summary>
    /// Saves a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="user">Who saves (the user a pipeline runs for).</param>
    /// <param name="origin">Where the row comes from, kept on the input.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Why the row was refused, or <see langword="null"/> when it was saved.</returns>
    public async Task<string> SaveAsync(PayRunInputRow row, ClaimsPrincipal user, string origin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var number = row.PayRun?.Trim();

        if (string.IsNullOrEmpty(number))
        {
            return S["The row names no pay run."];
        }

        var run = await RunAsync(number, user, cancellationToken);

        if (run is null)
        {
            return S["There is no pay run {0} the user may prepare.", number];
        }

        if (!run.IsOpen)
        {
            return S["Pay run {0} was processed or cancelled; it takes no more inputs.", number];
        }

        if (!await MayPrepareAsync(run, user))
        {
            return S["The user may not prepare the pay runs of the employer of run {0}.", number];
        }

        var employeeNumber = row.EmployeeNumber?.Trim();
        var staff = await StaffAsync(run.EmployerId, cancellationToken);

        if (string.IsNullOrEmpty(employeeNumber) || !staff.TryGetValue(employeeNumber, out var staffMemberId))
        {
            return S["No one with employee number {0} is paid by the employer of run {1}.", employeeNumber ?? string.Empty, number];
        }

        // Nobody enters their own pay, unless the setting allows changing one's own pay.
        if (!await _profileService.MayChangePayOfAsync(user, staffMemberId, cancellationToken))
        {
            return S["The user may not enter their own pay (employee number {0}).", employeeNumber];
        }

        _codesByCode ??= (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.Code, StringComparer.OrdinalIgnoreCase);

        var payCode = row.PayCode?.Trim();

        if (string.IsNullOrEmpty(payCode) || !_codesByCode.TryGetValue(payCode, out var code) || !code.Enabled)
        {
            return S["There is no pay code {0} in use.", payCode ?? string.Empty];
        }

        if (!TryNumber(row.Hours, out var hours) || !TryNumber(row.Amount, out var amount) || !TryNumber(row.Rate, out var rate))
        {
            return S["Hours, amounts and rates are numbers, with a dot for decimals."];
        }

        DateOnly? date = null;

        if (!string.IsNullOrWhiteSpace(row.Date))
        {
            var text = row.Date.Trim();

            if (!DateOnly.TryParse(text.Length > 10 ? text[..10] : text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return S["{0} is not a date such as 2026-11-20.", text];
            }

            date = parsed;
        }

        // A row saved again (the pipeline or the workflow ran twice) updates the input it made, never adds a second one.
        var importKey = ImportKey(origin, row, number, employeeNumber, payCode);
        var inputs = await RunInputsAsync(run.ItemId, cancellationToken);
        var existing = inputs.FirstOrDefault(candidate => candidate.ImportKey == importKey && candidate.StaffMemberId == staffMemberId);
        var input = existing ?? await _inputs.NewAsync(cancellationToken: cancellationToken);

        input.ImportKey = importKey;
        input.RunId = run.ItemId;
        input.StaffMemberId = staffMemberId;
        input.PayCodeId = code.ItemId;
        input.Hours = hours;
        input.Amount = amount;
        input.Rate = rate;
        input.Date = date;
        input.Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim();
        input.Origin = origin;

        var validation = await _inputs.ValidateAsync(input, cancellationToken);

        if (!validation.Succeeded)
        {
            return string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));
        }

        if (existing is null)
        {
            await _inputs.CreateAsync(input, cancellationToken);
            inputs.Add(input);
        }
        else
        {
            await _inputs.UpdateAsync(input, cancellationToken: cancellationToken);
        }

        if (!_changed.TryGetValue(run.ItemId, out var changed))
        {
            changed = new HashSet<string>(StringComparer.Ordinal);
            _changed[run.ItemId] = changed;
        }

        changed.Add(staffMemberId);

        return null;
    }

    /// <summary>
    /// Calculates again, for the people whose inputs changed, the runs that were already calculated.
    /// </summary>
    /// <param name="user">Who saves.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task RecalculateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        foreach (var (runId, staff) in _changed)
        {
            if (await _runs.FindByIdAsync(runId, cancellationToken) is { Status: not PayRunStatus.Draft } run && run.IsOpen)
            {
                await _service.CalculateAsync(run, user, staff, cancellationToken);
            }
        }

        _changed.Clear();
    }

    // What identifies a row: its key where it comes from, or else its values.
    private static string ImportKey(string origin, PayRunInputRow row, string run, string employeeNumber, string payCode)
    {
        var values = string.Join('|', run, employeeNumber, payCode, row.Hours?.Trim(), row.Amount?.Trim(), row.Rate?.Trim(), row.Date?.Trim(), row.Note?.Trim());
        var key = row.Key?.Trim();

        // A key that names the row (it has a part after the pipeline or the workflow) identifies it alone; a bare
        // origin is completed by the row's values.
        var identity = !string.IsNullOrEmpty(key) && key.Contains(':', StringComparison.Ordinal) ? "key:" + key : "values:" + key + "|" + values;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes((origin ?? string.Empty) + "\n" + identity));

        return Convert.ToHexString(hash, 0, 16);
    }

    private async Task<List<PayRunInput>> RunInputsAsync(string runId, CancellationToken cancellationToken)
    {
        if (!_inputsByRun.TryGetValue(runId, out var inputs))
        {
            inputs = [.. await _inputs.ListByRunAsync(runId, cancellationToken)];
            _inputsByRun[runId] = inputs;
        }

        return inputs;
    }

    private async Task<PayRun> RunAsync(string number, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!_runsByNumber.TryGetValue(number, out var run))
        {
            var scope = await _scope.GetScopeAsync(user, PayrollPermissions.PreparePayRuns, cancellationToken);
            var employers = scope.All
                ? [.. (await _employers.GetAllAsync(cancellationToken)).Select(employer => employer.ItemId)]
                : scope.EmployerIds.ToArray();

            run = (await _runs.ListByEmployersAsync(employers, cancellationToken)).FirstOrDefault(candidate => string.Equals(candidate.Number, number, StringComparison.OrdinalIgnoreCase));
            _runsByNumber[number] = run;
        }

        return run;
    }

    private async Task<bool> MayPrepareAsync(PayRun run, ClaimsPrincipal user)
    {
        if (!_mayPrepare.TryGetValue(run.EmployerId, out var may))
        {
            may = await _scope.AuthorizeAsync(user, PayrollPermissions.PreparePayRuns, run.EmployerId);
            _mayPrepare[run.EmployerId] = may;
        }

        return may;
    }

    // The employee numbers of the people an employer pays.
    private async Task<Dictionary<string, string>> StaffAsync(string employerId, CancellationToken cancellationToken)
    {
        if (!_staffByEmployer.TryGetValue(employerId, out var staff))
        {
            var profiles = await _profiles.ListByEmployersAsync([employerId], cancellationToken);
            var members = await _staff.GetAsync(profiles.Select(profile => profile.StaffMemberId), cancellationToken);

            staff = members.Values
                .Where(member => !string.IsNullOrEmpty(member.EmployeeNumber))
                .GroupBy(member => member.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().ItemId, StringComparer.OrdinalIgnoreCase);
            _staffByEmployer[employerId] = staff;
        }

        return staff;
    }

    private static bool TryNumber(string text, out decimal? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            value = number;

            return true;
        }

        return false;
    }
}
