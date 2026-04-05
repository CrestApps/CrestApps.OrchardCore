using CrestApps.AI;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Chat;
using CrestApps.AI.DataSources;
using CrestApps.AI.Deployments;
using CrestApps.AI.Indexing;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.AI.Services;
using CrestApps.Data.YesSql;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.A2A.Indexes;
using CrestApps.Mvc.Web.Areas.Admin.Handlers;
using CrestApps.Mvc.Web.Areas.Admin.Indexes;
using CrestApps.Mvc.Web.Areas.Admin.Models;
using CrestApps.Mvc.Web.Areas.Admin.Services;
using CrestApps.Mvc.Web.Areas.AI.Handlers;
using CrestApps.Mvc.Web.Areas.AI.Indexes;
using CrestApps.Mvc.Web.Areas.AI.Services;
using CrestApps.Mvc.Web.Areas.AIChat.Handlers;
using CrestApps.Mvc.Web.Areas.AIChat.Indexes;
using CrestApps.Mvc.Web.Areas.AIChat.Services;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Indexes;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Services;
using CrestApps.Mvc.Web.Areas.DataSources.Handlers;
using CrestApps.Mvc.Web.Areas.DataSources.Indexes;
using CrestApps.Mvc.Web.Areas.DataSources.Services;
using CrestApps.Mvc.Web.Areas.Indexing.Indexes;
using CrestApps.Mvc.Web.Areas.Indexing.Services;
using CrestApps.Mvc.Web.Areas.Mcp.Indexes;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.Mvc.Web.Services;

