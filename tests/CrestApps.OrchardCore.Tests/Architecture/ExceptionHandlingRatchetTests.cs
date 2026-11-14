using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Two ways an exception gets lost or leaks (see CQ-6 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// <para>
/// A broad catch (<c>catch (Exception)</c> or a bare <c>catch</c>) that neither logs, rethrows, returns a result nor
/// uses the exception turns a failure into silence: nobody is told and the caller carries on. <see cref="EmptyCatchRatchetTests"/>
/// already refuses an empty block; this one also counts the blocks that only hold a comment or unrelated code.
/// </para>
/// <para>
/// An exception's message is written for a developer. Returned as <c>Failure(ex.Message)</c> or put into a localized
/// text as an argument, it shows a buyer or an API client a gateway's internals in English. Log the exception and show
/// a safe, localized message instead.
/// </para>
/// The files that did either when the ratchet was set are listed with their count, which may only shrink.
/// </summary>
public sealed partial class ExceptionHandlingRatchetTests
{
    private static readonly Lazy<(Dictionary<string, int> SilentCatches, Dictionary<string, int> ExceptionText)> _actual = new(ScanSources);

    [Fact]
    public void NoBroadCatch_IsAddedThatNeitherLogsRethrowsNorReturns()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value.SilentCatches,
            _knownSilentCatches,
            "These files catch every exception and then neither log it, rethrow it, return a result nor use it.",
            "Log the exception, rethrow it, return a failure, or catch only the exception type you expect.");
    }

    [Fact]
    public void EveryListedSilentCatch_StillExists()
    {
        Ratchet.AssertNothingStale(_actual.Value.SilentCatches, _knownSilentCatches, $"{nameof(ExceptionHandlingRatchetTests)}.{nameof(_knownSilentCatches)}");
    }

    [Fact]
    public void NoExceptionMessage_IsAddedToAFailureOrALocalizedText()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value.ExceptionText,
            _knownExceptionText,
            "These files return an exception's message as a failure, or put it into a localized text.",
            "Log the exception and return a safe, localized message; never show ex.Message to a browser or an API client.");
    }

    [Fact]
    public void EveryListedExceptionText_StillExists()
    {
        Ratchet.AssertNothingStale(_actual.Value.ExceptionText, _knownExceptionText, $"{nameof(ExceptionHandlingRatchetTests)}.{nameof(_knownExceptionText)}");
    }

    [Theory]
    [InlineData("catch (Exception)\n{\n    // Best effort.\n}", 1)]
    [InlineData("catch\n{\n    result = null;\n}", 1)]
    [InlineData("catch (Exception ex) when (ex is not OperationCanceledException)\n{\n    count++;\n}", 1)]
    [InlineData("catch (Exception ex)\n{\n    _logger.LogWarning(ex, \"Failed.\");\n}", 0)]
    [InlineData("catch (Exception)\n{\n    throw;\n}", 0)]
    [InlineData("catch (Exception)\n{\n    return false;\n}", 0)]
    [InlineData("catch (Exception ex)\n{\n    failures.Add(ex);\n}", 0)]
    [InlineData("catch (Exception)\n{\n    context.AddModelError(\"File\", S[\"The file could not be read.\"]);\n}", 0)]
    [InlineData("catch (JsonException)\n{\n    value = null;\n}", 0)]
    [InlineData("catch (Exception)\n{\n    if (x)\n    {\n        y = 1;\n    }\n}", 1)]
    public void CountSilentCatches_FindsBroadCatchesThatDoNothingWithTheException(string code, int expected)
    {
        // Act
        var actual = CountSilentCatches(CSharpSourceMask.CommentsAndLiterals(code));

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("return PaymentResult.Failure(ex.Message);", 1)]
    [InlineData("return Result.Failed(exception.Message);", 1)]
    [InlineData("await _notifier.ErrorAsync(H[\"The voice could not be saved: {0}\", ex.Message]);", 1)]
    [InlineData("ModelState.AddModelError(string.Empty, S[\"Import failed: {0}\", e.GetBaseException().Message]);", 1)]
    [InlineData("return BadRequest(exception.Message);", 1)]
    [InlineData("return TypedResults.BadRequest(ex.GetBaseException().Message);", 1)]
    [InlineData("return PaymentResult.Failure(S[\"The payment could not be taken.\"]);", 0)]
    [InlineData("_logger.LogError(ex, \"Failed: {Message}\", ex.Message);", 0)]
    [InlineData("// return Result.Failure(ex.Message);", 0)]
    public void CountExceptionText_FindsMessagesReturnedOrLocalized(string code, int expected)
    {
        // Act
        var actual = CountExceptionText(CSharpSourceMask.Comments(code));

        // Assert
        Assert.Equal(expected, actual);
    }

    private static (Dictionary<string, int>, Dictionary<string, int>) ScanSources()
    {
        var silentCatches = new Dictionary<string, int>(StringComparer.Ordinal);
        var exceptionText = new Dictionary<string, int>(StringComparer.Ordinal);
        var scanned = 0;

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            scanned++;

            var source = Ratchet.ReadAllText(file);

            if (source.Contains("catch", StringComparison.Ordinal))
            {
                var silent = CountSilentCatches(CSharpSourceMask.CommentsAndLiterals(source));

                if (silent > 0)
                {
                    silentCatches[file.RelativePath] = silent;
                }
            }

            if (source.Contains(".Message", StringComparison.Ordinal))
            {
                var text = CountExceptionText(CSharpSourceMask.Comments(source));

                if (text > 0)
                {
                    exceptionText[file.RelativePath] = text;
                }
            }
        }

        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");

        return (silentCatches, exceptionText);
    }

    private static int CountSilentCatches(string code)
    {
        var count = 0;

        foreach (Match match in BroadCatch().Matches(code))
        {
            var open = match.Index + match.Length - 1;
            var close = CSharpSourceMask.FindClosing(code, open);
            var body = code[(open + 1)..close];
            var variable = match.Groups["variable"].Value;

            if (Handled().IsMatch(body) ||
                (variable.Length > 0 && Regex.IsMatch(body, $@"\b{variable}\b")))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static int CountExceptionText(string code)
        => FailureWithMessage().Count(code) + LocalizedWithMessage().Count(code) + ResponseWithMessage().Count(code);

    // A catch of Exception (or of everything), with an optional filter, up to the opening brace of its block.
    [GeneratedRegex(@"\bcatch\b(?:\s*\(\s*(?:System\.)?Exception(?:\s+(?<variable>\w+))?\s*\))?(?:\s*when\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\))?\s*\{")]
    private static partial Regex BroadCatch();

    // Logging, rethrowing, returning, or reporting through an error or failure call.
    [GeneratedRegex(@"\bthrow\b|\breturn\b|\byield\s+break\b|\bLog\w*\s*\(|\b\w*(?:Error|Fail)\w*\s*\(")]
    private static partial Regex Handled();

    [GeneratedRegex(@"\b\w*Fail\w*\s*\(\s*(?:ex|e|exc|exception|\w+Exception)\s*\.\s*(?:GetBaseException\s*\(\s*\)\s*\.\s*)?Message\b")]
    private static partial Regex FailureWithMessage();

    // An HTTP response that hands the exception's own text to the caller.
    [GeneratedRegex(@"\b(?:(?:BadRequest|Conflict|NotFound|UnprocessableEntity|Ok|Content|Json)\s*\(|StatusCode\s*\(\s*\d+\s*,)\s*(?:ex|e|exc|exception|\w+Exception)\s*\.\s*(?:GetBaseException\s*\(\s*\)\s*\.\s*)?Message\b")]
    private static partial Regex ResponseWithMessage();

    [GeneratedRegex(@"\b[STH]\s*\[\s*""(?:[^""\\]|\\.)*""\s*,[^\];]*?\b(?:ex|e|exc|exception|\w+Exception)\s*\.\s*(?:GetBaseException\s*\(\s*\)\s*\.\s*)?Message\b")]
    private static partial Regex LocalizedWithMessage();
}
