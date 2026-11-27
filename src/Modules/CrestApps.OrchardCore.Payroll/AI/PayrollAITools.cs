using CrestApps.Core.Security;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Payroll.AI;

/// <summary>
/// A Payroll AI tool: it runs for the signed-in person and checks the permission of the page it mirrors. A tool never
/// returns a tax id or a bank account number.
/// </summary>
public abstract class PayrollAIToolBase : AIFunction
{
    /// <summary>
    /// The options the tools write their answers with.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var user = arguments.Services?.GetService<IUserAccessor>()?.User ?? arguments.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return "Payroll tools can only be used by a signed-in person.";
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in person.
    /// </summary>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Writes a day as yyyy-MM-dd.
    /// </summary>
    protected static string Day(DateOnly day)
        => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Describes the lines that changed, for an answer.
    /// </summary>
    protected static IEnumerable<object> Changes(IEnumerable<PayStatementLineChange> changes, int take = 10)
        => changes.Take(take).Select(change => new
        {
            code = change.Key,
            description = change.Description,
            type = change.Type.ToString(),
            previous = change.Previous,
            current = change.Current,
            change = change.Change,
        });
}

/// <summary>
/// Explains the signed-in person's own pay statement, the latest or one by number, and what changed since their pay
/// before it. It mirrors My Pay and needs <see cref="PayrollPermissions.ViewOwnPay"/>.
/// </summary>
public sealed class MyPayStatementTool : PayrollAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "payrollMyPayStatement";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "number": { "type": "string", "description": "The statement's number. Leave out for the latest." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Explains the user's own pay statement (the latest, or one by number): each earning, deduction and tax with its amount, gross and net pay, how net pay was paid, time-off balances, and what changed since the pay before it.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, PayrollPermissions.ViewOwnPay))
        {
            return "You may not see pay statements.";
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || await services.GetRequiredService<IStaffDirectory>().FindByUserIdAsync(userId, cancellationToken) is not { } me)
        {
            return "Your account isn't linked to a staff member, so there is no pay to explain.";
        }

        var meId = me.ItemId;
        var draft = nameof(PayStatementStatus.Draft);
        var statements = (await services.GetRequiredService<PayStatementStore>().ListAsync(index => index.StaffMemberId == meId && index.Status != draft, cancellationToken))
            .Where(PayStatementComparison.Counts)
            .OrderByDescending(statement => statement.PayDate)
            .ThenByDescending(statement => statement.Number, StringComparer.Ordinal)
            .ToList();
        var number = Read(arguments, "number");
        var statement = number is null ? statements.FirstOrDefault() : statements.FirstOrDefault(candidate => string.Equals(candidate.Number, number, StringComparison.OrdinalIgnoreCase));

        if (statement is null)
        {
            return number is null ? "You have no pay statement yet." : $"You have no pay statement numbered {number}.";
        }

        var previous = statements.Skip(statements.IndexOf(statement) + 1).FirstOrDefault(candidate => candidate.EmployerId == statement.EmployerId);
        var codes = (await services.GetRequiredService<IPayCodeManager>().GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);

        return JsonSerializer.Serialize(new
        {
            number = statement.Number,
            employer = statement.Snapshot?.EmployerName,
            payDate = Day(statement.PayDate),
            periodStart = Day(statement.PeriodStart),
            periodEnd = Day(statement.PeriodEnd),
            currency = statement.Currency,
            gross = statement.Gross,
            net = statement.Net,
            lines = statement.Lines.Select(line => new
            {
                code = line.PayCodeId is not null && codes.TryGetValue(line.PayCodeId, out var code) ? code.Code : line.TaxCode,
                description = line.Description,
                type = line.Type.ToString(),
                paidByEmployer = line.Type == PayLineType.Tax && line.Payer == PayLinePayer.Employer,
                hours = line.Hours,
                rate = line.Rate,
                amount = line.Amount,
            }),

            // How net pay was paid, never to which account.
            payments = statement.Payments.Select(payment => new { method = payment.Method == NetPayMethod.BankAccount ? "Deposit" : "Check", amount = payment.Amount }),
            balances = statement.Balances.Select(balance => new { name = balance.Name, unit = balance.Unit, balance = balance.Balance }),
            previous = previous is null ? null : new
            {
                number = previous.Number,
                payDate = Day(previous.PayDate),
                gross = previous.Gross,
                net = previous.Net,
                changes = Changes(PayStatementComparison.Compare([statement], [previous], codes)),
            },
        }, JsonOptions);
    }
}

