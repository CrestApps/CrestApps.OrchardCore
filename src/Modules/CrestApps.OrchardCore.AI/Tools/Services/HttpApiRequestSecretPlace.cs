using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.AI.Tools.Services;

/// <summary>
/// Decides, once for each editor post, whether an HTTP API request tool instance still sends its stored secrets to the
/// place they were given for: the same base URL and token endpoint, compared with <see cref="SecretDestination"/>, and the
/// same way of signing in.
/// </summary>
/// <remarks>
/// The settings and the parameters of an instance are saved by two drivers on one post, and the settings driver replaces
/// the stored settings the comparison reads. Both drivers therefore ask this scoped service, which answers from the stored
/// settings as they were before either driver ran, so the answer is the same whichever driver runs first.
/// </remarks>
internal sealed class HttpApiRequestSecretPlace
{
    private readonly Dictionary<AIToolInstance, bool> _samePlace = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Returns whether the posted settings send to the place the stored secrets were given for. The settings driver calls
    /// it with the model it bound, before it replaces the stored settings.
    /// </summary>
    /// <param name="instance">The tool instance being edited.</param>
    /// <param name="posted">The settings the post carries.</param>
    public bool IsSamePlace(AIToolInstance instance, HttpApiRequestToolInstanceViewModel posted)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(posted);

        if (!_samePlace.TryGetValue(instance, out var same))
        {
            var stored = instance.GetOrCreate<HttpApiRequestToolSettings>();

            same = stored.AuthenticationType == posted.AuthenticationType &&
                SecretDestination.SameOrigin(stored.BaseUrl, posted.BaseUrl) &&
                SecretDestination.SameOrigin(stored.TokenEndpoint, posted.TokenEndpoint);

            _samePlace[instance] = same;
        }

        return same;
    }

    /// <summary>
    /// Returns whether the post sends the instance's stored secrets to the place they were given for, binding the posted
    /// settings when the settings driver has not run yet. An instance of another source has no such address and is
    /// always the same place.
    /// </summary>
    /// <param name="instance">The tool instance being edited.</param>
    /// <param name="updater">The updater that binds the post.</param>
    /// <param name="prefix">The prefix the instance's editor fields are posted with.</param>
    public async Task<bool> IsSamePlaceAsync(AIToolInstance instance, IUpdateModel updater, string prefix)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(updater);

        if (!string.Equals(instance.Source, HttpApiRequestToolConstants.SourceName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_samePlace.TryGetValue(instance, out var same))
        {
            return same;
        }

        var posted = new HttpApiRequestToolInstanceViewModel();

        await updater.TryUpdateModelAsync(posted, prefix);

        return IsSamePlace(instance, posted);
    }
}
