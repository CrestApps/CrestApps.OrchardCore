using System.Text;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Puts the run-time API script at the start of an HTML page of a package, so <c>window.API</c> exists before the
/// page's own scripts run. The page's bytes are kept as they are: the insertion point is found by reading the bytes as
/// Latin-1, which maps every byte to one character, and only ASCII is inserted.
/// </summary>
public static class PackageHtmlInjector
{
    /// <summary>
    /// Inserts a script block after the opening <c>head</c> tag, else after the opening <c>html</c> tag, else at the
    /// start (after a doctype when there is one).
    /// </summary>
    /// <param name="html">The page.</param>
    /// <param name="scriptBlock">The ASCII markup to insert, such as one or more <c>script</c> elements.</param>
    /// <returns>The page with the script.</returns>
    public static byte[] Inject(byte[] html, string scriptBlock)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(scriptBlock);

        var preamble = html.Length >= 3 && html[0] == 0xEF && html[1] == 0xBB && html[2] == 0xBF ? 3 : 0;
        var text = Encoding.Latin1.GetString(html, preamble, html.Length - preamble);
        var at = AfterTag(text, "head");

        if (at < 0)
        {
            at = AfterTag(text, "html");
        }

        if (at < 0)
        {
            at = text.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) ? text.IndexOf('>', StringComparison.Ordinal) + 1 : 0;
        }

        var insert = Encoding.ASCII.GetBytes(scriptBlock);
        var offset = preamble + at;
        var result = new byte[html.Length + insert.Length];

        Buffer.BlockCopy(html, 0, result, 0, offset);
        Buffer.BlockCopy(insert, 0, result, offset, insert.Length);
        Buffer.BlockCopy(html, offset, result, offset + insert.Length, html.Length - offset);

        return result;
    }

    // The position after the opening tag of an element: "<head>" or "<head ...>", not "<header>".
    private static int AfterTag(string text, string name)
    {
        var start = 0;

        while ((start = text.IndexOf("<" + name, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var next = start + name.Length + 1;

            if (next < text.Length && (text[next] == '>' || char.IsWhiteSpace(text[next]) || text[next] == '/'))
            {
                var end = text.IndexOf('>', next);

                return end < 0 ? -1 : end + 1;
            }

            start = next;
        }

        return -1;
    }
}
