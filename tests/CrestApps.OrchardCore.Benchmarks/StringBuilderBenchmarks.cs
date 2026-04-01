using System.Text;
using BenchmarkDotNet.Attributes;
using Cysharp.Text;

namespace CrestApps.OrchardCore.Benchmarks;

/// <summary>
/// Benchmarks comparing <see cref="StringBuilder"/> against ZString's
/// <see cref="Utf16ValueStringBuilder"/> for the string-building patterns
/// used throughout CrestApps.OrchardCore (system prompts, RAG context,
/// tool summaries, streaming accumulation, and CSV export).
/// </summary>
/// <remarks>
/// Run with:  dotnet run -c Release --project tests/CrestApps.OrchardCore.Benchmarks
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class StringBuilderBenchmarks
{
    [Params(10, 50, 200)]
    public int ItemCount { get; set; }

    // ──────────────────────────────────────────────────────────────
    //  1. System message generation (many AppendLine / Append calls)
    //     Simulates DefaultMcpMetadataPromptGenerator.Generate()
    // ──────────────────────────────────────────────────────────────

    [Benchmark(Description = "SystemMessage_StringBuilder")]
    public string SystemMessage_StringBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You have access to external MCP servers via the 'mcp_invoke' tool.");
        sb.AppendLine("Use the 'mcp_invoke' tool to call any of the capabilities listed below.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT invocation rules:");
        sb.AppendLine("- Always specify the correct 'clientId', 'type', and 'id' parameters.");
        sb.AppendLine("- For tools: set type='tool', id=<tool name>.");
        sb.AppendLine();
        sb.AppendLine("Available MCP Capabilities:");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.AppendLine();
            sb.Append("## Server: server-");
            sb.AppendLine(i.ToString());
            sb.Append("  clientId: conn-");
            sb.AppendLine(i.ToString());
            sb.AppendLine("  Tools:");

            for (var j = 0; j < 5; j++)
            {
                sb.Append("    - tool_");
                sb.Append(j);
                sb.Append(": Description of tool ");
                sb.AppendLine(j.ToString());
                sb.Append("      param1 (string, required): The first parameter");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    [Benchmark(Description = "SystemMessage_ZString")]
    public string SystemMessage_ZString()
    {
        using var sb = ZString.CreateStringBuilder();
        sb.AppendLine("You have access to external MCP servers via the 'mcp_invoke' tool.");
        sb.AppendLine("Use the 'mcp_invoke' tool to call any of the capabilities listed below.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT invocation rules:");
        sb.AppendLine("- Always specify the correct 'clientId', 'type', and 'id' parameters.");
        sb.AppendLine("- For tools: set type='tool', id=<tool name>.");
        sb.AppendLine();
        sb.AppendLine("Available MCP Capabilities:");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.AppendLine();
            sb.Append("## Server: server-");
            sb.AppendLine(i.ToString());
            sb.Append("  clientId: conn-");
            sb.AppendLine(i.ToString());
            sb.AppendLine("  Tools:");

            for (var j = 0; j < 5; j++)
            {
                sb.Append("    - tool_");
                sb.Append(j);
                sb.Append(": Description of tool ");
                sb.AppendLine(j.ToString());
                sb.Append("      param1 (string, required): The first parameter");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────
    //  2. RAG context building (interleaved formatting with indices)
    //     Simulates DocumentPreemptiveRagHandler / DataSourcePreemptiveRagOrchestrationHandler
    // ──────────────────────────────────────────────────────────────

    [Benchmark(Description = "RagContext_StringBuilder")]
    public string RagContext_StringBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("\n\n[Uploaded Document Context]");
        sb.AppendLine("The following content was retrieved from the user's uploaded documents via semantic search.");
        sb.AppendLine("If the documents do not contain relevant information, use your general knowledge.");
        sb.AppendLine("When citing information, include the corresponding reference marker (e.g., [doc:1]).");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.AppendLine("---");
            sb.Append("[doc:").Append(i + 1).Append("] ").AppendLine(
                "This is a sample document chunk that contains relevant information about the topic being discussed. " +
                "It may span multiple sentences and include technical details.");
        }

        sb.AppendLine();
        sb.AppendLine("References:");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.Append("[doc:").Append(i + 1).Append("] = {DocumentId: \"doc-").Append(i).Append("\", FileName: \"document_").Append(i).AppendLine(".pdf\"}");
        }

        return sb.ToString();
    }

    [Benchmark(Description = "RagContext_ZString")]
    public string RagContext_ZString()
    {
        using var sb = ZString.CreateStringBuilder();
        sb.AppendLine("\n\n[Uploaded Document Context]");
        sb.AppendLine("The following content was retrieved from the user's uploaded documents via semantic search.");
        sb.AppendLine("If the documents do not contain relevant information, use your general knowledge.");
        sb.AppendLine("When citing information, include the corresponding reference marker (e.g., [doc:1]).");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.AppendLine("---");
            sb.Append("[doc:");
            sb.Append(i + 1);
            sb.Append("] ");
            sb.AppendLine(
                "This is a sample document chunk that contains relevant information about the topic being discussed. " +
                "It may span multiple sentences and include technical details.");
        }

        sb.AppendLine();
        sb.AppendLine("References:");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.Append("[doc:");
            sb.Append(i + 1);
            sb.Append("] = {DocumentId: \"doc-");
            sb.Append(i);
            sb.Append("\", FileName: \"document_");
            sb.Append(i);
            sb.AppendLine(".pdf\"}");
        }

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────
    //  3. Streaming response accumulation (many small Append calls)
    //     Simulates AIChatHub / ChatInteractionHub streaming loop
    // ──────────────────────────────────────────────────────────────

    [Benchmark(Description = "Streaming_StringBuilder")]
    public string Streaming_StringBuilder()
    {
        var sb = new StringBuilder();

        for (var i = 0; i < ItemCount * 10; i++)
        {
            sb.Append("Token_");
            sb.Append(i);
            sb.Append(' ');
        }

        return sb.ToString();
    }

    [Benchmark(Description = "Streaming_ZString")]
    public string Streaming_ZString()
    {
        using var sb = ZString.CreateStringBuilder();

        for (var i = 0; i < ItemCount * 10; i++)
        {
            sb.Append("Token_");
            sb.Append(i);
            sb.Append(' ');
        }

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────
    //  4. CSV export (structured row-by-row append)
    //     Simulates ChatAnalyticsController.GenerateCsvContent()
    // ──────────────────────────────────────────────────────────────

    [Benchmark(Description = "CsvExport_StringBuilder")]
    public string CsvExport_StringBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.Append("session-").Append(i);
            sb.Append(',');
            sb.Append("profile-").Append(i);
            sb.Append(',');
            sb.Append("visitor-").Append(i);
            sb.Append(',');
            sb.Append("user-").Append(i);
            sb.Append(',');
            sb.Append(i % 2 == 0);
            sb.Append(',');
            sb.Append("2025-01-01T00:00:00Z");
            sb.Append(',');
            sb.Append("2025-01-01T01:00:00Z");
            sb.Append(',');
            sb.Append(i * 3);
            sb.Append(',');
            sb.Append(i * 120);
            sb.Append(',');
            sb.AppendLine((i % 3 == 0).ToString());
        }

        return sb.ToString();
    }

    [Benchmark(Description = "CsvExport_ZString")]
    public string CsvExport_ZString()
    {
        using var sb = ZString.CreateStringBuilder();
        sb.AppendLine("SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved");

        for (var i = 0; i < ItemCount; i++)
        {
            sb.Append("session-");
            sb.Append(i);
            sb.Append(',');
            sb.Append("profile-");
            sb.Append(i);
            sb.Append(',');
            sb.Append("visitor-");
            sb.Append(i);
            sb.Append(',');
            sb.Append("user-");
            sb.Append(i);
            sb.Append(',');
            sb.Append(i % 2 == 0);
            sb.Append(',');
            sb.Append("2025-01-01T00:00:00Z");
            sb.Append(',');
            sb.Append("2025-01-01T01:00:00Z");
            sb.Append(',');
            sb.Append(i * 3);
            sb.Append(',');
            sb.Append(i * 120);
            sb.Append(',');
            sb.AppendLine((i % 3 == 0).ToString());
        }

        return sb.ToString();
    }

    // ──────────────────────────────────────────────────────────────
    //  5. Tool summary building (conditional appends with iteration)
    //     Simulates DefaultOrchestrator.BuildToolSummary()
    // ──────────────────────────────────────────────────────────────

    [Benchmark(Description = "ToolSummary_StringBuilder")]
    public string ToolSummary_StringBuilder()
    {
        var sb = new StringBuilder();

        for (var i = 0; i < ItemCount; i++)
        {
            sb.Append("- ");
            sb.Append("tool_name_");
            sb.Append(i);

            if (i % 2 == 0)
            {
                sb.Append(": ");
                sb.Append("This tool performs operation ");
                sb.Append(i);
                sb.Append(" on the content items in the system.");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    [Benchmark(Description = "ToolSummary_ZString")]
    public string ToolSummary_ZString()
    {
        using var sb = ZString.CreateStringBuilder();

        for (var i = 0; i < ItemCount; i++)
        {
            sb.Append("- ");
            sb.Append("tool_name_");
            sb.Append(i);

            if (i % 2 == 0)
            {
                sb.Append(": ");
                sb.Append("This tool performs operation ");
                sb.Append(i);
                sb.Append(" on the content items in the system.");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
