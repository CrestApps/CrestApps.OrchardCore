using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public sealed class PlaywrightActionVisualizer : IPlaywrightActionVisualizer
{
    private readonly ILogger<PlaywrightActionVisualizer> _logger;

    public PlaywrightActionVisualizer(ILogger<PlaywrightActionVisualizer> logger)
    {
        _logger = logger;
    }

    public async Task ShowLocatorActionAsync(
        IPage page,
        ILocator locator,
        string action,
        string target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(locator);

        if (page.IsClosed)
        {
            return;
        }

        try
        {
            await locator.ScrollIntoViewIfNeededAsync().WaitAsync(cancellationToken);
        }
        catch
        {
            // Best effort. Some hidden editors cannot be scrolled into view directly.
        }

        double? x = null;
        double? y = null;
        double? width = null;
        double? height = null;
        try
        {
            var boundingBox = await locator.BoundingBoxAsync().WaitAsync(cancellationToken);
            if (boundingBox is not null)
            {
                x = boundingBox.X;
                y = boundingBox.Y;
                width = boundingBox.Width;
                height = boundingBox.Height;
            }
        }
        catch
        {
            // Best effort. Hidden or detached elements can still use the page-level indicator.
        }

        _logger.LogDebug(
            "Showing Playwright action indicator for {Action} on {Target}.",
            action,
            target);

        await ShowIndicatorAsync(page, new PlaywrightActionIndicatorPayload
        {
            Action = action,
            Target = target,
            X = x,
            Y = y,
            Width = width,
            Height = height,
        }, cancellationToken);
    }

    public async Task ShowPageActionAsync(
        IPage page,
        string action,
        string target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.IsClosed)
        {
            return;
        }

        _logger.LogDebug(
            "Showing page-level Playwright action indicator for {Action} on {Target}.",
            action,
            target);

        await ShowIndicatorAsync(page, new PlaywrightActionIndicatorPayload
        {
            Action = action,
            Target = target,
        }, cancellationToken);
    }

    private static async Task ShowIndicatorAsync(
        IPage page,
        PlaywrightActionIndicatorPayload payload,
        CancellationToken cancellationToken)
    {
        await page.EvaluateAsync(
            """
            payload => {
              const doc = document;
              const body = doc.body;

              if (!body) {
                return;
              }

              const normalize = (value) => (value || '').toString().trim();
              const rootId = '__crestapps-playwright-indicator';
              let root = doc.getElementById(rootId);

              if (!root) {
                root = doc.createElement('div');
                root.id = rootId;
                root.style.position = 'fixed';
                root.style.inset = '0';
                root.style.pointerEvents = 'none';
                root.style.zIndex = '2147483647';
                root.style.opacity = '0';
                root.style.transition = 'opacity 120ms ease-out';

                const box = doc.createElement('div');
                box.setAttribute('data-role', 'box');
                box.style.position = 'fixed';
                box.style.border = '3px solid #f97316';
                box.style.borderRadius = '10px';
                box.style.boxShadow = '0 0 0 6px rgba(249, 115, 22, 0.18)';
                box.style.background = 'rgba(249, 115, 22, 0.08)';
                box.style.display = 'none';
                root.appendChild(box);

                const pointer = doc.createElement('div');
                pointer.setAttribute('data-role', 'pointer');
                pointer.style.position = 'fixed';
                pointer.style.width = '18px';
                pointer.style.height = '18px';
                pointer.style.marginLeft = '-9px';
                pointer.style.marginTop = '-9px';
                pointer.style.borderRadius = '999px';
                pointer.style.border = '3px solid #fb7185';
                pointer.style.background = 'rgba(255, 255, 255, 0.92)';
                pointer.style.boxShadow = '0 0 0 8px rgba(251, 113, 133, 0.18)';
                pointer.style.display = 'none';
                root.appendChild(pointer);

                const label = doc.createElement('div');
                label.setAttribute('data-role', 'label');
                label.style.position = 'fixed';
                label.style.maxWidth = '420px';
                label.style.padding = '8px 12px';
                label.style.borderRadius = '999px';
                label.style.background = 'rgba(15, 23, 42, 0.94)';
                label.style.border = '1px solid rgba(125, 211, 252, 0.5)';
                label.style.color = '#e2e8f0';
                label.style.fontFamily = 'Consolas, Menlo, Monaco, monospace';
                label.style.fontSize = '12px';
                label.style.fontWeight = '700';
                label.style.letterSpacing = '0.01em';
                label.style.whiteSpace = 'nowrap';
                label.style.overflow = 'hidden';
                label.style.textOverflow = 'ellipsis';
                label.style.boxShadow = '0 12px 28px rgba(15, 23, 42, 0.28)';
                root.appendChild(label);

                body.appendChild(root);
              }

              const box = root.querySelector('[data-role="box"]');
              const pointer = root.querySelector('[data-role="pointer"]');
              const label = root.querySelector('[data-role="label"]');
              const labelText = [normalize(payload.action), normalize(payload.target)].filter(Boolean).join(' ');

              label.textContent = labelText || 'AI action';

              const hasTarget = Number.isFinite(payload.x)
                && Number.isFinite(payload.y)
                && Number.isFinite(payload.width)
                && Number.isFinite(payload.height);

              if (hasTarget) {
                const left = Math.max(8, payload.x - 6);
                const top = Math.max(8, payload.y - 6);
                const width = Math.max(24, payload.width + 12);
                const height = Math.max(24, payload.height + 12);
                box.style.display = 'block';
                box.style.left = `${left}px`;
                box.style.top = `${top}px`;
                box.style.width = `${width}px`;
                box.style.height = `${height}px`;

                pointer.style.display = 'block';
                pointer.style.left = `${left + Math.min(width, 28)}px`;
                pointer.style.top = `${top + Math.min(height, 22)}px`;

                label.style.left = `${Math.max(12, left)}px`;
                label.style.top = `${Math.max(12, top - 42)}px`;
              } else {
                box.style.display = 'none';
                pointer.style.display = 'none';
                label.style.left = '20px';
                label.style.top = '20px';
              }

              root.style.opacity = '1';

              if (window.__crestappsPlaywrightIndicatorTimer) {
                clearTimeout(window.__crestappsPlaywrightIndicatorTimer);
              }

              window.__crestappsPlaywrightIndicatorTimer = window.setTimeout(() => {
                const activeRoot = doc.getElementById(rootId);
                if (activeRoot) {
                  activeRoot.style.opacity = '0';
                }
              }, 1800);
            }
            """,
            payload).WaitAsync(cancellationToken);
    }

    private sealed class PlaywrightActionIndicatorPayload
    {
        public string Action { get; init; }

        public string Target { get; init; }

        public double? X { get; init; }

        public double? Y { get; init; }

        public double? Width { get; init; }

        public double? Height { get; init; }
    }
}