/// <summary>
/// Says what changed in a pay run since the run before it of the same employer and pay schedule: the totals, who joined
/// or left, and whose net pay changed and why. It mirrors the pay run page and needs
/// <see cref="PayrollPermissions.ViewPayRuns"/> for the run's employer.
/// </summary>
public sealed class PayRunChangesTool : PayrollAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "payrollPayRunChanges";

    private const int MaxPeople = 25;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "run": { "type": "string", "description": "The pay run's number, such as 12." }
          },
          "required": ["run"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Compares a pay run with the run before it of the same employer and pay schedule: the change in gross and net pay, taxes and deductions, who joined or left, and whose net pay changed with the lines that made the difference.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (Read(arguments, "run") is not { } number)
        {
            return "Say which pay run, by its number.";
        }

        var runs = services.GetRequiredService<PayRunStore>();
        var scope = services.GetRequiredService<IPayrollScopeService>();
        PayRun run = null;

        foreach (var candidate in await runs.ListAsync(index => index.Number == number, cancellationToken))
        {
            if (await scope.AuthorizeAsync(user, PayrollPermissions.ViewPayRuns, candidate.EmployerId))
            {
                run = candidate;

                break;
            }
        }

        // A run of an employer the person may not see is never found.
        if (run is null)
        {
            return $"There's no pay run numbered {number} that you may see.";
        }

        var employerId = run.EmployerId;
        var scheduleId = run.PayScheduleId;
        var paid = nameof(PayRunStatus.Paid);
        var partly = nameof(PayRunStatus.PartiallyPaid);
        var previous = (await runs.ListAsync(index => index.EmployerId == employerId && index.PayScheduleId == scheduleId && (index.Status == paid || index.Status == partly), cancellationToken))
            .Where(candidate => candidate.ItemId != run.ItemId && candidate.PayDate < run.PayDate && candidate.Type is not (PayRunType.Reversal or PayRunType.Historical))
            .OrderByDescending(candidate => candidate.PayDate)
            .FirstOrDefault();
        var service = services.GetRequiredService<IPayRunService>();
        var codes = (await services.GetRequiredService<IPayCodeManager>().GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var now = (await service.ListStatementsAsync(run, cancellationToken)).Where(PayStatementComparison.Counts).ToList();

        if (previous is null)
        {
            return JsonSerializer.Serialize(new
            {
                run = Describe(run),
                previous = (object)null,
                note = "This is the first paid run of its employer and pay schedule, so there is nothing to compare it with.",
            }, JsonOptions);
        }

        var before = (await service.ListStatementsAsync(previous, cancellationToken)).Where(PayStatementComparison.Counts).ToList();
        var nowByPerson = now.ToLookup(statement => statement.StaffMemberId, StringComparer.Ordinal);
        var beforeByPerson = before.ToLookup(statement => statement.StaffMemberId, StringComparer.Ordinal);

        string NameOf(string staffMemberId)
            => nowByPerson[staffMemberId].Concat(beforeByPerson[staffMemberId]).Select(statement => statement.Snapshot?.EmployeeName).FirstOrDefault(name => !string.IsNullOrEmpty(name)) ?? staffMemberId;

        var people = nowByPerson.Select(group => group.Key).Intersect(beforeByPerson.Select(group => group.Key), StringComparer.Ordinal)
            .Select(staffMemberId => new
            {
                name = NameOf(staffMemberId),
                previousNet = beforeByPerson[staffMemberId].Sum(statement => statement.Net),
                net = nowByPerson[staffMemberId].Sum(statement => statement.Net),
                lines = PayStatementComparison.Compare(nowByPerson[staffMemberId], beforeByPerson[staffMemberId], codes),
            })
            .Where(person => person.net != person.previousNet)
            .OrderByDescending(person => Math.Abs(person.net - person.previousNet))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            run = Describe(run),
            previous = Describe(previous),
            change = new
            {
                statements = run.Totals.Statements - previous.Totals.Statements,
                gross = run.Totals.Gross - previous.Totals.Gross,
                net = run.Totals.Net - previous.Totals.Net,
                employeeTaxes = run.Totals.EmployeeTaxes - previous.Totals.EmployeeTaxes,
                employerTaxes = run.Totals.EmployerTaxes - previous.Totals.EmployerTaxes,
                deductions = run.Totals.Deductions - previous.Totals.Deductions,
                employerContributions = run.Totals.EmployerContributions - previous.Totals.EmployerContributions,
            },
            joined = nowByPerson.Select(group => group.Key).Except(beforeByPerson.Select(group => group.Key), StringComparer.Ordinal).Select(NameOf),
            left = beforeByPerson.Select(group => group.Key).Except(nowByPerson.Select(group => group.Key), StringComparer.Ordinal).Select(NameOf),
            netChanged = people.Take(MaxPeople).Select(person => new
            {
                person.name,
                person.previousNet,
                person.net,
                change = person.net - person.previousNet,
                lines = Changes(person.lines, 5),
            }),
            morePeople = Math.Max(0, people.Count - MaxPeople),
            lines = Changes(PayStatementComparison.Compare(now, before, codes)),
        }, JsonOptions);
    }

    private static object Describe(PayRun run)
        => new
        {
            number = run.Number,
            type = run.Type.ToString(),
            status = run.Status.ToString(),
            periodStart = Day(run.PeriodStart),
            periodEnd = Day(run.PeriodEnd),
            payDate = Day(run.PayDate),
            statements = run.Totals.Statements,
            gross = run.Totals.Gross,
            net = run.Totals.Net,
        };
}
