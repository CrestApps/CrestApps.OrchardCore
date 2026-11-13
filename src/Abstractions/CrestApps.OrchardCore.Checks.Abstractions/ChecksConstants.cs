namespace CrestApps.OrchardCore.Checks;

/// <summary>
/// Names shared by the Checks module and the modules that build on it.
/// </summary>
public static class ChecksConstants
{
    /// <summary>
    /// The YesSql collection that holds the check register.
    /// </summary>
    public const string CollectionName = "Checks";

    /// <summary>
    /// The name of the recipe and deployment step that carries check layouts.
    /// </summary>
    public const string LayoutsRecipeStepName = "CheckLayouts";

    /// <summary>
    /// The name of the recipe and deployment step that carries the check register (opt-in history).
    /// </summary>
    public const string RegisterRecipeStepName = "CheckRegister";

    /// <summary>
    /// The prefix of the number series of each bank account; the account's id follows it.
    /// </summary>
    public const string NumberSeriesPrefix = "Checks:";

    /// <summary>
    /// The data protection purpose of the files of check layouts (signatures, the check line font).
    /// </summary>
    public const string FileProtectionPurpose = "Checks.Files";

    /// <summary>
    /// The folder of the tenant file store that holds the files of check layouts.
    /// </summary>
    public const string FileStoreFolder = "Checks";

    /// <summary>
    /// The feature ids of the Checks module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Checks feature: layouts, the register, printing and numbering.
        /// </summary>
        public const string Checks = "CrestApps.OrchardCore.Checks";
    }

    /// <summary>
    /// The kinds of files a check layout keeps in the encrypted file store.
    /// </summary>
    public static class LayoutFiles
    {
        /// <summary>
        /// The signature image (PNG or JPEG).
        /// </summary>
        public const string Signature = "signature";

        /// <summary>
        /// The second signature image, printed above a set amount.
        /// </summary>
        public const string SecondSignature = "signature2";

        /// <summary>
        /// The TrueType font of the check line (MICR E-13B).
        /// </summary>
        public const string MicrFont = "micr-font";
    }

    /// <summary>
    /// Returns the number series of a bank account's checks.
    /// </summary>
    /// <param name="bankAccountId">The bank account's id.</param>
    /// <returns>The series name.</returns>
    public static string NumberSeries(string bankAccountId)
        => NumberSeriesPrefix + bankAccountId;
}
