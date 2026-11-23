namespace CrestApps.OrchardCore.PublicForms;

/// <summary>
/// The hidden trap field of a public form: people never see or fill it, form-filling robots do, so a post with a value
/// in it is dropped without telling the sender.
/// </summary>
public static class PublicFormTrap
{
    /// <summary>
    /// The name of the hidden input every public form renders.
    /// </summary>
    public const string FieldName = "website";

    /// <summary>
    /// Returns whether the posted trap value shows that a robot filled the form.
    /// </summary>
    /// <param name="value">The value posted in the <see cref="FieldName"/> input.</param>
    public static bool IsTrapped(string value)
        => !string.IsNullOrWhiteSpace(value);
}
