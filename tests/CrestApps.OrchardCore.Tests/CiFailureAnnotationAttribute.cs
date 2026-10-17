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

    static CiFailureAnnotationAttribute()
    {
        AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
        {
            var test = TestContext.Current?.Test;

            if (test is null || !_failures.TryGetValue(test.UniqueID, out var queue) || queue.Count >= 3)
            {
                return;
            }

            if (args.Exception.GetType().Namespace == "Xunit.Sdk")
            {
                queue.Enqueue(args.Exception.GetType().Name + ": " + args.Exception.Message);
            }
        };
    }

    /// <summary>
    /// Runs a test body and reports an unexpected exception as a CI annotation.
    /// </summary>
    /// <param name="body">The test body.</param>
    /// <returns>A task.</returns>
    public static async Task AnnotateAsync(Func<Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        try
        {
            await body();
        }
        catch (Exception exception) when (exception.GetType().Namespace != "Xunit.Sdk")
        {
            throw new InvalidOperationException(Environment.NewLine + "::error::" + Escape(TestContext.Current?.Test?.TestDisplayName + " -> " + exception), exception);
        }
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

        throw new InvalidOperationException(Environment.NewLine + "::error::" + Escape(test.TestDisplayName + " -> " + string.Join(" || ", queue)));
    }

    private static string Escape(string text)
    {
        if (text.Length > 6000)
        {
            text = text.Substring(0, 6000);
        }

        return text.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
    }
}
