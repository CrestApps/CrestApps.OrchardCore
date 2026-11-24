using System.Globalization;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>
/// A <see cref="PayrollTaxForm"/> record as a form employers file: prepares a filing by working out each reported
/// person's boxes from their statements, adding up the person boxes the form totals, then working out the filing boxes
/// and the checks.
/// </summary>
public sealed class PayrollTaxFormFiling : IPayrollFilingForm
{
    private readonly PayrollTaxFormFunctionCatalog _functions;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxFormFiling"/> class.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="functions">The functions of the formulas.</param>
    /// <param name="localizer">The localizer of the messages.</param>
    public PayrollTaxFormFiling(PayrollTaxForm form, PayrollTaxFormFunctionCatalog functions, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(localizer);

        Form = form;
        _functions = functions;
        S = localizer;
        // The title is what the site's administrator typed, so there is no template to extract.
#pragma warning disable OCLOC103 // The value of the localization source is not a compile-time constant
        DisplayName = LocalizationSource.Create<PayrollTaxFormFiling>(string.IsNullOrWhiteSpace(form.DisplayName) ? form.Name : form.DisplayName);
#pragma warning restore OCLOC103
    }

    /// <summary>Gets the form record.</summary>
    [JsonIgnore]
    public PayrollTaxForm Form { get; }

    /// <inheritdoc />
    public string Name => Form.Name;

    /// <inheritdoc />
    [JsonIgnore]
    public LocalizationSource DisplayName { get; }

    /// <inheritdoc />
    public string Jurisdiction => Form.Jurisdiction;

    /// <inheritdoc />
    public bool IsQuarterly => Form.IsQuarterly;

    /// <inheritdoc />
    public bool HasPersonStatements => Form.HasPersonStatements;

    /// <inheritdoc />
    public async Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var formulas = PayrollTaxFormFormulas.Compile(Form, _functions.Functions, S, out var errors)
            ?? throw new InvalidOperationException(S["The formulas of {0} don't work: {1}", Form.Name, string.Join(" ", errors.Select(error => error.Message))]);
        var rules = context.Services.GetService<IIncomeTaxRuleManager>() is { } manager
            ? (await manager.ListAsync(cancellationToken)).Where(rule => rule.AppliesOn(context.To)).ToList()
            : [];

