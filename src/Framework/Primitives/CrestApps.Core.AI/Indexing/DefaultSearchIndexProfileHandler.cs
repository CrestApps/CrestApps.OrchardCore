using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;

namespace CrestApps.Core.AI.Indexing;

public sealed class DefaultSearchIndexProfileHandler : IndexProfileHandlerBase
{
    public override ValueTask ValidateAsync(
        SearchIndexProfile indexProfile,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(indexProfile.Name))
        {
            result.Fail(new ValidationResult("Name is required.", [nameof(SearchIndexProfile.Name)]));
        }

        if (string.IsNullOrWhiteSpace(indexProfile.IndexName))
        {
            result.Fail(new ValidationResult("Index name is required.", [nameof(SearchIndexProfile.IndexName)]));
        }

        if (string.IsNullOrWhiteSpace(indexProfile.ProviderName))
        {
            result.Fail(new ValidationResult("Provider is required.", [nameof(SearchIndexProfile.ProviderName)]));
        }

        if (string.IsNullOrWhiteSpace(indexProfile.Type))
        {
            result.Fail(new ValidationResult("Type is required.", [nameof(SearchIndexProfile.Type)]));
        }

        return ValueTask.CompletedTask;
    }
}
