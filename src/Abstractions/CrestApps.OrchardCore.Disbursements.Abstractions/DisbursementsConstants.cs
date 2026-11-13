namespace CrestApps.OrchardCore.Disbursements;

/// <summary>
/// Names shared by the Disbursements module and the modules that build on it.
/// </summary>
public static class DisbursementsConstants
{
    /// <summary>
    /// The YesSql collection that holds disbursements and batches.
    /// </summary>
    public const string CollectionName = "Disbursements";

    /// <summary>
    /// The name of the recipe and deployment step that carries disbursements (opt-in history).
    /// </summary>
    public const string RecipeStepName = "Disbursements";

    /// <summary>
    /// The data protection purpose of batch files, which hold account numbers.
    /// </summary>
    public const string FileProtectionPurpose = "Disbursements.Files";

    /// <summary>
    /// The folder of the tenant file store that holds batch files.
    /// </summary>
    public const string FileStoreFolder = "Disbursements";

    /// <summary>
    /// The feature ids of the Disbursements module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Disbursements feature: disbursements, batches, the manual provider and, with Checks on, the check provider.
        /// </summary>
        public const string Disbursements = "CrestApps.OrchardCore.Disbursements";

        /// <summary>
        /// The bank file (Nacha) provider: ACH files, prenotes, returns and change notices.
        /// </summary>
        public const string Nacha = "CrestApps.OrchardCore.Disbursements.Nacha";
    }

    /// <summary>
    /// How a disbursement is paid. Open: a provider can add its own method.
    /// </summary>
    public static class Methods
    {
        /// <summary>
        /// An ACH credit through a bank file.
        /// </summary>
        public const string Ach = "Ach";

        /// <summary>
        /// A printed check.
        /// </summary>
        public const string Check = "Check";

        /// <summary>
        /// Paid outside the system, and recorded.
        /// </summary>
        public const string Manual = "Manual";

        /// <summary>
        /// Through a payment provider's payout API.
        /// </summary>
        public const string Provider = "Provider";
    }

    /// <summary>
    /// The names of the built-in providers.
    /// </summary>
    public static class Providers
    {
        /// <summary>
        /// The bank file (Nacha) provider.
        /// </summary>
        public const string Nacha = "Nacha";

        /// <summary>
        /// The check provider.
        /// </summary>
        public const string Check = "Check";

        /// <summary>
        /// The manual provider.
        /// </summary>
        public const string Manual = "Manual";
    }

    /// <summary>
    /// The source kind of the zero-amount test entries that verify bank accounts.
    /// </summary>
    public const string PrenoteSourceKind = "Prenote";
}
