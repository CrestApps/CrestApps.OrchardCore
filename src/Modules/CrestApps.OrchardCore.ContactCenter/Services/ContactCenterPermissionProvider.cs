using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the baseline permissions exposed by the Contact Center feature.
/// </summary>
internal sealed class ContactCenterPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        ContactCenterPermissions.ManageContactCenter,
        ContactCenterPermissions.ManageInteractions,
        ContactCenterPermissions.ViewInteractions,
        ContactCenterPermissions.ManageAgents,
        ContactCenterPermissions.ManageQueues,
        ContactCenterPermissions.ManageQueueGroups,
        ContactCenterPermissions.ManageSkills,
        ContactCenterPermissions.ManageDialer,
        ContactCenterPermissions.ManageVoiceMedia,
        ContactCenterPermissions.SignIntoQueues,
        ContactCenterPermissions.SecurePauseRecording,
        ContactCenterPermissions.InitiateSecureCapture,
        ContactCenterPermissions.MonitorContactCenter,
        ContactCenterPermissions.TransferExternally,
        ContactCenterPermissions.ViewReports,
        ContactCenterPermissions.InterveneInCalls,
        ContactCenterPermissions.AccessSharedVoicemail,
        ContactCenterPermissions.ManageSharedVoicemail,
    ];

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = "Agent",
                Permissions =
                [
                    ContactCenterPermissions.ViewInteractions,
                    ContactCenterPermissions.SignIntoQueues,
                    ContactCenterPermissions.SecurePauseRecording,
                    ContactCenterPermissions.InitiateSecureCapture,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    ContactCenterPermissions.ViewInteractions,
                    ContactCenterPermissions.MonitorContactCenter,
                    ContactCenterPermissions.TransferExternally,
                    ContactCenterPermissions.ViewReports,
                    ContactCenterPermissions.InterveneInCalls,

                    // A queue's shared voicemail is the team's, and not every agent on a queue should hear it, so
                    // agents are not granted it by default: a tenant grants it to the roles that answer the box.
                    ContactCenterPermissions.AccessSharedVoicemail,
                    ContactCenterPermissions.ManageSharedVoicemail,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
