// @ts-check
/*
 * The leg a supervisor's own soft phone is rung on to listen to, coach or join a Contact Center call.
 *
 * The platform rings the supervisor's registered browser with a leg that carries a one-off token -- in its client state
 * ({ i: 'cc-sv', l: token }) and in an X-Monitor-Leg SIP header, since the provider's SDK may not hand over client
 * state -- and tells the supervisor's phone, over the real-time channel, to expect that token first. The phone answers
 * that leg by itself and never rings it as a call: an arm names the one token it is for, is consumed by the leg that
 * uses it, and lapses. A monitor leg nobody armed for is not a call anybody asked for, so it is hung up rather than
 * rung; a leg without a monitor tag is never touched here, so every other call rings or auto-answers exactly as before.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */
import { parseWebRtcStats, windowedMicrophoneLevel } from './quality.js';
import { readProviderClientState, readProviderHeader } from './offer-leg.js';

// The intent the platform stamps on a supervisor's monitor leg.
export var MONITOR_LEG_INTENT = 'cc-sv';

// The SIP header carrying the token, for an SDK that hands over no client state.
export var MONITOR_LEG_HEADER = 'x-monitor-leg';

// How long an arm waits for its leg. The platform rings within a second or two of the supervisor's click.
export var MONITOR_ARM_WINDOW_MS = 30000;

// How long a monitor leg that arrived before its arm (the real-time message racing the provider's invite) waits for
// it before it is hung up.
export var MONITOR_ARM_WAIT_MS = 3000;

export function createMonitorLegArms() {
    return {};
}

// Arms for one token. `info` travels with the arm to whoever handles the answered leg (the agent, the mode).
export function armMonitorLeg(arms, token, now, info, windowMs) {
    if (!arms || !token) {
        return false;
    }

    arms[token] = {
        until: now + (typeof windowMs === 'number' && windowMs > 0 ? windowMs : MONITOR_ARM_WINDOW_MS),
        info: info || null
    };

    return true;
}

export function disarmMonitorLeg(arms, token) {
    if (arms && token && Object.prototype.hasOwnProperty.call(arms, token)) {
        delete arms[token];
    }
}

// The monitor tag of an inbound leg: { token, legId }, or null for any other leg. A leg that says it is a monitor leg
// but carries no token is still a monitor leg -- and, with nothing it could match, is never answered.
export function readMonitorLegTag(options) {
    if (!options) {
        return null;
    }

    var readState = readProviderClientState;
    var readHeader = readProviderHeader;
    var state = typeof readState === 'function' ? readState(options.clientState || options.client_state) : null;
    var header = typeof readHeader === 'function'
        ? readHeader(options.customHeaders || options.custom_headers, MONITOR_LEG_HEADER)
        : '';
    var isMonitor = !!(state && state.i === MONITOR_LEG_INTENT);

    if (!isMonitor && !header) {
        return null;
    }

    return {
        token: String((isMonitor && state.l) || header || ''),
        legId: options.telnyxCallControlId || options.callControlId || ''
    };
}

// Consumes the arm the leg was expected on, returning it; null when nothing armed for it or the arm lapsed.
export function claimMonitorLegArm(arms, tag, now) {
    if (!arms || !tag || !tag.token || !Object.prototype.hasOwnProperty.call(arms, tag.token)) {
        return null;
    }

    var arm = arms[tag.token];

    delete arms[tag.token];

    return now < arm.until ? arm : null;
}

// What to do with a monitor leg now: 'answer' (its arm is here), 'wait' (not yet, but its arm may still be on its way)
// or 'hangup'. Does not consume the arm.
export function monitorLegAction(arms, tag, now, arrivedAt) {
    if (!tag) {
        return 'none';
    }

    var arm = tag.token && arms && Object.prototype.hasOwnProperty.call(arms, tag.token) ? arms[tag.token] : null;

    if (arm && now < arm.until) {
        return 'answer';
    }

    return now - arrivedAt < MONITOR_ARM_WAIT_MS ? 'wait' : 'hangup';
}

// Whether a monitor leg takes the place of one this phone already holds for the same engagement. A supervisor who
// takes a call over is rung again with the engagement's token, on an ordinary leg the customer can be bridged to (the
// provider takes no command on the supervising leg itself); the phone answers it without an arm, since it is the
// engagement it is already on.
//   legs - the phone's monitor legs, by token: { legId }
//   tag  - the arriving leg's monitor tag: { token, legId }
export function monitorLegReplaces(legs, tag) {
    if (!legs || !tag || !tag.token || !Object.prototype.hasOwnProperty.call(legs, tag.token)) {
        return false;
    }

    var held = legs[tag.token];

    return !!(held && held.legId && tag.legId && held.legId !== tag.legId);
}

