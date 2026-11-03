using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// A tool that keeps its name, description and arguments but returns a fixed result instead of running, so a
/// simulation test does not book, charge or send anything for real.
/// </summary>
public sealed class MockedAIFunction : DelegatingAIFunction
{
    private readonly string _result;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockedAIFunction"/> class.
    /// </summary>
    /// <param name="innerFunction">The tool the model sees.</param>
    /// <param name="result">What the tool returns.</param>
    public MockedAIFunction(AIFunction innerFunction, string result)
        : base(innerFunction)
    {
        _result = result ?? string.Empty;
    }

    /// <summary>
    /// Gets how many times the model called the tool.
    /// </summary>
    public int Calls { get; private set; }

    /// <inheritdoc/>
    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        Calls++;

        return ValueTask.FromResult<object>(_result);
    }
}
