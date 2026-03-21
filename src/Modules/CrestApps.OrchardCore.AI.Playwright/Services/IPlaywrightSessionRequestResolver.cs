using CrestApps.OrchardCore.AI.Playwright.Models;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public interface IPlaywrightSessionRequestResolver
{
    PlaywrightSessionRequest Resolve(object resource, string chatSessionId);
}
