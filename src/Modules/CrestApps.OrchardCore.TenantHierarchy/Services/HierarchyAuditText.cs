using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Puts the stored codes of a tenant hierarchy audit event into words.
/// </summary>
public static class HierarchyAuditText
{
    /// <summary>
    /// Returns the details of an event in words. A session event stores why the session ended as a reason code.
    /// </summary>
    /// <param name="T">The localizer of the view.</param>
    /// <param name="data">The event data.</param>
    /// <param name="labels">The words the parent uses for its child tenants.</param>
    public static string DescribeDetails(IHtmlLocalizer T, HierarchyAuditEvent data, HierarchyLabels labels)
    {
        ArgumentNullException.ThrowIfNull(T);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(labels);

        // An entry stores the roles the user received in the child tenant.
        if (data.Name == HierarchyAuditEventNames.Entered && !string.IsNullOrEmpty(data.Details))
        {
            return T.GetString("Roles: {0}", data.Details).Value;
        }

        if (data.Name != HierarchyAuditEventNames.SessionEnded)
        {
            return data.Details;
        }

        return data.Details switch
        {
            DelegatedSessionRules.Ended => T.GetString("Ended").Value,
            DelegatedSessionRules.IdleTimeout => T.GetString("Ended after a period without activity").Value,
            DelegatedSessionRules.LifetimeExceeded => T.GetString("Reached its maximum length").Value,
            DelegatedSessionRules.UserDisabled => T.GetString("The user was disabled").Value,
            DelegatedSessionRules.SecurityStampChanged => T.GetString("The user's password or security settings changed").Value,
            DelegatedSessionRules.GrantRemoved => T.GetString("The user's access was removed").Value,
            DelegatedSessionRules.ChildUnavailable => T.GetString("The {0} was unavailable", labels.ChildLower).Value,
            DelegatedSessionRules.SignedOut => T.GetString("The user signed out").Value,
            DelegatedSessionRules.SignedOutEverywhere => T.GetString("The user signed out of every {0}", labels.ChildLower).Value,
            DelegatedSessionRules.ChildSignOut => T.GetString("The user left the {0}", labels.ChildLower).Value,
            DelegatedSessionRules.ChildRemoved => T.GetString("The {0} was removed", labels.ChildLower).Value,
            _ => data.Details,
        };
    }

    /// <summary>
    /// Returns how the user signed in, from the authentication method references (RFC 8176) of the sign-in.
    /// </summary>
    /// <param name="T">The localizer of the view.</param>
    /// <param name="methods">The authentication method references.</param>
    public static string DescribeSignIn(IHtmlLocalizer T, IEnumerable<string> methods)
    {
        ArgumentNullException.ThrowIfNull(T);

        var words = (methods ?? [])
            .Select(method => method switch
            {
                "pwd" => T.GetString("password").Value,
                "mfa" => T.GetString("two-factor authentication").Value,
                "otp" => T.GetString("one-time code").Value,
                "sms" => T.GetString("text message code").Value,
                "hwk" or "swk" => T.GetString("security key").Value,
                "fido" => T.GetString("passkey").Value,
                "ext" or "fed" => T.GetString("external sign-in").Value,
                _ => method,
            })
            .Distinct()
            .ToList();

        return words.Count == 0
            ? null
            : T.GetString("Signed in with {0}", string.Join(", ", words)).Value;
    }
}
