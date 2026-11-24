using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Payroll.Core.TaxForms;

/// <summary>Indexes tax forms.</summary>
public sealed class PayrollTaxFormIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the stored name, in capitals.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }
}

/// <summary>Maps tax forms to their index rows.</summary>
public sealed class PayrollTaxFormIndexProvider : IndexProvider<PayrollTaxForm>
{
    /// <summary>Initializes a new instance of the <see cref="PayrollTaxFormIndexProvider"/> class.</summary>
    public PayrollTaxFormIndexProvider()
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<PayrollTaxForm> context)
        => context
            .For<PayrollTaxFormIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(form => new PayrollTaxFormIndex
            {
                ItemId = form.ItemId,
                Name = form.Name?.Trim().ToUpperInvariant(),
                Jurisdiction = form.Jurisdiction,
            });
}

/// <summary>Creates the table of tax forms.</summary>
public static class PayrollTaxFormIndexSchema
{
    /// <summary>Creates the table.</summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<PayrollTaxFormIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(50))
            .Column<string>("Jurisdiction", column => column.Nullable().WithLength(20)),
            collection: PayrollConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<PayrollTaxFormIndex>(table => table
            .CreateIndex("IDX_PayrollTaxFormIndex_Name", "Name"),
            collection: PayrollConstants.CollectionName);
    }
}

