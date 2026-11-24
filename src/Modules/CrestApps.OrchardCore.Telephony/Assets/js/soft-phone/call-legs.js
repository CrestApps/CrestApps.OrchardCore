// @ts-check
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
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */

// { platform, platformLegs, browser, browserLegs }: the latest leg carrying a server-tracked call and every such leg
// still up, the latest call placed from this browser's keypad, and every keypad call still up. The agent can place a
// second call while the first is on hold, and the first is no less this browser's for it: with only the latest
// remembered, the held call was forgotten the moment the second one ended, and its own hang-up was never heard.
export function createCallLegs() {
    return { platform: null, platformLegs: [], browser: null, browserLegs: [] };
}

export function notePlatformLeg(legs, leg) {
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

export function noteBrowserLeg(legs, leg) {
    if (legs && leg) {
        legs.browserLegs = legs.browserLegs || [];

        if (legs.browserLegs.indexOf(leg) < 0) {
            legs.browserLegs.push(leg);
        }

        legs.browser = leg;
    }
}

// The call the server reported live on a platform leg, or '' while it has reported none. A Contact Center call is
// reported by the caller's leg, never the agent's, so this is what ties the agent's leg to the call it carries.
export function boundCallOf(legs, leg) {
    var bindings = (legs && legs.bindings) || [];

    for (var i = 0; i < bindings.length; i++) {
        if (bindings[i].leg === leg) {
            return bindings[i].callId;
        }
    }

    return '';
}

function legBoundTo(legs, callId) {
    var bindings = (legs && legs.bindings) || [];

    for (var i = 0; i < bindings.length; i++) {
        if (bindings[i].callId === callId) {
            return bindings[i].leg;
        }
    }

    return null;
}

function unbind(legs, leg) {
    if (legs && legs.bindings) {
        legs.bindings = legs.bindings.filter(function (binding) {
            return binding.leg !== leg;
        });
    }
}

// Ties a platform leg to the call it carries; a leg carries one call, and a call is carried by one leg.
export function bindPlatformLeg(legs, leg, callId) {
    if (!legs || !leg || !callId) {
        return;
    }

    var id = String(callId);

    legs.bindings = ((legs.bindings) || []).filter(function (binding) {
        return binding.leg !== leg && binding.callId !== id;
    });
    legs.bindings.push({ leg: leg, callId: id });
}

// The platform legs that are up but carry no call the server has reported yet: a leg parked for a dialer call while the
// customer rings, or one just joined to its caller whose report is still on its way. isLive(leg) says whether a leg is
// up and answered.
export function unreportedLiveLegs(legs, isLive) {
    var live = typeof isLive === 'function' ? isLive : function () { return true; };

    return ((legs && legs.platformLegs) || []).filter(function (leg) {
        return !!leg && !boundCallOf(legs, leg) && live(leg);
    });
}

// The provider recovered a call across a reconnect as a new object under the same id: the new object takes the old
// one's place, and the call it carried, so the server's reports reach the leg that is up rather than the dead one.
export function replaceLeg(legs, oldLeg, newLeg) {
    if (!legs || !oldLeg || !newLeg || oldLeg === newLeg) {
        return;
    }

    ['platformLegs', 'browserLegs'].forEach(function (key) {
        var index = legs[key] ? legs[key].indexOf(oldLeg) : -1;

        if (index >= 0) {
            legs[key][index] = newLeg;
        }
    });

    if (legs.platform === oldLeg) {
        legs.platform = newLeg;
    }

    if (legs.browser === oldLeg) {
        legs.browser = newLeg;
    }

    (legs.bindings || []).forEach(function (binding) {
        if (binding.leg === oldLeg) {
            binding.leg = newLeg;
        }
    });
}

export function forgetLeg(legs, leg) {
    if (!legs || !leg) {
        return;
    }

    unbind(legs, leg);

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
export function platformLegFor(legs, callId) {
    var candidates = (legs && legs.platformLegs) || [];
    var id = callId ? String(callId) : '';

    if (id) {
        for (var i = 0; i < candidates.length; i++) {
            if (legCallId(candidates[i]) === id) {
                return candidates[i];
            }
        }

        var bound = legBoundTo(legs, id);

        if (bound && candidates.indexOf(bound) >= 0) {
            return bound;
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
//   view      - optional: { wasShown(callId), isShown(callId) }, whether the phone showed a call before this report
//               and whether it shows it now. Without it, no call counts as shown.
// Returns { action: 'none' | 'hold' | 'resume' | 'hangup', leg }. A keypad call is never the leg: the server
// cannot know when it ends and has no hold of it to report.
//
// Regression: agents on Power campaigns answered, said hello, and the call dropped. The agent's leg carries a call the
// server reports by the customer's leg, so every report that named no leg here fell through to the latest platform leg,
// and a report that a call had ended hung that leg up -- whichever call it was about. A late "ended" for the previous
// dial, an orphaned history entry the server swept, or an active-calls refresh that did not list the dialer call yet
// (the server lists it only once the agent is joined) each hung up the live leg of the call the agent had just
// answered. The leg is now tied to the first call reported live on it, and an end hangs up only the leg carrying that
// call, or a leg tied to nothing when the call that ended is the one the phone was showing. The server releases the
// agent's leg itself when a Contact Center call ends, so a leg left alone here is not left up.
export function planPlatformReport(legs, stateName, callId, view) {
    noteReportedCall(legs, callId);

    var id = callId ? String(callId) : '';
    var terminal = !stateName || stateName === 'Disconnected' || stateName === 'Failed';
    var live = stateName === 'Connected' || stateName === 'OnHold';
    var wasShown = function (shownId) { return !!(view && typeof view.wasShown === 'function' && view.wasShown(shownId)); };
    var isShown = function (shownId) { return !(view && typeof view.isShown === 'function') || !!view.isShown(shownId); };
    var leg = platformLegFor(legs, callId);

    if (!leg) {
        return { action: 'none', leg: null };
    }

    var ownCall = !!id && (legCallId(leg) === id || boundCallOf(legs, leg) === id);

    if (!ownCall) {
        var bound = boundCallOf(legs, leg);

        if (!id) {
            // The server lists no call: a leg ends only once the call it was carrying, which the phone showed, has gone.
            // One that carries nothing reported yet -- parked for a dialer call, or joined a moment ago -- stays up.
            if (terminal && !(bound && wasShown(bound) && !isShown(bound))) {
                return { action: 'none', leg: null };
            }
        } else if (bound) {
            // The leg carries another call. A live report of a new call takes it over only once the phone no longer
            // shows the call it carried; anything else about some other call is not about this leg.
            if (!live || isShown(bound)) {
                return { action: 'none', leg: null };
            }

            bindPlatformLeg(legs, leg, id);
        } else if (terminal) {
            // The leg carries no reported call: an end names it only when the call that ended is the one on screen.
            if (!wasShown(id)) {
                return { action: 'none', leg: null };
            }
        } else if (live) {
            bindPlatformLeg(legs, leg, id);
        }
    }

    if (terminal) {
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
export function legAfterEnd(legs, endedLeg) {
    forgetLeg(legs, endedLeg);

    return (legs && (legs.platform || legs.browser)) || null;
}

// Every call this browser holds: the platform leg and each keypad call, once each.
export function allLegs(legs) {
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
export function canConferenceCall(call) {
    return !!(call && call.callId && !call.browserOriginated);
}
