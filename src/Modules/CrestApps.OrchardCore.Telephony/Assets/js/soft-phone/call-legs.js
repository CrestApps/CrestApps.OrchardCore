/*
 * Which provider call in this browser a report about a platform call applies to.
 *
 * The browser can hold two kinds of provider call at once. One carries the media of a call the server tracks: the leg
 * the platform rang for a Contact Center offer (pre-dialed while the offer rang, or rung at the accept), or the agent's
 * own leg of an extension call. The server is the authority on that call, and its reports -- held, connected, ended,
 * or no longer there -- are instructions for that leg. The other kind is a call the agent placed from the keypad, which
 * the server knows nothing about and whose own provider session is its only authority.
 *
 * The media adapter used to keep a single "current" call for both. Placing a keypad call while a platform call was on
 * hold made the keypad call current, so from then on every report about the platform call went to the keypad call:
 * resuming unheld the wrong call, and when the agent hung up the platform call its leg stayed up, because the adapter
 * also still believed its current call was browser-placed and treated the server's "no call" as silence. These are the
 * pure decisions that keep the two apart.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // { platform, browser }: the leg carrying a server-tracked call, and a call placed from this browser's keypad.
    function createCallLegs() {
        return { platform: null, browser: null };
    }

    function notePlatformLeg(legs, leg) {
        if (legs && leg) {
            legs.platform = leg;
        }
    }

    function noteBrowserLeg(legs, leg) {
        if (legs && leg) {
            legs.browser = leg;
        }
    }

    function forgetLeg(legs, leg) {
        if (!legs || !leg) {
            return;
        }

        if (legs.platform === leg) {
            legs.platform = null;
        }

        if (legs.browser === leg) {
            legs.browser = null;
        }
    }

    // What a report about the platform call asks of the media.
    //   stateName - the reported state ('Connected', 'OnHold', 'Disconnected', ...), or null when the server reports
    //               no call at all.
    // Returns { action: 'none' | 'hold' | 'resume' | 'hangup', leg }. A keypad call is never the leg: the server
    // cannot know when it ends and has no hold of it to report.
    function planPlatformReport(legs, stateName) {
        var leg = (legs && legs.platform) || null;

        if (!leg) {
            return { action: 'none', leg: null };
        }

        if (!stateName || stateName === 'Disconnected' || stateName === 'Failed') {
            return { action: 'hangup', leg: leg };
        }

        if (stateName === 'OnHold') {
            return { action: 'hold', leg: leg };
        }

        if (stateName === 'Connected') {
            return { action: 'resume', leg: leg };
        }

        return { action: 'none', leg: leg };
    }

    // Forgets a leg that ended and returns the one that should now be the adapter's current call: the platform leg,
    // when a keypad call placed on top of it has ended, so the platform leg's own events and hang-up are heard again.
    function legAfterEnd(legs, endedLeg) {
        forgetLeg(legs, endedLeg);

        return (legs && legs.platform) || null;
    }

    // Whether a call may be offered for a conference. Merging is a server command over calls the server tracks; a
    // call this browser placed itself cannot be part of one, so it is never offered.
    function canConferenceCall(call) {
        return !!(call && call.callId && !call.browserOriginated);
    }

    softPhone.createCallLegs = createCallLegs;
    softPhone.notePlatformLeg = notePlatformLeg;
    softPhone.noteBrowserLeg = noteBrowserLeg;
    softPhone.forgetLeg = forgetLeg;
    softPhone.planPlatformReport = planPlatformReport;
    softPhone.legAfterEnd = legAfterEnd;
    softPhone.canConferenceCall = canConferenceCall;
}(typeof globalThis !== 'undefined' ? globalThis : window));
