using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent.Records;

/// <summary>
/// Builds the services of a generic record tool call: the real display manager and MVC model binding with a test record
/// driver, an in-memory catalog manager whose validation stands in for the record's handlers, and a test record kind
/// registered through <see cref="AIRecordServiceCollectionExtensions.AddAIRecordKind{T}"/>. Every secret of the fixture
/// starts with <see cref="Canary"/>.
/// </summary>
internal sealed class RecordToolHarness
{
    /// <summary>
    /// The text every value a tool must not return starts with.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The name of the test record kind.
    /// </summary>
    public const string Kind = "testRecord";

    public static readonly Permission ManageRecords = new("ManageTestRecords");

    public static readonly Permission ViewRecords = new("ViewTestRecords");

    public RecordToolHarness()
    {
        Records.Items.Add(CreateRecord("record-1", "Alpha", "A", 1));
        Records.Items.Add(CreateRecord("record-2", "Beta", "B", 2));
        Records.Items.Add(CreateRecord("record-3", "Gamma", "USED", 3));
    }

    public bool SignedIn { get; set; } = true;

    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    public InMemoryRecordManager<TestRecord> Records { get; } = new();

    /// <summary>
    /// Gets or sets a last change to the services, made before the provider is built.
    /// </summary>
    public Action<IServiceCollection> Configure { get; set; }

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, Authorize);

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<IDisplayDriver<TestRecord>, TestRecordDisplayDriver>();
        services.AddSingleton<ICatalogManager<TestRecord>>(Records);
        services.AddScoped<AIRecordScopeChanges>();

        services.AddAIRecordKind<TestRecord>(Kind, kind => kind
            .WithTitle("Test record")
            .WithDescription("a record of the tests")
            .WithPermission(ManageRecords)
            .WithReadPermission(ViewRecords)
            .WithProjection(record => new
            {
                id = record.ItemId,
                name = record.Name,
                code = record.Code,
                limit = record.Limit,
                hasApiKey = !string.IsNullOrEmpty(record.ApiKey),
            })
            .WithInUseCheck((_, record, _) => ValueTask.FromResult(record.Code == "USED" ? "It is used by an order." : null))
            .UseEditorReplay());

        Configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets the registered record kinds of a test container.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The options.</returns>
    public static IOptions<AIRecordKindOptions> Kinds(IServiceProvider services)
        => services.GetRequiredService<IOptions<AIRecordKindOptions>>();

    private static TestRecord CreateRecord(string id, string name, string code, int limit)
    {
        var record = new TestRecord
        {
            ItemId = id,
            Name = name,
            Code = code,
            Limit = limit,
            ApiKey = $"{Canary}-{id}-key",
        };

        record.Properties["PrivateToken"] = $"{Canary}-{id}-properties";

        return record;
    }
}

/// <summary>
/// A catalog record of the tests, with a secret and extension data a tool must never return.
/// </summary>
public sealed class TestRecord : CatalogItem, INameAwareModel, ICloneable<TestRecord>
{
    public string Name { get; set; }

    public string Code { get; set; }

    public int Limit { get; set; }

    public string ApiKey { get; set; }

    public TestRecord Clone()
    {
        var clone = new TestRecord
        {
            ItemId = ItemId,
            Name = Name,
            Code = Code,
            Limit = Limit,
            ApiKey = ApiKey,
        };

        foreach (var property in Properties)
        {
            clone.Properties[property.Key] = property.Value;
        }

        return clone;
    }
}

/// <summary>
/// The editor view model of the test record. It is public, as Orchard Core builds a proxy type of each editor view model.
/// </summary>
public class TestRecordViewModel
{
    public bool IsNew { get; set; }

    [Display(Description = "The name of the record. It is set when the record is created.")]
    public string Name { get; set; }

    public string Code { get; set; }

    public int Limit { get; set; }

    public string ApiKey { get; set; }
}

