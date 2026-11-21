using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Xapi;

/// <summary>
/// Runs around the storing of statements a client sent, after the LRS validated them and before it stores them. A
/// filter may check its own rules and refuse the whole request (cmi5 refuses a statement that breaks its rules with
/// 403), take a lock that covers the write, and act on what was stored after <c>next</c> returns. Statements the LRS
/// writes for a module (such as the cmi5 <c>launched</c> and <c>satisfied</c> statements) do not pass the filters.
/// </summary>
public interface IXapiStatementFilter
{
    /// <summary>
    /// Runs the filter.
    /// </summary>
    /// <param name="context">The statements and the credential.</param>
    /// <param name="next">Stores the statements, through the next filters. Not calling it stores nothing.</param>
    /// <returns>A task.</returns>
    Task OnWritingAsync(XapiStatementWriteContext context, Func<Task> next);
}

/// <summary>
/// The statements a client sent to the LRS, as the filters see them.
/// </summary>
public sealed class XapiStatementWriteContext
{
    /// <summary>Gets the credential of the request.</summary>
    public XapiCredential Credential { get; init; }

    /// <summary>Gets the statements, validated, with their ids, in the order they were sent.</summary>
    public IReadOnlyList<JsonObject> Statements { get; init; } = [];

    /// <summary>Gets the ids of the statements that the LRS already holds with the same content; storing them again
    /// changes nothing.</summary>
    public ISet<string> ExistingIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the records stored, once <c>next</c> returned.</summary>
    public IReadOnlyList<XapiStatementRecord> Stored { get; set; } = [];

    /// <summary>Gets the HTTP status a filter refused the request with, or <see langword="null"/>.</summary>
    public int? RejectedStatus { get; private set; }

    /// <summary>Gets why a filter refused the request.</summary>
    public string RejectedMessage { get; private set; }

    /// <summary>
    /// Refuses the request. Nothing is stored when a filter refuses before calling <c>next</c>.
    /// </summary>
    /// <param name="status">The HTTP status, such as 403.</param>
    /// <param name="message">Why.</param>
    public void Reject(int status, string message)
    {
        RejectedStatus = status;
        RejectedMessage = message;
    }
}

/// <summary>
/// Learns of statements once they are stored and committed, such as to start a workflow.
/// </summary>
public interface IXapiStatementObserver
{
    /// <summary>
    /// Called after statements were stored.
    /// </summary>
    /// <param name="records">The stored records, without those that were already in the LRS.</param>
    /// <param name="credential">The credential, or <see langword="null"/> for statements the LRS wrote for a module.</param>
    /// <returns>A task.</returns>
    Task StoredAsync(IReadOnlyList<XapiStatementRecord> records, XapiCredential credential);
}

/// <summary>
/// Registers statement filters and observers.
/// </summary>
public static class XapiStatementExtensionsServiceCollectionExtensions
{
    /// <summary>
    /// Adds a statement filter.
    /// </summary>
    /// <typeparam name="TFilter">The filter.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddXapiStatementFilter<TFilter>(this IServiceCollection services)
        where TFilter : class, IXapiStatementFilter
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IXapiStatementFilter, TFilter>();

        return services;
    }

    /// <summary>
    /// Adds a statement observer.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddXapiStatementObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IXapiStatementObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IXapiStatementObserver, TObserver>();

        return services;
    }
}
