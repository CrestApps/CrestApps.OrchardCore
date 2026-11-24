namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// Names shared by the Skills module and the modules that integrate with it.
/// </summary>
public static class SkillsConstants
{
    /// <summary>
    /// The YesSql collection that holds skills, assignments, certifications and certification records.
    /// </summary>
    public const string CollectionName = "Skills";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries skills and certifications.
    /// </summary>
    public const string RecipeStepName = "Skills";

    /// <summary>
    /// The feature identifiers of the Skills module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Skills feature.
        /// </summary>
        public const string Skills = "CrestApps.OrchardCore.Skills";

        /// <summary>
        /// The feature that prints issued certificates from a template and proves them on a public page.
        /// </summary>
        public const string IssuedCertificates = "CrestApps.OrchardCore.Skills.IssuedCertificates";
    }

    /// <summary>
    /// The number series certificate numbers are drawn from, one per prefix.
    /// </summary>
    public const string CertificateNumberSeries = "Skills:CertificateNumber:";

    /// <summary>
    /// The default prefix of a generated certificate number.
    /// </summary>
    public const string DefaultCertificateNumberPrefix = "CERT-";

    /// <summary>
    /// How many characters a verification code has: 16 characters of a 32-letter alphabet hold 80 random bits.
    /// </summary>
    public const int VerificationCodeLength = 16;

    /// <summary>
    /// The content type of a certificate template.
    /// </summary>
    public const string CertificateTemplateContentType = "CertificateTemplate";

    /// <summary>
    /// The rate limit group of the public verification page.
    /// </summary>
    public const string VerificationRateLimitGroup = "Skills.CertificateVerification";
}
