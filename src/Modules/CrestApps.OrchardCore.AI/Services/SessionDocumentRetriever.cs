using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class SessionDocumentRetriever
{
    private readonly int _chunkSize;
    private readonly int _maxChunks;

    public SessionDocumentRetriever() : this(chunkSize: 1000, maxChunks: 6)
    {
    }

    public SessionDocumentRetriever(int chunkSize, int maxChunks)
    {
        _chunkSize = chunkSize > 0 ? chunkSize : 1000;
        _maxChunks = maxChunks > 0 ? maxChunks : 6;
    }

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

        return chunks
            .OrderByDescending(x => x.Score)
            .Take(_maxChunks)
            .Select(x => x.Text)
            .ToArray();
    }

    private IEnumerable<string> Chunk(string text)
    {
        for (var i = 0; i < text.Length; i += _chunkSize)
        {
            yield return text.Substring(i, Math.Min(_chunkSize, text.Length - i));
        }
    }

    private static int Score(string chunk, string prompt)
    {
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
