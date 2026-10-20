// @ts-check
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
 * An ES module: the scripts that use it import it, the asset build bundles it into each of them, and the unit tests
 * import it directly.
 */

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

// Where a call the dialer placed for a held agent stands, in DialAttemptPhase order, for a client that ever receives
// the numeric form.
var DIAL_PHASE_NAMES = ['None', 'Dialing', 'Screening', 'Connecting'];

// The label key and English fallback of each phase, and of the line that tells the agent what is happening.
var DIAL_PHASE_LABELS = {
    Dialing: ['dialPhaseDialing', 'Dialing…', 'dialPhaseDialingHint', 'Calling the customer. You will be connected when they answer.'],
    Screening: ['dialPhaseScreening', 'Screening…', 'dialPhaseScreeningHint', 'The call was answered. Checking for an answering machine before connecting you.'],
    Connecting: ['dialPhaseConnecting', 'Connecting…', 'dialPhaseConnectingHint', 'A person answered. Connecting you now.']
};

// What a call the dialer is placing reads as when its dialer profile does not show its steps to the agent.
var DIAL_WAITING_LABEL = ['dialWaiting', 'Waiting for a connected call', 'dialWaitingHint', 'The dialer is calling for you. You will be connected when a person answers.'];

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

// The states whose work a break has to wait for, as the presence manager decides it: while the agent is reserved,
// on a call or in wrap-up (or holds an offer), choosing a break only requests it; otherwise it starts at once.
var WORKING_STATES = {
    Reserved: true,
    Busy: true,
    WrapUp: true
};

export function normalizePresenceStatus(value) {
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
export function presenceLabel(presence, labels) {
    presence = presence || {};

    var status = normalizePresenceStatus(presence.status);

    if (REASONED_STATES[status] && hasReason(presence.reason)) {
        return presence.reason.trim();
    }

    var entry = STATUS_LABELS[status];

    return entry ? text(labels, entry[0], entry[1]) : status;
}

// The phase of a call the dialer is placing for the agent (state.pendingDial), or '' when there is none.
export function normalizeDialPhase(pendingDial) {
    var value = pendingDial && pendingDial.phase;

    if (typeof value === 'number') {
        value = DIAL_PHASE_NAMES[value];
    } else if (typeof value === 'string' && /^\d+$/.test(value)) {
        value = DIAL_PHASE_NAMES[Number(value)];
    }

    return typeof value === 'string' && DIAL_PHASE_LABELS[value] ? value : '';
}

// Whether the agent is shown each step of the call the dialer is placing for them (its dialer profile's choice), or
// only that they are waiting for a connected call, as most contact-center dialers show it.
export function showsDialProgress(pendingDial) {
    return !!pendingDial && pendingDial.showsProgress !== false;
}

// What the call the dialer is placing for the agent reads as -- "Dialing…", "Screening…", "Connecting…", or "Waiting for
// a connected call" when its steps are not shown -- or ''.
export function dialPhaseLabel(pendingDial, labels) {
    var entry = DIAL_PHASE_LABELS[normalizeDialPhase(pendingDial)];

    if (!entry) {
        return '';
    }

    return showsDialProgress(pendingDial)
        ? text(labels, entry[0], entry[1])
        : text(labels, DIAL_WAITING_LABEL[0], DIAL_WAITING_LABEL[1]);
}

// The line under it that tells the agent what is happening and whether to wait, or ''.
export function dialPhaseHint(pendingDial, labels) {
    var entry = DIAL_PHASE_LABELS[normalizeDialPhase(pendingDial)];

    if (!entry) {
        return '';
    }

    return showsDialProgress(pendingDial)
        ? text(labels, entry[2], entry[3])
        : text(labels, DIAL_WAITING_LABEL[2], DIAL_WAITING_LABEL[3]);
}

// The agent's status as their screens show it. A power, progressive or predictive dial holds the agent Reserved while
// the customer is reached, and puts them on a call only once a person answers and they are connected. A dialer
// profile that shows its steps to the agent has the status say what the dialer is doing ("Dialing…", "Screening…",
// "Connecting…") instead; otherwise it reads "Reserved" until they are connected. presence: { status, reason,
// requestedStatus }; pendingDial: { phase, showsProgress } or null.
export function agentStatusLabel(presence, pendingDial, labels) {
    var phase = showsDialProgress(pendingDial) ? dialPhaseLabel(pendingDial, labels) : '';
    var status = normalizePresenceStatus((presence || {}).status);

    if (phase && (status === 'Reserved' || status === 'Busy')) {
        return phase;
    }

    return presenceLabel(presence, labels);
}

// What a break still waiting for the current work to end says, or '' when none is waiting.
export function pendingPresenceLabel(presence, labels) {
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

// What the presence menu calls its break choice: "Break" when picking one starts the break now, "Request break"
// while the agent's work holds it until that work ends. presence: { status, hasActiveReservation }.
export function breakChoiceLabel(presence, labels) {
    presence = presence || {};

    var waitsForWork = presence.hasActiveReservation === true ||
        WORKING_STATES[normalizePresenceStatus(presence.status)] === true;

    return waitsForWork
        ? text(labels, 'requestBreak', 'Request break')
        : text(labels, 'break', 'Break');
}

// Whether a presence notification is about the agent this page belongs to. A supervisor's connection also
// receives every other agent's changes; an unknown owner on either side is taken as this agent's own.
export function isOwnPresence(notification, ownUserId) {
    if (!notification) {
        return false;
    }

    if (!ownUserId || !notification.userId) {
        return true;
    }

    return String(notification.userId) === String(ownUserId);
}

// Whether an offer notification (an offer revoked, accepted or expired) is about this agent's own offer. The server
// also sends every offer's to its queue and to supervisors, for their views; acting on another agent's -- arming the
// phone to answer the accepted offer's leg -- had the next call to reach this phone answered by itself. An unknown
// owner on either side is taken as this agent's own, as before.
export function isOwnOfferNotification(notification, ownUserId) {
    return isOwnPresence(notification, ownUserId);
}
