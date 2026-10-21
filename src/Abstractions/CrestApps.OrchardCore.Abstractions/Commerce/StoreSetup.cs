using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// One step of the store's setup guide, under Commerce › Set up your store, such as the store's details, how it's
/// paid or its first product. Each module adds the steps for what it does, so a store sees only the steps of the
/// features it has on.
/// </summary>
public interface IStoreSetupStep
{
    /// <summary>
    /// Gets the step's name, unique among the steps, such as <c>shipping</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets where the step comes in the guide. The guide's own steps are 10 apart, from 10 to 90.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Describes the step as it stands: what it's for, whether it's done, and what to do next.
    /// </summary>
    Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the step's quick action with the values entered in its fields, such as adding a flat shipping rate, or
    /// returns <see langword="null"/> for a step without one.
    /// </summary>
    Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<StoreSetupActionResult>(null);
}

/// <summary>
/// What a setup step says about itself.
/// </summary>
public sealed class StoreSetupStepDescription
{
    public LocalizedString Title { get; init; }

    /// <summary>
    /// Gets what the step is for, in a sentence or two for an owner who hasn't read the docs.
    /// </summary>
    public LocalizedString Description { get; init; }

    /// <summary>
    /// Gets whether the step is done. <see langword="null"/> means it's done once its quick action has been run.
    /// </summary>
    public bool? IsDone { get; init; }

    /// <summary>
    /// Gets what's set up now, such as "Stripe, in test mode", or <see langword="null"/>.
    /// </summary>
    public LocalizedString Status { get; init; }

    /// <summary>
    /// Gets whether a store can sell without it, such as staff roles.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Gets the page where the step is done in full, or <see langword="null"/>.
    /// </summary>
    public string Url { get; init; }

    public LocalizedString UrlText { get; init; }

    /// <summary>
    /// Gets the fields the quick action asks for. A quick action without fields is a single button.
    /// </summary>
    public IReadOnlyList<StoreSetupField> Fields { get; init; } = [];

    /// <summary>
    /// Gets the quick action's button, or <see langword="null"/> for a step without one.
    /// </summary>
    public LocalizedString ActionText { get; init; }
}

/// <summary>
/// A field of a setup step's quick action.
/// </summary>
public sealed class StoreSetupField
{
    public string Name { get; init; }

    public LocalizedString Label { get; init; }

    public string Value { get; init; }

    /// <summary>
    /// Gets the field's kind: <c>text</c>, <c>email</c>, <c>number</c>, <c>textarea</c> or <c>select</c>.
    /// </summary>
    public string Type { get; init; } = "text";

    public LocalizedString Hint { get; init; }

    public bool Required { get; init; }

    /// <summary>
    /// Gets a select's choices, by value.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Options { get; init; } = [];
}

/// <summary>
/// The values entered for a setup step's quick action.
/// </summary>
public sealed class StoreSetupActionContext
{
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    public string GetValue(string name)
        => Values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

/// <summary>
/// How a setup step's quick action went.
/// </summary>
public sealed class StoreSetupActionResult
{
    public bool Succeeded { get; init; }

    public LocalizedString Message { get; init; }

    public static StoreSetupActionResult Success(LocalizedString message)
        => new() { Succeeded = true, Message = message };

    public static StoreSetupActionResult Failure(LocalizedString message)
        => new() { Message = message };
}

/// <summary>
/// The setup guide's progress, kept in the site's settings: the steps whose quick action was run and those the owner
/// skipped.
/// </summary>
public sealed class StoreSetupSettings
{
    public List<string> Completed { get; set; } = [];

    public List<string> Skipped { get; set; } = [];
}
