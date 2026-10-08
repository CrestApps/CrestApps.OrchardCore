/*
 * Keeping track of the calls this browser placed itself.
 *
 * A call dialed from the keypad (or a colleague's direct call the agent answered) runs entirely in the provider SDK.
 * The platform never sees it, so the phone's own reports are the only record it gets: the call was placed, it is
 * still up, it ended. Every one of those calls has to report its end exactly once, whatever happened to it.
 *
 * The media adapter used to keep a single state callback for "the" outbound call. Dialing a second number while the
 * first call was on hold handed that callback to the second call, so when the first call ended the phone was never
 * told: its end never reached the server (the history stayed "in progress" for good) and the phone went on showing
 * it in a call. The same happened to a call the SDK dropped across a socket reconnect without a word, and to a call
 * that was live when the page went away. These are the pure decisions that keep every call accounted for:
 *
 *   - per-call state callbacks, keyed by the provider's call id, which deliver the end once and then forget the call;
 *   - which tracked calls the SDK no longer has, so a reconnect can end them instead of leaving them on screen;
 *   - which of the calls on screen still have a live session behind them;
 *   - a small journal, kept in the tab's session storage, of calls whose end has not been confirmed to the server yet,
 *     so a reload or a hub outage cannot lose one;
 *   - when to tell the server which calls are still up, so it can settle one the phone stopped reporting.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // ---- Per-call state callbacks ----

    function createCallNotifiers() {
        return { entries: {} };
    }

    // Registers the callback that owns a call's state. A call is tracked until its end has been delivered.
    function trackCall(notifiers, callId, notify) {
        if (!notifiers || !callId || typeof notify !== 'function') {
            return false;
        }

        notifiers.entries[callId] = notify;

        return true;
    }

    function isCallTracked(notifiers, callId) {
        return !!(notifiers && callId && Object.prototype.hasOwnProperty.call(notifiers.entries, callId));
    }

    function trackedCallIds(notifiers) {
        return notifiers ? Object.keys(notifiers.entries) : [];
    }

    // Hands a soft-phone state ('Ringing', 'Connected', 'Disconnected') to the callback of the call it is about. The end
    // is delivered once: the call is forgotten before its callback runs, so a second hang-up notification, or a sweep
    // that finds the same call gone, finds nothing left to end. Returns whether a callback was told.
    function deliverCallState(notifiers, callId, stateName) {
        if (!stateName || !isCallTracked(notifiers, callId)) {
            return false;
        }

        var notify = notifiers.entries[callId];

        if (stateName === 'Disconnected') {
            delete notifiers.entries[callId];
        }

        try {
            notify(stateName);
        } catch (error) { /* a failing callback must not stop the others */ }

        return true;
    }

    // The tracked calls the provider SDK no longer has live: gone from its call registry, or there in a terminal state.
    //   sdkCalls   - the SDK's registry of calls by id (Telnyx: client.calls), or null when the client itself is gone.
    //   isTerminal - tells a terminal SDK state from a live one.
    function findVanishedCalls(notifiers, sdkCalls, isTerminal) {
        var registry = sdkCalls || {};

        return trackedCallIds(notifiers).filter(function (callId) {
            var call = Object.prototype.hasOwnProperty.call(registry, callId) ? registry[callId] : null;

            return !call || (typeof isTerminal === 'function' && isTerminal(call.state));
        });
    }

    // Ends every tracked call: the session they ran on is going away. Returns the ids it ended.
    function endAllCalls(notifiers) {
        var callIds = trackedCallIds(notifiers);

        callIds.forEach(function (callId) {
            deliverCallState(notifiers, callId, 'Disconnected');
        });

        return callIds;
    }

    // ---- The calls on screen ----

    // Splits the browser-placed calls the phone shows into those that still have a live session behind them and those
    // that do not. A call is kept only while its controller exists and, when the controller can say, reports it live;
    // a controller that cannot say (a provider that has no such check) is believed. The rest are gone and must be
    // ended, or the phone shows a call that no longer exists. A colleague's call still ringing has no controller until
    // it is answered; its ring prompt owns it, so it is kept.
    //   calls       - the calls shown, as the phone holds them ({ callId, browserOriginated, browserInbound, state }).
    //   controllers - the session controllers by call id.
    function planShownBrowserCalls(calls, controllers) {
        var plan = { keep: [], ended: [] };

        (calls || []).forEach(function (call) {
            if (!call || !call.callId || !call.browserOriginated) {
                return;
            }

            if (call.browserInbound && call.state === 'Ringing') {
                plan.keep.push(call.callId);

                return;
            }

            var controller = controllers ? controllers[call.callId] : null;
            var live = !!controller && (typeof controller.isLive !== 'function' || controller.isLive() !== false);

            (live ? plan.keep : plan.ended).push(call.callId);
        });

        return plan;
    }

    // ---- The end-report journal ----

    var JOURNAL_KEY = 'crestapps-soft-phone:browser-calls';

    // The journal: { [callId]: { connected, ended } }. Storage can be missing or throw (a private window, blocked site
    // data); the phone then works without it and the server's own sweep settles what it cannot.
    function readCallJournal(storage) {
        try {
            var text = storage ? storage.getItem(JOURNAL_KEY) : null;
            var journal = text ? JSON.parse(text) : null;

            return journal && typeof journal === 'object' && !Array.isArray(journal) ? journal : {};
        } catch (error) {
            return {};
        }
    }

    function writeCallJournal(storage, journal) {
        try {
            if (!storage) {
                return;
            }

            if (!journal || !Object.keys(journal).length) {
                storage.removeItem(JOURNAL_KEY);
            } else {
                storage.setItem(JOURNAL_KEY, JSON.stringify(journal));
            }
        } catch (error) { /* best effort */ }
    }

    function updateCallJournal(storage, callId, update) {
        if (!callId) {
            return;
        }

        var journal = readCallJournal(storage);

        update(journal);
        writeCallJournal(storage, journal);
    }

    function journalCallStarted(storage, callId) {
        updateCallJournal(storage, callId, function (journal) {
            journal[callId] = journal[callId] || { connected: false, ended: false };
        });
    }

    function journalCallConnected(storage, callId) {
        updateCallJournal(storage, callId, function (journal) {
            var entry = journal[callId] = journal[callId] || { connected: false, ended: false };

            entry.connected = true;
        });
    }

    function journalCallEnded(storage, callId, connected) {
        updateCallJournal(storage, callId, function (journal) {
            var entry = journal[callId] = journal[callId] || { connected: false, ended: false };

            entry.connected = !!(entry.connected || connected);
            entry.ended = true;
        });
    }

    // The server confirmed the end: nothing is owed for the call any more.
    function journalCallSettled(storage, callId) {
        updateCallJournal(storage, callId, function (journal) {
            delete journal[callId];
        });
    }

    // The ends still owed to the server: every call whose end was not confirmed, and every call the journal holds that
    // this page is not running -- a call from before a reload, whose session went with the old page.
    //   liveCallIds - the calls this page still has a live session for.
    // Returns [{ callId, connected }].
    function planOwedCallEnds(journal, liveCallIds) {
        var live = {};

        (liveCallIds || []).forEach(function (callId) {
            live[callId] = true;
        });

        return Object.keys(journal || {}).filter(function (callId) {
            var entry = journal[callId];

            return !!entry && (entry.ended || !live[callId]);
        }).map(function (callId) {
            return { callId: callId, connected: !!journal[callId].connected };
        });
    }

    // ---- Telling the server which calls are still up ----

    var HEARTBEAT_INTERVAL_MS = 30 * 1000;

    // Whether to tell the server now which calls are up, and what to tell it. The server settles a call the phone
    // stopped reporting, so the report has to come well inside its timeout, but not with every refresh of the call
    // list. Only calls the phone placed are reported: a colleague's call it answered is on the platform's own record.
    // Returns null when there is nothing to send yet.
    //   calls      - the calls shown ({ callId, browserOriginated, browserInbound, everConnected, state }).
    //   lastSentMs - when the last report went, or 0.
    function planCallHeartbeat(calls, lastSentMs, nowMs, intervalMs) {
        var interval = typeof intervalMs === 'number' && intervalMs > 0 ? intervalMs : HEARTBEAT_INTERVAL_MS;

        if (lastSentMs && nowMs - lastSentMs < interval) {
            return null;
        }

        var browserCalls = (calls || []).filter(function (call) {
            return call && call.callId && call.browserOriginated && !call.browserInbound;
        });

        if (!browserCalls.length) {
            return null;
        }

        return {
            callIds: browserCalls.map(function (call) { return call.callId; }),
            connectedCallIds: browserCalls.filter(function (call) {
                return !!call.everConnected || call.state === 'Connected' || call.state === 'OnHold';
            }).map(function (call) { return call.callId; })
        };
    }

    softPhone.createCallNotifiers = createCallNotifiers;
    softPhone.trackCall = trackCall;
    softPhone.isCallTracked = isCallTracked;
    softPhone.trackedCallIds = trackedCallIds;
    softPhone.deliverCallState = deliverCallState;
    softPhone.findVanishedCalls = findVanishedCalls;
    softPhone.endAllCalls = endAllCalls;
    softPhone.planShownBrowserCalls = planShownBrowserCalls;
    softPhone.readCallJournal = readCallJournal;
    softPhone.journalCallStarted = journalCallStarted;
    softPhone.journalCallConnected = journalCallConnected;
    softPhone.journalCallEnded = journalCallEnded;
    softPhone.journalCallSettled = journalCallSettled;
    softPhone.planOwedCallEnds = planOwedCallEnds;
    softPhone.planCallHeartbeat = planCallHeartbeat;
    softPhone.BROWSER_CALL_HEARTBEAT_INTERVAL_MS = HEARTBEAT_INTERVAL_MS;
}(typeof globalThis !== 'undefined' ? globalThis : window));
