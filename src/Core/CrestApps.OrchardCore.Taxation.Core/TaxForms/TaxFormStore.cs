using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Taxation.Core.TaxForms;

/// <summary>Indexes tax forms.</summary>
public sealed class TaxFormIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the stored name, in capitals.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the source that prepares it.</summary>
    public string Source { get; set; }

    /// <summary>Gets or sets the jurisdiction.</summary>
    public string Jurisdiction { get; set; }
}

/// <summary>Maps tax forms to their index rows.</summary>
public sealed class TaxFormIndexProvider : IndexProvider<TaxForm>
{
    /// <summary>Initializes a new instance of the <see cref="TaxFormIndexProvider"/> class.</summary>
    public TaxFormIndexProvider()
    {
        CollectionName = TaxationConstants.TaxFormsCollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<TaxForm> context)
        => context
            .For<TaxFormIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(form => new TaxFormIndex
            {
                ItemId = form.ItemId,
                Name = form.Name?.Trim().ToUpperInvariant(),
                Source = form.Source,
                Jurisdiction = form.Jurisdiction,
            });
}

/// <summary>Creates the table of tax forms.</summary>
public static class TaxFormIndexSchema
{
    /// <summary>Creates the table.</summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TaxFormIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(50))
            .Column<string>("Source", column => column.Nullable().WithLength(50))
            .Column<string>("Jurisdiction", column => column.Nullable().WithLength(20)),
            collection: TaxationConstants.TaxFormsCollectionName);

        await schemaBuilder.AlterIndexTableAsync<TaxFormIndex>(table => table
            .CreateIndex("IDX_TaxFormIndex_Name", "Name", "Source"),
            collection: TaxationConstants.TaxFormsCollectionName);
    }
}

/// <summary>Stores tax forms in their own collection.</summary>
public sealed class TaxFormStore : DocumentCatalog<TaxForm, TaxFormIndex>
{
    /// <summary>Initializes a new instance of the <see cref="TaxFormStore"/> class.</summary>
    /// <param name="session">The session.</param>
    public TaxFormStore(ISession session)
        : base(session)
    {
        CollectionName = TaxationConstants.TaxFormsCollectionName;
    }

    /// <summary>Finds a form by its stored name, ignoring case.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The form, or <see langword="null"/>.</returns>
    public async Task<TaxForm> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var key = name.Trim().ToUpperInvariant();

        return await Session.Query<TaxForm, TaxFormIndex>(index => index.Name == key, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Lists the forms of a source.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The forms.</returns>
    public async Task<IReadOnlyList<TaxForm>> ListBySourceAsync(string source, CancellationToken cancellationToken = default)
        => (await Session.Query<TaxForm, TaxFormIndex>(index => index.Source == source, collection: CollectionName).ListAsync(cancellationToken)).ToList();
}

/// <summary>The default <see cref="ITaxFormManager"/>.</summary>
public sealed class TaxFormManager : CatalogManager<TaxForm>, ITaxFormManager
{
    private readonly TaxFormStore _store;

