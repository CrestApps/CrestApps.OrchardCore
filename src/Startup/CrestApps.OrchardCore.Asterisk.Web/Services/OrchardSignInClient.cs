using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Asterisk.Web.Models;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Creates authenticated Orchard Core HTTP clients for the simulator.
/// </summary>
public sealed class OrchardSignInClient
{
    private const string RequestVerificationTokenFieldName = "__RequestVerificationToken";

    private static readonly Regex FormRegex = new(
        "<form\\b(?<attrs>[^>]*)>(?<body>.*?)</form>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex InputRegex = new(
        "<input\\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex = new(
        "(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// Creates an authenticated HTTP client by submitting the Orchard login form.
    /// </summary>
    /// <param name="input">The simulator input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An authenticated HTTP client and its request-verification token, when available.</returns>
    public async Task<(HttpClient Client, string RequestVerificationToken)> CreateAuthenticatedClientAsync(
        InboundCallSimulationInputModel input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var baseUri = NormalizeBaseUri(input.OrchardBaseUrl);
        var handler = CreateHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = baseUri,
        };

        try
        {
            var loginUri = new Uri(baseUri, input.LoginPath.TrimStart('/'));
            var loginPage = await client.GetStringAsync(loginUri, cancellationToken);
            var loginForm = ParseLoginForm(loginPage, loginUri);
            loginForm.HiddenFields.TryGetValue(RequestVerificationTokenFieldName, out var requestVerificationToken);

            var fields = new Dictionary<string, string>(loginForm.HiddenFields, StringComparer.Ordinal);
            fields[loginForm.UserNameFieldName] = input.UserName;
            fields[loginForm.PasswordFieldName] = input.Password;

            using var response = await client.PostAsync(
                loginForm.ActionUri,
                new FormUrlEncodedContent(fields),
                cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (LooksLikeLoginPage(response.RequestMessage?.RequestUri ?? loginForm.ActionUri, responseContent, input.LoginPath))
            {
                throw new InvalidOperationException("Sign-in failed. Verify the Orchard URL, login path, and credentials.");
            }

            return (client, requestVerificationToken);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static HttpClientHandler CreateHandler()
    {
        return new HttpClientHandler
        {
            AllowAutoRedirect = true,
            CookieContainer = new CookieContainer(),
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None || IsLoopback(request.RequestUri),
        };
    }

    private static Uri NormalizeBaseUri(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Enter a valid Orchard Core base URL.");
        }

        var builder = new UriBuilder(uri);

        if (string.IsNullOrEmpty(builder.Path) || builder.Path == "/")
        {
            builder.Path = "/";
        }
        else if (builder.Path[builder.Path.Length - 1] != '/')
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }

    private static LoginForm ParseLoginForm(string html, Uri pageUri)
    {
        foreach (Match formMatch in FormRegex.Matches(html))
        {
            var formBody = formMatch.Groups["body"].Value;

            if (!formBody.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var formAttributes = ParseAttributes(formMatch.Groups["attrs"].Value);
            var actionUri = formAttributes.TryGetValue("action", out var actionValue) && !string.IsNullOrWhiteSpace(actionValue)
                ? new Uri(pageUri, WebUtility.HtmlDecode(actionValue))
                : pageUri;

            string userNameFieldName = null;
            string passwordFieldName = null;
            var hiddenFields = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match inputMatch in InputRegex.Matches(formBody))
            {
                var inputAttributes = ParseAttributes(inputMatch.Groups["attrs"].Value);

                if (!inputAttributes.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var decodedName = WebUtility.HtmlDecode(name);
                inputAttributes.TryGetValue("type", out var type);
                inputAttributes.TryGetValue("value", out var value);
                var decodedValue = WebUtility.HtmlDecode(value ?? string.Empty);
                var normalizedType = string.IsNullOrWhiteSpace(type) ? "text" : type.Trim().ToLowerInvariant();

                if (normalizedType == "hidden")
                {
                    hiddenFields[decodedName] = decodedValue;

                    continue;
                }

                if (normalizedType == "password")
                {
                    passwordFieldName ??= decodedName;

                    continue;
                }

                if (normalizedType is "text" or "email")
                {
                    userNameFieldName ??= decodedName;
                }
            }

            if (!string.IsNullOrWhiteSpace(userNameFieldName) && !string.IsNullOrWhiteSpace(passwordFieldName))
            {
                return new LoginForm(actionUri, userNameFieldName, passwordFieldName, hiddenFields);
            }
        }

        throw new InvalidOperationException("The simulator could not locate a login form on the Orchard sign-in page.");
    }

    private static Dictionary<string, string> ParseAttributes(string rawAttributes)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AttributeRegex.Matches(rawAttributes))
        {
            attributes[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return attributes;
    }

    private static bool LooksLikeLoginPage(Uri requestUri, string responseContent, string loginPath)
    {
        var normalizedLoginPath = NormalizePath(loginPath);
        var normalizedRequestPath = NormalizePath(requestUri.AbsolutePath);

        return (normalizedRequestPath == normalizedLoginPath ||
                normalizedRequestPath.EndsWith(normalizedLoginPath, StringComparison.OrdinalIgnoreCase)) &&
            responseContent.Contains("password", StringComparison.OrdinalIgnoreCase) &&
            responseContent.Contains("<form", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');

        return string.IsNullOrEmpty(normalized) ? "/" : normalized;
    }

    private static bool IsLoopback(Uri uri)
    {
        return uri is not null && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record LoginForm(
        Uri ActionUri,
        string UserNameFieldName,
        string PasswordFieldName,
        IReadOnlyDictionary<string, string> HiddenFields);
}
