namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Well-known prompt template identifiers for system prompts defined in AI/Prompts/.
/// </summary>
public static class AITemplateIds
{
    public const string ChartGeneration = "chart-generation";

    public const string SearchQueryExtraction = "search-query-extraction";

    public const string TitleGeneration = "title-generation";

    public const string PostSessionAnalysis = "post-session-analysis";

    public const string DataExtraction = "data-extraction";

    public const string UseMarkdownSyntax = "use-markdown-syntax";

    public const string TaskPlanning = "task-planning";

    public const string RagResponseGuidelines = "rag-response-guidelines";

    public const string RagScopeNoRefsToolsDisabled = "rag-scope-no-refs-tools-disabled";

    public const string RagScopeNoRefsToolsEnabled = "rag-scope-no-refs-tools-enabled";

    public const string RagScopeWithRefs = "rag-scope-with-refs";

    public const string RagToolSearchStrict = "rag-tool-search-strict";

    public const string RagToolSearchRelaxed = "rag-tool-search-relaxed";

    public const string DataSourceContextHeader = "datasource-context-header";

    public const string DocumentContextHeader = "document-context-header";

    public const string DocumentAvailability = "document-availability";

    public const string TabularBatchProcessing = "tabular-batch-processing";
}
