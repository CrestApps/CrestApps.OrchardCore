using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The ids a tool knows people by (the <c>sub</c> of its launches, the <c>user_id</c> of its roster and the <c>userId</c>
/// of its scores): an HMAC-SHA256 of the user id with the tool's own salt, in base64url. Stable per tool and person,
/// different for each tool, and not the user id, which a tool never learns.
/// </summary>
public static class LtiSubjects
{
    /// <summary>
    /// Makes a new salt for a tool.
    /// </summary>
    /// <returns>32 random bytes in base64.</returns>
    public static string NewSalt()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Returns the id a tool knows a person by.
    /// </summary>
    /// <param name="tool">The tool, with its salt.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The id.</returns>
    public static string For(ExternalTool tool, string userId)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        if (string.IsNullOrEmpty(tool.SubjectSalt))
        {
            throw new InvalidOperationException($"The external tool '{tool.ItemId}' has no subject salt.");
        }

        var hash = HMACSHA256.HashData(Convert.FromBase64String(tool.SubjectSalt), Encoding.UTF8.GetBytes("lti-sub:" + userId));

        return Base64Url.EncodeToString(hash);
    }
}
