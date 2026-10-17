using System.Collections.Concurrent;
using System.Reflection;
using Xunit.v3;

[assembly: CrestApps.OrchardCore.Tests.CiFailureAnnotationAttribute]

namespace CrestApps.OrchardCore.Tests;

/// <summary>
/// Temporary diagnostic: reports failing assertions as CI annotations.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CiFailureAnnotationAttribute : BeforeAfterTestAttribute
{
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _failures = new(StringComparer.Ordinal);

    private static readonly string[] _watched =
    [
        "AgentWorkforceReportScalingTests",
        "AgentActivityTimelineSelectionTests",
        "WorkforceReportRangeTests",
        "InteractionEventWindowPersistenceTests",
    ];

    static CiFailureAnnotationAttribute()
    {
        AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
        {
            var test = TestContext.Current?.Test;

            if (test is null || !_failures.TryGetValue(test.UniqueID, out var queue) || queue.Count >= 3)
            {
                return;
            }

            var watched = _watched.Any(name => test.TestDisplayName.Contains(name, StringComparison.Ordinal));

            if (args.Exception.GetType().Namespace == "Xunit.Sdk" || watched)
            {
                queue.Enqueue(args.Exception.GetType().Name + ": " + args.Exception.Message + " @ " + args.Exception.StackTrace);
            }
        };
    }

    /// <inheritdoc/>
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
        => _failures[test.UniqueID] = new ConcurrentQueue<string>();

    /// <inheritdoc/>
    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (!_failures.TryRemove(test.UniqueID, out var queue) || queue.IsEmpty)
        {
            return;
        }

        var text = test.TestDisplayName + " -> " + string.Join(" || ", queue);

        if (text.Length > 6000)
        {
            text = text.Substring(0, 6000);
        }

        text = text.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

        throw new InvalidOperationException(Environment.NewLine + "::error::" + text);
    }
}
