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

    // { platform, platformLegs, browser, browserLegs }: the latest leg carrying a server-tracked call and every such leg
    // still up, the latest call placed from this browser's keypad, and every keypad call still up. The agent can place a
    // second call while the first is on hold, and the first is no less this browser's for it: with only the latest
    // remembered, the held call was forgotten the moment the second one ended, and its own hang-up was never heard.
    function createCallLegs() {
        return { platform: null, platformLegs: [], browser: null, browserLegs: [] };
    }

    function notePlatformLeg(legs, leg) {
        if (legs && leg) {
            legs.platformLegs = legs.platformLegs || [];

            if (legs.platformLegs.indexOf(leg) < 0) {
                legs.platformLegs.push(leg);
            }

            legs.platform = leg;
        }
    }

    // The provider's id for a leg, as the platform names the call it carries: the leg the platform rang for a number
    // dialed here, or for an extension call, is the call the server tracks.
    function legCallId(leg) {
        var options = (leg && leg.options) || {};

        return String(options.telnyxCallControlId || options.callControlId || '');
    }

    function noteBrowserLeg(legs, leg) {
        if (legs && leg) {
            legs.browserLegs = legs.browserLegs || [];

            if (legs.browserLegs.indexOf(leg) < 0) {
                legs.browserLegs.push(leg);
            }

            legs.browser = leg;
        }
    }

    function forgetLeg(legs, leg) {
        if (!legs || !leg) {
            return;
        }

        var platformIndex = legs.platformLegs ? legs.platformLegs.indexOf(leg) : -1;

        if (platformIndex >= 0) {
            legs.platformLegs.splice(platformIndex, 1);
        }

        if (legs.platform === leg) {
            legs.platform = legs.platformLegs && legs.platformLegs.length
                ? legs.platformLegs[legs.platformLegs.length - 1]
                : null;
        }

        var index = legs.browserLegs ? legs.browserLegs.indexOf(leg) : -1;

        if (index >= 0) {
            legs.browserLegs.splice(index, 1);
        }

        if (legs.browser === leg) {
            legs.browser = legs.browserLegs && legs.browserLegs.length
                ? legs.browserLegs[legs.browserLegs.length - 1]
                : null;
        }
    }

    // The leg a report about the platform call `callId` is about. Several calls the server tracks can be up at once --
    // numbers dialed from the keypad and connected by the platform each ring a leg of their own -- and a report about
    // one of them applied to whichever leg arrived last held, resumed or hung up the wrong call. A leg whose own id is
    // the call's is that call's. A Contact Center call is reported by the caller's leg, which is not the agent's, so a
    // report that names no leg here goes to the latest platform leg -- unless that leg is already known to carry a
    // call of its own.
    function platformLegFor(legs, callId) {
        var candidates = (legs && legs.platformLegs) || [];
        var id = callId ? String(callId) : '';

        if (id) {
            for (var i = 0; i < candidates.length; i++) {
                if (legCallId(candidates[i]) === id) {
                    return candidates[i];
                }
            }
        }

        var latest = (legs && legs.platform) || null;

        if (latest && id && legs.reportedCallIds && legs.reportedCallIds.indexOf(legCallId(latest)) >= 0) {
            return null;
        }

        return latest;
    }

    // Remembers that the server reported a call by this id, so its leg is known to carry that call.
    function noteReportedCall(legs, callId) {
        if (legs && callId) {
            legs.reportedCallIds = legs.reportedCallIds || [];

            if (legs.reportedCallIds.indexOf(String(callId)) < 0) {
                legs.reportedCallIds.push(String(callId));
            }
        }
    }

    // What a report about the platform call asks of the media.
    //   stateName - the reported state ('Connected', 'OnHold', 'Disconnected', ...), or null when the server reports
    //               no call at all.
    //   callId    - the call the report is about, when it names one.
    // Returns { action: 'none' | 'hold' | 'resume' | 'hangup', leg }. A keypad call is never the leg: the server
    // cannot know when it ends and has no hold of it to report.
    function planPlatformReport(legs, stateName, callId) {
        noteReportedCall(legs, callId);

        var leg = platformLegFor(legs, callId);

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
    // when a keypad call placed on top of it has ended, so the platform leg's own events and hang-up are heard again;
    // else the latest keypad call still up, such as the one the agent held to place the call that just ended.
    function legAfterEnd(legs, endedLeg) {
        forgetLeg(legs, endedLeg);

        return (legs && (legs.platform || legs.browser)) || null;
    }

    // Every call this browser holds: the platform leg and each keypad call, once each.
    function allLegs(legs) {
        var all = [];

        if (!legs) {
            return all;
        }

        [legs.platform].concat(legs.platformLegs || [], [legs.browser], legs.browserLegs || []).forEach(function (leg) {
            if (leg && all.indexOf(leg) < 0) {
                all.push(leg);
            }
        });

        return all;
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
    softPhone.platformLegFor = platformLegFor;
    softPhone.legAfterEnd = legAfterEnd;
    softPhone.allLegs = allLegs;
    softPhone.canConferenceCall = canConferenceCall;
}(typeof globalThis !== 'undefined' ? globalThis : window));
