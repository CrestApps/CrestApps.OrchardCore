using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Puts <see cref="EmailBoundConfirmationTokenProvider"/> in place of Orchard Core's e-mail confirmation token provider,
/// under the same name. It runs after Orchard Core's own configuration because this feature depends on Users.
/// </summary>
public sealed class EmailBoundConfirmationIdentityOptions : IConfigureOptions<IdentityOptions>
{
    private readonly EmailConfirmationTokenProviderOptions _tokenOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailBoundConfirmationIdentityOptions"/> class.
    /// </summary>
    /// <param name="tokenOptions">Orchard Core's options for its e-mail confirmation token.</param>
    public EmailBoundConfirmationIdentityOptions(IOptions<EmailConfirmationTokenProviderOptions> tokenOptions)
        => _tokenOptions = tokenOptions.Value;

    /// <inheritdoc/>
    public void Configure(IdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Tokens.EmailConfirmationTokenProvider = _tokenOptions.Name;
        options.Tokens.ProviderMap[_tokenOptions.Name] = new TokenProviderDescriptor(typeof(EmailBoundConfirmationTokenProvider));
    }
}
