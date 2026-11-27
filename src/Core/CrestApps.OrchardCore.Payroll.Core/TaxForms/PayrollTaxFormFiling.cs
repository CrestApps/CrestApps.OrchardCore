using System.Globalization;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>
/// A Taxation tax form whose source is Payroll, as a form employers file: each reported person (or each person and
/// jurisdiction) is a recipient whose pay statements the recipient fields read, every reported statement is what the
/// filing fields read, and pay codes report amounts to the fields that accept them.
/// </summary>
public sealed class PayrollTaxFormFiling : IPayrollFilingForm
{
    private readonly TaxFormFunctionCatalog _functions;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxFormFiling"/> class.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="functions">The functions of the formulas.</param>
    /// <param name="localizer">The localizer of the messages.</param>
    public PayrollTaxFormFiling(TaxForm form, TaxFormFunctionCatalog functions, IStringLocalizer localizer)
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
    public TaxForm Form { get; }

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
    public bool HasPersonStatements => Form.HasRecipientStatements;

    /// <inheritdoc />
    public async Task PrepareAsync(PayrollFilingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var formulas = TaxFormFormulas.Compile(Form, _functions.For(Form.Source), S, out var errors)
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
    public static void Prepare(TaxFormFormulas formulas, PayrollFilingContext context, IReadOnlyList<IncomeTaxRule> rules)
    {
        ArgumentNullException.ThrowIfNull(formulas);
        ArgumentNullException.ThrowIfNull(context);

        var form = formulas.Form;
        var settings = form.GetPayrollSettings();
        var reported = Reported(form, context);
        var filingData = Data(context, rules, reported);
        var recipients = new List<TaxFormRecipientInput>();

        foreach (var person in reported
            .GroupBy(statement => statement.ProfileId ?? statement.StaffMemberId, StringComparer.Ordinal)
            .OrderBy(group => group.Last().Snapshot?.EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            var all = person.ToList();
            var latest = all.Where(statement => statement.VoidOf is null).OrderBy(statement => statement.PayDate).LastOrDefault() ?? all[^1];
            var profile = latest.ProfileId is not null && context.Profiles.TryGetValue(latest.ProfileId, out var found) ? found : null;
            IReadOnlyList<(string Jurisdiction, IReadOnlyList<PayStatement> Statements)> parts = form.StatementPerJurisdiction
                ? PayrollFilingSums.ByJurisdictionUnder(all, form.Jurisdiction)
                : [(null, all)];

            foreach (var (jurisdiction, statements) in parts)
            {
                recipients.Add(new TaxFormRecipientInput
                {
                    Data = filingData with { Statements = statements },
                    Tag = new Recipient(person.Key, latest, profile, jurisdiction, statements),
                });
            }
        }

        var result = TaxFormEvaluator.Evaluate(new TaxFormEvaluation
        {
            Formulas = formulas,
            Context = new TaxFormFormulaContext
            {
                Form = form,
                Year = context.Filing.Year,
                Quarter = context.Filing.Quarter,
                From = context.From,
                To = context.To,
                Services = context.Services,
            },
            Recipients = recipients,
            FilingData = filingData,
            ReportedAmount = (field, data) => PayrollFilingSums.ReportedTo(((PayrollTaxFormData)data).Statements, context.Codes, form.PayCodeFieldPrefix() + field.Number),
        });

        // A failed check is noted before what the people's statements noted.
        foreach (var note in result.FailedChecks)
        {
            context.Filing.Notes.Add(note);
        }

        var noted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reportedRecipient in result.Recipients)
        {
            var recipient = (Recipient)reportedRecipient.Input.Tag;

            if (recipient.Profile?.TaxId is null && !string.IsNullOrWhiteSpace(form.MissingTaxIdNote) && noted.Add(recipient.Key ?? string.Empty))
            {
                context.Filing.Notes.Add(string.Format(CultureInfo.CurrentCulture, form.MissingTaxIdNote, recipient.Latest.Snapshot?.EmployeeName));
            }

            if (form.HasRecipientStatements)
            {
                context.Filing.People.Add(new PayrollFilingPerson
                {
                    StaffMemberId = recipient.Latest.StaffMemberId,
                    ProfileId = recipient.Latest.ProfileId,
                    Name = string.IsNullOrEmpty(recipient.Profile?.LegalName) ? recipient.Latest.Snapshot?.EmployeeName : recipient.Profile.LegalName,
                    Address = [.. recipient.Latest.Snapshot?.EmployeeAddress ?? []],
                    TaxIdHint = recipient.Profile?.TaxId?.Hint,
                    Jurisdiction = recipient.Jurisdiction,
                    Boxes = new Dictionary<string, decimal>(reportedRecipient.Fields, StringComparer.Ordinal),
                    JurisdictionAmounts = settings.ListJurisdictionAmounts ? [.. PayrollFilingSums.JurisdictionAmounts(recipient.Statements, context.Employer)] : [],
                });
            }
        }

        foreach (var (number, value) in result.Fields)
        {
            context.Filing.Boxes[number] = value;
        }
    }

    /// <summary>
    /// Works out a person's fields from their statements, as a filing would, whoever the form reports.
    /// </summary>
    /// <param name="formulas">The form's compiled formulas.</param>
    /// <param name="context">The formula context.</param>
    /// <param name="data">The person's statements and the period's pay codes, deposits, rules and employer.</param>
    /// <returns>Every recipient field, zero or not.</returns>
    public static IReadOnlyDictionary<string, decimal> PersonFields(TaxFormFormulas formulas, TaxFormFormulaContext context, PayrollTaxFormData data)
    {
        ArgumentNullException.ThrowIfNull(formulas);
        ArgumentNullException.ThrowIfNull(data);

        return TaxFormEvaluator.RecipientFields(formulas, context, data, (field, scope) => PayrollFilingSums.ReportedTo(((PayrollTaxFormData)scope).Statements, data.Codes, formulas.Form.PayCodeFieldPrefix() + field.Number));
    }

    /// <summary>
    /// Gets the statements a form reports: those of its workers, of people with a tax line of its people's jurisdiction.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="context">The filing context.</param>
    /// <returns>The statements, by pay date.</returns>
    public static IReadOnlyList<PayStatement> Reported(TaxForm form, PayrollFilingContext context)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(context);

        var settings = form.GetPayrollSettings();
        var statements = context.Statements.Where(statement => settings.Workers switch
        {
            PayrollTaxFormWorkers.Employees => !IsContractor(context, statement),
            PayrollTaxFormWorkers.Contractors => IsContractor(context, statement),
            _ => true,
        }).ToList();

        if (string.IsNullOrWhiteSpace(settings.PeopleJurisdiction))
        {
            return statements;
        }

        var jurisdiction = settings.PeopleJurisdiction.Trim();
        var people = statements
            .Where(statement => statement.Lines.Any(line => line.Type == PayLineType.Tax && line.Jurisdiction is { } candidate
                && (string.Equals(candidate, jurisdiction, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(jurisdiction + "-", StringComparison.OrdinalIgnoreCase))))
            .Select(statement => statement.ProfileId ?? statement.StaffMemberId)
            .ToHashSet(StringComparer.Ordinal);

        return [.. statements.Where(statement => people.Contains(statement.ProfileId ?? statement.StaffMemberId))];
    }

    private static PayrollTaxFormData Data(PayrollFilingContext context, IReadOnlyList<IncomeTaxRule> rules, IReadOnlyList<PayStatement> statements)
        => new()
        {
            Statements = statements,
            Codes = context.Codes,
            Deposits = context.Deposits,
            Rules = rules ?? [],
            Employer = context.Employer,
            To = context.To,
        };

    private static bool IsContractor(PayrollFilingContext context, PayStatement statement)
        => statement.ProfileId is not null && context.Profiles.TryGetValue(statement.ProfileId, out var profile) && profile.WorkerType == PayWorkerType.Contractor;

    // A person's statement (or one of them, for a form with a statement per jurisdiction).
    private sealed record Recipient(string Key, PayStatement Latest, EmployeePayProfile Profile, string Jurisdiction, IReadOnlyList<PayStatement> Statements);
}
