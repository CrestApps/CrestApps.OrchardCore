import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/browser-calls.js';

const {
    createCallNotifiers,
    trackCall,
    isCallTracked,
    trackedCallIds,
    deliverCallState,
    findVanishedCalls,
    endAllCalls,
    planShownBrowserCalls,
    readCallJournal,
    journalCallStarted,
    journalCallConnected,
    journalCallEnded,
    journalCallSettled,
    planOwedCallEnds,
    planCallHeartbeat,
    BROWSER_CALL_HEARTBEAT_INTERVAL_MS,
} = globalThis.CrestAppsSoftPhone;

const isTerminal = state => state === 'hangup' || state === 'destroy' || state === 'purge';

function recorder() {
    const states = [];

    return { states, notify: state => states.push(state) };
}

function memoryStorage() {
    const items = {};

    return {
        items,
        getItem: key => (Object.prototype.hasOwnProperty.call(items, key) ? items[key] : null),
        setItem: (key, value) => { items[key] = String(value); },
        removeItem: key => { delete items[key]; },
    };
}

describe('per-call state callbacks', () => {
    // Bug: the adapter kept one callback for "the" outbound call. The agent held a call and dialed another; the second
    // call took the callback over, and when the held call ended nothing told the phone, so it never reported the end.
    it('delivers each call its own states, the held call included', () => {
        const notifiers = createCallNotifiers();
        const held = recorder();
        const second = recorder();
        trackCall(notifiers, 'call-1', held.notify);
        trackCall(notifiers, 'call-2', second.notify);

        deliverCallState(notifiers, 'call-2', 'Connected');
        deliverCallState(notifiers, 'call-1', 'Disconnected');

        expect(held.states).toEqual(['Disconnected']);
        expect(second.states).toEqual(['Connected']);
    });

    it('delivers an end once, however many times the SDK reports it', () => {
        const notifiers = createCallNotifiers();
        const call = recorder();
        trackCall(notifiers, 'call-1', call.notify);

        expect(deliverCallState(notifiers, 'call-1', 'Disconnected')).toBe(true);
        expect(deliverCallState(notifiers, 'call-1', 'Disconnected')).toBe(false);
        expect(deliverCallState(notifiers, 'call-1', 'Connected')).toBe(false);

        expect(call.states).toEqual(['Disconnected']);
        expect(isCallTracked(notifiers, 'call-1')).toBe(false);
    });

    it('forgets the call before its callback runs, so a callback that ends it again finds nothing', () => {
        const notifiers = createCallNotifiers();
        const states = [];
        trackCall(notifiers, 'call-1', state => {
            states.push(state);
            deliverCallState(notifiers, 'call-1', 'Disconnected');
        });

        deliverCallState(notifiers, 'call-1', 'Disconnected');

        expect(states).toEqual(['Disconnected']);
    });

    it('keeps delivering to the other calls when one callback throws', () => {
        const notifiers = createCallNotifiers();
        const other = recorder();
        trackCall(notifiers, 'call-1', () => { throw new Error('broken'); });
        trackCall(notifiers, 'call-2', other.notify);

        expect(endAllCalls(notifiers)).toEqual(['call-1', 'call-2']);
        expect(other.states).toEqual(['Disconnected']);
        expect(trackedCallIds(notifiers)).toEqual([]);
    });

    it('ignores a state for a call nobody tracks, and a callback that is not a function', () => {
        const notifiers = createCallNotifiers();

        expect(trackCall(notifiers, 'call-1', null)).toBe(false);
        expect(deliverCallState(notifiers, 'call-1', 'Disconnected')).toBe(false);
        expect(deliverCallState(notifiers, 'call-1', null)).toBe(false);
    });
});

describe('findVanishedCalls', () => {
    // Bug: the socket dropped and came back without the call, and the SDK never said so. The phone showed the call
    // "In call" long after it was gone.
    it('finds a tracked call the SDK no longer has in its registry', () => {
        const notifiers = createCallNotifiers();
        trackCall(notifiers, 'call-1', () => { });
        trackCall(notifiers, 'call-2', () => { });

        expect(findVanishedCalls(notifiers, { 'call-2': { state: 'active' } }, isTerminal)).toEqual(['call-1']);
    });

    it('finds a tracked call the SDK holds in a terminal state', () => {
        const notifiers = createCallNotifiers();
        trackCall(notifiers, 'call-1', () => { });

        expect(findVanishedCalls(notifiers, { 'call-1': { state: 'hangup' } }, isTerminal)).toEqual(['call-1']);
    });

    // A call the SDK recovered across a reconnect is a new object under the same id: it is still live.
    it('keeps a call the SDK holds live under its id, whatever object carries it', () => {
        const notifiers = createCallNotifiers();
        trackCall(notifiers, 'call-1', () => { });

        expect(findVanishedCalls(notifiers, { 'call-1': { state: 'recovering' } }, isTerminal)).toEqual([]);
    });

    it('treats every call as gone when the SDK has no registry left', () => {
        const notifiers = createCallNotifiers();
        trackCall(notifiers, 'call-1', () => { });

        expect(findVanishedCalls(notifiers, null, isTerminal)).toEqual(['call-1']);
    });
});

