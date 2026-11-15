using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Services;

/// <summary>
/// Tells the audience admin which required learning rules use an audience.
/// </summary>
internal sealed class RequiredLearningAudienceUsage : IUserAudienceUsage
{
    private readonly IRequiredLearningService _requiredLearning;

    public RequiredLearningAudienceUsage(IRequiredLearningService requiredLearning)
    {
        _requiredLearning = requiredLearning;
    }

    public async Task<IReadOnlyList<string>> ListUsesAsync(string audienceName, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(audienceName)
            ? []
            : (await _requiredLearning.ListAsync(cancellationToken))
                .Where(requirement => string.Equals(requirement.AudienceName, audienceName.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(requirement => requirement.Name)
                .ToArray();
}
