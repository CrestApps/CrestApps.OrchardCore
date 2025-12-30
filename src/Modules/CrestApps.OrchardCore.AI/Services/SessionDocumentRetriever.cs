using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class SessionDocumentRetriever
{
    private const int ChunkSize = 1000;
    private const int MaxChunks = 6;

    public IReadOnlyList<string> Retrieve(CustomChatSessionDocuments SessionDocuments, string userPrompt)
    {
        if (SessionDocuments?.Items == null || SessionDocuments.Items.Count == 0 || string.IsNullOrWhiteSpace(userPrompt))
        {
            return [];
        }

        var chunks = new List<(string Text, int Score)>();

        foreach (var document in SessionDocuments.Items)
        {
            if (!File.Exists(document.TempFilePath))
            {
                continue;
            }

            var text = File.ReadAllText(document.TempFilePath);

            foreach (var chunk in Chunk(text))
            {
                var score = Score(chunk, userPrompt);

                if (score > 0)
                {
                    chunks.Add((chunk, score));
                }
            }
        }

        return chunks.OrderByDescending(x => x.Score).Take(MaxChunks).Select(x => x.Text).ToArray();
    }

    private static IEnumerable<string> Chunk(string text)
    {
        for (var i = 0; i < text.Length; i += ChunkSize)
        {
            //   lazy sequence yield
            //   Each call returns one value and pauses execution,
            //   preserving local state. Execution resumes on the next iteration.
            yield return text.Substring(i, Math.Min(ChunkSize, text.Length - i));
        }
    }

    private static int Score(string chunk, string prompt)
    {
        // Split prompt into terms on spaces, ignoring empties.
        // For each term, check if it appears anywhere in chunk, case -insensitive.
        // Increment once per matching term.
        // Score equals number of distinct prompt terms found.
        var score = 0;

        foreach (var term in prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (chunk.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
        }

        return score;
    }
}
