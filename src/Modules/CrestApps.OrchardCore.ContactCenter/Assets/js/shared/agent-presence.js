/*
 * How an agent's Contact Center presence reads on the agent's own screens.
 *
 * The soft phone header, the agent workspace and the docked agent bar each show the agent's state. They used to
 * print the reason if there was one and otherwise look the state up among the menu's buttons, so a break taken with
 * no reason read as the first break reason in the menu ("Short break") while the audit record said plain "Break", and
 * a state with no button of its own (on a call, wrap-up) printed the raw enum name. These are the pure decisions:
 * which label a state gets, when its reason is worth showing, what a pending break says, and whether a presence
 * notification is about this agent at all -- a supervisor's connection also receives every other agent's changes.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundles and under the unit tests.
 */
(function (root) {
    'use strict';

    var contactCenter = root.CrestAppsContactCenter = root.CrestAppsContactCenter || {};

    // In AgentPresenceStatus order, for a client that ever receives the numeric form.
    var STATUS_NAMES = [
        'Offline',
        'Available',
        'Reserved',
        'Busy',
        'WrapUp',
        'Break',
        'RequestBreak',
        'Away',
        'DoNotDisturb',
        'Meeting',
        'Training',
        'AfterHoursUnavailable'
    ];

    // The label key and English fallback for each state.
    var STATUS_LABELS = {
        Offline: ['offline', 'Offline'],
        Available: ['available', 'Available'],
        Reserved: ['reserved', 'Reserved'],
        Busy: ['busy', 'On a call'],
        WrapUp: ['wrapUp', 'Wrap-up'],
        Break: ['break', 'Break'],
        RequestBreak: ['breakPending', 'Break pending'],
        Away: ['away', 'Away'],
        DoNotDisturb: ['doNotDisturb', 'Do not disturb'],
        Meeting: ['meeting', 'Meeting'],
        Training: ['training', 'Training'],
        AfterHoursUnavailable: ['afterHoursUnavailable', 'After-hours unavailable']
    };

    // The not-ready states an agent chooses with a reason. Only these show it: a reason left on the profile while the
    // agent is reserved, on a call or in wrap-up belongs to a break still waiting to start, not to the current state.
    var REASONED_STATES = {
        Break: true,
        Away: true,
        DoNotDisturb: true,
        Meeting: true,
        Training: true,
        AfterHoursUnavailable: true
    };

    function normalizePresenceStatus(value) {
        if (typeof value === 'number') {
            return STATUS_NAMES[value] || 'Offline';
        }

        if (typeof value === 'string' && value) {
            if (/^\d+$/.test(value)) {
                return STATUS_NAMES[Number(value)] || 'Offline';
            }

            return value;
        }

        return 'Offline';
    }

    function text(labels, key, fallback) {
        var value = labels && labels[key];

        return typeof value === 'string' && value ? value : fallback;
    }

    function hasReason(reason) {
        return typeof reason === 'string' && reason.trim().length > 0;
    }

    // The label for the agent's current state. presence: { status, reason, requestedStatus }.
    function presenceLabel(presence, labels) {
        presence = presence || {};

        var status = normalizePresenceStatus(presence.status);

        if (REASONED_STATES[status] && hasReason(presence.reason)) {
            return presence.reason.trim();
        }

        var entry = STATUS_LABELS[status];

        return entry ? text(labels, entry[0], entry[1]) : status;
    }

    // What a break still waiting for the current work to end says, or '' when none is waiting.
    function pendingPresenceLabel(presence, labels) {
        presence = presence || {};

        var status = normalizePresenceStatus(presence.status);
        var requested = presence.requestedStatus == null || presence.requestedStatus === ''
            ? ''
            : normalizePresenceStatus(presence.requestedStatus);

        if (requested !== 'Break' || status === 'Break' || status === 'RequestBreak') {
            return '';
        }

        if (hasReason(presence.reason)) {
            return text(labels, 'breakPendingWithReason', 'Break pending: {0}').replace('{0}', presence.reason.trim());
        }

        return text(labels, 'breakPending', 'Break pending');
    }

    // Whether a presence notification is about the agent this page belongs to. A supervisor's connection also
    // receives every other agent's changes; an unknown owner on either side is taken as this agent's own.
    function isOwnPresence(notification, ownUserId) {
        if (!notification) {
            return false;
        }

        if (!ownUserId || !notification.userId) {
            return true;
        }

        return String(notification.userId) === String(ownUserId);
    }

    contactCenter.normalizePresenceStatus = normalizePresenceStatus;
    contactCenter.presenceLabel = presenceLabel;
    contactCenter.pendingPresenceLabel = pendingPresenceLabel;
    contactCenter.isOwnPresence = isOwnPresence;
}(typeof globalThis !== 'undefined' ? globalThis : window));
