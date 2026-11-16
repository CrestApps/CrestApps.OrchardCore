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

    /// <summary>
    /// Gets or sets the standard conditions a policy of this type is chosen by, which the policy editor shows: an
    /// amount range and currency, and an access scope picked from the handler's
    /// <see cref="IApprovalTypeHandler.GetScopesAsync"/>. A type adds other conditions with its own policy display
    /// driver and <see cref="IApprovalTypeHandler.MatchesAsync"/>.
    /// </summary>
    public ApprovalConditions Conditions { get; set; }
}

/// <summary>
/// The standard conditions an approval type's policies are chosen by.
/// </summary>
[Flags]
public enum ApprovalConditions
{
    /// <summary>
    /// None: a policy of the type is chosen by its type alone.
    /// </summary>
    None = 0,

    /// <summary>
    /// The request's amount, between a least and a most amount, in a currency.
    /// </summary>
    Amount = 1,

    /// <summary>
    /// The request's access scope, such as one employer.
    /// </summary>
    Scope = 2,
}

/// <summary>
/// An access scope a policy can be limited to, such as one employer.
/// </summary>
/// <param name="Value">The stored scope, a request's <c>AccessScope</c>.</param>
/// <param name="DisplayText">What people see.</param>
public sealed record ApprovalScope(string Value, string DisplayText);
