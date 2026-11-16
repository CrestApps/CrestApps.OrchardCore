using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// The approval types modules register with <c>services.AddApprovalType&lt;THandler&gt;(name, ...)</c>, such as a
/// pay run, a ticket or a time sheet.
/// </summary>
public sealed class ApprovalTypeOptions
{
    /// <summary>
    /// Gets the types by name.
    /// </summary>
    public Dictionary<string, ApprovalType> Types { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A kind of thing people approve: its name (a request's <c>SubjectKind</c>), what people see, and the handler that
/// fills, checks and follows its requests.
/// </summary>
public sealed class ApprovalType
{
    /// <summary>
    /// Gets or sets the stored name; it never changes.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what people see, such as "Pay run".
    /// </summary>
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what is approved, for the policy editor.
    /// </summary>
    public LocalizationSource Description { get; set; }

    /// <summary>
    /// Gets or sets the handler, an <see cref="IApprovalTypeHandler"/> registered as itself; <see langword="null"/>
    /// for a type that needs none.
    /// </summary>
    public Type HandlerType { get; set; }
}
