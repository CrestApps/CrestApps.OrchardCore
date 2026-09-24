/*
 * Keeping a hold the agent placed until the agent resumes it.
 *
 * With a provider that delivers the call's audio to this browser, hold happens here: the phone swaps the microphone
 * for hold audio and the provider is never told. So everything the server says about the call afterwards -- the
 * provider's own view of it, the periodic active-call refresh, a state push when anything else about the call changes
 * -- still describes it as connected. The phone used to take each of those reports at its word, set the call back to
 * connected and hand that to the media adapter, which dutifully took the caller off hold a few seconds after the
 * agent put them on it.
 *
 * The phone now remembers the holds the agent placed. A report that the call is connected does not end such a hold
 * when the hold is performed in this browser; only the agent resuming the call, or the call ending, does. Where the
 * provider performs the hold itself, the provider is the authority, and a report that the call is connected again
 * means the hold is over.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function isUp(stateName) {
        return stateName === 'Connected' || stateName === 'OnHold';
    }

    // States a call passes through before it is first answered.
    function isPreConnect(stateName) {
        return stateName === 'Ringing' || stateName === 'Connecting';
    }

    // The state to take from a report about a call the phone already knows.
    //
    // A call that has been up does not start ringing again. A report that says it has is describing something that
    // has not caught up with the call: the Contact Center describes a call from its interaction, and an interaction
    // the server never moved past ringing kept reporting "Ringing" for a call the agent had been talking on for a
    // minute. Taken at its word, that reopened the ringing panel over a live call and made the phone forget the hold
    // the agent had placed on it. Such a report leaves the call as it was; every other report is taken as it comes.
    function resolveReportedState(previousStateName, reportedStateName) {
        if (isUp(previousStateName) && isPreConnect(reportedStateName)) {
            return previousStateName;
        }

        return reportedStateName;
    }

    // Remembers (onHold true) or forgets (false) the agent's hold on a call.
    function rememberAgentHold(holds, callId, onHold) {
        if (!holds || !callId) {
            return;
        }

        if (onHold) {
            holds[callId] = true;
        } else {
            delete holds[callId];
        }
    }

    function isAgentHeld(holds, callId) {
        return !!(holds && callId && holds[callId] === true);
    }

    // Forgets the holds on every call that is no longer live, so the memory cannot outlast the calls it is about.
    function pruneAgentHolds(holds, liveCallIds) {
        if (!holds) {
            return;
        }

        var live = {};

        (liveCallIds || []).forEach(function (callId) {
            live[callId] = true;
        });

        Object.keys(holds).forEach(function (callId) {
            if (!live[callId]) {
                delete holds[callId];
            }
        });
    }

    // What the phone should show for a call a report describes.
    //   stateName      - the state the report gives (already normalized: 'Connected', 'OnHold', ...).
    //   reportedOnHold - the report's own hold flag.
    //   agentHeld      - whether the agent placed a hold on this call that has not been resumed.
    //   holdIsLocal    - whether this browser performs the hold (the provider cannot see it).
    // Returns { stateName, isOnHold, agentHeld }: the state and hold flag to apply, and whether the agent's hold
    // still stands afterwards.
    function reconcileAgentHold(stateName, reportedOnHold, agentHeld, holdIsLocal) {
        var reportedHeld = stateName === 'OnHold' || (!!reportedOnHold && isUp(stateName));

        // A call the agent held has been up, so a report that it is ringing or connecting is stale (see
        // resolveReportedState), not the end of the hold. Forgetting the hold here is what took the caller off the
        // first hold of a call a few seconds after the agent placed it: the next report said "Connected".
        if (agentHeld && isPreConnect(stateName)) {
            return { stateName: 'OnHold', isOnHold: true, agentHeld: true };
        }

        // A call that is not up has no hold to keep: it is still ringing, or it is over.
        if (!isUp(stateName)) {
            return { stateName: stateName, isOnHold: false, agentHeld: false };
        }

        if (!agentHeld) {
            return { stateName: stateName, isOnHold: reportedHeld, agentHeld: false };
        }

        // The report agrees the call is held, or it cannot know: this browser is the one holding it.
        if (reportedHeld || holdIsLocal) {
            return { stateName: 'OnHold', isOnHold: true, agentHeld: true };
        }

        // The provider holds the call itself, and says it is connected again: the hold is over.
        return { stateName: stateName, isOnHold: false, agentHeld: false };
    }

    // Where the agent's hold or resume of a call goes. Both commands use this one decision, so they can never take
    // different paths for the same call.
    //   'hub'   - a call the server tracks. The server records the hold time and hands the new state back to the
    //             media adapter, which swaps the hold audio in or out on the leg carrying the call.
    //   'local' - a call this browser placed itself: there is nothing on the server to tell, so its own session holds.
    //   'none'  - nothing to act on.
    function holdCommandRoute(call, hasBrowserController) {
        if (!call || !call.callId) {
            return 'none';
        }

        if (!call.browserOriginated) {
            return 'hub';
        }

        return hasBrowserController ? 'local' : 'none';
    }

    softPhone.resolveReportedState = resolveReportedState;
    softPhone.holdCommandRoute = holdCommandRoute;
    softPhone.rememberAgentHold = rememberAgentHold;
    softPhone.isAgentHeld = isAgentHeld;
    softPhone.pruneAgentHolds = pruneAgentHolds;
    softPhone.reconcileAgentHold = reconcileAgentHold;
}(typeof globalThis !== 'undefined' ? globalThis : window));
