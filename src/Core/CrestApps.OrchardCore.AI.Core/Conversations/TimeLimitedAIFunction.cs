using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// A tool that gives up after a time limit, so a slow endpoint cannot leave a caller listening to silence.
/// </summary>
/// <remarks>
/// When the limit passes, the tool's work is cancelled and the model is told the tool did not answer in time, so it
/// can say so and offer to follow up rather than wait. A tool sets its own limit with the
/// <see cref="TimeoutSecondsProperty"/> additional property; otherwise the limit given to the wrapper applies.
/// </remarks>
public sealed class TimeLimitedAIFunction : DelegatingAIFunction
{
    /// <summary>
    /// The additional property a tool sets to choose its own time limit, in seconds. Zero or less means no limit.
    /// </summary>
    public const string TimeoutSecondsProperty = "CrestApps.TimeoutSeconds";

    private readonly TimeSpan _timeout;
    private readonly Action<string> _started;
    private readonly Action<string> _finished;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeLimitedAIFunction"/> class.
    /// </summary>
    /// <param name="innerFunction">The tool to limit.</param>
    /// <param name="timeout">The limit when the tool does not set its own.</param>
    /// <param name="started">Called with the tool's name when it starts running.</param>
    /// <param name="finished">Called with the tool's name when it stops running, whatever the outcome.</param>
    public TimeLimitedAIFunction(AIFunction innerFunction, TimeSpan timeout, Action<string> started = null, Action<string> finished = null)
        : base(innerFunction)
    {
        _timeout = GetTimeout(innerFunction, timeout);
        _started = started;
        _finished = finished;
    }

    /// <summary>
    /// Gets the limit that applies to this tool. <see cref="Timeout.InfiniteTimeSpan"/> means none.
    /// </summary>
    public TimeSpan Limit => _timeout;

    /// <summary>
    /// The line the model receives when a tool did not answer in time.
    /// </summary>
    /// <param name="toolName">The tool's name.</param>
    /// <param name="timeout">The limit that passed.</param>
    /// <returns>The tool result.</returns>
    public static string TimedOutResult(string toolName, TimeSpan timeout)
        => $"The tool '{toolName}' did not answer within {Math.Round(timeout.TotalSeconds)} seconds and was stopped. Do not call it again on this turn. Tell the customer you could not get that right now, and offer another way to help or to follow up later.";

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        _started?.Invoke(Name);

        try
        {
            if (_timeout == Timeout.InfiniteTimeSpan)
            {
                return await base.InvokeCoreAsync(arguments, cancellationToken);
            }

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(_timeout);

            try
            {
                return await base.InvokeCoreAsync(arguments, limit.Token).AsTask().WaitAsync(limit.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                arguments?.Services?.GetService<ILoggerFactory>()?
                    .CreateLogger<TimeLimitedAIFunction>()
                    .LogWarning("The AI tool '{ToolName}' did not answer within {TimeoutSeconds} seconds and was stopped.", Name, _timeout.TotalSeconds);

                return TimedOutResult(Name, _timeout);
            }
        }
        finally
        {
            _finished?.Invoke(Name);
        }
    }

    private static TimeSpan GetTimeout(AIFunction function, TimeSpan fallback)
    {
        if (function.AdditionalProperties is not null &&
            function.AdditionalProperties.TryGetValue(TimeoutSecondsProperty, out var value) &&
            value is not null &&
            double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : Timeout.InfiniteTimeSpan;
        }

        return fallback > TimeSpan.Zero
            ? fallback
            : Timeout.InfiniteTimeSpan;
    }
}