describe('planShownBrowserCalls', () => {
    const live = { isLive: () => true };
    const gone = { isLive: () => false };

    it('drops a call whose session says it is gone, and one that has no controller at all', () => {
        const plan = planShownBrowserCalls(
            [
                { callId: 'browser-1', browserOriginated: true, state: 'Connected' },
                { callId: 'browser-2', browserOriginated: true, state: 'OnHold' },
                { callId: 'browser-3', browserOriginated: true, state: 'Connected' },
            ],
            { 'browser-1': live, 'browser-2': gone });

        expect(plan).toEqual({ keep: ['browser-1'], ended: ['browser-2', 'browser-3'] });
    });

    it('believes a controller that cannot say whether its call is live', () => {
        const plan = planShownBrowserCalls(
            [{ callId: 'browser-1', browserOriginated: true, state: 'Connected' }],
            { 'browser-1': { terminate() { } } });

        expect(plan).toEqual({ keep: ['browser-1'], ended: [] });
    });

    // A colleague's call ringing on this phone has no controller until it is answered; the ring prompt owns it.
    it('keeps a colleague call that is still ringing', () => {
        const plan = planShownBrowserCalls(
            [{ callId: 'browser-in-1', browserOriginated: true, browserInbound: true, state: 'Ringing' }],
            {});

        expect(plan).toEqual({ keep: ['browser-in-1'], ended: [] });
    });

    it('leaves the calls the server tracks alone', () => {
        const plan = planShownBrowserCalls([{ callId: 'call-1', state: 'Connected' }], {});

        expect(plan).toEqual({ keep: [], ended: [] });
    });
});

describe('the end-report journal', () => {
    it('owes the end of a call the server has not confirmed, with whether it connected', () => {
        const storage = memoryStorage();
        journalCallStarted(storage, 'browser-1');
        journalCallConnected(storage, 'browser-1');
        journalCallEnded(storage, 'browser-1', false);

        expect(planOwedCallEnds(readCallJournal(storage), [])).toEqual([{ callId: 'browser-1', connected: true }]);
    });

    it('owes nothing once the server confirmed the end', () => {
        const storage = memoryStorage();
        journalCallStarted(storage, 'browser-1');
        journalCallEnded(storage, 'browser-1', true);
        journalCallSettled(storage, 'browser-1');

        expect(planOwedCallEnds(readCallJournal(storage), [])).toEqual([]);
        expect(storage.items).toEqual({});
    });

    // The page was reloaded mid-call: the call went with the old page, and the new one has no session for it.
    it('owes the end of a call from before a reload, which this page is not running', () => {
        const storage = memoryStorage();
        journalCallStarted(storage, 'browser-old');
        journalCallStarted(storage, 'browser-live');

        expect(planOwedCallEnds(readCallJournal(storage), ['browser-live'])).toEqual([{ callId: 'browser-old', connected: false }]);
    });

    it('works without storage, and with storage that throws or holds garbage', () => {
        const throwing = {
            getItem() { throw new Error('blocked'); },
            setItem() { throw new Error('blocked'); },
            removeItem() { throw new Error('blocked'); },
        };
        const garbage = memoryStorage();
        garbage.setItem('crestapps-soft-phone:browser-calls', '[1,2');

        expect(() => journalCallStarted(null, 'browser-1')).not.toThrow();
        expect(() => journalCallEnded(throwing, 'browser-1', true)).not.toThrow();
        expect(readCallJournal(throwing)).toEqual({});
        expect(readCallJournal(garbage)).toEqual({});
    });
});

describe('planCallHeartbeat', () => {
    const calls = [
        { callId: 'browser-1', browserOriginated: true, state: 'OnHold', everConnected: true },
        { callId: 'browser-2', browserOriginated: true, state: 'Ringing' },
        { callId: 'browser-in-1', browserOriginated: true, browserInbound: true, state: 'Connected' },
        { callId: 'call-1', state: 'Connected' },
    ];

    it('reports the placed calls that are up, and which of them connected', () => {
        expect(planCallHeartbeat(calls, 0, 1000)).toEqual({
            callIds: ['browser-1', 'browser-2'],
            connectedCallIds: ['browser-1'],
        });
    });

    it('waits out the interval between reports', () => {
        expect(planCallHeartbeat(calls, 1000, 1000 + BROWSER_CALL_HEARTBEAT_INTERVAL_MS - 1)).toBe(null);
        expect(planCallHeartbeat(calls, 1000, 1000 + BROWSER_CALL_HEARTBEAT_INTERVAL_MS)).not.toBe(null);
    });

    // The server settles a call the phone stopped reporting; the report has to come well inside that timeout.
    it('reports well inside the five minutes the server waits', () => {
        expect(BROWSER_CALL_HEARTBEAT_INTERVAL_MS).toBeLessThanOrEqual(60 * 1000);
    });

    it('sends nothing when there is no placed call', () => {
        expect(planCallHeartbeat([{ callId: 'call-1', state: 'Connected' }], 0, 1000)).toBe(null);
    });
});
