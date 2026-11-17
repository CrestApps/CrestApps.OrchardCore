using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using OrchardCore.Abstractions.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Core.Models;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Tests.Agent.Indexing;

/// <summary>
/// Stands in for the index profile manager in a tool test: it keeps the index profiles in memory, fills a new profile
/// from its data and validates a profile the way the index profile handlers of Orchard Core do, and records every
/// call that changes or starts something, in order.
/// </summary>
internal sealed class FakeIndexProfileManager : IIndexProfileManager
{
    /// <summary>
    /// Gets the stored index profiles.
    /// </summary>
    public List<IndexProfile> Profiles { get; } = [];

    /// <summary>
    /// Gets the calls that changed or started something, in order, for example <c>Reset:blog</c>.
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// Gets the profiles that were validated, as they were when they were validated.
    /// </summary>
    public List<IndexProfile> Validated { get; } = [];

    public ValueTask<IndexProfile> FindByIdAsync(string id)
        => ValueTask.FromResult(Profiles.Find(profile => profile.Id == id));

    public ValueTask<IndexProfile> FindByNameAsync(string name)
        => ValueTask.FromResult(Profiles.Find(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)));

    public ValueTask<IndexProfile> FindByNameAndProviderAsync(string indexName, string providerName)
    {
        return ValueTask.FromResult(Profiles.Find(profile =>
            string.Equals(profile.IndexName, indexName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(profile.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)));
    }

    public ValueTask<IEnumerable<IndexProfile>> GetAllAsync()
        => ValueTask.FromResult<IEnumerable<IndexProfile>>(Profiles.ToList());

    public ValueTask<PageResult<IndexProfile>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
        where TQuery : QueryContext
    {
        var matches = Profiles
            .Where(profile => string.IsNullOrEmpty(context?.Name) || profile.Name.Contains(context.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ValueTask.FromResult(new PageResult<IndexProfile>
        {
            Count = matches.Count,
            Models = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        });
    }

    public ValueTask<bool> DeleteAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Delete:{indexProfile.Name}");

        return ValueTask.FromResult(Profiles.Remove(indexProfile));
    }

    public ValueTask<IndexProfile> NewAsync(string providerName, string type, JsonNode data = null)
    {
        var indexProfile = new IndexProfile
        {
            Id = $"new-{Profiles.Count + 1}",
            ProviderName = providerName,
            Type = type,
            Name = data?[nameof(IndexProfile.Name)]?.GetValue<string>(),
            IndexName = data?[nameof(IndexProfile.IndexName)]?.GetValue<string>(),
        };

        indexProfile.IndexFullName = $"tenant_{indexProfile.IndexName}";

        if (type == IndexingConstants.ContentsIndexSource)
        {
            indexProfile.Put(new ContentIndexMetadata
            {
                IndexLatest = data?[nameof(ContentIndexMetadata.IndexLatest)]?.GetValue<bool>() ?? false,
                Culture = data?[nameof(ContentIndexMetadata.Culture)]?.GetValue<string>(),
                IndexedContentTypes = data?[nameof(ContentIndexMetadata.IndexedContentTypes)]?.AsArray().Select(node => node.GetValue<string>()).ToArray(),
            });
        }

        return ValueTask.FromResult(indexProfile);
    }

    public ValueTask CreateAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Create:{indexProfile.Name}");
        Profiles.Add(indexProfile);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(IndexProfile indexProfile, JsonNode data = null)
    {
        Calls.Add($"Update:{indexProfile.Name}");

        return ValueTask.CompletedTask;
    }

    public ValueTask<ValidationResultDetails> ValidateAsync(IndexProfile indexProfile)
    {
        Validated.Add(indexProfile);

        var result = new ValidationResultDetails();

        if (string.IsNullOrWhiteSpace(indexProfile.Name))
        {
            result.Fail(new ValidationResult("Index name is required."));
        }
        else if (Profiles.Exists(profile => profile.Id != indexProfile.Id && string.Equals(profile.Name, indexProfile.Name, StringComparison.OrdinalIgnoreCase)))
        {
            result.Fail(new ValidationResult("There is already another index with the same name."));
        }

        if (indexProfile.Type == IndexingConstants.ContentsIndexSource &&
            indexProfile.GetOrCreate<ContentIndexMetadata>().IndexedContentTypes is not { Length: > 0 })
        {
            result.Fail(new ValidationResult("At least one content type must be selected."));
        }

        return ValueTask.FromResult(result);
    }

    public ValueTask<IEnumerable<IndexProfile>> GetByProviderAsync(string providerName)
        => ValueTask.FromResult(Profiles.Where(profile => string.Equals(profile.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)));

    public ValueTask<IEnumerable<IndexProfile>> GetAsync(string providerName, string type)
        => ValueTask.FromResult(Profiles.Where(profile => profile.ProviderName == providerName && profile.Type == type));

    public ValueTask SynchronizeAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Synchronize:{indexProfile.Name}");

        return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Reset:{indexProfile.Name}");

        return ValueTask.CompletedTask;
    }
}
