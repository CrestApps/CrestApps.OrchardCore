using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// An <see cref="ICheckWriteHandler"/> that does nothing until a step is overridden.
/// </summary>
public abstract class CheckWriteHandlerBase : ICheckWriteHandler
{
    /// <inheritdoc />
    public virtual Task WritingAsync(CheckWritingContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task WrittenAsync(CheckWrittenContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