internal static class YesSqlServiceCollectionExtensions
{
    /// <summary>
    /// Registers YesSql with SQLite, all index providers, and the catalog/manager
    /// services that the MVC sample application needs. Call this from Program.cs to
    /// keep the data-store wiring in one place.
    /// </summary>
    public static IServiceCollection AddYesSqlDataStore(this IServiceCollection services, string appDataPath)
    {
        var dbPath = Path.Combine(appDataPath, "crestapps.db");

        services.AddSingleton(sp =>
        {
            var store = StoreFactory.CreateAndInitializeAsync(
                new Configuration()
                    .UseSqLite($"Data Source={dbPath};Cache=Shared")
                    .SetTablePrefix("CA_"))
                .GetAwaiter().GetResult();

            store.RegisterIndexes<AIProfileIndexProvider>();
            store.RegisterIndexes<AIProviderConnectionIndexProvider>();
            store.RegisterIndexes<A2AConnectionIndexProvider>();
            store.RegisterIndexes<McpConnectionIndexProvider>();
            store.RegisterIndexes<McpPromptIndexProvider>();
            store.RegisterIndexes<McpResourceIndexProvider>();
            store.RegisterIndexes<AIDeploymentIndexProvider>();
            store.RegisterIndexes<AIProfileTemplateIndexProvider>();
            store.RegisterIndexes<AIChatSessionIndexProvider>();
            store.RegisterIndexes<AIChatSessionMetricsIndexProvider>();
            store.RegisterIndexes<AIChatSessionExtractedDataIndexProvider>();
            store.RegisterIndexes<AIChatSessionPromptIndexProvider>();
            store.RegisterIndexes<AIDocumentIndexProvider>();
            store.RegisterIndexes<AIDocumentChunkIndexProvider>();
            store.RegisterIndexes<SearchIndexProfileIndexProvider>();
            store.RegisterIndexes<AIDataSourceIndexProvider>();
            store.RegisterIndexes<AIMemoryEntryIndexProvider>();
            store.RegisterIndexes<ChatInteractionIndexProvider>();
            store.RegisterIndexes<ChatInteractionPromptIndexProvider>();
            store.RegisterIndexes<ArticleIndexProvider>();

            return store;
        });

        services.AddScoped(sp => sp.GetRequiredService<IStore>().CreateSession());

        // YesSql-backed catalogs and managers.
        services
            .AddNamedSourceDocumentCatalog<AIProfile, AIProfileIndex>()
            .AddNamedSourceDocumentCatalog<AIProviderConnection, AIProviderConnectionIndex>()
            .AddDocumentCatalog<A2AConnection, A2AConnectionIndex>()
            .AddSourceDocumentCatalog<McpConnection, McpConnectionIndex>()
            .AddNamedDocumentCatalog<McpPrompt, McpPromptIndex>()
            .AddSourceDocumentCatalog<McpResource, McpResourceIndex>()
            .AddNamedSourceDocumentCatalog<AIProfileTemplate, AIProfileTemplateIndex>()
            .AddScoped<DefaultAIDeploymentManager>()
            .AddScoped<IAIDeploymentManager>(sp => sp.GetRequiredService<DefaultAIDeploymentManager>())
            .AddScoped<INamedSourceCatalogManager<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentManager>())
            .AddScoped<IAIProfileManager, SimpleAIProfileManager>()
            .AddScoped<AIProfileDocumentService>()
             .AddScoped<IAIChatSessionManager, YesSqlAIChatSessionManager>()
             .AddScoped<IAIChatSessionPromptStore, YesSqlAIChatSessionPromptStore>()
             .AddScoped<MvcAIChatSessionEventService>()
             .AddScoped<MvcAIChatSessionEventPostCloseObserver>()
             .AddScoped<MvcAIChatSessionExtractedDataService>()
             .AddScoped<IAIChatSessionAnalyticsRecorder>(sp => sp.GetRequiredService<MvcAIChatSessionEventPostCloseObserver>())
             .AddScoped<IAIChatSessionConversionGoalRecorder>(sp => sp.GetRequiredService<MvcAIChatSessionEventPostCloseObserver>())
             .AddScoped<IAIChatSessionExtractedDataRecorder>(sp => sp.GetRequiredService<MvcAIChatSessionExtractedDataService>())
             .AddScoped<IAIChatSessionHandler, AnalyticsChatSessionHandler>()
             .AddScoped<IAIDocumentStore, YesSqlAIDocumentStore>()
            .AddScoped<IAIDocumentChunkStore, YesSqlAIDocumentChunkStore>()
             .AddScoped<ISearchIndexProfileStore, YesSqlSearchIndexProfileStore>()
             .AddScoped<IAIDataSourceStore, YesSqlAIDataSourceStore>()
             .AddScoped<ICatalog<AIDataSource>>(sp => sp.GetRequiredService<IAIDataSourceStore>())
             .AddScoped<ICatalogManager<AIDataSource>, CatalogManager<AIDataSource>>()
             .AddScoped<IAIMemoryStore, YesSqlAIMemoryStore>()
             .AddScoped<ICatalogEntryHandler<AIMemoryEntry>, AIMemoryEntryIndexingHandler>()
             .AddScoped<MvcAIDocumentIndexingService>()
             .AddScoped<ISearchIndexProfileManager, SearchIndexProfileManager>()
             .AddScoped<IAuthorizationHandler, MvcChatInteractionDocumentAuthorizationHandler>()
             .AddScoped<IAuthorizationHandler, MvcAIChatSessionDocumentAuthorizationHandler>()
             .AddScoped<IAIChatDocumentEventHandler, MvcAIChatDocumentEventHandler>()
            .AddDocumentCatalog<ChatInteraction, ChatInteractionIndex>()
            .AddScoped<ICatalogManager<ChatInteraction>, CatalogManager<ChatInteraction>>()
            .AddScoped<IChatInteractionPromptStore, YesSqlChatInteractionPromptStore>()
            .AddDocumentCatalog<Article, ArticleIndex>()
            .AddScoped<ICatalogManager<Article>, CatalogManager<Article>>()
            .AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceIndexingHandler>()
            .AddScoped<ICatalogEntryHandler<Article>, ArticleIndexingHandler>()
            .AddScoped<ArticleIndexingService>();