/// <summary>
/// A record driver written the way the CrestApps admin editors are: it binds its view model with its prefix, trims and
/// upper-cases the code, rejects a negative limit, sets the name only on a new record and keeps the API key when none is
/// posted.
/// </summary>
public sealed class TestRecordDisplayDriver : DisplayDriver<TestRecord>
{
    public override IDisplayResult Edit(TestRecord record, BuildEditorContext context)
        => Initialize<TestRecordViewModel>("TestRecordFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = record.Name;
            model.Code = record.Code;
            model.Limit = record.Limit;
            model.ApiKey = record.ApiKey;
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(TestRecord record, UpdateEditorContext context)
    {
        var model = new TestRecordViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Limit < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Limit), "The limit cannot be negative.");
        }

        if (context.IsNew)
        {
            record.Name = model.Name?.Trim();
        }

        record.Code = model.Code?.Trim().ToUpperInvariant();
        record.Limit = model.Limit;

        if (!string.IsNullOrEmpty(model.ApiKey))
        {
            record.ApiKey = model.ApiKey;
        }

        return Edit(record, context);
    }
}

/// <summary>
/// An in-memory named catalog manager that records what the tools create, update and delete. The catalog handlers do not
/// run; <see cref="Validate"/> stands in for them and, by default, requires a name that no other record has.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
internal sealed class InMemoryRecordManager<T> : INamedCatalogManager<T>
    where T : CatalogItem, INameAwareModel, new()
{
    private int _nextId;

    public InMemoryRecordManager()
    {
        Validate = model =>
        {
            var result = new ValidationResultDetails();

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                result.Fail(new ValidationResult("Name is required.", ["Name"]));
            }
            else if (Items.Any(item => item.ItemId != model.ItemId && string.Equals(item.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
            {
                result.Fail(new ValidationResult("A record with the same name already exists.", ["Name"]));
            }

            return result;
        };
    }

    public List<T> Items { get; } = [];

    public List<T> Created { get; } = [];

    public List<T> Updated { get; } = [];

    public List<T> Deleted { get; } = [];

    public Func<T, ValidationResultDetails> Validate { get; set; }

    public T Find(string id)
        => Items.FirstOrDefault(item => string.Equals(item.ItemId, id, StringComparison.Ordinal));

    public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Copy(Find(id)));

    public ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IEnumerable<T>>(Items.Select(Copy).ToList());

    public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var matches = Items
            .Where(item => string.IsNullOrEmpty(context?.Name) || item.Name?.Contains(context.Name, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ValueTask.FromResult(new PageResult<T>
        {
            Count = matches.Count,
            Entries = matches.Skip((page - 1) * pageSize).Take(pageSize).Select(Copy).ToList(),
        });
    }

    public ValueTask<bool> DeleteAsync(T model, CancellationToken cancellationToken = default)
    {
        var stored = Find(model.ItemId);

        if (stored is null)
        {
            return ValueTask.FromResult(false);
        }

        Deleted.Add(model);
        Items.Remove(stored);

        return ValueTask.FromResult(true);
    }

    public ValueTask CreateAsync(T model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(model.ItemId))
        {
            model.ItemId = string.Create(CultureInfo.InvariantCulture, $"new-{++_nextId}");
        }

        Created.Add(model);
        Items.Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(T model, JsonNode data = null, CancellationToken cancellationToken = default)
    {
        // As the catalog stores do: the record with the same ID is replaced.
        var index = Items.FindIndex(item => string.Equals(item.ItemId, model.ItemId, StringComparison.Ordinal));

        if (index < 0)
        {
            throw new InvalidOperationException("Cannot update a record that does not exist.");
        }

        Items[index] = model;
        Updated.Add(model);

        return ValueTask.CompletedTask;
    }

    public ValueTask<ValidationResultDetails> ValidateAsync(T model, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Validate(model));

    public ValueTask<T> NewAsync(JsonNode data = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new T());

    public ValueTask<T> NewAsync(string name, JsonNode data = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new T());

    public ValueTask<T> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Copy(Items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))));

    private static T Copy(T item)
        => item is ICloneable<T> cloneable ? cloneable.Clone() : item;
}

/// <summary>
/// A localizer that returns each text as written, for running a startup in a test.
/// </summary>
/// <typeparam name="T">The type the texts belong to.</typeparam>
internal sealed class RecordTestLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name]
        => new(name, name);

    public LocalizedString this[string name, params object[] arguments]
        => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => [];
}
