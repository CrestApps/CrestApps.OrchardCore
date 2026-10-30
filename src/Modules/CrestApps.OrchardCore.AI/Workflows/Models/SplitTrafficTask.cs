using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Sends each run down one of several weighted outcomes, for an A/B test or a sample. With a key, such as the
/// customer's phone number, the same key always takes the same outcome.
/// </summary>
public sealed class SplitTrafficTask : TaskActivity<SplitTrafficTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplitTrafficTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator, which renders the key.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SplitTrafficTask(IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer<SplitTrafficTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Split Traffic"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the outcomes and their weights, one <c>name: weight</c> per line.
    /// </summary>
    public string Variants
    {
        get => GetProperty(() => "A: 50\nB: 50");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the key that keeps a customer on the same outcome, or empty to pick at
    /// random each time.
    /// </summary>
    public string StickyKey
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => ParseVariants(Variants).Select(variant => new Outcome(variant.Name, new LocalizedString(variant.Name, variant.Name)));

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var variants = ParseVariants(Variants);

        if (variants.Count == 0)
        {
            return Noop();
        }

        var key = string.IsNullOrWhiteSpace(StickyKey)
            ? null
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(StickyKey), workflowContext, null);

        var name = Pick(variants, string.IsNullOrWhiteSpace(key)
            ? Random.Shared.NextDouble()
            : Position(workflowContext.WorkflowType?.WorkflowTypeId + "|" + key.Trim()));

        workflowContext.Properties["Variant"] = name;
        workflowContext.LastResult = name;

        return Outcome(name);
    }

    /// <summary>
    /// Reads <c>name: weight</c> lines. Lines without a positive weight are skipped, and a name used twice keeps the
    /// first line.
    /// </summary>
    /// <param name="text">The lines.</param>
    internal static List<(string Name, double Weight)> ParseVariants(string text)
    {
        var variants = new List<(string Name, double Weight)>();

        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var separator = line.LastIndexOf(':');

            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();

            if (name.Length == 0 ||
                !double.TryParse(line[(separator + 1)..].Trim().TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var weight) ||
                weight <= 0 ||
                variants.Exists(variant => string.Equals(variant.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            variants.Add((name, weight));
        }

        return variants;
    }

    /// <summary>
    /// Picks the variant whose share of the total weight covers <paramref name="position"/>, a number from 0 up to
    /// but not including 1.
    /// </summary>
    /// <param name="variants">The variants.</param>
    /// <param name="position">Where in the range the run falls.</param>
    internal static string Pick(IReadOnlyList<(string Name, double Weight)> variants, double position)
    {
        var total = variants.Sum(variant => variant.Weight);
        var target = position * total;
        var running = 0d;

        foreach (var (name, weight) in variants)
        {
            running += weight;

            if (target < running)
            {
                return name;
            }
        }

        return variants[^1].Name;
    }

    /// <summary>
    /// Maps a key to a stable position from 0 up to but not including 1.
    /// </summary>
    /// <param name="key">The key.</param>
    internal static double Position(string key)
        => BinaryPrimitives.ReadUInt32BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(key))) / ((double)uint.MaxValue + 1);
}