/// <summary>Stores tax forms in the Payroll collection.</summary>
public sealed class PayrollTaxFormStore : DocumentCatalog<PayrollTaxForm, PayrollTaxFormIndex>
{
    /// <summary>Initializes a new instance of the <see cref="PayrollTaxFormStore"/> class.</summary>
    /// <param name="session">The session.</param>
    public PayrollTaxFormStore(ISession session)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
    }

    /// <summary>Finds a form by its stored name, ignoring case.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The form, or <see langword="null"/>.</returns>
    public async Task<PayrollTaxForm> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var key = name.Trim().ToUpperInvariant();

        return await Session.Query<PayrollTaxForm, PayrollTaxFormIndex>(index => index.Name == key, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>The default <see cref="IPayrollTaxFormManager"/>.</summary>
public sealed class PayrollTaxFormManager : CatalogManager<PayrollTaxForm>, IPayrollTaxFormManager
{
    private readonly PayrollTaxFormStore _store;

    /// <summary>Initializes a new instance of the <see cref="PayrollTaxFormManager"/> class.</summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayrollTaxFormManager(PayrollTaxFormStore store, IEnumerable<ICatalogEntryHandler<PayrollTaxForm>> handlers, ILogger<CatalogManager<PayrollTaxForm>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<PayrollTaxForm> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => _store.FindByNameAsync(name, cancellationToken);
}

/// <summary>
/// Fills a tax form from a recipe or a deployment plan, trims what was typed, and refuses a form whose name is taken or
/// whose formulas don't work.
/// </summary>
public sealed class PayrollTaxFormHandler : CatalogEntryHandlerBase<PayrollTaxForm>
{
    private readonly PayrollTaxFormStore _store;
    private readonly PayrollTaxFormFunctionCatalog _functions;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>Initializes a new instance of the <see cref="PayrollTaxFormHandler"/> class.</summary>
    /// <param name="store">The store.</param>
    /// <param name="functions">The functions of the formulas.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollTaxFormHandler(PayrollTaxFormStore store, PayrollTaxFormFunctionCatalog functions, IClock clock, IStringLocalizer<PayrollTaxFormHandler> stringLocalizer)
    {
        _store = store;
        _functions = functions;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<PayrollTaxForm> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<PayrollTaxForm> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<PayrollTaxForm> context, CancellationToken cancellationToken = default)
    {
        // A recipe's lists replace the form's, so a box it leaves out is gone rather than kept from before.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<PayrollTaxForm> context, CancellationToken cancellationToken = default)
    {
        var form = context.Model;

        Normalize(form);

        if (string.IsNullOrEmpty(form.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(PayrollTaxForm.Name)]));
        }
        else if (form.Name.Length > 50 || form.Name.Any(character => char.IsWhiteSpace(character) || character == ':'))
        {
            context.Result.Fail(new ValidationResult(S["The name has at most 50 characters, with no spaces or colons, such as US-941."], [nameof(PayrollTaxForm.Name)]));
        }
        else if (await _store.FindByNameAsync(form.Name, cancellationToken) is { } other && other.ItemId != form.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another tax form is named {0}.", form.Name], [nameof(PayrollTaxForm.Name)]));
        }

        if (string.IsNullOrEmpty(form.DisplayName))
        {
            context.Result.Fail(new ValidationResult(S["The title is required."], [nameof(PayrollTaxForm.DisplayName)]));
        }

        if (string.IsNullOrEmpty(form.Jurisdiction))
        {
            context.Result.Fail(new ValidationResult(S["Give the jurisdiction an employer files the form in, such as US."], [nameof(PayrollTaxForm.Jurisdiction)]));
        }

        if (form.Fields.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Add at least one field."], [nameof(PayrollTaxForm.Fields)]));
        }

        if (form.HasPersonStatements && !form.Fields.Any(box => box.Level == PayrollTaxFormFieldLevel.Person))
        {
            context.Result.Fail(new ValidationResult(S["A form with a statement per person needs at least one person field."], [nameof(PayrollTaxForm.Fields)]));
        }

        if ((form.StatementPerJurisdiction || form.ListJurisdictionAmounts) && !form.HasPersonStatements)
        {
            context.Result.Fail(new ValidationResult(S["Only a form with a statement per person has one per jurisdiction."], [nameof(PayrollTaxForm.StatementPerJurisdiction)]));
        }

        if (form.Values.Any(value => string.IsNullOrEmpty(value.Name) || value.Year is < 1900 or > 9999))
        {
            context.Result.Fail(new ValidationResult(S["Every yearly value needs a name and a year."], [nameof(PayrollTaxForm.Values)]));
        }

        if (form.Values.GroupBy(value => (value.Name?.ToUpperInvariant(), value.Year)).Any(group => group.Count() > 1))
        {
            context.Result.Fail(new ValidationResult(S["A yearly value is given twice for the same year."], [nameof(PayrollTaxForm.Values)]));
        }

        if (form.Checks.Any(check => string.IsNullOrEmpty(check.Note)))
        {
            context.Result.Fail(new ValidationResult(S["Every check needs the note it adds."], [nameof(PayrollTaxForm.Checks)]));
        }

        if (!string.IsNullOrEmpty(form.MissingTaxIdNote))
        {
            try
            {
                _ = string.Format(System.Globalization.CultureInfo.InvariantCulture, form.MissingTaxIdNote, "x");
            }
            catch (FormatException)
            {
                context.Result.Fail(new ValidationResult(S["The note for a person with no tax id may only use {0}, for their name."], [nameof(PayrollTaxForm.MissingTaxIdNote)]));
            }
        }

        if (PayrollTaxFormFormulas.Compile(form, _functions.Functions, S, out var errors) is null)
        {
            foreach (var (_, message) in errors)
            {
                // One member, so the message is listed once.
                context.Result.Fail(new ValidationResult(message, [nameof(PayrollTaxForm.Fields)]));
            }
        }
    }

    // Trims what a page, a recipe or a deployment plan typed.
    private static void Normalize(PayrollTaxForm form)
    {
        form.Name = form.Name?.Trim().ToUpperInvariant();
        form.DisplayName = form.DisplayName?.Trim();
        form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        form.Jurisdiction = form.Jurisdiction?.Trim().ToUpperInvariant();
        form.PeopleJurisdiction = string.IsNullOrWhiteSpace(form.PeopleJurisdiction) ? null : form.PeopleJurisdiction.Trim().ToUpperInvariant();
        form.PersonCondition = string.IsNullOrWhiteSpace(form.PersonCondition) ? null : form.PersonCondition.Trim();
        form.MissingTaxIdNote = string.IsNullOrWhiteSpace(form.MissingTaxIdNote) ? null : form.MissingTaxIdNote.Trim();
        form.PayCodeFieldPrefix = string.IsNullOrWhiteSpace(form.PayCodeFieldPrefix) ? null : form.PayCodeFieldPrefix.Trim();
        form.Fields ??= [];
        form.Values ??= [];
        form.Checks ??= [];
        form.Statement ??= new PayrollTaxFormStatement();

        foreach (var box in form.Fields)
        {
            box.Number = box.Number?.Trim();
            box.Label = string.IsNullOrWhiteSpace(box.Label) ? box.Number : box.Label.Trim();
            box.Formula = string.IsNullOrWhiteSpace(box.Formula) ? null : box.Formula.Trim();

            if (box.Level == PayrollTaxFormFieldLevel.Filing)
            {
                box.AddToFiling = false;
                box.Print = PayrollTaxFormFieldPrint.Never;
            }
        }

        foreach (var value in form.Values)
        {
            value.Name = value.Name?.Trim();
        }

        foreach (var check in form.Checks)
        {
            check.Condition = check.Condition?.Trim();
            check.Note = check.Note?.Trim();
        }
    }
}