        Prepare(formulas, context, rules);
    }

    /// <summary>
    /// Prepares a filing from compiled formulas.
    /// </summary>
    /// <param name="formulas">The form's compiled formulas.</param>
    /// <param name="context">The filing context.</param>
    /// <param name="rules">The income tax rules that apply on the period's last day.</param>
    public static void Prepare(PayrollTaxFormFormulas formulas, PayrollFilingContext context, IReadOnlyList<IncomeTaxRule> rules)
    {
        ArgumentNullException.ThrowIfNull(formulas);
        ArgumentNullException.ThrowIfNull(context);

        var form = formulas.Form;
        var reported = Reported(form, context);
        var formula = new PayrollTaxFormFormulaContext
        {
            Form = form,
            Employer = context.Employer,
            Filing = context.Filing,
            From = context.From,
            To = context.To,
            Codes = context.Codes,
            Deposits = context.Deposits,
            Rules = rules ?? [],
            Services = context.Services,
        };
        var totals = formulas.TotaledFields.ToDictionary(box => box.Number, _ => 0m, StringComparer.Ordinal);
        var people = reported
            .GroupBy(statement => statement.ProfileId ?? statement.StaffMemberId, StringComparer.Ordinal)
            .OrderBy(group => group.Last().Snapshot?.EmployeeName, StringComparer.CurrentCultureIgnoreCase);

        if (formulas.PersonFields.Count > 0)
        {
            foreach (var person in people)
            {
                var all = person.ToList();
                var latest = all.Where(statement => statement.VoidOf is null).OrderBy(statement => statement.PayDate).LastOrDefault() ?? all[^1];
                var profile = latest.ProfileId is not null && context.Profiles.TryGetValue(latest.ProfileId, out var found) ? found : null;
                var noted = false;
                IReadOnlyList<(string Jurisdiction, IReadOnlyList<PayStatement> Statements)> parts = form.StatementPerJurisdiction
                    ? PayrollFilingSums.ByJurisdictionUnder(all, form.Jurisdiction)
                    : [(null, all)];

                foreach (var (jurisdiction, statements) in parts)
                {
                    var (row, boxes) = WorkPerson(formulas, formula, statements);

                    var include = formulas.PersonCondition is { } condition
                        ? PayrollTaxFormFormulas.IsTrue(condition.Evaluate(new ExpressionContext { Row = row }))
                        : boxes.Values.Any(value => value != 0);

                    if (!include)
                    {
                        continue;
                    }

                    if (!noted && profile?.TaxId is null && !string.IsNullOrWhiteSpace(form.MissingTaxIdNote))
                    {
                        context.Filing.Notes.Add(string.Format(CultureInfo.CurrentCulture, form.MissingTaxIdNote, latest.Snapshot?.EmployeeName));
                        noted = true;
                    }

                    foreach (var box in formulas.TotaledFields)
                    {
                        totals[box.Number] += (decimal)row[formulas.PersonSlot(box.Number)];
                    }

                    formula.People.Add(boxes);

                    if (form.HasPersonStatements)
                    {
                        context.Filing.People.Add(new PayrollFilingPerson
                        {
                            StaffMemberId = latest.StaffMemberId,
                            ProfileId = latest.ProfileId,
                            Name = string.IsNullOrEmpty(profile?.LegalName) ? latest.Snapshot?.EmployeeName : profile.LegalName,
                            Address = [.. latest.Snapshot?.EmployeeAddress ?? []],
                            TaxIdHint = profile?.TaxId?.Hint,
                            Jurisdiction = jurisdiction,
                            Boxes = boxes,
                            JurisdictionAmounts = form.ListJurisdictionAmounts ? [.. PayrollFilingSums.JurisdictionAmounts(statements, context.Employer)] : [],
                        });
                    }
                }
            }
        }

        // The filing boxes, worked out on every reported statement, after the totals of the person boxes.
        formula.Statements = reported;

        var filingRow = formulas.FilingRow(formula);

        foreach (var box in formulas.TotaledFields)
        {
            filingRow[formulas.FilingSlot(box.Number)] = totals[box.Number];

            if (!string.IsNullOrWhiteSpace(box.Formula) || totals[box.Number] != 0)
            {
                context.Filing.Boxes[box.Number] = totals[box.Number];
            }
        }

        foreach (var box in formulas.FilingFields)
        {
            var value = Work(formulas, box, filingRow, reported, context.Codes);

            filingRow[formulas.FilingSlot(box.Number)] = value;

            if (!string.IsNullOrWhiteSpace(box.Formula) || value != 0)
            {
                context.Filing.Boxes[box.Number] = value;
            }
        }

        // A failed check is noted before what the people's statements noted.
        var position = 0;

        foreach (var (check, condition) in formulas.Checks)
        {
            if (!PayrollTaxFormFormulas.IsTrue(condition.Evaluate(new ExpressionContext { Row = filingRow })))
            {
                context.Filing.Notes.Insert(position++, check.Note ?? check.Condition);
            }
        }
    }

    /// <summary>
    /// Works out a person's boxes from their statements, as a filing would, whoever the form reports.
    /// </summary>
    /// <param name="formulas">The form's compiled formulas.</param>
    /// <param name="context">The formula context.</param>
    /// <param name="statements">The person's statements.</param>
    /// <returns>Every person box, zero or not.</returns>
    public static IReadOnlyDictionary<string, decimal> PersonFields(PayrollTaxFormFormulas formulas, PayrollTaxFormFormulaContext context, IReadOnlyList<PayStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(formulas);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(statements);

        var (row, _) = WorkPerson(formulas, context, statements);

        return formulas.PersonFields.ToDictionary(box => box.Number, box => (decimal)row[formulas.PersonSlot(box.Number)], StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the statements a form reports: those of its workers, of people with a tax line of its people's jurisdiction.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="context">The filing context.</param>
    /// <returns>The statements, by pay date.</returns>
    public static IReadOnlyList<PayStatement> Reported(PayrollTaxForm form, PayrollFilingContext context)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(context);

        var statements = context.Statements.Where(statement => form.Workers switch
        {
            PayrollTaxFormWorkers.Employees => !IsContractor(context, statement),
            PayrollTaxFormWorkers.Contractors => IsContractor(context, statement),
            _ => true,
        }).ToList();

        if (string.IsNullOrWhiteSpace(form.PeopleJurisdiction))
        {
            return statements;
        }

        var jurisdiction = form.PeopleJurisdiction.Trim();
        var people = statements
            .Where(statement => statement.Lines.Any(line => line.Type == PayLineType.Tax && line.Jurisdiction is { } candidate
                && (string.Equals(candidate, jurisdiction, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(jurisdiction + "-", StringComparison.OrdinalIgnoreCase))))
            .Select(statement => statement.ProfileId ?? statement.StaffMemberId)
            .ToHashSet(StringComparer.Ordinal);

        return [.. statements.Where(statement => people.Contains(statement.ProfileId ?? statement.StaffMemberId))];
    }

    // Works out a person's boxes in order: the row the formulas read, and the boxes kept on the statement (a box with
    // no formula only when a pay code reported to it).
    private static (object[] Row, Dictionary<string, decimal> Boxes) WorkPerson(PayrollTaxFormFormulas formulas, PayrollTaxFormFormulaContext context, IReadOnlyList<PayStatement> statements)
    {
        context.Statements = statements;

        var row = formulas.PersonRow(context);
        var boxes = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var box in formulas.PersonFields)
        {
            var value = Work(formulas, box, row, statements, context.Codes);

            row[formulas.PersonSlot(box.Number)] = value;

            if (!string.IsNullOrWhiteSpace(box.Formula) || value != 0)
            {
                boxes[box.Number] = value;
            }
        }

        return (row, boxes);
    }

    private static decimal Work(PayrollTaxFormFormulas formulas, PayrollTaxFormField box, object[] row, IReadOnlyList<PayStatement> statements, IReadOnlyDictionary<string, PayCode> codes)
    {
        var value = formulas.Compiled.TryGetValue(box, out var compiled)
            ? PayrollTaxFormFormulas.Amount(compiled.Evaluate(new ExpressionContext { Row = row }))
            : 0m;

        if (box.PayCodes)
        {
            value += PayrollFilingSums.ReportedTo(statements, codes, formulas.Form.EffectivePayCodeFieldPrefix + box.Number);
        }

        return value;
    }

    private static bool IsContractor(PayrollFilingContext context, PayStatement statement)
        => statement.ProfileId is not null && context.Profiles.TryGetValue(statement.ProfileId, out var profile) && profile.WorkerType == PayWorkerType.Contractor;
}
