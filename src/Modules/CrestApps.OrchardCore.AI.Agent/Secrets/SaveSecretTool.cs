using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.AI.Agent.Secrets;

/// <summary>
/// Saves through the secret manager the Secrets admin page saves to, and only what holds no secret material: a new
/// X.509 secret that points to a certificate in a certificate store of the server, and the description and expiry
/// date of any secret. To change the description or the expiry date, the stored secret is saved back unchanged with
/// the new values, as the Edit page does when its value fields are left empty; the value never leaves the tool. The
/// tool has no argument for a text value, a PEM certificate or a private key, and it does not create RSA key secrets:
/// the Secrets admin page generates the key pair in its own controller, with no service a tool can call. For those it
/// returns the admin page where a person does it.
/// </summary>
public sealed class SaveSecretTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveSecret";

    private static readonly string[] _localFormats = ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the secret, the name settings reference it by, for example OpenId.SigningCertificate. It cannot be changed later."
        },
        "create": {
          "type": "boolean",
          "description": "True to create a secret with this name; the call is refused when a secret with the name exists. False or left out to update the description or expiry date of an existing secret."
        },
        "store": {
          "type": "string",
          "description": "The name of the secret store, from listSecrets. For a new secret the default is the first writable store. For an update it is needed only when the name is in several stores."
        },
        "type": {
          "type": "string",
          "enum": [
            "X509Secret",
            "RsaKeySecret",
            "TextSecret"
          ],
          "description": "For a new secret only. This tool creates X509Secret only: a reference to a certificate in a certificate store of the server. For RsaKeySecret and TextSecret it creates nothing and returns the admin page where a person creates the secret."
        },
        "description": {
          "type": "string",
          "description": "What the secret is for. Leave it out to keep the description of a secret that is updated."
        },
        "expiresOn": {
          "type": "string",
          "description": "The local date, or date and time, the secret expires, as yyyy-MM-dd or yyyy-MM-ddTHH:mm, without a time zone. It is a reminder only; an expired secret keeps working. Leave it out to keep the expiry date of a secret that is updated."
        },
        "removeExpiry": {
          "type": "boolean",
          "description": "True to remove the expiry date of a secret that is updated."
        },
        "certificateStoreLocation": {
          "type": "string",
          "enum": [
            "CurrentUser",
            "LocalMachine"
          ],
          "description": "For a new X509Secret: the location of the certificate store on the server. The default is CurrentUser."
        },
        "certificateStoreName": {
          "type": "string",
          "enum": [
            "My",
            "Root",
            "CertificateAuthority",
            "AuthRoot",
            "TrustedPeople",
            "TrustedPublisher",
            "AddressBook",
            "Disallowed"
          ],
          "description": "For a new X509Secret: the name of the certificate store on the server. The default is My."
        },
        "thumbprint": {
          "type": "string",
          "description": "For a new X509Secret: the thumbprint of the certificate in that store. A thumbprint is a public fingerprint of the certificate, not a secret."
        }
      },
      "required": [
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates an X.509 secret that points to a certificate in the server's certificate store, or updates any secret's description and expiry date. It cannot set a text value, a PEM certificate or a private key, and it does not create RSA key secrets; a person must enter or create those on the Secrets page, and the tool returns the link. Never ask for a secret value in the chat.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageSecrets))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return "Nothing was saved: give the name of the secret.";
        }

        name = name.Trim();

        arguments.TryGetFirstString("store", out var store);
        arguments.TryGetFirstString("type", out var type);

        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        var hasExpiresOn = arguments.TryGetFirstString("expiresOn", out var expiresOn);
        var removeExpiry = arguments.GetFirstValueOrDefault("removeExpiry", false);
        var hasCertificate = arguments.TryGetFirst("certificateStoreLocation", out _) ||
            arguments.TryGetFirst("certificateStoreName", out _) ||
            arguments.TryGetFirst("thumbprint", out _);

        DateTime? expiresUtc = null;

        if (hasExpiresOn)
        {
            if (removeExpiry)
            {
                return "Nothing was saved: give expiresOn or removeExpiry, not both.";
            }

            if (!DateTime.TryParseExact(expiresOn.Trim(), _localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return "Nothing was saved: expiresOn must be a local date as yyyy-MM-dd, or a local date and time as yyyy-MM-ddTHH:mm, without a time zone.";
            }

            expiresUtc = DateTime.SpecifyKind(await services.GetRequiredService<ILocalClock>().ConvertToUtcAsync(local), DateTimeKind.Utc);
        }

        var manager = services.GetRequiredService<ISecretManager>();

        return arguments.GetFirstValueOrDefault("create", false)
            ? await CreateAsync(services, manager, arguments, name, store?.Trim(), type?.Trim(), description, expiresUtc)
            : await UpdateAsync(services, manager, name, store, type, hasCertificate, hasDescription, description, hasExpiresOn, expiresUtc, removeExpiry);
    }

    private static async Task<object> CreateAsync(
        IServiceProvider services,
        ISecretManager manager,
        AIFunctionArguments arguments,
        string name,
        string store,
        string type,
        string description,
        DateTime? expiresUtc)
    {
        // The check of the Create page: no secret of any store has the name.
        if ((await manager.GetSecretInfosAsync()).Any(info => string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Nothing was saved: a secret named '{name}' exists. Leave create out to update its description or expiry date, or pick another name.";
        }

        if (string.Equals(type, SecretToolSupport.TextType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was saved: a text secret is created with its value, and a value never passes through the chat. A person creates the secret '{name}' on the Secrets admin page{SecretToolSupport.FormatLink(SecretToolSupport.GetCreateUrl(services, SecretToolSupport.TextType, name))}. Never ask for the value in the chat.";
        }

        if (string.Equals(type, SecretToolSupport.RsaKeyType, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was saved: this tool does not create RSA key secrets, because the Secrets admin page generates the key pair itself. A person creates the secret '{name}' there{SecretToolSupport.FormatLink(SecretToolSupport.GetCreateUrl(services, SecretToolSupport.RsaKeyType, name))}; no key has to be typed.";
        }

        if (!string.Equals(type, SecretToolSupport.X509Type, StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was saved: give type for a new secret. This tool creates X509Secret, a reference to a certificate in a certificate store of the server.";
        }

        var writableStores = SecretToolSupport.GetWritableStores(manager);

        // As on the Create page: the first writable store unless another is picked.
        store = string.IsNullOrEmpty(store)
            ? writableStores.FirstOrDefault()
            : writableStores.FirstOrDefault(candidate => string.Equals(candidate, store, StringComparison.OrdinalIgnoreCase));

        if (store is null)
        {
            return writableStores.Count == 0
                ? "Nothing was saved: the site has no writable secret store."
                : $"Nothing was saved: select a writable secret store. The writable stores are: {string.Join(", ", writableStores)}.";
        }

        var storeLocation = StoreLocation.CurrentUser;
        var storeName = StoreName.My;

        if (arguments.TryGetFirstString("certificateStoreLocation", out var locationText) &&
            (!Enum.TryParse(locationText.Trim(), true, out storeLocation) || !Enum.IsDefined(storeLocation)))
        {
            return "Nothing was saved: certificateStoreLocation must be CurrentUser or LocalMachine.";
        }

        if (arguments.TryGetFirstString("certificateStoreName", out var storeNameText) &&
            (!Enum.TryParse(storeNameText.Trim(), true, out storeName) || !Enum.IsDefined(storeName)))
        {
            return $"Nothing was saved: certificateStoreName must be one of {string.Join(", ", Enum.GetNames<StoreName>())}.";
        }

        // The check of the Create page: a certificate of a store is named by its thumbprint.
        if (!arguments.TryGetFirstString("thumbprint", out var thumbprint) || string.IsNullOrWhiteSpace(thumbprint))
        {
            return "Nothing was saved: give the thumbprint of the certificate.";
        }

        // A thumbprint is often copied with spaces or colons between its bytes.
        thumbprint = thumbprint.Replace(" ", string.Empty).Replace(":", string.Empty).Trim().ToUpperInvariant();

        if (thumbprint.Length is < 40 or > 128 || !thumbprint.All(Uri.IsHexDigit))
        {
            return "Nothing was saved: the thumbprint must be the hexadecimal fingerprint of the certificate, for example 40 characters for SHA-1.";
        }

        var x509Secret = new X509Secret
        {
            StoreLocation = storeLocation,
            StoreName = storeName,
            Thumbprint = thumbprint,
        };

        bool certificateFound;

        // Only whether the certificate is there is read; the certificate stays on the server.
        using (var certificate = x509Secret.GetCertificate())
        {
            certificateFound = certificate is not null;
        }

        await manager.SaveSecretAsync(name, Secret.FromValue(x509Secret), store, new SecretSaveOptions
        {
            Description = description,
            ExpiresUtc = expiresUtc,
        });

        var (saved, error) = await SecretToolSupport.FindAsync(manager, name, store);

        if (error is not null)
        {
            return $"Nothing was saved: {error}";
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            created = true,
            secret = await SecretToolSupport.DescribeAsync(services, saved),
            certificate = new
            {
                storeLocation = storeLocation.ToString(),
                storeName = storeName.ToString(),
                thumbprint,
                found = certificateFound,
            },
            warning = certificateFound
                ? null
                : "No certificate with this thumbprint is in that certificate store of this server now. A setting that uses the secret will not work until the certificate is installed there; check the thumbprint, the store and, on a web farm, every server.",
            note = "Reference the secret by its name in the setting that needs it.",
        });
    }

    private static async Task<object> UpdateAsync(
        IServiceProvider services,
        ISecretManager manager,
        string name,
        string store,
        string type,
        bool hasCertificate,
        bool hasDescription,
        string description,
        bool hasExpiresOn,
        DateTime? expiresUtc,
        bool removeExpiry)
    {
        var (info, error) = await SecretToolSupport.FindAsync(manager, name, store);

        if (error is not null)
        {
            return $"Nothing was saved: {error} Set create to true to create a secret.";
        }

        var editUrl = SecretToolSupport.GetEditUrl(services, info.Name, info.Store);

        // The type of a secret cannot be changed, and what a secret holds is changed by a person only.
        if (hasCertificate || (!string.IsNullOrEmpty(type) && !string.Equals(type.Trim(), SecretToolSupport.GetTypeName(info.Type), StringComparison.OrdinalIgnoreCase)))
        {
            return $"Nothing was saved: this tool changes the description and the expiry date of an existing secret only. What the secret '{info.Name}' holds is changed by a person on the Secrets admin page{SecretToolSupport.FormatLink(editUrl)}.";
        }

        if (!hasDescription && !hasExpiresOn && !removeExpiry)
        {
            return $"Nothing was saved: give description, expiresOn or removeExpiry. The value of a secret is changed by a person on the Secrets admin page{SecretToolSupport.FormatLink(editUrl)}; never ask for it in the chat.";
        }

        // The check of the Edit page.
        if (!SecretToolSupport.IsWritableStore(manager, info.Store))
        {
            return $"Nothing was saved: the secret store '{info.Store}' is read-only.";
        }

        // The stored secret is saved back as it is, as the Edit page does when its value fields are left empty. It is
        // read only to be written again and is in no result.
        var secret = await manager.GetSecretAsync(info.Name, info.Store);

        if (secret.Value is null)
        {
            return $"Nothing was saved: the secret '{info.Name}' could not be read from the store '{info.Store}', so it was left as it is.";
        }

        await manager.SaveSecretAsync(info.Name, secret, info.Store, new SecretSaveOptions
        {
            Description = hasDescription
                ? description
                : info.Description,
            ExpiresUtc = removeExpiry
                ? null
                : hasExpiresOn
                    ? expiresUtc
                    : info.ExpiresUtc,
        });

        var (saved, savedError) = await SecretToolSupport.FindAsync(manager, info.Name, info.Store);

        if (savedError is not null)
        {
            return $"Nothing was saved: {savedError}";
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            created = false,
            secret = await SecretToolSupport.DescribeAsync(services, saved),
            note = "The description and the expiry date were saved. What the secret holds was not changed.",
        });
    }
}