    /// <summary>Initializes a new instance of the <see cref="TaxFormManager"/> class.</summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public TaxFormManager(TaxFormStore store, IEnumerable<ICatalogEntryHandler<TaxForm>> handlers, ILogger<CatalogManager<TaxForm>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task<TaxForm> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => _store.FindByNameAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxForm>> ListBySourceAsync(string source, CancellationToken cancellationToken = default)
        => _store.ListBySourceAsync(source, cancellationToken);
}

/// <summary>
/// Fills a tax form from a recipe or a deployment plan, trims what was typed, and refuses a form whose name is taken or
/// whose formulas don't work.
/// </summary>
public sealed class TaxFormHandler : CatalogEntryHandlerBase<TaxForm>
{
    private readonly TaxFormStore _store;
    private readonly TaxFormFunctionCatalog _functions;
    private readonly TaxFormSourceOptions _sources;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>Initializes a new instance of the <see cref="TaxFormHandler"/> class.</summary>
    /// <param name="store">The store.</param>
    /// <param name="functions">The functions of the formulas.</param>
    /// <param name="sources">The tax form sources.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxFormHandler(TaxFormStore store, TaxFormFunctionCatalog functions, IOptions<TaxFormSourceOptions> sources, IClock clock, IStringLocalizer<TaxFormHandler> stringLocalizer)
    {
        _store = store;
        _functions = functions;
        _sources = sources.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<TaxForm> context, CancellationToken cancellationToken = default)
    {
        // A recipe or a deployment plan hands the values in; a page sets them itself and passes none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task InitializedAsync(InitializedContext<TaxForm> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<TaxForm> context, CancellationToken cancellationToken = default)
    {
        // A recipe's lists replace the form's, so a box it leaves out is gone rather than kept from before.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        Normalize(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidatingAsync(ValidatingContext<TaxForm> context, CancellationToken cancellationToken = default)
    {
        var form = context.Model;

        Normalize(form);

        if (string.IsNullOrEmpty(form.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(TaxForm.Name)]));
        }
        else if (form.Name.Length > 50 || form.Name.Any(character => char.IsWhiteSpace(character) || character == ':'))
        {
            context.Result.Fail(new ValidationResult(S["The name has at most 50 characters, with no spaces or colons, such as US-941."], [nameof(TaxForm.Name)]));
        }
        else if (await _store.FindByNameAsync(form.Name, cancellationToken) is { } other && other.ItemId != form.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another tax form is named {0}.", form.Name], [nameof(TaxForm.Name)]));
        }

        if (string.IsNullOrEmpty(form.DisplayName))
        {
            context.Result.Fail(new ValidationResult(S["The title is required."], [nameof(TaxForm.DisplayName)]));
        }

        var source = string.IsNullOrEmpty(form.Source) ? null : _sources.Sources.FirstOrDefault(candidate => string.Equals(candidate.Name, form.Source, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(form.Source))
        {
            context.Result.Fail(new ValidationResult(S["Choose the source that prepares the form."], [nameof(TaxForm.Source)]));
        }
        else if (source is null)
        {
            context.Result.Fail(new ValidationResult(S["No module prepares forms from {0}: turn on the feature that does, such as Payroll.", form.Source], [nameof(TaxForm.Source)]));
        }

        if (string.IsNullOrEmpty(form.Jurisdiction) && source?.RequiresJurisdiction != false)
        {
            context.Result.Fail(new ValidationResult(S["Give the jurisdiction a filer files the form in, such as US."], [nameof(TaxForm.Jurisdiction)]));
        }

        // A source without recipients, such as sales tax returns, works out only filing fields.
        if (source is { HasRecipients: false } && (form.HasRecipientStatements || form.Fields.Any(field => field.Level == TaxFormFieldLevel.Recipient)))
        {
            context.Result.Fail(new ValidationResult(S["A form prepared from {0} has no recipients: make every field a filing field.", form.Source], [nameof(TaxForm.Fields)]));
        }

        if (form.Fields.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Add at least one field."], [nameof(TaxForm.Fields)]));
        }

        if (form.HasRecipientStatements && !form.Fields.Any(field => field.Level == TaxFormFieldLevel.Recipient))
        {
            context.Result.Fail(new ValidationResult(S["A form with a statement per recipient needs at least one recipient field."], [nameof(TaxForm.Fields)]));
        }

        if (form.StatementPerJurisdiction && !form.HasRecipientStatements)
        {
            context.Result.Fail(new ValidationResult(S["Only a form with a statement per recipient has one per jurisdiction."], [nameof(TaxForm.StatementPerJurisdiction)]));
        }

        if (form.Values.Any(value => string.IsNullOrEmpty(value.Name) || value.Year is < 1900 or > 9999))
        {
            context.Result.Fail(new ValidationResult(S["Every yearly value needs a name and a year."], [nameof(TaxForm.Values)]));
        }

        if (form.Values.GroupBy(value => (value.Name?.ToUpperInvariant(), value.Year)).Any(group => group.Count() > 1))
        {
            context.Result.Fail(new ValidationResult(S["A yearly value is given twice for the same year."], [nameof(TaxForm.Values)]));
        }

        if (form.Checks.Any(check => string.IsNullOrEmpty(check.Note)))
        {
            context.Result.Fail(new ValidationResult(S["Every check needs the note it adds."], [nameof(TaxForm.Checks)]));
        }

        if (!string.IsNullOrEmpty(form.MissingTaxIdNote))
        {
            try
            {
                _ = string.Format(System.Globalization.CultureInfo.InvariantCulture, form.MissingTaxIdNote, "x");
            }
            catch (FormatException)
            {
                context.Result.Fail(new ValidationResult(S["The note for a recipient with no tax id may only use {0}, for their name."], [nameof(TaxForm.MissingTaxIdNote)]));
            }
        }

        if (_sources.Sources.Any(source => string.Equals(source.Name, form.Source, StringComparison.OrdinalIgnoreCase))
            && TaxFormFormulas.Compile(form, _functions.For(form.Source), S, out var errors) is null)
        {
            foreach (var (_, message) in errors)
            {
                // One member, so the message is listed once.
                context.Result.Fail(new ValidationResult(message, [nameof(TaxForm.Fields)]));
            }
        }
    }

    // Trims what a page, a recipe or a deployment plan typed.
    private static void Normalize(TaxForm form)
    {
        form.Name = form.Name?.Trim().ToUpperInvariant();
        form.DisplayName = form.DisplayName?.Trim();
        form.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        form.Source = form.Source?.Trim();
        form.Jurisdiction = form.Jurisdiction?.Trim().ToUpperInvariant();
        form.RecipientCondition = string.IsNullOrWhiteSpace(form.RecipientCondition) ? null : form.RecipientCondition.Trim();
        form.MissingTaxIdNote = string.IsNullOrWhiteSpace(form.MissingTaxIdNote) ? null : form.MissingTaxIdNote.Trim();
        form.Fields ??= [];
        form.Values ??= [];
        form.Checks ??= [];
        form.Statement ??= new TaxFormStatement();

        foreach (var box in form.Fields)
        {
            box.Number = box.Number?.Trim();
            box.Label = string.IsNullOrWhiteSpace(box.Label) ? box.Number : box.Label.Trim();
            box.Formula = string.IsNullOrWhiteSpace(box.Formula) ? null : box.Formula.Trim();

            if (box.Level == TaxFormFieldLevel.Filing)
            {
                box.AddToFiling = false;
                box.Print = TaxFormFieldPrint.Never;
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
