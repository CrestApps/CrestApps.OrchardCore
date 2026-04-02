using System.Text.Json.Nodes;
using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Mvc.Web.Areas.Indexing.Controllers;
using CrestApps.Mvc.Web.Models;
using CrestApps.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class SearchIndexProfileProvisioningServiceTests
{
    [Fact]
    public async Task Create_ShouldApplyProviderPrefixAndCreateRemoteIndex()
    {
        var remoteManager = new TestRemoteSearchIndexManager
        {
            Prefix = "tenant-",
        };
        var profileManager = new TestSearchIndexProfileManager();
        var controller = CreateController(profileManager, remoteManager);

        var result = await controller.Create(new IndexProfileViewModel
        {
            Name = "articles",
            IndexName = "articles",
            ProviderName = CrestApps.Elasticsearch.ServiceCollectionExtensions.ProviderName,
            Type = IndexProfileTypes.Articles,
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(IndexProfileController.Index), redirect.ActionName);
        Assert.Equal("tenant-articles", remoteManager.CreatedIndexName);
        Assert.Single(profileManager.CreatedProfiles);
        Assert.Equal("tenant-articles", profileManager.CreatedProfiles[0].IndexFullName);
        Assert.Contains(remoteManager.CreatedFields, field => field.Name == "article_id" && field.IsKey);
    }

    [Fact]
    public async Task Create_ShouldRejectExistingRemoteIndex()
    {
        var remoteManager = new TestRemoteSearchIndexManager
        {
            Prefix = "tenant-",
            ExistsResult = true,
        };
        var controller = CreateController(new TestSearchIndexProfileManager(), remoteManager);

        var result = await controller.Create(new IndexProfileViewModel
        {
            Name = "articles",
            IndexName = "articles",
            ProviderName = CrestApps.Elasticsearch.ServiceCollectionExtensions.ProviderName,
            Type = IndexProfileTypes.Articles,
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<IndexProfileViewModel>(view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(IndexProfileViewModel.IndexName)].Errors, error => error.ErrorMessage.Contains("already exists", StringComparison.Ordinal));
        Assert.Null(remoteManager.CreatedIndexName);
    }

    private static IndexProfileController CreateController(
        ISearchIndexProfileManager profileManager,
        TestRemoteSearchIndexManager remoteManager)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ISearchIndexManager>(CrestApps.Elasticsearch.ServiceCollectionExtensions.ProviderName, remoteManager);
        var serviceProvider = services.BuildServiceProvider();
        var sourceOptions = new IndexProfileSourceOptions();
        sourceOptions.Sources.Add(new IndexProfileSourceDescriptor
        {
            ProviderName = CrestApps.Elasticsearch.ServiceCollectionExtensions.ProviderName,
            ProviderDisplayName = "Elasticsearch",
            Type = IndexProfileTypes.Articles,
            DisplayName = "Articles",
            Description = "Articles",
        });

        var controller = new IndexProfileController(
            profileManager,
            new TestDeploymentCatalog(),
            serviceProvider,
            Options.Create(sourceOptions),
            NullLogger<IndexProfileController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
            },
        };
        controller.Url = Mock.Of<IUrlHelper>();
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());

        return controller;
    }

    private sealed class TestRemoteSearchIndexManager : ISearchIndexManager
    {
        public bool ExistsResult { get; set; }

        public string Prefix { get; set; }

        public string CreatedIndexName { get; private set; }

        public IReadOnlyCollection<SearchIndexField> CreatedFields { get; private set; }

        public string ComposeIndexFullName(IIndexProfileInfo profile)
            => string.Concat(Prefix, profile.IndexName);

        public Task<bool> ExistsAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistsResult);

        public Task CreateAsync(IIndexProfileInfo profile, IReadOnlyCollection<SearchIndexField> fields, CancellationToken cancellationToken = default)
        {
            CreatedIndexName = profile.IndexFullName;
            CreatedFields = fields;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestSearchIndexProfileManager : ISearchIndexProfileManager
    {
        public List<SearchIndexProfile> CreatedProfiles { get; } = [];

        public ValueTask CreateAsync(SearchIndexProfile model)
        {
            CreatedProfiles.Add(model.Clone());
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(SearchIndexProfile model)
            => ValueTask.FromResult(true);

        public ValueTask<SearchIndexProfile> FindByIdAsync(string id)
            => ValueTask.FromResult<SearchIndexProfile>(null);

        public Task<SearchIndexProfile> FindByNameAsync(string name)
            => Task.FromResult<SearchIndexProfile>(null);

        public ValueTask<IReadOnlyCollection<SearchIndexField>> GetFieldsAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(profile.Type, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult<IReadOnlyCollection<SearchIndexField>>(null);
            }

            IReadOnlyCollection<SearchIndexField> fields =
            [
                new SearchIndexField
                {
                    Name = "article_id",
                    FieldType = SearchFieldType.Keyword,
                    IsKey = true,
                    IsFilterable = true,
                },
            ];

            return ValueTask.FromResult(fields);
        }

        public ValueTask<IEnumerable<SearchIndexProfile>> GetAllAsync()
            => ValueTask.FromResult<IEnumerable<SearchIndexProfile>>([]);

        public Task<IReadOnlyCollection<SearchIndexProfile>> GetByTypeAsync(string type)
            => Task.FromResult<IReadOnlyCollection<SearchIndexProfile>>([]);

        public ValueTask<SearchIndexProfile> NewAsync(JsonNode data = null)
            => ValueTask.FromResult(new SearchIndexProfile());

        public ValueTask<PageResult<SearchIndexProfile>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
            => ValueTask.FromResult(new PageResult<SearchIndexProfile>());

        public Task ResetAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SynchronizeAsync(SearchIndexProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask UpdateAsync(SearchIndexProfile model, JsonNode data = null)
            => ValueTask.CompletedTask;

        public ValueTask<ValidationResultDetails> ValidateAsync(SearchIndexProfile model)
            => ValueTask.FromResult(new ValidationResultDetails());
    }

    private sealed class TestDeploymentCatalog : ICatalog<AIDeployment>
    {
        public ValueTask CreateAsync(AIDeployment entry)
            => ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(AIDeployment entry)
            => ValueTask.FromResult(true);

        public ValueTask<AIDeployment> FindByIdAsync(string id)
            => ValueTask.FromResult<AIDeployment>(null);

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAllAsync()
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>([]);

        public ValueTask<IReadOnlyCollection<AIDeployment>> GetAsync(IEnumerable<string> ids)
            => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>([]);

        public ValueTask<PageResult<AIDeployment>> PageAsync<TQuery>(int page, int pageSize, TQuery context)
            where TQuery : QueryContext
            => ValueTask.FromResult(new PageResult<AIDeployment>());

        public ValueTask SaveChangesAsync()
            => ValueTask.CompletedTask;

        public ValueTask UpdateAsync(AIDeployment entry)
            => ValueTask.CompletedTask;
    }
}
