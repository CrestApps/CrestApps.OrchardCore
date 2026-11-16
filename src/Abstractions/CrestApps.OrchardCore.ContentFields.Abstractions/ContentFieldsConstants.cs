namespace CrestApps.OrchardCore.ContentFields;

/// <summary>
/// The constants of the CrestApps Content Fields module.
/// </summary>
public static class ContentFieldsConstants
{
    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The phone field and the module's base feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.ContentFields";

        /// <summary>
        /// The address field.
        /// </summary>
        public const string Address = "CrestApps.OrchardCore.ContentFields.Address";

        /// <summary>
        /// Rules on content fields: a field shows only when other answers say so.
        /// </summary>
        public const string ConditionalFields = "CrestApps.OrchardCore.ContentFields.ConditionalFields";
    }

    /// <summary>
    /// The name of the script that shows and hides conditional fields in the browser.
    /// </summary>
    public const string ConditionalFieldsScript = "crestapps-conditional-fields";

    /// <summary>
    /// The name of the script of the <c>TaxonomyCascadingPicker</c> shape.
    /// </summary>
    public const string CascadingPickerScript = "crestapps-cascading-picker";
}
