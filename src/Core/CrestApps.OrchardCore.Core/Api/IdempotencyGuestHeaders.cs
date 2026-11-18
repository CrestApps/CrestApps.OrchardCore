namespace CrestApps.OrchardCore.Api;

/// <summary>
/// Endpoint metadata naming the headers a guest is known by, such as a cart's token, in order. An idempotent write by a
/// guest keeps its answer per the first of them the request carries, and a replayed answer gives back the ones the first
/// answer handed out.
/// </summary>
/// <param name="Headers">The header names, in the order they are tried.</param>
public sealed record IdempotencyGuestHeaders(IReadOnlyList<string> Headers);
