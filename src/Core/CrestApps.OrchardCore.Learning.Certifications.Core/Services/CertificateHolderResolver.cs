using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Finds who holds a certificate a learner earns: their staff record when Staff is on and they have one, so Workforce
/// scheduling and Dispatch see it, or else the user.
/// </summary>
public class CertificateHolderResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LearnerUserLookup _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateHolderResolver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the staff directory when Staff is on.</param>
    /// <param name="users">The learner lookup.</param>
    public CertificateHolderResolver(IServiceProvider serviceProvider, LearnerUserLookup users)
    {
        _serviceProvider = serviceProvider;
        _users = users;
    }

    /// <summary>
    /// Finds the holder of a learner's certificates.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The holder and the name printed on the certificate.</returns>
    public virtual async Task<(SkillSubject Subject, string Name)> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        var directory = _serviceProvider.GetService<IStaffDirectory>();
        var member = directory is null ? null : await directory.FindByUserIdAsync(userId, cancellationToken);

        if (member is not null)
        {
            return (SkillSubject.Staff(member.ItemId), string.IsNullOrWhiteSpace(member.DisplayName) ? await UserNameAsync(userId) : member.DisplayName);
        }

        return (SkillSubject.User(userId), await UserNameAsync(userId));
    }

    /// <summary>
    /// Lists every holder a learner may hold certificates as.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The holders.</returns>
    public virtual async Task<IReadOnlyList<SkillSubject>> ListSubjectsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var subjects = new List<SkillSubject> { SkillSubject.User(userId) };
        var directory = _serviceProvider.GetService<IStaffDirectory>();
        var member = directory is null ? null : await directory.FindByUserIdAsync(userId, cancellationToken);

        if (member is not null)
        {
            subjects.Add(SkillSubject.Staff(member.ItemId));
        }

        return subjects;
    }

    private async Task<string> UserNameAsync(string userId)
    {
        var names = await _users.GetUserNamesAsync([userId]);

        return names.TryGetValue(userId, out var name) ? name : userId;
    }
}
