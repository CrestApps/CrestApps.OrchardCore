using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// The education credits a course, a learning path or other learning awards on completion.
/// </summary>
public sealed class EducationCreditsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the credits awarded.
    /// </summary>
    public List<EducationCreditAward> Awards { get; set; } = [];
}

/// <summary>
/// An amount of a credit type awarded on completion.
/// </summary>
public sealed class EducationCreditAward
{
    /// <summary>
    /// Gets or sets the credit type.
    /// </summary>
    public string CreditTypeId { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets how many months the credits count; <see langword="null"/> for good.
    /// </summary>
    public int? ValidityMonths { get; set; }
}
