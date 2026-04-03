# Content Items Examples

## Example 1: Creating and Publishing an Article Programmatically

A service that creates an Article content item, populates its parts and fields, and publishes it.

```csharp
public sealed class ArticleService
{
    private readonly IContentManager _contentManager;

    public ArticleService(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<ContentItem> CreateArticleAsync(
        string title, string body, string category)
    {
        var contentItem = await _contentManager.NewAsync("Article");

        contentItem.Alter<TitlePart>(part =>
        {
            part.Title = title;
        });

        contentItem.Alter<AutoroutePart>(part =>
        {
            part.Path = title.ToLower().Replace(' ', '-');
        });

        contentItem.Alter<HtmlBodyPart>(part =>
        {
            part.Html = body;
        });

        contentItem.Alter<ContentPart>("Article", part =>
        {
            part.Alter<TextField>("Category", field =>
            {
                field.Text = category;
            });
        });

        await _contentManager.CreateAsync(contentItem, VersionOptions.Published);

        return contentItem;
    }
}
```

## Example 2: Querying Content Items with Filters

A service that queries published content items using YesSql indexes with multiple filter conditions.

```csharp
public sealed class ProductQueryService
{
    private readonly ISession _session;
    private readonly IContentManager _contentManager;

    public ProductQueryService(ISession session, IContentManager contentManager)
    {
        _session = session;
        _contentManager = contentManager;
    }

    public async Task<IEnumerable<ContentItem>> GetRecentProductsAsync(int count)
    {
        var items = await _session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == "Product"
                && index.Published)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(count)
            .ListAsync();

        return items;
    }

    public async Task<ContentItem?> GetProductByRouteAsync(string path)
    {
        var item = await _session
            .Query<ContentItem, AutoroutePartIndex>(index =>
                index.Path == path
                && index.Published)
            .FirstOrDefaultAsync();

        return item;
    }
}
```

## Example 3: Content Handler for Auditing Changes

A handler that logs content item lifecycle events for audit purposes.

```csharp
public sealed class AuditContentHandler : ContentHandlerBase
{
    private readonly ILogger<AuditContentHandler> _logger;

    public AuditContentHandler(ILogger<AuditContentHandler> logger)
    {
        _logger = logger;
    }

    public override Task CreatedAsync(CreateContentContext context)
    {
        _logger.LogInformation(
            "Content item created: Type={ContentType}, Id={ContentItemId}, Title={DisplayText}",
            context.ContentItem.ContentType,
            context.ContentItem.ContentItemId,
            context.ContentItem.DisplayText);

        return Task.CompletedTask;
    }

    public override Task PublishedAsync(PublishContentContext context)
    {
        _logger.LogInformation(
            "Content item published: Type={ContentType}, Id={ContentItemId}, Title={DisplayText}",
            context.ContentItem.ContentType,
            context.ContentItem.ContentItemId,
            context.ContentItem.DisplayText);

        return Task.CompletedTask;
    }

    public override Task RemovedAsync(RemoveContentContext context)
    {
        _logger.LogWarning(
            "Content item removed: Type={ContentType}, Id={ContentItemId}, Title={DisplayText}",
            context.ContentItem.ContentType,
            context.ContentItem.ContentItemId,
            context.ContentItem.DisplayText);

        return Task.CompletedTask;
    }
}
```

Register in the module startup:

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentHandler<AuditContentHandler>();
    }
}
```

## Example 4: Importing Content Items via Recipe

A recipe that imports a set of FAQ content items with title and body fields.

```json
{
  "steps": [
    {
      "name": "Content",
      "data": [
        {
          "ContentItemId": "faq-getting-started",
          "ContentType": "Faq",
          "DisplayText": "How do I get started?",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "How do I get started?"
          },
          "Faq": {
            "Answer": {
              "Html": "<p>Visit the documentation page and follow the quick-start guide to set up your first project.</p>"
            }
          }
        },
        {
          "ContentItemId": "faq-reset-password",
          "ContentType": "Faq",
          "DisplayText": "How do I reset my password?",
          "Latest": true,
          "Published": true,
          "TitlePart": {
            "Title": "How do I reset my password?"
          },
          "Faq": {
            "Answer": {
              "Html": "<p>Click the 'Forgot Password' link on the login page and follow the instructions sent to your email.</p>"
            }
          }
        }
      ]
    }
  ]
}
```

## Example 5: Draft and Publish Workflow

A service that demonstrates the draft-edit-publish workflow for content items.

```csharp
public sealed class ContentPublishingService
{
    private readonly IContentManager _contentManager;

    public ContentPublishingService(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<ContentItem> CreateDraftAsync(string title, string body)
    {
        var contentItem = await _contentManager.NewAsync("Article");

        contentItem.Alter<TitlePart>(part =>
        {
            part.Title = title;
        });

        contentItem.Alter<HtmlBodyPart>(part =>
        {
            part.Html = body;
        });

        // Save as draft without publishing.
        await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);

        return contentItem;
    }

    public async Task UpdateDraftAsync(string contentItemId, string newBody)
    {
        var contentItem = await _contentManager.GetAsync(
            contentItemId, VersionOptions.DraftRequired);

        if (contentItem == null)
        {
            return;
        }

        contentItem.Alter<HtmlBodyPart>(part =>
        {
            part.Html = newBody;
        });

        await _contentManager.UpdateAsync(contentItem);
    }

    public async Task PublishDraftAsync(string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(
            contentItemId, VersionOptions.DraftRequired);

        if (contentItem == null)
        {
            return;
        }

        await _contentManager.PublishAsync(contentItem);
    }

    public async Task UnpublishAsync(string contentItemId)
    {
        var contentItem = await _contentManager.GetAsync(
            contentItemId, VersionOptions.Published);

        if (contentItem == null)
        {
            return;
        }

        await _contentManager.UnpublishAsync(contentItem);
    }
}
```
