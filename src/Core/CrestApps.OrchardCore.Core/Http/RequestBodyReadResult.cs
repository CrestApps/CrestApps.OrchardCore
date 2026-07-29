namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// The outcome of reading a request body that an untrusted caller controls.
/// </summary>
public readonly struct RequestBodyReadResult : IEquatable<RequestBodyReadResult>
{
    private RequestBodyReadResult(bool isTooLarge, string body)
    {
        IsTooLarge = isTooLarge;
        Body = body;
    }

    /// <summary>
    /// Gets a result reporting that the caller sent more than it is allowed to send.
    /// </summary>
    public static RequestBodyReadResult TooLarge { get; } = new(true, null);

    /// <summary>
    /// Gets a value indicating whether the caller sent more than it is allowed to send.
    /// </summary>
    public bool IsTooLarge { get; }

    /// <summary>
    /// Gets the body the caller sent, or <see langword="null"/> when it was refused.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// Creates a result carrying a body that was within the allowance.
    /// </summary>
    /// <param name="body">The body the caller sent.</param>
    /// <returns>A result carrying <paramref name="body"/>.</returns>
    public static RequestBodyReadResult FromBody(string body) => new(false, body);

    /// <inheritdoc/>
    public bool Equals(RequestBodyReadResult other)
        => IsTooLarge == other.IsTooLarge && string.Equals(Body, other.Body, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is RequestBodyReadResult other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(IsTooLarge, Body);

    /// <summary>
    /// Determines whether two results are equal.
    /// </summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true"/> when the results are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(RequestBodyReadResult left, RequestBodyReadResult right) => left.Equals(right);

    /// <summary>
    /// Determines whether two results differ.
    /// </summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true"/> when the results differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(RequestBodyReadResult left, RequestBodyReadResult right) => !left.Equals(right);
}
