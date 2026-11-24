using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.ContentFields.Settings;

/// <summary>
/// When a content field shows, in the field's settings (the <b>Conditional Fields</b> feature). A field without a rule
/// always shows. Conditions read another field of the item by <c>{TypePartName}.{FieldName}</c> (as in
/// <c>Ticket.Outage</c>), or a value a module provides by its name (<see cref="Conditions.IContentConditionSource"/>).
/// </summary>
public sealed class ConditionalFieldSettings
{
    /// <summary>
    /// Gets or sets when the field shows, or <see langword="null"/> for always.
    /// </summary>
    public ConditionRule ShowWhen { get; set; }
}
