using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The default <see cref="IApprovalTypeCatalog"/>, over <see cref="ApprovalTypeOptions"/>.
/// </summary>
public sealed class ApprovalTypeCatalog : IApprovalTypeCatalog
{
    private static readonly IApprovalTypeHandler _none = new NoApprovalTypeHandler();

    private readonly ApprovalTypeOptions _options;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalTypeCatalog"/> class.
    /// </summary>
    /// <param name="options">The registered types.</param>
    /// <param name="serviceProvider">The services, for the handlers.</param>
    public ApprovalTypeCatalog(IOptions<ApprovalTypeOptions> options, IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public IReadOnlyList<ApprovalType> List()
        => [.. _options.Types.Values.OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc />
    public ApprovalType Find(string name)
        => string.IsNullOrWhiteSpace(name) ? null : _options.Types.GetValueOrDefault(name.Trim());

    /// <inheritdoc />
    public IApprovalTypeHandler GetHandler(string name)
        => Find(name)?.HandlerType is { } handlerType && _serviceProvider.GetService(handlerType) is IApprovalTypeHandler handler
            ? handler
            : _none;

    private sealed class NoApprovalTypeHandler : ApprovalTypeHandlerBase
    {
    }
}
