namespace CrestApps.Core.AI;

/// <summary>
/// Well-known prompt template identifiers for system prompts defined in AI/Prompts/.
/// </summary>
public static class AITemplateIds
{
    public const string ChartGeneration = "chart-generation";

    public const string SearchQueryExtraction = "search-query-extraction";

    public const string TitleGeneration = "title-generation";

    public const string PostSessionAnalysis = "post-session-analysis";

    public const string PostSessionAnalysisPrompt = "post-session-analysis-prompt";

    public const string ResolutionAnalysis = "resolution-analysis";

    public const string ResolutionAnalysisPrompt = "resolution-analysis-prompt";

    public const string ConversionGoalEvaluation = "conversion-goal-evaluation";

    public const string ConversionGoalEvaluationPrompt = "conversion-goal-evaluation-prompt";

    public const string DataExtraction = "data-extraction";

    public const string DataExtractionPrompt = "data-extraction-prompt";

    public const string ExtractedDataAvailability = "extracted-data-availability";

    public const string UseMarkdownSyntax = "use-markdown-syntax";

    public const string TaskPlanning = "task-planning";

    public const string RagResponseGuidelines = "rag-response-guidelines";

    public const string RagScopeNoRefsToolsDisabled = "rag-scope-no-refs-tools-disabled";

    public const string RagScopeNoRefsToolsEnabled = "rag-scope-no-refs-tools-enabled";

    public const string RagScopeWithRefs = "rag-scope-with-refs";

    public const string RagToolSearchStrict = "rag-tool-search-strict";

    public const string RagToolSearchRelaxed = "rag-tool-search-relaxed";

    public const string DataSourceAvailability = "data-source-availability";

    public const string DataSourceContextHeader = "data-source-context-header";

    public const string DocumentContextHeader = "document-context-header";

    public const string DocumentAvailability = "document-availability";

    public const string AgentAvailability = "agent-availability";

    public const string TabularBatchProcessing = "tabular-batch-processing";
}
