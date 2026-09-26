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
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The intent the platform stamps on a supervisor's monitor leg.
    var MONITOR_LEG_INTENT = 'cc-sv';

    // The SIP header carrying the token, for an SDK that hands over no client state.
    var MONITOR_LEG_HEADER = 'x-monitor-leg';

    // How long an arm waits for its leg. The platform rings within a second or two of the supervisor's click.
    var MONITOR_ARM_WINDOW_MS = 30000;

    // How long a monitor leg that arrived before its arm (the real-time message racing the provider's invite) waits for
    // it before it is hung up.
    var MONITOR_ARM_WAIT_MS = 3000;

    function createMonitorLegArms() {
        return {};
    }

    // Arms for one token. `info` travels with the arm to whoever handles the answered leg (the agent, the mode).
    function armMonitorLeg(arms, token, now, info, windowMs) {
        if (!arms || !token) {
            return false;
        }

        arms[token] = {
            until: now + (typeof windowMs === 'number' && windowMs > 0 ? windowMs : MONITOR_ARM_WINDOW_MS),
            info: info || null
        };

        return true;
    }

    function disarmMonitorLeg(arms, token) {
        if (arms && token && Object.prototype.hasOwnProperty.call(arms, token)) {
            delete arms[token];
        }
    }

    // The monitor tag of an inbound leg: { token, legId }, or null for any other leg. A leg that says it is a monitor leg
    // but carries no token is still a monitor leg -- and, with nothing it could match, is never answered.
    function readMonitorLegTag(options) {
        if (!options) {
            return null;
        }

        var readState = softPhone.readProviderClientState;
        var readHeader = softPhone.readProviderHeader;
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
    function claimMonitorLegArm(arms, tag, now) {
        if (!arms || !tag || !tag.token || !Object.prototype.hasOwnProperty.call(arms, tag.token)) {
            return null;
        }

        var arm = arms[tag.token];

        delete arms[tag.token];

        return now < arm.until ? arm : null;
    }

    // What to do with a monitor leg now: 'answer' (its arm is here), 'wait' (not yet, but its arm may still be on its way)
    // or 'hangup'. Does not consume the arm.
    function monitorLegAction(arms, tag, now, arrivedAt) {
        if (!tag) {
            return 'none';
        }

        var arm = tag.token && arms && Object.prototype.hasOwnProperty.call(arms, tag.token) ? arms[tag.token] : null;

        if (arm && now < arm.until) {
            return 'answer';
        }

        return now - arrivedAt < MONITOR_ARM_WAIT_MS ? 'wait' : 'hangup';
    }

    softPhone.MONITOR_LEG_INTENT = MONITOR_LEG_INTENT;
    softPhone.MONITOR_LEG_HEADER = MONITOR_LEG_HEADER;
    softPhone.MONITOR_ARM_WINDOW_MS = MONITOR_ARM_WINDOW_MS;
    softPhone.MONITOR_ARM_WAIT_MS = MONITOR_ARM_WAIT_MS;
    softPhone.createMonitorLegArms = createMonitorLegArms;
    softPhone.armMonitorLeg = armMonitorLeg;
    softPhone.disarmMonitorLeg = disarmMonitorLeg;
    softPhone.readMonitorLegTag = readMonitorLegTag;
    softPhone.claimMonitorLegArm = claimMonitorLegArm;
    softPhone.monitorLegAction = monitorLegAction;
}(typeof globalThis !== 'undefined' ? globalThis : window));
