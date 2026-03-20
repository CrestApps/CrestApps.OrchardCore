namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

internal static class BrowserAutomationScripts
{
    public const string FindNavigationItem =
        """
        (input) => {
          const normalize = (value) => (value || '').replace(/\s+/g, ' ').trim().toLowerCase();
          const isVisible = (element) => !!(element && (element.offsetWidth || element.offsetHeight || element.getClientRects().length));
          const target = normalize(input.segment);
          const containers = Array.from(document.querySelectorAll('nav, aside, [role="navigation"], .ta-navbar-nav, .admin-menu, .menu'));
          const scopes = containers.length > 0 ? containers : [document.body];
          const seen = new Set();
          const candidates = [];

          for (const scope of scopes) {
            const elements = scope.querySelectorAll('a, button, [role="menuitem"], [aria-expanded]');
            for (const element of elements) {
              if (!(element instanceof HTMLElement) || seen.has(element)) {
                continue;
              }

              seen.add(element);

              const texts = [
                normalize(element.innerText || element.textContent),
                normalize(element.getAttribute('aria-label')),
                normalize(element.getAttribute('title'))
              ].filter(Boolean);

              if (texts.length === 0) {
                continue;
              }

              const exact = texts.some((text) => text === target);
              const contains = texts.some((text) => text.includes(target) || target.includes(text));

              if (!exact && !contains) {
                continue;
              }

              const score =
                (exact ? 100 : 50) +
                (isVisible(element) ? 25 : 0) +
                (element.closest('nav, aside, [role="navigation"]') ? 20 : 0) +
                (element.getAttribute('aria-expanded') === 'false' ? 5 : 0);

              candidates.push({
                element,
                score,
                text: (element.innerText || element.textContent || '').replace(/\s+/g, ' ').trim(),
                href: element.getAttribute('href') || '',
                tagName: element.tagName.toLowerCase(),
                ariaExpanded: element.getAttribute('aria-expanded') || '',
              });
            }
          }

          candidates.sort((left, right) => right.score - left.score);

          const match = candidates[0];
          if (!match) {
            return null;
          }

          match.element.setAttribute('data-ai-nav-match', input.marker);

          return JSON.stringify({
            text: match.text,
            href: match.href,
            tagName: match.tagName,
            ariaExpanded: match.ariaExpanded,
          });
        }
        """;

    public const string RemoveNavigationMarker =
        """
        (marker) => {
          const element = document.querySelector('[data-ai-nav-match="' + marker + '"]');
          if (element) {
            element.removeAttribute('data-ai-nav-match');
          }
        }
        """;
}
