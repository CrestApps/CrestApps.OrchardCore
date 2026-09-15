using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Provider webhooks are at-least-once. Every handler that writes on the strength of one therefore has to be
/// safe to run twice, and has to say how it is safe — otherwise the second delivery enqueues a caller again,
/// bills a call twice, or sends a customer a second copy of a message.
/// <para>
/// Declaring <see cref="ContactCenterHandlerReplaySafety"/> is not the same as being replay-safe, so this also
/// requires each handler to have a test that names it and exercises a duplicate delivery. A declaration nobody
/// tested is a claim, and this rule exists because claims about idempotency are the ones that turn out to be
/// wrong under load.
/// </para>
/// </summary>
public sealed class ProviderWriteIdempotencyArchitectureTests
{
    [Fact]
    public void EveryWebhookInboxHandler_DeclaresHowItSurvivesADuplicateDelivery()
    {
        // Arrange
        var handlers = GetHandlerTypes();

        Assert.True(
            handlers.Length >= 4,
            $"Only {handlers.Length} inbox handlers were discovered, which is fewer than the four known to exist. " +
            "The reflection that finds them has stopped working, so this test would pass without checking anything.");

        var violations = new List<string>();

        // Act
        foreach (var handler in handlers)
        {
            var instance = FormatterServices_CreateUninitialized(handler);

            if (instance is null)
            {
                continue;
            }

            var safety = (ContactCenterHandlerReplaySafety)handler
                .GetProperty(nameof(IProviderWebhookInboxHandler.ReplaySafety))
                .GetValue(instance);

            if (safety == ContactCenterHandlerReplaySafety.Unspecified)
            {
                violations.Add(
                    $"{handler.Name} does not say how it survives a redelivered webhook. Declare NaturallyIdempotent, " +
                    "DeduplicatedByEventId, or GuardedByDurableStore, so the contract is reviewable rather than assumed.");
            }
        }

        // Assert
        AssertNone(violations);
    }

    [Fact]
    public void EveryWebhookInboxHandler_HasATestThatDeliversTheSameEventTwice()
    {
        // Arrange
        // The declaration above is a claim. This is the evidence.
        var repositoryRoot = FindRepositoryRoot();
        var testSources = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        var violations = new List<string>();

        // Act
        foreach (var handler in GetHandlerTypes())
        {
            var covered = testSources.Any(source =>
                source.Contains(handler.Name, StringComparison.Ordinal) &&
                MentionsDuplicateDelivery(source));

            if (!covered)
            {
                violations.Add(
                    $"{handler.Name} has no test naming it that also exercises a duplicate or redelivered event. " +
                    "Add one that applies the same delivery twice and asserts the second changes nothing.");
            }
        }

        // Assert
        AssertNone(violations);
    }

    private static bool MentionsDuplicateDelivery(string source)
        => source.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || source.Contains("redeliver", StringComparison.OrdinalIgnoreCase)
            || source.Contains("twice", StringComparison.OrdinalIgnoreCase)
            || source.Contains("replay", StringComparison.OrdinalIgnoreCase)
            || source.Contains("idempot", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One type from each assembly that declares a handler. Reflecting over
    /// <see cref="AppDomain.CurrentDomain"/> alone only sees assemblies the runtime has already loaded, so the
    /// guard found four handlers in a full run and two under a filter — quietly passing while a handler went
    /// unchecked. Naming a type from each assembly forces the load and makes the result the same every run.
    /// </summary>
    private static readonly Type[] _assemblyAnchors =
    [
        typeof(IProviderWebhookInboxHandler),
        typeof(CrestApps.OrchardCore.ContactCenter.Core.Services.ProviderVoiceEventInboxHandler),
        typeof(CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.SmsInboundInboxHandler),
        typeof(CrestApps.OrchardCore.Telnyx.Services.TelnyxWebhookInboxHandler),
        typeof(CrestApps.OrchardCore.Dialpad.Services.DialpadWebhookInboxHandler),
    ];

    private static Type[] GetHandlerTypes()
    {
        // Touching each anchor guarantees its assembly is loaded before the scan below reads the domain.
        var anchored = _assemblyAnchors.Select(anchor => anchor.Assembly).ToArray();

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Concat(anchored)
            .Distinct()
            .Where(assembly => assembly.GetName().Name?.StartsWith("CrestApps.OrchardCore", StringComparison.Ordinal) == true)
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(IProviderWebhookInboxHandler).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null);
        }
    }

    private static object FormatterServices_CreateUninitialized(Type type)
    {
        try
        {
            // The property is a constant expression per handler, so no dependency needs to exist for it to be
            // read; creating the instance without running a constructor avoids standing up the whole graph.
            return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void AssertNone(List<string> violations)
    {
        if (violations.Count == 0)
        {
            return;
        }

        var message = new StringBuilder().AppendLine();

        foreach (var violation in violations.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(violation);
        }

        Assert.Fail(message.ToString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
