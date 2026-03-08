using System.Buffers;
using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Services;

namespace CrestApps.AI.Prompting;

/// <summary>
/// A high-performance builder for composing system prompts from a mix of
/// <see cref="AITemplate"/> instances, template IDs, and raw strings.
/// Uses pooled buffers to minimize allocations during prompt assembly.
/// </summary>
public sealed class AITemplateBuilder
{
    private readonly List<Segment> _segments = [];
    private string _separator = Environment.NewLine + Environment.NewLine;

    /// <summary>
    /// Sets the separator used between segments when building the final string.
    /// Defaults to a double newline.
    /// </summary>
    public AITemplateBuilder WithSeparator(string separator)
    {
        _separator = separator ?? string.Empty;

        return this;
    }

    /// <summary>
    /// Appends a raw string segment.
    /// </summary>
    public AITemplateBuilder Append(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _segments.Add(new Segment(text));
        }

        return this;
    }

    /// <summary>
    /// Appends the body of an <see cref="AITemplate"/>.
    /// </summary>
    public AITemplateBuilder Append(AITemplate template)
    {
        if (template is not null && !string.IsNullOrEmpty(template.Content))
        {
            _segments.Add(new Segment(template.Content));
        }

        return this;
    }

    /// <summary>
    /// Appends a template by ID. The template will be resolved and rendered
    /// when <see cref="BuildAsync"/> is called.
    /// </summary>
    public AITemplateBuilder AppendTemplate(string templateId, IDictionary<string, object> arguments = null)
    {
        if (!string.IsNullOrEmpty(templateId))
        {
            _segments.Add(new Segment(templateId, arguments, isTemplateId: true));
        }

        return this;
    }

    /// <summary>
    /// Builds the final composed string by resolving any template IDs
    /// through the provided <paramref name="templateService"/> and joining
    /// all segments with the configured separator.
    /// </summary>
    public async Task<string> BuildAsync(IAITemplateService templateService)
    {
        ArgumentNullException.ThrowIfNull(templateService);

        if (_segments.Count == 0)
        {
            return string.Empty;
        }

        // Resolve template IDs to rendered strings.
        var resolved = new string[_segments.Count];

        for (var i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];

            if (segment.IsTemplateId)
            {
                resolved[i] = await templateService.RenderAsync(segment.Text, segment.Arguments);
            }
            else
            {
                resolved[i] = segment.Text;
            }
        }

        // Calculate total length for the final string.
        var totalLength = 0;
        var nonEmptyCount = 0;

        for (var i = 0; i < resolved.Length; i++)
        {
            if (!string.IsNullOrEmpty(resolved[i]))
            {
                if (nonEmptyCount > 0)
                {
                    totalLength += _separator.Length;
                }

                totalLength += resolved[i].Length;
                nonEmptyCount++;
            }
        }

        if (nonEmptyCount == 0)
        {
            return string.Empty;
        }

        // Single segment: return directly without allocation.
        if (nonEmptyCount == 1)
        {
            for (var i = 0; i < resolved.Length; i++)
            {
                if (!string.IsNullOrEmpty(resolved[i]))
                {
                    return resolved[i];
                }
            }
        }

        // Build the final string using a rented buffer.
        var buffer = ArrayPool<char>.Shared.Rent(totalLength);

        try
        {
            var position = 0;
            var written = false;

            for (var i = 0; i < resolved.Length; i++)
            {
                if (string.IsNullOrEmpty(resolved[i]))
                {
                    continue;
                }

                if (written)
                {
                    _separator.AsSpan().CopyTo(buffer.AsSpan(position));
                    position += _separator.Length;
                }

                resolved[i].AsSpan().CopyTo(buffer.AsSpan(position));
                position += resolved[i].Length;
                written = true;
            }

            return new string(buffer, 0, position);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Builds the final composed string from only raw string and
    /// <see cref="AITemplate"/> segments. Does not require a template service.
    /// Throws <see cref="InvalidOperationException"/> if any segment requires
    /// template resolution.
    /// </summary>
    public string Build()
    {
        if (_segments.Count == 0)
        {
            return string.Empty;
        }

        // Verify no template IDs are present.
        for (var i = 0; i < _segments.Count; i++)
        {
            if (_segments[i].IsTemplateId)
            {
                throw new InvalidOperationException(
                    $"Segment at index {i} references template ID '{_segments[i].Text}' which requires an IAITemplateService. Use BuildAsync(IAITemplateService) instead.");
            }
        }

        // Calculate total length.
        var totalLength = 0;
        var nonEmptyCount = 0;

        for (var i = 0; i < _segments.Count; i++)
        {
            if (!string.IsNullOrEmpty(_segments[i].Text))
            {
                if (nonEmptyCount > 0)
                {
                    totalLength += _separator.Length;
                }

                totalLength += _segments[i].Text.Length;
                nonEmptyCount++;
            }
        }

        if (nonEmptyCount == 0)
        {
            return string.Empty;
        }

        if (nonEmptyCount == 1)
        {
            for (var i = 0; i < _segments.Count; i++)
            {
                if (!string.IsNullOrEmpty(_segments[i].Text))
                {
                    return _segments[i].Text;
                }
            }
        }

        return string.Create(totalLength, (Segments: _segments, Separator: _separator), static (span, state) =>
        {
            var position = 0;
            var written = false;

            for (var i = 0; i < state.Segments.Count; i++)
            {
                var text = state.Segments[i].Text;

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (written)
                {
                    state.Separator.AsSpan().CopyTo(span[position..]);
                    position += state.Separator.Length;
                }

                text.AsSpan().CopyTo(span[position..]);
                position += text.Length;
                written = true;
            }
        });
    }

    private readonly struct Segment
    {
        public Segment(string text)
        {
            Text = text;
            Arguments = null;
            IsTemplateId = false;
        }

        public Segment(string text, IDictionary<string, object> arguments, bool isTemplateId)
        {
            Text = text;
            Arguments = arguments;
            IsTemplateId = isTemplateId;
        }

        public string Text { get; }

        public IDictionary<string, object> Arguments { get; }

        public bool IsTemplateId { get; }
    }
}
