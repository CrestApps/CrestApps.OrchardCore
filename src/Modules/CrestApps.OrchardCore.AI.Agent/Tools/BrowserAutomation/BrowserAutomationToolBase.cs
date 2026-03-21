using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public abstract class BrowserAutomationToolBase<TTool> : AIFunction
    where TTool : AITool
{
    protected BrowserAutomationToolBase(
        BrowserAutomationService browserAutomationService,
        ILogger<TTool> logger)
    {
        BrowserAutomationService = browserAutomationService;
        Logger = logger;
    }

    protected BrowserAutomationService BrowserAutomationService { get; }

    protected ILogger<TTool> Logger { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    protected static JsonElement ParseJson(string json)
        => BrowserAutomationJson.ParseJson(json);

    protected static string Success(string action, object data)
        => BrowserAutomationResultFactory.Success(action, data);

    protected static string Failure(string action, string message)
        => BrowserAutomationResultFactory.Failure(action, message);

    protected static string GetRequiredString(AIFunctionArguments arguments, string key)
    {
        if (!arguments.TryGetFirstString(key, out var value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }

        return value.Trim();
    }

    protected static string GetOptionalString(AIFunctionArguments arguments, string key)
        => arguments.TryGetFirstString(key, out var value) ? value.Trim() : null;

    protected static bool GetBoolean(AIFunctionArguments arguments, string key, bool fallbackValue = false)
        => arguments.TryGetFirst<bool>(key, out var value) ? value : fallbackValue;

    protected static int GetTimeout(AIFunctionArguments arguments, int fallbackValue = AgentConstants.DefaultTimeoutMs)
    {
        var timeout = arguments.TryGetFirst<int>("timeoutMs", out var parsedTimeout)
            ? parsedTimeout
            : fallbackValue;

        return Math.Clamp(timeout, 1_000, AgentConstants.MaxTimeoutMs);
    }

    protected static int GetMaxItems(AIFunctionArguments arguments, int fallbackValue = AgentConstants.DefaultMaxItems)
    {
        var maxItems = arguments.TryGetFirst<int>("maxItems", out var parsedMaxItems)
            ? parsedMaxItems
            : fallbackValue;

        return Math.Clamp(maxItems, 1, AgentConstants.MaxCollectionItems);
    }

    protected static int GetMaxTextLength(AIFunctionArguments arguments, int fallbackValue = AgentConstants.DefaultMaxTextLength)
    {
        var maxLength = arguments.TryGetFirst<int>("maxLength", out var parsedMaxLength)
            ? parsedMaxLength
            : fallbackValue;

        return Math.Clamp(maxLength, 256, 20_000);
    }

    protected static int? GetNullableInt(AIFunctionArguments arguments, string key)
        => arguments.TryGetFirst<int>(key, out var value) ? value : null;

    protected static string[] GetStringArray(AIFunctionArguments arguments, string key)
    {
        if (!arguments.TryGetFirst<string[]>(key, out var values) || values is null || values.Length == 0)
        {
            throw new InvalidOperationException($"{key} is required.");
        }

        var sanitizedValues = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray();

        if (sanitizedValues.Length == 0)
        {
            throw new InvalidOperationException($"{key} is required.");
        }

        return sanitizedValues;
    }

    protected static string GetSessionId(AIFunctionArguments arguments)
        => GetOptionalString(arguments, "sessionId") ?? AgentConstants.DefaultSessionId;

    protected static string GetPageId(AIFunctionArguments arguments)
        => GetOptionalString(arguments, "pageId");

    protected static WaitUntilState ParseWaitUntil(AIFunctionArguments arguments, string key = "waitUntil", WaitUntilState fallbackValue = WaitUntilState.Load)
    {
        var value = GetOptionalString(arguments, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "load" => WaitUntilState.Load,
            "domcontentloaded" => WaitUntilState.DOMContentLoaded,
            "networkidle" => WaitUntilState.NetworkIdle,
            "commit" => WaitUntilState.Commit,
            _ => throw new InvalidOperationException($"Unsupported waitUntil value '{value}'. Supported values are load, domcontentloaded, networkidle, and commit."),
        };
    }

    protected static LoadState ParseLoadState(AIFunctionArguments arguments, string key = "state", LoadState fallbackValue = LoadState.Load)
    {
        var value = GetOptionalString(arguments, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "load" => LoadState.Load,
            "domcontentloaded" => LoadState.DOMContentLoaded,
            "networkidle" => LoadState.NetworkIdle,
            _ => throw new InvalidOperationException($"Unsupported state value '{value}'. Supported values are load, domcontentloaded, and networkidle."),
        };
    }

    protected static WaitForSelectorState ParseSelectorState(AIFunctionArguments arguments, string key = "state", WaitForSelectorState fallbackValue = WaitForSelectorState.Visible)
    {
        var value = GetOptionalString(arguments, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "attached" => WaitForSelectorState.Attached,
            "detached" => WaitForSelectorState.Detached,
            "hidden" => WaitForSelectorState.Hidden,
            "visible" => WaitForSelectorState.Visible,
            _ => throw new InvalidOperationException($"Unsupported selector state '{value}'. Supported values are attached, detached, hidden, and visible."),
        };
    }

    protected static MouseButton ParseMouseButton(AIFunctionArguments arguments, string key = "button", MouseButton fallbackValue = MouseButton.Left)
    {
        var value = GetOptionalString(arguments, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "left" => MouseButton.Left,
            "middle" => MouseButton.Middle,
            "right" => MouseButton.Right,
            _ => throw new InvalidOperationException($"Unsupported button value '{value}'. Supported values are left, middle, and right."),
        };
    }

    protected static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    protected static void RequestLiveNavigation(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var invocationContext = AIInvocationScope.Current;
        if (invocationContext is null)
        {
            return;
        }

        invocationContext.Items[AIInvocationItemKeys.LiveNavigationUrl] = uri.ToString();
    }

    protected async Task<object> ExecuteSafeAsync(string action, Func<Task<object>> callback)
    {
        try
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("AI browser tool '{ToolName}' invoked.", Name);
            }

            var result = await callback();

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("AI browser tool '{ToolName}' completed.", Name);
            }

            return result;
        }
        catch (TimeoutException exception)
        {
            Logger.LogWarning(exception, "AI browser tool '{ToolName}' timed out.", Name);
            return Failure(action, exception.Message);
        }
        catch (ObjectDisposedException exception)
        {
            Logger.LogWarning(exception, "AI browser tool '{ToolName}' referenced a disposed browser resource.", Name);
            return Failure(action, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            Logger.LogWarning(exception, "AI browser tool '{ToolName}' failed validation.", Name);
            return Failure(action, exception.Message);
        }
        catch (PlaywrightException exception)
        {
            Logger.LogWarning(exception, "AI browser tool '{ToolName}' failed during Playwright execution.", Name);

            var message = exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                ? exception.Message + " Run the Playwright browser install script generated by the app build before using browser automation tools."
                : exception.Message;

            return Failure(action, message);
        }
    }
}