// The leg a supervisor took the call over on, taken out of the phone's monitor legs so it can become a call of the
// phone's own. Null when the phone holds no leg for `token` that can be promoted -- the take-over is missed, and the
// legs are left as they are.
//   legs - the phone's monitor legs, by token: { legId, controller: { promote } }
export function planMonitorLegPromotion(legs, token) {
    var leg = token && legs ? legs[token] : null;

    if (!leg || !leg.controller || typeof leg.controller.promote !== 'function') {
        return null;
    }

    delete legs[token];

    return leg;
}

// What promoting a monitor call does to the phone's calls: the call leaves the monitor calls whatever happens, and
// becomes the current call only when the phone has no other. A call that has ended, or a phone that is gone, has
// nothing left to promote. Returns { promoted, current }: the call the phone's current call is after it.
//   calls   - the phone's monitor calls
//   current - the phone's current call, or null
//   ended   - whether the call has ended or the phone is disposed
export function planMonitorCallPromotion(calls, call, current, ended) {
    var index = calls ? calls.indexOf(call) : -1;

    if (index >= 0) {
        calls.splice(index, 1);
    }

    if (ended) {
        return { promoted: false, current: current };
    }

    return { promoted: true, current: current || call };
}

// Whether the supervisor is heard on a monitor leg in `mode` (the engagement's mode, as the platform names it).
// Listening is silent: the platform joins the supervisor muted, and the phone keeps its microphone off too. Coaching
// is heard by the agent, and joining by everyone -- as is a call the supervisor took over, which is on as joined.
export function monitorLegTalks(mode) {
    return mode === 'Whisper' || mode === 'Barge';
}

// Whether any monitor leg this phone holds has the supervisor talking. The shared microphone is live while one does:
// the phone turns it off whenever it holds no call of its own, and a monitor leg is never one, so live, a supervisor
// who joined a call or took it over was heard by nobody.
//   legs - the phone's monitor legs, by token: { info: { mode } }
export function anyMonitorLegTalks(legs) {
    return Object.keys(legs || {}).some(function (token) {
        var leg = legs[token];

        return !!(leg && leg.info && monitorLegTalks(leg.info.mode));
    });
}

// Whether the phone holds a monitor leg. A monitor leg is live media like any call of the phone's own: live, a
// microphone that dropped while the supervisor listened found no call up, registered the phone again, and that hung
// the leg up and changed the address the next engagement was rung at (refused, 480).
//   legs - the phone's monitor legs, by token
export function holdsMonitorLeg(legs) {
    return Object.keys(legs || {}).some(function (token) {
        return !!legs[token];
    });
}

// What a monitor leg carries each way, from one reading of its stats (parseWebRtcStats) and the one before it. The
// levels are over the window between the two readings (-1 when the browser reports none): live, a supervisor heard
// nothing and only what the phone SENT was on record, so silence arriving could not be told from nothing arriving.
export function readMonitorLegMedia(report, previous) {
    var parse = parseWebRtcStats;
    var windowed = windowedMicrophoneLevel;
    var parsed = typeof parse === 'function' && report && typeof report.forEach === 'function' ? parse(report) : {};
    var inbound = parsed.inbound || null;
    var outbound = parsed.outbound || null;
    var mediaSource = parsed.mediaSource || null;
    var level = function (stat, before) {
        return typeof windowed === 'function' ? windowed(stat, before) : -1;
    };

    return {
        bytesSent: (outbound && outbound.bytesSent) || 0,
        bytesReceived: (inbound && inbound.bytesReceived) || 0,
        heardLevel: level(inbound, previous && previous.inbound),
        microphoneLevel: level(mediaSource, previous && previous.mediaSource),
        codec: parsed.codec || '',
        inbound: inbound,
        mediaSource: mediaSource
    };
}

// The line the server log gets for a monitor leg's media.
//   directions - each transceiver's direction and send track, as the phone reads them
export function describeMonitorLegMedia(directions, media) {
    var format = function (value) {
        return typeof value === 'number' && value >= 0 ? value.toFixed(3) : '-';
    };

    media = media || {};

    return 'Monitor leg media: transceivers ' + (directions || 'none') +
        ', sent ' + (media.bytesSent || 0) + ' bytes (mic ' + format(media.microphoneLevel) + ')' +
        ', received ' + (media.bytesReceived || 0) + ' bytes (heard ' + format(media.heardLevel) + ')' +
        ', codec ' + (media.codec || '-') + '.';
}
