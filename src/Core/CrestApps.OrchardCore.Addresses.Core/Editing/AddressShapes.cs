namespace CrestApps.OrchardCore.Addresses.Core.Editing;

/// <summary>
/// The shapes the Addresses feature renders.
/// </summary>
public static class AddressShapes
{
    /// <summary>
    /// The address editor, laid out for the chosen country. Its model is an <see cref="AddressEditorViewModel"/>.
    /// </summary>
    public const string Editor = "AddressEditor";

    /// <summary>
    /// An address printed the way its country's post expects. Its model is an <see cref="AddressDisplayViewModel"/>.
    /// </summary>
    public const string Display = "AddressDisplay";
}
