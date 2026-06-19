namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal static class ContentItemToolGuidance
{
    internal const string NestedContentInstruction = "Call this function only for the top-level content item. If the item contains nested or contained content items, such as items inside BagPart, FlowPart, widgets, or blocks, include them inside the same parent payload instead of calling this function separately for each nested item.";
    internal const string ContentSchemaInstruction = "Before calling this function, call the 'getContentItemSchema' tool first whenever it is available. Request the parent content type plus any nested content types you plan to include, then build the payload to match that schema before submitting this function call.";

    internal static string EnsureNestedContentInstruction(string guidance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guidance);

        var instructions = new List<string>();

        if (!guidance.Contains(NestedContentInstruction, StringComparison.Ordinal))
        {
            instructions.Add(NestedContentInstruction);
        }

        if (!guidance.Contains(ContentSchemaInstruction, StringComparison.Ordinal))
        {
            instructions.Add(ContentSchemaInstruction);
        }

        if (instructions.Count == 0)
        {
            return guidance;
        }

        return
            $"""
            {string.Join(Environment.NewLine + Environment.NewLine, instructions)}

            {guidance}
            """;
    }
}
