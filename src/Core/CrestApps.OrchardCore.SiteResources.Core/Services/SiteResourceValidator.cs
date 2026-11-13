using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Documents;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// The rules every site resource follows, whether it is saved from the admin, a recipe or another module.
/// </summary>
public sealed class SiteResourceValidator
{
    /// <summary>
    /// The largest content a site resource may hold, in characters.
    /// </summary>
    public const int MaxContentLength = 1024 * 1024;

    private readonly IDocumentManager<DictionaryDocument<SiteResource>> _documentManager;
    private readonly ResourceManagementOptions _resourceOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteResourceValidator"/> class.
    /// </summary>
    /// <param name="documentManager">The document the site resources catalog is stored in.</param>
    /// <param name="resourceOptions">The resources the modules and themes define.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SiteResourceValidator(
        IDocumentManager<DictionaryDocument<SiteResource>> documentManager,
        IOptions<ResourceManagementOptions> resourceOptions,
        IStringLocalizer<SiteResourceValidator> stringLocalizer)
    {
        _documentManager = documentManager;
        _resourceOptions = resourceOptions.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Validates <paramref name="resource"/> against the published site resources.
    /// </summary>
    /// <param name="resource">The resource to validate.</param>
    public async Task<IReadOnlyList<ValidationResult>> ValidateAsync(SiteResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var document = await _documentManager.GetOrCreateImmutableAsync();

        return Validate(resource, document.Records.Values.Select(other => (other.ItemId, other.Name)));
    }

    /// <summary>
    /// Validates <paramref name="resource"/> against the names other resources already use.
    /// </summary>
    /// <param name="resource">The resource to validate.</param>
    /// <param name="others">The id and name of every other site resource.</param>
    public IReadOnlyList<ValidationResult> Validate(SiteResource resource, IEnumerable<(string ItemId, string Name)> others)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(others);

        var errors = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(resource.Name))
        {
            errors.Add(new ValidationResult(S["Name is required."], [nameof(SiteResource.Name)]));
        }
        else if (!SiteResourceNames.IsValid(resource.Name))
        {
            errors.Add(new ValidationResult(S["Use up to {0} letters, digits, dots, dashes and underscores for the name, starting with a letter or a digit.", SiteResourceNames.MaxLength], [nameof(SiteResource.Name)]));
        }
        else if (others.Any(other => other.ItemId != resource.ItemId && string.Equals(other.Name, resource.Name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new ValidationResult(S["Another site resource is already named '{0}'.", resource.Name], [nameof(SiteResource.Name)]));
        }
        else if (IsDefinedByModuleOrTheme(resource))
        {
            errors.Add(new ValidationResult(S["A module or theme already defines a {0} named '{1}'. Choose another name.", resource.GetResourceType(), resource.Name], [nameof(SiteResource.Name)]));
        }

        if (string.IsNullOrWhiteSpace(resource.Content))
        {
            errors.Add(new ValidationResult(S["Enter the stylesheet or the script."], [nameof(SiteResource.Content)]));
        }
        else if (resource.Content.Length > MaxContentLength)
        {
            errors.Add(new ValidationResult(S["A site resource can hold up to {0:N0} characters.", MaxContentLength], [nameof(SiteResource.Content)]));
        }

        if (resource.Dependencies?.Any(dependency => string.Equals(dependency, resource.Name, StringComparison.OrdinalIgnoreCase)) == true)
        {
            errors.Add(new ValidationResult(S["A site resource can't depend on itself."], [nameof(SiteResource.Dependencies)]));
        }

        return errors;
    }

    private bool IsDefinedByModuleOrTheme(SiteResource resource)
    {
        var resourceType = resource.GetResourceType();

        return _resourceOptions.ResourceManifests.Any(manifest =>
            manifest.GetResources(resourceType).Keys.Any(name => string.Equals(name, resource.Name, StringComparison.OrdinalIgnoreCase)));
    }
}
