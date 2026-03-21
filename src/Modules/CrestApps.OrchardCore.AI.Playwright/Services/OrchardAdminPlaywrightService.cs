using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public sealed class OrchardAdminPlaywrightService : IOrchardAdminPlaywrightService
{
    private const int EditorSubmitTimeoutMs = 60_000;
    private static readonly Regex RowScopedActionPattern = new(
        @"^(?<action>Edit|View|Preview|Display|Delete|Clone|Duplicate|Publish|Unpublish|Draft|Save Draft)\s+(?<title>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] ContentItemContainerSelectors =
    [
        "tbody tr",
        "table tr",
        "tr",
        "[data-content-item-id]",
        ".content-item",
        ".list-group-item",
        "article",
        "li",
    ];
    private static readonly string[] RowActionMenuNames =
    [
        "Actions",
        "More actions",
        "More",
        "Options",
    ];

    private readonly IPlaywrightSessionManager _sessionManager;
    private readonly IPlaywrightObservationService _observationService;
    private readonly IPlaywrightActionVisualizer _actionVisualizer;
    private readonly ILogger<OrchardAdminPlaywrightService> _logger;

    public OrchardAdminPlaywrightService(
        IPlaywrightSessionManager sessionManager,
        IPlaywrightObservationService observationService,
        IPlaywrightActionVisualizer actionVisualizer,
        ILogger<OrchardAdminPlaywrightService> logger)
    {
        _sessionManager = sessionManager;
        _observationService = observationService;
        _actionVisualizer = actionVisualizer;
        _logger = logger;
    }

    public Task<PlaywrightObservation> CaptureStateAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
        => _observationService.CaptureAsync(session, cancellationToken);

    public async Task<PlaywrightObservation> OpenAdminHomeAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
    {
        await _sessionManager.EnsureAdminReadyAsync(session, cancellationToken);
        return await _observationService.CaptureAsync(session, cancellationToken);
    }

    public async Task<PlaywrightObservation> OpenContentItemsAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
    {
        await _sessionManager.EnsureAdminReadyAsync(session, cancellationToken);
        var page = await GetActivePageAsync(session, cancellationToken);

        var targetUrl = PlaywrightSessionRequestResolver.CombineUrl(session.AdminBaseUrl, "Contents/ContentItems");
        await page.GotoAsync(targetUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30_000,
        }).WaitAsync(cancellationToken);

        var observation = await _observationService.CaptureAsync(session, cancellationToken);
        if (observation.IsLoginPage || !observation.IsAuthenticated)
        {
            return observation;
        }

        if (!observation.IsLoginPage && !IsContentItemsPage(observation))
        {
            await TryClickNamedElementAsync(page, "Content Items", cancellationToken);
            observation = await _observationService.CaptureAsync(session, cancellationToken);
        }

        return observation;
    }

    public async Task<PlaywrightContentListResult> ListVisibleContentItemsAsync(
        IPlaywrightSession session,
        int maxItems = 20,
        CancellationToken cancellationToken = default)
    {
        var observation = await EnsureContentItemsPageAsync(session, cancellationToken);
        var page = await GetActivePageAsync(session, cancellationToken);

        var items = observation.IsLoginPage || !observation.IsAuthenticated
            ? Array.Empty<PlaywrightContentListItem>()
            : await GetVisibleContentItemsAsync(page, maxItems, cancellationToken);

        return new PlaywrightContentListResult
        {
            Url = page.Url,
            Title = await page.TitleAsync().WaitAsync(cancellationToken),
            MainHeading = observation.MainHeading,
            ItemCount = items.Count,
            Items = items,
        };
    }

    public async Task<PlaywrightObservation> OpenNewContentItemAsync(IPlaywrightSession session, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var observation = await OpenContentItemsAsync(session, cancellationToken);
        if (observation.IsLoginPage || !observation.IsAuthenticated)
        {
            return observation;
        }

        var page = await GetActivePageAsync(session, cancellationToken);
        await TryClickAnyAsync(page, [("button", "New"), ("link", "New"), ("button", "Create"), ("link", "Create")], cancellationToken);

        var contentTypeLocator = await ResolveContentTypeLocatorAsync(page, contentType, cancellationToken);
        await ClickWithoutNavigationWaitAsync(page, contentTypeLocator, $"selecting {contentType}", cancellationToken);
        await WaitForEditorAsync(page, cancellationToken);

        return await _observationService.CaptureAsync(session, cancellationToken);
    }

    public async Task<PlaywrightContentItemOpenResult> OpenContentItemEditorAsync(
        IPlaywrightSession session,
        string title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var observation = await EnsureContentItemsPageAsync(session, cancellationToken);
        var page = await GetActivePageAsync(session, cancellationToken);

        if (observation.IsLoginPage || !observation.IsAuthenticated)
        {
            return new PlaywrightContentItemOpenResult
            {
                RequestedTitle = title,
                Observation = observation,
            };
        }

        var visibleItems = await GetVisibleContentItemsAsync(page, 25, cancellationToken);
        var selection = SelectBestContentItem(visibleItems, title);
        var usedSearch = false;

        if (selection is null && await TrySearchContentItemsAsync(page, title, cancellationToken))
        {
            usedSearch = true;
            visibleItems = await GetVisibleContentItemsAsync(page, 25, cancellationToken);
            selection = SelectBestContentItem(visibleItems, title);
        }

        if (selection is null)
        {
            return new PlaywrightContentItemOpenResult
            {
                RequestedTitle = title,
                UsedSearch = usedSearch,
                ClosestTitles = GetClosestTitles(visibleItems, title),
                Observation = await _observationService.CaptureAsync(session, cancellationToken),
            };
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Opening content item editor for requested title {RequestedTitle} using match {MatchedTitle} ({MatchMode}).",
                title,
                selection.Item.Title,
                selection.MatchMode);
        }

        if (!await TryClickContentItemRowActionAsync(page, "link", $"Edit {selection.Item.Title}", cancellationToken))
        {
            return new PlaywrightContentItemOpenResult
            {
                RequestedTitle = title,
                MatchedTitle = selection.Item.Title,
                MatchMode = selection.MatchMode,
                UsedSearch = usedSearch,
                ClosestTitles = GetClosestTitles(visibleItems, title),
                Observation = await _observationService.CaptureAsync(session, cancellationToken),
            };
        }

        await WaitForEditorAsync(page, cancellationToken);
        observation = await _observationService.CaptureAsync(session, cancellationToken);

        return new PlaywrightContentItemOpenResult
        {
            RequestedTitle = title,
            MatchedTitle = selection.Item.Title,
            MatchMode = selection.MatchMode,
            UsedSearch = usedSearch,
            ClosestTitles = GetClosestTitles(visibleItems, title),
            Observation = observation,
        };
    }

    public async Task<PlaywrightObservation> SetContentTitleAsync(IPlaywrightSession session, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var page = await GetActivePageAsync(session, cancellationToken);
        var titleField = page.GetByLabel("Title", new() { Exact = true }).First;

        if (await titleField.CountAsync().WaitAsync(cancellationToken) == 0)
        {
            titleField = page.Locator("input[name='TitlePart.Title'], input[id*='TitlePart_Title'], input[name='Title']").First;
        }

        await ShowFillIndicatorAsync(page, titleField, "Title", cancellationToken);
        await titleField.FillAsync(title, new LocatorFillOptions { Timeout = 10_000 }).WaitAsync(cancellationToken);
        await titleField.PressAsync("Tab").WaitAsync(cancellationToken);

        return await _observationService.CaptureAsync(session, cancellationToken);
    }

    public Task<PlaywrightObservation> SaveDraftAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
        => ClickEditorActionAsync(session, ["Save Draft", "Save"], cancellationToken);

    public Task<PlaywrightObservation> PublishContentAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
        => ClickEditorActionAsync(session, ["Publish", "Publish Draft"], cancellationToken);

    public async Task<PlaywrightEditorTargetResult> OpenEditorTabAsync(
        IPlaywrightSession session,
        string tabName,
        bool exact = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabName);

        var page = await GetActivePageAsync(session, cancellationToken);
        var target = await ResolveEditorTargetAsync(page, tabName, exact, cancellationToken);
        if (target is null)
        {
            throw new TimeoutException($"Unable to locate an Orchard editor tab or section named '{tabName}'.");
        }

        if (!await IsEditorTargetExpandedAsync(target.Locator, cancellationToken))
        {
            await ClickWithoutNavigationWaitAsync(page, target.Locator, $"opening {target.MatchedText}", cancellationToken);
        }

        var observation = await CaptureAfterInteractionAsync(session, page, cancellationToken);

        return new PlaywrightEditorTargetResult
        {
            RequestedTarget = tabName,
            MatchedTarget = target.MatchedText,
            TargetKind = target.TargetKind,
            Observation = observation,
        };
    }

    public async Task<PlaywrightFieldEditResult> SetFieldValueAsync(
        IPlaywrightSession session,
        string label,
        string value,
        string fieldType = "auto",
        bool exact = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var normalizedFieldType = NormalizeFieldType(fieldType);
        var page = await GetActivePageAsync(session, cancellationToken);
        var locator = await ResolveFieldLocatorAsync(page, label, exact, cancellationToken);
        await ShowFillIndicatorAsync(page, locator, label, cancellationToken);

        var resolvedFieldType = await TrySetFieldValueAsync(locator, label, value ?? string.Empty, normalizedFieldType, append: false, cancellationToken);
        if (string.IsNullOrWhiteSpace(resolvedFieldType))
        {
            throw new TimeoutException($"Unable to set the Orchard field '{label}' as '{normalizedFieldType}'.");
        }

        return new PlaywrightFieldEditResult
        {
            Label = label,
            RequestedFieldType = normalizedFieldType,
            RequestedEditMode = "replace",
            ResolvedFieldType = resolvedFieldType,
            Observation = await _observationService.CaptureAsync(session, cancellationToken),
        };
    }

    public async Task<PlaywrightFieldEditResult> SetBodyFieldAsync(
        IPlaywrightSession session,
        string label,
        string value,
        string mode = "append",
        bool exact = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var normalizedMode = NormalizeBodyEditMode(mode);
        var page = await GetActivePageAsync(session, cancellationToken);
        var locator = await ResolveFieldLocatorAsync(page, label, exact, cancellationToken);
        await ShowFillIndicatorAsync(page, locator, label, cancellationToken);

        var resolvedFieldType = await TrySetFieldValueAsync(
            locator,
            label,
            value ?? string.Empty,
            requestedFieldType: "richtext",
            append: normalizedMode.Equals("append", StringComparison.Ordinal),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(resolvedFieldType))
        {
            throw new TimeoutException($"Unable to edit the Orchard body field '{label}'.");
        }

        return new PlaywrightFieldEditResult
        {
            Label = label,
            RequestedFieldType = "body",
            RequestedEditMode = normalizedMode,
            ResolvedFieldType = resolvedFieldType,
            Observation = await _observationService.CaptureAsync(session, cancellationToken),
        };
    }

    public async Task<PlaywrightPublishVerificationResult> PublishAndVerifyAsync(
        IPlaywrightSession session,
        string expectedStatus = "Published",
        CancellationToken cancellationToken = default)
    {
        var observation = await PublishContentAsync(session, cancellationToken);
        var verificationSignals = BuildPublishVerificationSignals(observation, expectedStatus);

        return new PlaywrightPublishVerificationResult
        {
            Action = "publish",
            ExpectedStatus = string.IsNullOrWhiteSpace(expectedStatus) ? "Published" : expectedStatus.Trim(),
            Verified = verificationSignals.Count > 0,
            VerificationSignals = verificationSignals,
            Observation = observation,
        };
    }

    private async Task<PlaywrightObservation> ClickEditorActionAsync(
        IPlaywrightSession session,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken)
    {
        var observation = await _observationService.CaptureAsync(session, cancellationToken);
        if (observation.IsLoginPage || !observation.IsAuthenticated)
        {
            return observation;
        }

        var page = await GetActivePageAsync(session, cancellationToken);
        try
        {
            await TryClickEditorActionAsync(page, candidateNames, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            var stalledObservation = await CaptureAfterEditorActionFailureAsync(session, page, cancellationToken);
            throw new TimeoutException(BuildEditorActionFailureMessage(candidateNames, stalledObservation), ex);
        }

        return await CaptureAfterEditorActionAsync(session, page, cancellationToken);
    }

    private async Task<bool> TryClickContentItemRowActionAsync(
        IPage page,
        string role,
        string name,
        CancellationToken cancellationToken)
    {
        if (!role.Equals("link", StringComparison.OrdinalIgnoreCase)
            && !role.Equals("button", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var match = RowScopedActionPattern.Match(name);
        if (!match.Success)
        {
            return false;
        }

        var actionName = match.Groups["action"].Value.Trim();
        var contentItemTitle = match.Groups["title"].Value.Trim();
        var contentRow = await FindContentItemContainerAsync(page, contentItemTitle, cancellationToken);

        if (contentRow is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Could not find a content row for {ContentItemTitle} while resolving {ActionName}.", contentItemTitle, actionName);
            }
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Trying row-scoped action {ActionName} for content item {ContentItemTitle}.", actionName, contentItemTitle);
        }

        if (await TryClickActionWithinContainerAsync(page, contentRow, actionName, contentItemTitle, cancellationToken))
        {
            return true;
        }

        foreach (var menuName in RowActionMenuNames)
        {
            if (!await TryClickNamedElementAsync(page, contentRow, menuName, false, cancellationToken))
            {
                continue;
            }

            if (await TryClickAnyAsync(page, [("button", actionName), ("link", actionName)], false, false, cancellationToken))
            {
                return true;
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Found the content row for {ContentItemTitle} but no visible {ActionName} action inside it.",
                contentItemTitle,
                actionName);
        }

        return false;
    }

    private async Task<PlaywrightObservation> EnsureContentItemsPageAsync(
        IPlaywrightSession session,
        CancellationToken cancellationToken)
    {
        var observation = await _observationService.CaptureAsync(session, cancellationToken);
        if (observation.IsLoginPage || !observation.IsAuthenticated)
        {
            return observation;
        }

        if (IsContentItemsPage(observation))
        {
            return observation;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Current page is not the Orchard content items list. Opening it for the active Playwright session.");
        }

        return await OpenContentItemsAsync(session, cancellationToken);
    }

    private static async Task<IPage> GetActivePageAsync(IPlaywrightSession session, CancellationToken cancellationToken)
    {
        return session switch
        {
            PlaywrightSession concreteSession => await concreteSession.GetOrCreatePageAsync(cancellationToken),
            _ => session.Page,
        };
    }

    private static bool IsContentItemsPage(PlaywrightObservation observation)
    {
        if (observation is null)
        {
            return false;
        }

        return observation.CurrentUrl?.Contains("/Contents/ContentItems", StringComparison.OrdinalIgnoreCase) == true
            || observation.MainHeading?.Contains("Content", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ContentItemSelection SelectBestContentItem(
        IReadOnlyList<PlaywrightContentListItem> items,
        string requestedTitle)
    {
        ContentItemSelection best = null;

        foreach (var item in items)
        {
            var match = GetTitleMatch(item.Title, requestedTitle);
            if (match.Score <= 0)
            {
                continue;
            }

            if (best is null || match.Score > best.Score)
            {
                best = new ContentItemSelection
                {
                    Item = item,
                    MatchMode = match.Mode,
                    Score = match.Score,
                };
            }
        }

        return best is { Score: >= 550 } ? best : null;
    }

    private static IReadOnlyList<string> GetClosestTitles(
        IReadOnlyList<PlaywrightContentListItem> items,
        string requestedTitle)
    {
        return items
            .Select(item => new
            {
                item.Title,
                Match = GetTitleMatch(item.Title, requestedTitle),
            })
            .Where(entry => entry.Match.Score > 0)
            .OrderByDescending(entry => entry.Match.Score)
            .Select(entry => entry.Title)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static ILocator GetLocatorByRole(IPage page, string role, string name, bool exact)
    {
        return role.ToLowerInvariant() switch
        {
            "button" => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = exact }).First,
            "link" => page.GetByRole(AriaRole.Link, new() { Name = name, Exact = exact }).First,
            "textbox" => page.GetByRole(AriaRole.Textbox, new() { Name = name, Exact = exact }).First,
            "menuitem" => page.GetByRole(AriaRole.Menuitem, new() { Name = name, Exact = exact }).First,
            "tab" => page.GetByRole(AriaRole.Tab, new() { Name = name, Exact = exact }).First,
            _ => throw new InvalidOperationException($"Unsupported role '{role}'."),
        };
    }

    private static async Task<ILocator> ResolveFieldLocatorAsync(
        IPage page,
        string label,
        bool exact,
        CancellationToken cancellationToken)
    {
        var labeledLocator = page.GetByLabel(label, new() { Exact = exact }).First;
        if (await labeledLocator.CountAsync().WaitAsync(cancellationToken) > 0)
        {
            return labeledLocator;
        }

        var selectorValue = Regex.Replace(label, @"\s+", string.Empty);
        var fallbackSelectors = new[]
        {
            $"textarea[id*='{selectorValue}' i], textarea[name*='{selectorValue}' i]",
            $"input[id*='{selectorValue}' i], input[name*='{selectorValue}' i]",
            $"select[id*='{selectorValue}' i], select[name*='{selectorValue}' i]",
            $"[contenteditable='true'][aria-label*='{label}' i], iframe[title*='{label}' i]",
        };

        foreach (var selector in fallbackSelectors)
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync().WaitAsync(cancellationToken) > 0)
            {
                return locator;
            }
        }

        throw new TimeoutException($"Unable to locate a field labeled '{label}'.");
    }

    private static async Task<ILocator> ResolveContentTypeLocatorAsync(
        IPage page,
        string contentType,
        CancellationToken cancellationToken)
    {
        var exactCandidates = new ILocator[]
        {
            page.GetByRole(AriaRole.Link, new() { Name = contentType, Exact = true }).First,
            page.GetByRole(AriaRole.Button, new() { Name = contentType, Exact = true }).First,
            page.GetByText(contentType, new PageGetByTextOptions { Exact = true }).First,
        };

        foreach (var candidate in exactCandidates)
        {
            if (await candidate.CountAsync().WaitAsync(cancellationToken) > 0)
            {
                return candidate;
            }
        }

        var relaxedCandidates = new ILocator[]
        {
            page.GetByRole(AriaRole.Link, new() { Name = contentType, Exact = false }).First,
            page.GetByRole(AriaRole.Button, new() { Name = contentType, Exact = false }).First,
            page.GetByText(contentType, new PageGetByTextOptions { Exact = false }).First,
        };

        foreach (var candidate in relaxedCandidates)
        {
            if (await candidate.CountAsync().WaitAsync(cancellationToken) > 0)
            {
                return candidate;
            }
        }

        return page.GetByText(contentType, new PageGetByTextOptions { Exact = false }).First;
    }

    private static string NormalizeFieldType(string fieldType)
        => (fieldType ?? "auto").Trim().ToLowerInvariant() switch
        {
            "" or "auto" => "auto",
            "text" => "text",
            "textarea" => "textarea",
            "select" or "dropdown" => "select",
            "checkbox" or "bool" or "boolean" => "checkbox",
            "radio" => "radio",
            "richtext" or "html" or "wysiwyg" => "richtext",
            var unsupported => throw new InvalidOperationException($"Unsupported Orchard field type '{unsupported}'."),
        };

    private static string NormalizeBodyEditMode(string mode)
        => (mode ?? "append").Trim().ToLowerInvariant() switch
        {
            "" or "append" => "append",
            "replace" => "replace",
            var unsupported => throw new InvalidOperationException($"Unsupported body edit mode '{unsupported}'. Use 'append' or 'replace'."),
        };

    private static IReadOnlyList<string> BuildPublishVerificationSignals(PlaywrightObservation observation, string expectedStatus)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(expectedStatus)
            ? "published"
            : expectedStatus.Trim().ToLowerInvariant();

        var signals = new List<string>();
        if (observation is null)
        {
            return signals;
        }

        if (!string.IsNullOrWhiteSpace(observation.ToastMessage)
            && observation.ToastMessage.Contains(normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            signals.Add($"Toast confirmed {expectedStatus}.");
        }

        if (observation.VisibleButtons.Any(button => button.Contains("Unpublish", StringComparison.OrdinalIgnoreCase)))
        {
            signals.Add("Editor actions now include Unpublish.");
        }

        if (observation.VisibleButtons.Any(button => button.Contains("View", StringComparison.OrdinalIgnoreCase)
            || button.Contains("Preview", StringComparison.OrdinalIgnoreCase)))
        {
            signals.Add("Editor actions now include view or preview.");
        }

        if (observation.ValidationMessages.Count > 0)
        {
            return [];
        }

        return signals
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<EditorTargetMatch> ResolveEditorTargetAsync(
        IPage page,
        string tabName,
        bool exact,
        CancellationToken cancellationToken)
    {
        foreach (var currentExact in exact ? new[] { true } : new[] { true, false })
        {
            foreach (var (kind, locator) in new[]
            {
                ("tab", page.GetByRole(AriaRole.Tab, new() { Name = tabName, Exact = currentExact }).First),
                ("tab", page.Locator($".nav-tabs a:has-text(\"{EscapeHasTextSelector(tabName)}\"), .nav-tabs button:has-text(\"{EscapeHasTextSelector(tabName)}\"), .nav-pills a:has-text(\"{EscapeHasTextSelector(tabName)}\"), .nav-pills button:has-text(\"{EscapeHasTextSelector(tabName)}\"), [data-bs-toggle='tab']:has-text(\"{EscapeHasTextSelector(tabName)}\")").First),
                ("accordion", page.Locator($".accordion-button:has-text(\"{EscapeHasTextSelector(tabName)}\"), [data-bs-toggle='collapse']:has-text(\"{EscapeHasTextSelector(tabName)}\")").First),
                ("summary", page.Locator($"details > summary:has-text(\"{EscapeHasTextSelector(tabName)}\"), summary:has-text(\"{EscapeHasTextSelector(tabName)}\")").First),
                ("section", page.Locator($".card-header button:has-text(\"{EscapeHasTextSelector(tabName)}\"), .card-header a:has-text(\"{EscapeHasTextSelector(tabName)}\"), fieldset > legend:has-text(\"{EscapeHasTextSelector(tabName)}\")").First),
            })
            {
                if (await locator.CountAsync().WaitAsync(cancellationToken) == 0
                    || !await locator.IsVisibleAsync().WaitAsync(cancellationToken))
                {
                    continue;
                }

                var matchedText = NormalizeTitle(await locator.InnerTextAsync().WaitAsync(cancellationToken))
                    ?? tabName;

                if (currentExact
                    && !matchedText.Equals(tabName, StringComparison.OrdinalIgnoreCase)
                    && !matchedText.Contains(tabName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new EditorTargetMatch
                {
                    Locator = locator,
                    MatchedText = matchedText,
                    TargetKind = kind,
                };
            }
        }

        return null;
    }

    private static async Task<bool> IsEditorTargetExpandedAsync(ILocator locator, CancellationToken cancellationToken)
    {
        if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
        {
            return false;
        }

        return await locator.EvaluateAsync<bool>(
            """
            element => {
              const ariaSelected = (element.getAttribute("aria-selected") || "").toLowerCase();
              const ariaExpanded = (element.getAttribute("aria-expanded") || "").toLowerCase();
              const className = (element.className || "").toString().toLowerCase();

              if (ariaSelected === "true" || ariaExpanded === "true") {
                return true;
              }

              if (className.includes("active") || className.includes("show")) {
                return true;
              }

              const details = element.closest("details");
              if (details?.open) {
                return true;
              }

              const controls = element.getAttribute("aria-controls");
              if (controls) {
                const target = document.getElementById(controls);
                if (target) {
                  const targetClass = (target.className || "").toString().toLowerCase();
                  const style = window.getComputedStyle(target);
                  if (targetClass.includes("show") || style.display !== "none") {
                    return true;
                  }
                }
              }

              return false;
            }
            """).WaitAsync(cancellationToken);
    }

    private static string EscapeHasTextSelector(string value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private async Task<bool> TryClickNamedElementAsync(
        IPage page,
        ILocator scope,
        string name,
        bool throwOnFailure,
        CancellationToken cancellationToken)
    {
        foreach (var locator in new[]
        {
            scope.GetByRole(AriaRole.Link, new() { Name = name, Exact = true }).First,
            scope.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).First,
            scope.GetByRole(AriaRole.Link, new() { Name = name, Exact = false }).First,
            scope.GetByRole(AriaRole.Button, new() { Name = name, Exact = false }).First,
            scope.GetByText(name, new LocatorGetByTextOptions { Exact = true }).First,
            scope.GetByText(name, new LocatorGetByTextOptions { Exact = false }).First,
        })
        {
            if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
            {
                continue;
            }

            if (!await locator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                continue;
            }

            await ClickWithoutNavigationWaitAsync(page, locator, $"clicking {name}", cancellationToken);

            return true;
        }

        if (throwOnFailure)
        {
            throw new TimeoutException($"Unable to click '{name}'.");
        }

        return false;
    }

    private async Task TryClickNamedElementAsync(IPage page, string name, CancellationToken cancellationToken)
    {
        await TryClickAnyAsync(page, [("link", name), ("button", name)], cancellationToken);
    }

    private async Task<bool> TrySearchContentItemsAsync(
        IPage page,
        string searchText,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in new[]
        {
            page.GetByLabel("Search", new() { Exact = false }).First,
            page.GetByPlaceholder("Search", new() { Exact = false }).First,
            page.Locator("input[type='search'], input[name*='search' i], input[name='q'], input[placeholder*='search' i]").First,
        })
        {
            if (await candidate.CountAsync().WaitAsync(cancellationToken) == 0
                || !await candidate.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                continue;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Searching Orchard content items for {SearchText}.", searchText);
            }

            await candidate.FillAsync(searchText, new LocatorFillOptions
            {
                Timeout = 10_000,
            }).WaitAsync(cancellationToken);

            if (!await TryClickAnyAsync(page, [("button", "Search"), ("button", "Filter"), ("link", "Search")], false, false, cancellationToken))
            {
                await candidate.PressAsync("Enter").WaitAsync(cancellationToken);
            }

            await CaptureAfterInteractionAsyncFromPageAsync(page, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task TryClickAnyAsync(
        IPage page,
        IReadOnlyList<(string role, string name)> candidates,
        CancellationToken cancellationToken)
    {
        if (page.IsClosed)
        {
            throw new InvalidOperationException("The Playwright browser page is no longer available.");
        }

        foreach (var candidate in candidates)
        {
            var locator = GetLocatorByRole(page, candidate.role, candidate.name, false);

            if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
            {
                continue;
            }

            if (await locator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                await ClickWithoutNavigationWaitAsync(page, locator, $"clicking {candidate.name}", cancellationToken);
                return;
            }
        }

        var fallback = candidates[0];
        var textLocator = page.GetByText(fallback.name, new PageGetByTextOptions { Exact = true }).First;
        await ClickWithoutNavigationWaitAsync(page, textLocator, $"clicking {fallback.name}", cancellationToken);
    }

    private async Task TryClickEditorActionAsync(
        IPage page,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken)
    {
        if (page.IsClosed)
        {
            throw new InvalidOperationException("The Playwright browser page is no longer available.");
        }

        foreach (var name in candidateNames)
        {
            if (await TryClickAnyAsync(page, [("button", name), ("link", name)], false, true, cancellationToken))
            {
                return;
            }
        }

        foreach (var name in candidateNames)
        {
            var submitLocator = page.Locator(
                $"button[name*='{name}' i], input[type='submit'][value*='{name}' i], button[title*='{name}' i], [aria-label*='{name}' i]").First;

            if (await submitLocator.CountAsync().WaitAsync(cancellationToken) > 0
                && await submitLocator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                await ClickWithoutNavigationWaitAsync(page, submitLocator, $"clicking {name}", cancellationToken);
                return;
            }
        }

        var visibleButtons = await page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('button, input[type=""submit""], a.btn'))
                .filter(element => element.offsetParent !== null)
                .map(element => (element.innerText || element.value || element.getAttribute('aria-label') || '').trim())
                .filter(Boolean)
                .slice(0, 12)").WaitAsync(cancellationToken);

        throw new TimeoutException(
            $"No matching editor action was found. Tried: {string.Join(", ", candidateNames)}. Visible actions: {string.Join(", ", visibleButtons ?? [])}");
    }

    private async Task<bool> TryClickAnyAsync(
        IPage page,
        IReadOnlyList<(string role, string name)> candidates,
        bool throwOnFailure,
        bool treatAsSubmit,
        CancellationToken cancellationToken)
    {
        if (page.IsClosed)
        {
            throw new InvalidOperationException("The Playwright browser page is no longer available.");
        }

        foreach (var candidate in candidates)
        {
            var locator = GetLocatorByRole(page, candidate.role, candidate.name, false);

            if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
            {
                continue;
            }

            if (await locator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                await ClickWithoutNavigationWaitAsync(
                    page,
                    locator,
                    treatAsSubmit ? $"submitting {candidate.name}" : $"clicking {candidate.name}",
                    cancellationToken);
                return true;
            }
        }

        var fallback = candidates[0];
        var textLocator = page.GetByText(fallback.name, new PageGetByTextOptions { Exact = true }).First;
        if (await textLocator.CountAsync().WaitAsync(cancellationToken) > 0
            && await textLocator.IsVisibleAsync().WaitAsync(cancellationToken))
        {
            await ClickWithoutNavigationWaitAsync(
                page,
                textLocator,
                treatAsSubmit ? $"submitting {fallback.name}" : $"clicking {fallback.name}",
                cancellationToken);
            return true;
        }

        if (throwOnFailure)
        {
            throw new TimeoutException($"Unable to click '{fallback.name}'.");
        }

        return false;
    }

    private async Task<PlaywrightObservation> CaptureAfterInteractionAsync(
        IPlaywrightSession session,
        IPage page,
        CancellationToken cancellationToken)
    {
        await CaptureAfterInteractionAsyncFromPageAsync(page, cancellationToken);
        return await _observationService.CaptureAsync(session, cancellationToken);
    }

    private async Task<PlaywrightObservation> CaptureAfterEditorActionAsync(
        IPlaywrightSession session,
        IPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
            {
                Timeout = 15_000,
            }).WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            // Some editor actions update in-place and never trigger a full navigation.
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = 5_000,
            }).WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            // Network activity can continue after the editor returns. Observation below is authoritative.
        }

        return await _observationService.CaptureAsync(session, cancellationToken);
    }

    private async Task<PlaywrightObservation> CaptureAfterEditorActionFailureAsync(
        IPlaywrightSession session,
        IPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
            {
                Timeout = 2_000,
            }).WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
        }

        return await _observationService.CaptureAsync(session, cancellationToken);
    }

    private static string BuildEditorActionFailureMessage(
        IReadOnlyList<string> candidateNames,
        PlaywrightObservation observation)
    {
        var details = new List<string>
        {
            $"Action did not finish in time after clicking: {string.Join(", ", candidateNames)}.",
        };

        if (!string.IsNullOrWhiteSpace(observation?.MainHeading))
        {
            details.Add($"Heading: {observation.MainHeading}.");
        }

        if (!string.IsNullOrWhiteSpace(observation?.ToastMessage))
        {
            details.Add($"Toast: {observation.ToastMessage}.");
        }

        if (observation?.ValidationMessages?.Count > 0)
        {
            details.Add($"Messages: {string.Join(" | ", observation.ValidationMessages)}.");
        }

        if (!string.IsNullOrWhiteSpace(observation?.CurrentUrl))
        {
            details.Add($"URL: {observation.CurrentUrl}.");
        }

        if (observation?.VisibleButtons?.Count > 0)
        {
            details.Add($"Visible actions: {string.Join(", ", observation.VisibleButtons)}.");
        }

        return string.Join(" ", details);
    }

    private static async Task<string> TrySetFieldValueAsync(
        ILocator locator,
        string label,
        string value,
        string requestedFieldType,
        bool append,
        CancellationToken cancellationToken)
    {
        if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
        {
            return null;
        }

        return await locator.EvaluateAsync<string>(
            """
            (element, payload) => {
              const normalize = (input) => (input ?? "").toString().trim().replace(/\s+/g, " ");
              const isVisible = (candidate) => {
                if (!candidate) {
                  return false;
                }

                const style = window.getComputedStyle(candidate);
                if (style.visibility === "hidden" || style.display === "none") {
                  return false;
                }

                return candidate.offsetParent !== null || style.position === "fixed";
              };

              const dispatch = (candidate) => {
                for (const eventName of ["input", "change", "blur"]) {
                  candidate.dispatchEvent(new Event(eventName, { bubbles: true }));
                }
              };

              const appendPlainText = (currentValue, nextValue, separator) => {
                if (!nextValue) {
                  return currentValue ?? "";
                }

                if (!currentValue) {
                  return nextValue;
                }

                return `${currentValue}${separator}${nextValue}`;
              };

              const findFirstVisible = (candidates) => candidates.find((candidate) => isVisible(candidate));

              const parseBoolean = (input) => {
                const normalized = normalize(input).toLowerCase();
                if (["true", "1", "yes", "y", "on", "checked", "check", "selected"].includes(normalized)) {
                  return true;
                }

                if (["false", "0", "no", "n", "off", "unchecked", "uncheck", "clear"].includes(normalized)) {
                  return false;
                }

                return null;
              };

              const setStandardValue = (candidate, append) => {
                if (!(candidate instanceof HTMLInputElement) && !(candidate instanceof HTMLTextAreaElement)) {
                  return null;
                }

                if (!isVisible(candidate)) {
                  return null;
                }

                const separator = candidate instanceof HTMLTextAreaElement || append ? "\n\n" : " ";
                candidate.focus();
                candidate.value = append ? appendPlainText(candidate.value, payload.value, separator) : payload.value;
                dispatch(candidate);

                if (candidate instanceof HTMLTextAreaElement) {
                  return "textarea";
                }

                return "input";
              };

              const setSelectValue = (candidate) => {
                if (!(candidate instanceof HTMLSelectElement) || !isVisible(candidate)) {
                  return null;
                }

                const requestedValue = normalize(payload.value).toLowerCase();
                const options = Array.from(candidate.options || []);
                const matched = options.find((option) => {
                  return normalize(option.value).toLowerCase() === requestedValue
                    || normalize(option.label).toLowerCase() === requestedValue
                    || normalize(option.text).toLowerCase() === requestedValue;
                }) || options.find((option) => {
                  return normalize(option.value).toLowerCase().includes(requestedValue)
                    || normalize(option.label).toLowerCase().includes(requestedValue)
                    || normalize(option.text).toLowerCase().includes(requestedValue);
                });

                if (!matched) {
                  return null;
                }

                candidate.value = matched.value;
                dispatch(candidate);
                return "select";
              };

              const setBooleanValue = (candidate) => {
                if (!(candidate instanceof HTMLInputElement) || !isVisible(candidate)) {
                  return null;
                }

                if (candidate.type !== "checkbox" && candidate.type !== "radio") {
                  return null;
                }

                const nextValue = parseBoolean(payload.value);
                if (nextValue === null) {
                  return null;
                }

                if (candidate.type === "radio" && nextValue === false) {
                  return null;
                }

                candidate.focus();
                candidate.checked = nextValue;
                dispatch(candidate);
                return candidate.type;
              };

              const escapeHtml = (input) => {
                const div = document.createElement("div");
                div.textContent = input ?? "";
                return div.innerHTML;
              };

              const toParagraphHtml = (input) => {
                const lines = (input ?? "").split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
                if (lines.length === 0) {
                  return "";
                }

                return lines.map((line) => `<p>${escapeHtml(line)}</p>`).join("");
              };

              const appendRichText = (root, nextValue, append) => {
                const htmlToAppend = toParagraphHtml(nextValue);
                if (!htmlToAppend) {
                  return root.innerHTML ?? "";
                }

                if (append && normalize(root.innerText)) {
                  root.insertAdjacentHTML("beforeend", htmlToAppend);
                } else {
                  root.innerHTML = htmlToAppend;
                }

                dispatch(root);
                return root.innerHTML ?? "";
              };

              const syncSourceField = (content) => {
                if (element instanceof HTMLTextAreaElement || element instanceof HTMLInputElement) {
                  element.value = content ?? "";
                  dispatch(element);
                }
              };

              const fieldId = element.id || "";
              const fieldName = element.getAttribute("name") || "";
              const labelToken = normalize(payload.label).replace(/\s+/g, "").toLowerCase();
              const fieldTokens = [fieldId, fieldName, payload.label, labelToken]
                .filter(Boolean)
                .map((token) => token.toString().toLowerCase());

              const matchesField = (candidate) => {
                const values = [
                  candidate?.id,
                  candidate?.getAttribute?.("name"),
                  candidate?.getAttribute?.("aria-label"),
                  candidate?.getAttribute?.("title"),
                  candidate?.getAttribute?.("data-field-name"),
                  candidate?.getAttribute?.("for"),
                ]
                  .filter(Boolean)
                  .map((token) => token.toString().toLowerCase());

                return fieldTokens.some((token) => values.some((value) => value.includes(token)));
              };

              const container = element.closest(".mb-3, .form-group, .field, fieldset, section, .card-body, .content-field")
                || element.parentElement
                || document.body;

              const requestedKind = normalize(payload.requestedFieldType).toLowerCase();
              const preferAppend = Boolean(payload.append);
              const preferRichText = requestedKind === "richtext";
              const preferSelect = requestedKind === "select";
              const preferCheckbox = requestedKind === "checkbox" || requestedKind === "radio" || requestedKind === "boolean";
              const directMode = setBooleanValue(element)
                || setSelectValue(element)
                || setStandardValue(
                  element,
                  preferAppend || element instanceof HTMLTextAreaElement || Number.parseInt(element.getAttribute?.("rows") || "0", 10) > 1);

              if (directMode && (!preferSelect || directMode === "select") && (!preferCheckbox || directMode === requestedKind || directMode === "checkbox")) {
                return directMode;
              }

              if (fieldId && window.tinymce && typeof window.tinymce.get === "function") {
                const editor = window.tinymce.get(fieldId);
                if (editor) {
                  const currentContent = editor.getContent({ format: "html" }) || "";
                  const nextHtml = toParagraphHtml(payload.value);
                  const combinedContent = preferAppend && currentContent
                    ? `${currentContent}${nextHtml}`
                    : nextHtml;

                  editor.setContent(combinedContent);
                  editor.save();
                  syncSourceField(combinedContent);
                  return "tinymce";
                }
              }

              const standardCandidates = [
                ...container.querySelectorAll("textarea, input, select"),
                ...document.querySelectorAll("textarea, input, select"),
              ];

              for (const candidate of standardCandidates) {
                if (candidate === element) {
                  continue;
                }

                if (!matchesField(candidate) && !isVisible(candidate)) {
                  continue;
                }

                const mode = setBooleanValue(candidate)
                  || setSelectValue(candidate)
                  || setStandardValue(
                    candidate,
                    preferAppend || candidate instanceof HTMLTextAreaElement || Number.parseInt(candidate.getAttribute("rows") || "0", 10) > 1);

                if (preferSelect && mode !== "select") {
                  continue;
                }

                if (preferCheckbox && mode !== requestedKind && mode !== "checkbox") {
                  continue;
                }

                if (mode) {
                  syncSourceField(candidate.value ?? "");
                  return `${mode}-related`;
                }
              }

              const editableCandidates = [
                ...container.querySelectorAll("[contenteditable='true'], .ck-editor__editable, .ProseMirror, .trumbowyg-editor, .ql-editor"),
                ...document.querySelectorAll("[contenteditable='true'], .ck-editor__editable, .ProseMirror, .trumbowyg-editor, .ql-editor"),
              ];

              const editable = findFirstVisible(editableCandidates.filter((candidate) => matchesField(candidate) || container.contains(candidate)));
              if (editable && !preferSelect && !preferCheckbox) {
                const updatedHtml = appendRichText(editable, payload.value, preferAppend || normalize(editable.innerText).length > 0);
                syncSourceField(updatedHtml);
                return "contenteditable";
              }

              const iframeCandidates = [
                ...container.querySelectorAll("iframe"),
                ...document.querySelectorAll("iframe"),
              ].filter((candidate) => matchesField(candidate) || container.contains(candidate));

              for (const iframe of iframeCandidates) {
                if (!isVisible(iframe)) {
                  continue;
                }

                try {
                  const doc = iframe.contentDocument;
                  if (!doc?.body) {
                    continue;
                  }

                  const updatedHtml = appendRichText(doc.body, payload.value, preferAppend || normalize(doc.body.innerText).length > 0);
                  syncSourceField(updatedHtml);
                  return "iframe";
                } catch {
                  // Ignore inaccessible iframe candidates and continue.
                }
              }

              if (preferRichText) {
                return null;
              }

              return null;
            }
            """,
            new
            {
                label,
                value,
                append,
                requestedFieldType,
            }).WaitAsync(cancellationToken);
    }

    private static bool ShouldAppendToField(string label)
        => label.Contains("body", StringComparison.OrdinalIgnoreCase)
            || label.Contains("html", StringComparison.OrdinalIgnoreCase)
            || label.Contains("content", StringComparison.OrdinalIgnoreCase)
            || label.Contains("description", StringComparison.OrdinalIgnoreCase)
            || label.Contains("summary", StringComparison.OrdinalIgnoreCase);

    private static async Task<IReadOnlyList<PlaywrightContentListItem>> GetVisibleContentItemsAsync(
        IPage page,
        int maxItems,
        CancellationToken cancellationToken)
    {
        maxItems = Math.Clamp(maxItems, 1, 25);

        var payload = await page.EvaluateAsync<VisibleContentItemPayload[]>(
            """
            (maxItems) => {
              const normalize = (value) => (value || "").replace(/\s+/g, " ").trim();
              const isVisible = (element) => {
                if (!(element instanceof HTMLElement) || element.hidden) {
                  return false;
                }

                const style = window.getComputedStyle(element);
                if (style.display === "none" || style.visibility === "hidden" || style.opacity === "0") {
                  return false;
                }

                return element.offsetWidth > 0 || element.offsetHeight > 0 || element.getClientRects().length > 0;
              };

              const isActionLine = (value) => /^(edit|view|preview|delete|clone|duplicate|save|publish|unpublish|actions|more|more actions|options)$/i.test(value);
              const isStatusLine = (value) => /(draft|published|unpublished|latest|modified)/i.test(value);
              const results = [];
              const seen = new Set();
              const rows = Array.from(document.querySelectorAll("tbody tr, table tr, [data-content-item-id], .content-item, .list-group-item, article"));

              for (const row of rows) {
                if (!isVisible(row)) {
                  continue;
                }

                const lines = normalize(row.innerText).split(/\s{2,}|\n+/).map(normalize).filter(Boolean);
                const filteredLines = lines.filter((line) => !isActionLine(line));
                const linkTexts = Array.from(row.querySelectorAll("a, button, strong, h2, h3, h4"))
                  .filter((element) => isVisible(element))
                  .map((element) => normalize(element.innerText || element.textContent || ""))
                  .filter((text) => text.length > 2 && text.length < 180 && !isActionLine(text));

                const title = linkTexts.find((text) => !isStatusLine(text))
                  || filteredLines.find((line) => line.length > 2 && line.length < 180 && !isStatusLine(line))
                  || "";
                if (!title) {
                  continue;
                }

                const titleKey = title.toLowerCase();
                if (seen.has(titleKey)) {
                  continue;
                }

                seen.add(titleKey);
                const status = filteredLines.find((line) => isStatusLine(line)) || "";
                const contentType = filteredLines.find((line, index) => index > 0 && line !== title && line !== status && line.length < 100) || "";
                const canEdit = Array.from(row.querySelectorAll("a, button, input[type='submit']")).some((element) => {
                  const text = normalize(element.innerText || element.value || element.getAttribute("aria-label") || element.getAttribute("title") || "");
                  return /edit/i.test(text);
                });

                results.push({
                  title,
                  contentType,
                  status,
                  canEdit
                });

                if (results.length >= maxItems) {
                  break;
                }
              }

              return results;
            }
            """,
            maxItems).WaitAsync(cancellationToken);

        return (payload ?? [])
            .Select(item => new PlaywrightContentListItem
            {
                Title = NormalizeTitle(item.Title),
                ContentType = NormalizeTitle(item.ContentType),
                Status = NormalizeTitle(item.Status),
                CanEdit = item.CanEdit,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .ToList();
    }

    private async Task ShowFillIndicatorAsync(
        IPage page,
        ILocator locator,
        string label,
        CancellationToken cancellationToken)
    {
        try
        {
            await _actionVisualizer.ShowLocatorActionAsync(page, locator, "Typing", label, cancellationToken);
        }
        catch
        {
            await _actionVisualizer.ShowPageActionAsync(page, "Typing", label, cancellationToken);
        }
    }

    private async Task ClickWithoutNavigationWaitAsync(
        IPage page,
        ILocator locator,
        string actionDescription,
        CancellationToken cancellationToken)
    {
        await locator.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10_000,
        }).WaitAsync(cancellationToken);

        await _actionVisualizer.ShowLocatorActionAsync(page, locator, "AI", actionDescription, cancellationToken);
        await locator.ScrollIntoViewIfNeededAsync().WaitAsync(cancellationToken);
        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = 10_000,
        }).WaitAsync(cancellationToken);
    }

    private static async Task CaptureAfterInteractionAsyncFromPageAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
            {
                Timeout = 10_000,
            }).WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
        }
    }

    private static (int Score, string Mode) GetTitleMatch(string candidateTitle, string requestedTitle)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle) || string.IsNullOrWhiteSpace(requestedTitle))
        {
            return (0, null);
        }

        if (candidateTitle.Equals(requestedTitle, StringComparison.OrdinalIgnoreCase))
        {
            return (1000, "Exact");
        }

        var normalizedCandidate = NormalizeTitleKey(candidateTitle);
        var normalizedRequested = NormalizeTitleKey(requestedTitle);
        if (string.IsNullOrWhiteSpace(normalizedCandidate) || string.IsNullOrWhiteSpace(normalizedRequested))
        {
            return (0, null);
        }

        if (normalizedCandidate.Equals(normalizedRequested, StringComparison.Ordinal))
        {
            return (950, "Normalized");
        }

        if (normalizedCandidate.Contains(normalizedRequested, StringComparison.Ordinal)
            || normalizedRequested.Contains(normalizedCandidate, StringComparison.Ordinal))
        {
            return (860, "Contains");
        }

        var requestedTokens = TokenizeTitle(requestedTitle);
        var candidateTokens = TokenizeTitle(candidateTitle);
        var overlap = requestedTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase).Count();

        if (overlap == requestedTokens.Count && overlap > 0)
        {
            return (880, "TokenMatch");
        }

        if (overlap > 0)
        {
            return (700 + (overlap * 40), "PartialTokenMatch");
        }

        var distance = GetLevenshteinDistance(normalizedCandidate, normalizedRequested);
        var threshold = Math.Max(2, Math.Max(normalizedCandidate.Length, normalizedRequested.Length) / 4);
        if (distance <= threshold)
        {
            return (650 - (distance * 25), "Fuzzy");
        }

        return (0, null);
    }

    private static string NormalizeTitle(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : Regex.Replace(value.Trim(), @"\s+", " ");

    private static string NormalizeTitleKey(string value)
        => string.Concat((value ?? string.Empty)
            .Where(char.IsLetterOrDigit))
            .ToLowerInvariant();

    private static IReadOnlyList<string> TokenizeTitle(string value)
        => Regex.Split(value ?? string.Empty, @"[\s\-_:/\\]+")
            .Select(token => token.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();

    private static int GetLevenshteinDistance(string source, string target)
    {
        var matrix = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= target.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,
                        matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }

    private static async Task<ILocator> FindContentItemContainerAsync(
        IPage page,
        string contentItemTitle,
        CancellationToken cancellationToken)
    {
        foreach (var exact in new[] { true, false })
        {
            foreach (var selector in ContentItemContainerSelectors)
            {
                var locator = page.Locator(selector).Filter(new LocatorFilterOptions
                {
                    Has = page.GetByText(contentItemTitle, new PageGetByTextOptions { Exact = exact }),
                }).First;

                if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
                {
                    continue;
                }

                if (await locator.IsVisibleAsync().WaitAsync(cancellationToken))
                {
                    return locator;
                }
            }
        }

        return null;
    }

    private async Task<bool> TryClickActionWithinContainerAsync(
        IPage page,
        ILocator container,
        string actionName,
        string contentItemTitle,
        CancellationToken cancellationToken)
    {
        var exactCandidates = new ILocator[]
        {
            container.GetByRole(AriaRole.Link, new() { Name = actionName, Exact = true }).First,
            container.GetByRole(AriaRole.Button, new() { Name = actionName, Exact = true }).First,
            container.GetByText(actionName, new LocatorGetByTextOptions { Exact = true }).First,
        };

        foreach (var locator in exactCandidates)
        {
            if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
            {
                continue;
            }

            if (!await locator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                continue;
            }

            await ClickWithoutNavigationWaitAsync(page, locator, $"clicking {actionName} for {contentItemTitle}", cancellationToken);

            return true;
        }

        var fallback = container.Locator(
            $"a[title*='{actionName}' i], button[title*='{actionName}' i], a[aria-label*='{actionName}' i], button[aria-label*='{actionName}' i], input[type='submit'][value*='{actionName}' i]").First;

        if (await fallback.CountAsync().WaitAsync(cancellationToken) == 0
            || !await fallback.IsVisibleAsync().WaitAsync(cancellationToken))
        {
            return actionName.Equals("Edit", StringComparison.OrdinalIgnoreCase)
                && await TryOpenContentItemTitleWithinContainerAsync(page, container, contentItemTitle, cancellationToken);
        }

        await ClickWithoutNavigationWaitAsync(page, fallback, $"clicking {actionName} for {contentItemTitle}", cancellationToken);

        return true;
    }

    private async Task<bool> TryOpenContentItemTitleWithinContainerAsync(
        IPage page,
        ILocator container,
        string contentItemTitle,
        CancellationToken cancellationToken)
    {
        foreach (var exact in new[] { true, false })
        {
            foreach (var locator in new[]
            {
                container.GetByRole(AriaRole.Link, new() { Name = contentItemTitle, Exact = exact }).First,
                container.GetByRole(AriaRole.Button, new() { Name = contentItemTitle, Exact = exact }).First,
                container.GetByText(contentItemTitle, new LocatorGetByTextOptions { Exact = exact }).First,
            })
            {
                if (await locator.CountAsync().WaitAsync(cancellationToken) == 0
                    || !await locator.IsVisibleAsync().WaitAsync(cancellationToken))
                {
                    continue;
                }

                await ClickWithoutNavigationWaitAsync(page, locator, $"opening {contentItemTitle}", cancellationToken);
                return true;
            }
        }

        return false;
    }

    private sealed class VisibleContentItemPayload
    {
        public string Title { get; set; }

        public string ContentType { get; set; }

        public string Status { get; set; }

        public bool CanEdit { get; set; }
    }

    private sealed class ContentItemSelection
    {
        public PlaywrightContentListItem Item { get; init; }

        public string MatchMode { get; init; }

        public int Score { get; init; }
    }

    private sealed class EditorTargetMatch
    {
        public ILocator Locator { get; init; }

        public string MatchedText { get; init; }

        public string TargetKind { get; init; }
    }

    private static async Task WaitForEditorAsync(IPage page, CancellationToken cancellationToken)
    {
        var titleLocator = page.GetByLabel("Title", new() { Exact = true }).First;

        try
        {
            await titleLocator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15_000,
            }).WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            var fallback = page.Locator("input[name='TitlePart.Title'], input[id*='TitlePart_Title']").First;
            await fallback.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15_000,
            }).WaitAsync(cancellationToken);
        }
    }
}