        services
            .AddScoped<YesSqlAIDeploymentStore>()
            .AddScoped<IAIDeploymentStore>(sp => sp.GetRequiredService<YesSqlAIDeploymentStore>())
            .AddScoped<ConfigurationAIDeploymentCatalog>()
            .AddScoped<ICatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<INamedCatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<INamedSourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>());

        return services;
    }

    /// <summary>
    /// Creates YesSql index tables if they do not already exist.
    /// Call once at startup after <see cref="WebApplication.Build"/>.
    /// </summary>
    public static async Task InitializeYesSqlSchemaAsync(this IServiceProvider services)
    {
        var store = services.GetRequiredService<IStore>();
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();

        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIProfileIndex>(t => t
                .Column<string>(nameof(AIProfileIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIProfileIndex.Name), c => c.WithLength(255))
                .Column<string>(nameof(AIProfileIndex.Source), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIProviderConnectionIndex>(t => t
                .Column<string>(nameof(AIProviderConnectionIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIProviderConnectionIndex.Name), c => c.WithLength(255))
                .Column<string>(nameof(AIProviderConnectionIndex.Source), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<A2AConnectionIndex>(t => t
                .Column<string>(nameof(A2AConnectionIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(A2AConnectionIndex.DisplayText), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<McpConnectionIndex>(t => t
                .Column<string>(nameof(McpConnectionIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(McpConnectionIndex.DisplayText), c => c.WithLength(255))
                .Column<string>(nameof(McpConnectionIndex.Source), c => c.WithLength(50))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<McpPromptIndex>(t => t
                .Column<string>(nameof(McpPromptIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(McpPromptIndex.Name), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<McpResourceIndex>(t => t
                .Column<string>(nameof(McpResourceIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(McpResourceIndex.DisplayText), c => c.WithLength(255))
                .Column<string>(nameof(McpResourceIndex.Source), c => c.WithLength(50))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIDeploymentIndex>(t => t
                .Column<string>(nameof(AIDeploymentIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIDeploymentIndex.Name), c => c.WithLength(255))
                .Column<string>(nameof(AIDeploymentIndex.Source), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIProfileTemplateIndex>(t => t
                .Column<string>(nameof(AIProfileTemplateIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIProfileTemplateIndex.Name), c => c.WithLength(255))
                .Column<string>(nameof(AIProfileTemplateIndex.Source), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIChatSessionIndex>(t => t
                .Column<string>(nameof(AIChatSessionIndex.ItemId), c => c.WithLength(44))
                .Column<string>(nameof(AIChatSessionIndex.SessionId), c => c.WithLength(44))
                .Column<string>(nameof(AIChatSessionIndex.ProfileId), c => c.WithLength(26))
                .Column<string>(nameof(AIChatSessionIndex.UserId), c => c.WithLength(255))
                .Column<int>(nameof(AIChatSessionIndex.Status))
                .Column<DateTime>(nameof(AIChatSessionIndex.LastActivityUtc))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIChatSessionMetricsIndex>(t => t
                .Column<string>(nameof(AIChatSessionMetricsIndex.SessionId), c => c.WithLength(44))
                .Column<string>(nameof(AIChatSessionMetricsIndex.ProfileId), c => c.WithLength(26))
                .Column<string>(nameof(AIChatSessionMetricsIndex.VisitorId), c => c.WithLength(255))
                .Column<string>(nameof(AIChatSessionMetricsIndex.UserId), c => c.WithLength(255))
                .Column<bool>(nameof(AIChatSessionMetricsIndex.IsAuthenticated))
                .Column<DateTime>(nameof(AIChatSessionMetricsIndex.SessionStartedUtc))
                .Column<DateTime?>(nameof(AIChatSessionMetricsIndex.SessionEndedUtc))
                .Column<int>(nameof(AIChatSessionMetricsIndex.MessageCount))
                .Column<double>(nameof(AIChatSessionMetricsIndex.HandleTimeSeconds))
                .Column<bool>(nameof(AIChatSessionMetricsIndex.IsResolved))
                .Column<int>(nameof(AIChatSessionMetricsIndex.HourOfDay))
                .Column<int>(nameof(AIChatSessionMetricsIndex.DayOfWeek))
                .Column<int>(nameof(AIChatSessionMetricsIndex.TotalInputTokens))
                .Column<int>(nameof(AIChatSessionMetricsIndex.TotalOutputTokens))
                .Column<double>(nameof(AIChatSessionMetricsIndex.AverageResponseLatencyMs))
                .Column<bool?>(nameof(AIChatSessionMetricsIndex.UserRating))
                .Column<int>(nameof(AIChatSessionMetricsIndex.ThumbsUpCount))
                .Column<int>(nameof(AIChatSessionMetricsIndex.ThumbsDownCount))
                .Column<int?>(nameof(AIChatSessionMetricsIndex.ConversionScore))
                .Column<int?>(nameof(AIChatSessionMetricsIndex.ConversionMaxScore))
                .Column<DateTime>(nameof(AIChatSessionMetricsIndex.CreatedUtc))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIChatSessionExtractedDataIndex>(t => t
                .Column<string>(nameof(AIChatSessionExtractedDataIndex.SessionId), c => c.WithLength(44))
                .Column<string>(nameof(AIChatSessionExtractedDataIndex.ProfileId), c => c.WithLength(26))
                .Column<DateTime>(nameof(AIChatSessionExtractedDataIndex.SessionStartedUtc))
                .Column<DateTime?>(nameof(AIChatSessionExtractedDataIndex.SessionEndedUtc))
                .Column<int>(nameof(AIChatSessionExtractedDataIndex.FieldCount))
                .Column<string>(nameof(AIChatSessionExtractedDataIndex.FieldNames), c => c.WithLength(4000))
                .Column<string>(nameof(AIChatSessionExtractedDataIndex.ValuesText), c => c.WithLength(4000))
                .Column<DateTime>(nameof(AIChatSessionExtractedDataIndex.UpdatedUtc))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIChatSessionPromptIndex>(t => t
                .Column<string>(nameof(AIChatSessionPromptIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIChatSessionPromptIndex.SessionId), c => c.WithLength(44))
                .Column<string>(nameof(AIChatSessionPromptIndex.Role), c => c.WithLength(50))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIDocumentIndex>(t => t
                .Column<string>(nameof(AIDocumentIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIDocumentIndex.ReferenceId), c => c.WithLength(26))
                .Column<string>(nameof(AIDocumentIndex.ReferenceType), c => c.WithLength(50))
                .Column<string>(nameof(AIDocumentIndex.FileName), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIDocumentChunkIndex>(t => t
                .Column<string>(nameof(AIDocumentChunkIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIDocumentChunkIndex.AIDocumentId), c => c.WithLength(26))
                .Column<string>(nameof(AIDocumentChunkIndex.ReferenceId), c => c.WithLength(26))
                .Column<string>(nameof(AIDocumentChunkIndex.ReferenceType), c => c.WithLength(50))
                .Column<int>(nameof(AIDocumentChunkIndex.Index))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<SearchIndexProfileIndex>(t => t
                .Column<string>(nameof(SearchIndexProfileIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(SearchIndexProfileIndex.Name), c => c.WithLength(255))
                .Column<string>(nameof(SearchIndexProfileIndex.ProviderName), c => c.WithLength(50))
                .Column<string>(nameof(SearchIndexProfileIndex.Type), c => c.WithLength(50))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIDataSourceIndex>(t => t
                .Column<string>(nameof(AIDataSourceIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIDataSourceIndex.DisplayText), c => c.WithLength(255))
                .Column<string>(nameof(AIDataSourceIndex.SourceIndexProfileName), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<AIMemoryEntryIndex>(t => t
                .Column<string>(nameof(AIMemoryEntryIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(AIMemoryEntryIndex.UserId), c => c.WithLength(255))
                .Column<string>(nameof(AIMemoryEntryIndex.Name), c => c.WithLength(255))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<ChatInteractionIndex>(t => t
                .Column<string>(nameof(ChatInteractionIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(ChatInteractionIndex.UserId), c => c.WithLength(255))
                .Column<string>(nameof(ChatInteractionIndex.Title), c => c.WithLength(255))
                .Column<DateTime>(nameof(ChatInteractionIndex.CreatedUtc))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<ChatInteractionPromptIndex>(t => t
                .Column<string>(nameof(ChatInteractionPromptIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(ChatInteractionPromptIndex.ChatInteractionId), c => c.WithLength(26))
                .Column<string>(nameof(ChatInteractionPromptIndex.Role), c => c.WithLength(50))
                .Column<DateTime>(nameof(ChatInteractionPromptIndex.CreatedUtc))));

        await TryCreateTableAsync(() =>
            schemaBuilder.CreateMapIndexTableAsync<ArticleIndex>(t => t
                .Column<string>(nameof(ArticleIndex.ItemId), c => c.WithLength(26))
                .Column<string>(nameof(ArticleIndex.Title), c => c.WithLength(255))));

        await transaction.CommitAsync();
    }

    private static async Task TryCreateTableAsync(Func<Task> createTable)
    {
        try { await createTable(); }
        catch { /* Table already exists. */ }
    }

    /// <summary>
    /// Seeds the database with sample articles on first run. Subsequent runs
    /// skip seeding because articles already exist.
    /// </summary>
    public static async Task SeedArticlesAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalog<Article>>();
        var existing = await catalog.GetAllAsync();

        if (existing.Count > 0)
        {
            return;
        }

        var articles = new[]
        {
            new Article
            {
                ItemId = UniqueId.GenerateId(),
                Title = "What Are Large Language Models?",
                CreatedUtc = DateTime.UtcNow,
                Description = """
                    # What Are Large Language Models?

                    Large Language Models (LLMs) are deep learning models trained on vast corpora of text data. They learn statistical patterns in language and can generate coherent, context-aware text.

                    ## Key Characteristics

                    - **Scale**: Billions of parameters trained on terabytes of text.
                    - **Generalization**: Capable of performing many tasks without task-specific training.
                    - **Context Window**: The amount of text the model can consider at once.

                    ## Common Use Cases

                    1. Conversational AI and chatbots
                    2. Content generation and summarization
                    3. Code assistance and generation
                    4. Translation and language understanding

                    LLMs form the backbone of modern AI assistants and are the foundation for tools like GitHub Copilot.
                    """,
            },
            new Article
            {
                ItemId = UniqueId.GenerateId(),
                Title = "Understanding Embeddings and Vector Search",
                CreatedUtc = DateTime.UtcNow,
                Description = """
                    # Understanding Embeddings and Vector Search

                    Embeddings are numerical representations of text (or other data) in a high-dimensional vector space. Similar concepts end up close together in this space.

                    ## How Embeddings Work

                    An embedding model converts text into a fixed-length array of floating-point numbers. For example, the sentence "The cat sat on the mat" might become a 1536-dimensional vector.

                    ## Vector Search

                    Vector search (also called semantic search) finds documents whose embeddings are closest to a query embedding using distance metrics like **cosine similarity** or **dot product**.

                    ### Why It Matters

                    - Traditional keyword search misses synonyms and paraphrases.
                    - Vector search understands meaning, not just exact words.
                    - Combining both approaches (hybrid search) gives the best results.

                    ## Providers

                    Popular embedding providers include OpenAI (`text-embedding-3-small`), Azure OpenAI, and open-source models like Sentence Transformers.
                    """,
            },
            new Article
            {
                ItemId = UniqueId.GenerateId(),
                Title = "Retrieval-Augmented Generation (RAG) Explained",
                CreatedUtc = DateTime.UtcNow,
                Description = """
                    # Retrieval-Augmented Generation (RAG) Explained

                    RAG is an architecture that combines information retrieval with text generation. Instead of relying solely on the model's training data, RAG retrieves relevant documents at query time and includes them in the prompt.

                    ## The RAG Pipeline

                    1. **User Query** — The user asks a question.
                    2. **Retrieval** — The system searches a knowledge base (using vector search) for relevant documents.
                    3. **Augmentation** — Retrieved documents are added to the prompt as context.
                    4. **Generation** — The LLM generates a response grounded in the retrieved context.

                    ## Benefits

                    - **Accuracy**: Responses are grounded in actual data, reducing hallucinations.
                    - **Freshness**: The knowledge base can be updated without retraining the model.
                    - **Transparency**: Sources can be cited alongside the generated answer.

                    ## Implementation Tips

                    - Use chunk sizes of 500–1000 tokens for best retrieval quality.
                    - Overlap chunks by 10–20% to preserve context boundaries.
                    - Always include metadata (source, date) with each chunk.
                    """,
            },
            new Article
            {
                ItemId = UniqueId.GenerateId(),
                Title = "Search Indexing with Elasticsearch",
                CreatedUtc = DateTime.UtcNow,
                Description = """
                    # Search Indexing with Elasticsearch

                    Elasticsearch is a distributed search and analytics engine built on Apache Lucene. It supports full-text search, structured queries, and dense vector search for AI workloads.

                    ## Core Concepts

                    - **Index**: A collection of documents with a defined schema (mapping).
                    - **Document**: A JSON object stored in an index.
                    - **Mapping**: Defines field types (keyword, text, dense_vector, date, etc.).

                    ## Best Practices

                    1. Use `keyword` for IDs and exact-match filters.
                    2. Use `text` for full-text searchable fields.
                    3. Keep index mappings minimal — only index fields you need to query.
                    4. Use bulk operations for efficient batch indexing.
                    """,
            },
            new Article
            {
                ItemId = UniqueId.GenerateId(),
                Title = "Azure AI Search for Knowledge Bases",
                CreatedUtc = DateTime.UtcNow,
                Description = """
                    # Azure AI Search for Knowledge Bases

                    Azure AI Search (formerly Azure Cognitive Search) is a fully managed cloud search service that supports full-text search, vector search, and hybrid queries.

                    ## Key Features

                    - **Vector Search**: Built-in support for HNSW-based approximate nearest neighbor search.
                    - **Semantic Ranking**: AI-powered re-ranking of search results for better relevance.
                    - **Integrated Vectorization**: Automatic embedding generation during indexing.
                    - **Hybrid Search**: Combine keyword and vector search in a single query.

                    ## Creating an Index

                    Define fields with appropriate types:

                    - `Edm.String` with `searchable: true` for text fields.
                    - `Edm.String` with `filterable: true` for keyword fields.
                    - `Collection(Edm.Single)` for vector fields with HNSW configuration.

                    ## Integration Patterns

                    Azure AI Search integrates naturally with Azure OpenAI for RAG scenarios. The "On Your Data" feature allows direct connection between Azure OpenAI chat completions and an Azure AI Search index.
                    """,
            },
            new Article
            {
                ItemId = UniqueId.GenerateId(),
                Title = "Building Custom Data Sources for AI",
                CreatedUtc = DateTime.UtcNow,
                Description = """
                    # Building Custom Data Sources for AI

                    A data source connects your application's structured data to AI search indexes. This allows AI assistants to answer questions using your specific domain knowledge.

                    ## Architecture Overview

                    ```
                    Application Data → Indexing Service → Search Index → Data Source → AI Profile
                    ```

                    1. **Application Data**: Your domain models (articles, products, tickets, etc.).
                    2. **Indexing Service**: Transforms models into search documents with defined field mappings.
                    3. **Search Index**: Stores documents in Elasticsearch or Azure AI Search.
                    4. **Data Source**: Maps the search index to the AI system with field name configuration.
                    5. **AI Profile**: Uses data sources as knowledge bases during chat.

                    ## Keeping Data in Sync

                    Use catalog handlers (`ICatalogEntryHandler<T>`) to automatically trigger re-indexing whenever a record is created, updated, or deleted. This ensures the search index always reflects the current state of your data.

                    ## Field Mapping

                    Define your index fields carefully:
                    - **Key field**: Unique identifier (usually the record ID).
                    - **Searchable fields**: Title, description, content — fields users will search.
                    - **Filterable fields**: Author, category, date — fields used for filtering.
                    """,
            },
        };

        foreach (var article in articles)
        {
            await catalog.CreateAsync(article);
        }

        await catalog.SaveChangesAsync();
    }
}
