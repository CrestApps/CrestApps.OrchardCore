using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Skills.AI;

/// <summary>
/// Builds the results of the Skills tools from an explicit list of fields; what other modules keep in a record's
/// properties is never returned.
/// </summary>
internal static class SkillsToolResults
{
    /// <summary>
    /// Gets the names of the certification types by id.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certification types by id.</returns>
    public static async Task<IReadOnlyDictionary<string, Certification>> GetCertificationsAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (await services.GetRequiredService<ICertificationManager>().GetAllAsync(cancellationToken))
            .Where(certification => !string.IsNullOrEmpty(certification.ItemId))
            .ToDictionary(certification => certification.ItemId, StringComparer.Ordinal);

    /// <summary>
    /// Gets the skills by id.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The skills by id.</returns>
    public static async Task<IReadOnlyDictionary<string, Skill>> GetSkillsAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (await services.GetRequiredService<ISkillManager>().GetAllAsync(cancellationToken))
            .Where(skill => !string.IsNullOrEmpty(skill.ItemId))
            .ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);

    /// <summary>
    /// Describes a skill.
    /// </summary>
    /// <param name="skill">The skill.</param>
    /// <param name="certifications">The certification types by id, to name the required one.</param>
    /// <returns>The description.</returns>
    public static object Skill(Skill skill, IReadOnlyDictionary<string, Certification> certifications)
        => new
        {
            id = skill.ItemId,
            name = skill.Name,
            category = skill.Category,
            description = skill.Description,
            requiredCertificationId = skill.RequiredCertificationId,
            requiredCertificationName = skill.RequiredCertificationId is not null && certifications.TryGetValue(skill.RequiredCertificationId, out var certification) ? certification.Name : null,
            enabled = skill.Enabled,
        };

    /// <summary>
    /// Describes a certification type.
    /// </summary>
    /// <param name="certification">The certification type.</param>
    /// <returns>The description.</returns>
    public static object Certification(Certification certification)
        => new
        {
            id = certification.ItemId,
            name = certification.Name,
            issuer = certification.Issuer,
            description = certification.Description,
            validityMonths = certification.ValidityMonths,
            reminderDays = certification.ReminderDays,
            requiresDocument = certification.RequiresDocument,
            enabled = certification.Enabled,
        };

    /// <summary>
    /// Describes a certificate a person holds.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="record">The certificate.</param>
    /// <param name="certifications">The certification types by id.</param>
    /// <param name="names">The subject names by <see cref="SkillSubject.Key"/>.</param>
    /// <param name="today">Today, to tell whether it counts.</param>
    /// <returns>The description.</returns>
    public static async Task<object> CertificateAsync(IServiceProvider services, CertificationRecord record, IReadOnlyDictionary<string, Certification> certifications, IReadOnlyDictionary<string, string> names, DateOnly today)
        => new
        {
            id = record.ItemId,
            subjectKind = record.SubjectKind,
            subjectId = record.SubjectId,
            subjectName = names is not null && names.TryGetValue(record.Subject.Key, out var name) ? name : record.SubjectName,
            certificationId = record.CertificationId,
            certificationName = record.CertificationId is not null && certifications.TryGetValue(record.CertificationId, out var certification) ? certification.Name : null,
            number = record.Number,
            issuedOn = Date(record.IssuedOn),
            expiresOn = Date(record.ExpiresOn),
            documentPath = record.DocumentPath,
            status = record.Status.ToString(),
            validToday = record.IsValidOn(today),
            verifiedBy = record.VerifiedBy,
            verifiedAt = await SkillsAIToolBase.LocalAsync(services, record.VerifiedUtc),
            notes = record.Notes,
        };

    /// <summary>
    /// Writes a date as yyyy-MM-dd.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static string Date(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
