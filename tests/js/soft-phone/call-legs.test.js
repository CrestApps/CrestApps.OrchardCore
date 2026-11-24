import { describe, expect, it } from 'vitest';

import {
    allLegs,
    boundCallOf,
    canConferenceCall,
    createCallLegs,
    forgetLeg,
    legAfterEnd,
    noteBrowserLeg,
    notePlatformLeg,
    planPlatformReport,
    platformLegFor,
    replaceLeg,
    unreportedLiveLegs,
} from '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-legs.js';

const predialedLeg = { name: 'pre-dialed agent leg' };
const browserCall = { name: 'call placed from the keypad' };

// The phone showed these calls before the report and shows none of them now.
const gone = (...callIds) => ({ wasShown: id => callIds.includes(id), isShown: () => false });
// The phone shows these calls, before the report and after it.
const showing = (...callIds) => ({ wasShown: id => callIds.includes(id), isShown: id => callIds.includes(id) });

// The browser holds the media of a call the server tracks -- the leg the platform rang (pre-dialed for an offer, or
// rung at the accept) -- and may also place calls of its own. The media adapter used to keep one "current" provider
// call for both: placing a keypad call made that call current, and every server report about the platform call from
// then on -- hold, resume, hang up -- went to the keypad call instead.
describe('planPlatformReport', () => {
    // Bug: resume did not resume. The hub was never asked, and when it is, the media swap has to reach the leg the
    // caller is on, not whichever provider call happens to be current.
    it('holds and resumes the platform leg even after a keypad call was placed on top of it', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, browserCall);

        expect(planPlatformReport(legs, 'OnHold')).toEqual({ action: 'hold', leg: predialedLeg });
        expect(planPlatformReport(legs, 'Connected')).toEqual({ action: 'resume', leg: predialedLeg });
    });

    // Bug: the agent hung up and the pre-dialed leg stayed up another half a minute. A keypad call had been placed and
    // ended in the meantime, which left the adapter believing its current call was browser-placed, so the server's
    // "no call" was ignored, and the leg was no longer the adapter's current call to hang up anyway.
    it('hangs the platform leg up when the server ends the call, after a keypad call came and went', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, browserCall);
        planPlatformReport(legs, 'Connected', 'caller-1');
        forgetLeg(legs, browserCall);

        expect(planPlatformReport(legs, 'Disconnected', 'caller-1')).toEqual({ action: 'hangup', leg: predialedLeg });
        expect(planPlatformReport(legs, 'Failed', 'caller-1')).toEqual({ action: 'hangup', leg: predialedLeg });
        expect(planPlatformReport(legs, null, null, gone('caller-1'))).toEqual({ action: 'hangup', leg: predialedLeg });
    });

    it('hangs the platform leg up while a keypad call is still live, and leaves the keypad call alone', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, browserCall);
        planPlatformReport(legs, 'Connected', 'caller-1');

        expect(planPlatformReport(legs, null, null, gone('caller-1'))).toEqual({ action: 'hangup', leg: predialedLeg });
    });

    // A call the browser placed has no platform interaction behind it; the server reporting nothing about it is
    // silence, not a hang-up.
    it('never ends a keypad call on a platform report', () => {
        const legs = createCallLegs();
        noteBrowserLeg(legs, browserCall);

        expect(planPlatformReport(legs, null)).toEqual({ action: 'none', leg: null });
        expect(planPlatformReport(legs, 'Disconnected')).toEqual({ action: 'none', leg: null });
        expect(planPlatformReport(legs, 'OnHold')).toEqual({ action: 'none', leg: null });
    });

    it('does nothing for a state that asks nothing of the media', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);

        expect(planPlatformReport(legs, 'Ringing')).toEqual({ action: 'none', leg: predialedLeg });
        expect(planPlatformReport(legs, 'Connecting')).toEqual({ action: 'none', leg: predialedLeg });
    });

    it('forgets a platform leg once it has ended', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);
        forgetLeg(legs, predialedLeg);

        expect(planPlatformReport(legs, null)).toEqual({ action: 'none', leg: null });
    });
});

describe('legAfterEnd', () => {
    // When the keypad call ends, the leg the caller is still on becomes the adapter's current call again, so its own
    // events (and its hang-up) are heard.
    it('hands the adapter back to the platform leg when a keypad call ends', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, browserCall);

        expect(legAfterEnd(legs, browserCall)).toBe(predialedLeg);
        expect(legs.browser).toBe(null);
    });

    // Bug: the agent held a keypad call and dialed a second number. Only the latest keypad call was remembered, so when
    // the second call ended nothing was current, and the held call's own hang-up was never heard.
    it('hands the adapter back to the keypad call held under the one that ended', () => {
        const legs = createCallLegs();
        const heldCall = { name: 'keypad call on hold' };
        noteBrowserLeg(legs, heldCall);
        noteBrowserLeg(legs, browserCall);

        expect(legAfterEnd(legs, browserCall)).toBe(heldCall);
        expect(legs.browser).toBe(heldCall);
        expect(legAfterEnd(legs, heldCall)).toBe(null);
    });

    it('prefers the platform leg over a held keypad call', () => {
        const legs = createCallLegs();
        const heldCall = { name: 'keypad call on hold' };
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, heldCall);
        noteBrowserLeg(legs, browserCall);

        expect(legAfterEnd(legs, browserCall)).toBe(predialedLeg);
    });

    it('leaves nothing current when the platform leg itself ends', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);

        expect(legAfterEnd(legs, predialedLeg)).toBe(null);
        expect(legs.platform).toBe(null);
    });
});

// Bug: the phone offered to conference the caller with a second call that only this browser knew about. Merging is a
// server command over calls the server tracks; a call the browser placed itself cannot be part of it.
describe('canConferenceCall', () => {
    it('offers a server-tracked call for a conference', () => {
        expect(canConferenceCall({ callId: 'v3:caller' })).toBe(true);
    });

    it('never offers a call this browser placed', () => {
        expect(canConferenceCall({ callId: 'browser-1', browserOriginated: true })).toBe(false);
        expect(canConferenceCall(null)).toBe(false);
    });
});

describe('allLegs', () => {
    // Tearing the registration down hangs up every call this browser holds; a held keypad call is one of them.
    it('lists the platform leg and every keypad call once', () => {
        const legs = createCallLegs();
        const heldCall = { name: 'keypad call on hold' };
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, heldCall);
        noteBrowserLeg(legs, browserCall);
        noteBrowserLeg(legs, browserCall);

        expect(allLegs(legs)).toEqual([predialedLeg, browserCall, heldCall]);
        expect(allLegs(null)).toEqual([]);
    });
});

// Numbers dialed from the keypad and connected by the platform each ring a leg of their own, so the phone holds several
// calls the server tracks at once. A report about one of them used to reach whichever leg arrived last: holding the
// first call held the third, and the first call's hang-up hung up the third.
describe('several platform calls at once', () => {
    const legFor = callId => ({ name: `leg of ${callId}`, options: { telnyxCallControlId: callId } });

    it('applies each report to the leg of the call it names', () => {
        const legs = createCallLegs();
        const first = legFor('agent-leg-1');
        const second = legFor('agent-leg-2');
        const third = legFor('agent-leg-3');
        notePlatformLeg(legs, first);
        notePlatformLeg(legs, second);
        notePlatformLeg(legs, third);

        expect(planPlatformReport(legs, 'OnHold', 'agent-leg-1')).toEqual({ action: 'hold', leg: first });
        expect(planPlatformReport(legs, 'Connected', 'agent-leg-2')).toEqual({ action: 'resume', leg: second });
        expect(planPlatformReport(legs, 'Disconnected', 'agent-leg-3')).toEqual({ action: 'hangup', leg: third });
    });

    it('hands the latest platform leg back when the newest one ends', () => {
        const legs = createCallLegs();
        const first = legFor('agent-leg-1');
        const second = legFor('agent-leg-2');
        notePlatformLeg(legs, first);
        notePlatformLeg(legs, second);
        forgetLeg(legs, second);

        expect(legs.platform).toBe(first);
        expect(allLegs(legs)).toEqual([first]);
    });

    // The call a Contact Center offer rang the agent for is reported by the caller's leg, which is not the agent's.
    it('still sends a report that names no leg here to the platform leg', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, legFor('agent-leg-cc'));

        expect(platformLegFor(legs, 'caller-leg-1')).toBe(legs.platform);
        expect(planPlatformReport(legs, 'OnHold', 'caller-leg-1').action).toBe('hold');
    });

    // A dialed call's first report arrives before its leg does; it must not act on the previous call's leg.
    it("never hands a report about a call whose leg has not arrived to another call's leg", () => {
        const legs = createCallLegs();
        const first = legFor('agent-leg-1');
        notePlatformLeg(legs, first);
        planPlatformReport(legs, 'Connected', 'agent-leg-1');

        expect(planPlatformReport(legs, 'Disconnected', 'agent-leg-2')).toEqual({ action: 'none', leg: null });
    });
});

// Regression: agents on Power campaigns answered, said hello, and the call dropped. The agent's leg carries a call the
// server reports by the customer's leg, so any report naming no leg here fell through to the latest platform leg, and a
// report that some call had ended -- the previous dial, an orphaned history entry the server swept, or an active-calls
// refresh that did not list the dialer call yet -- hung up the leg the agent had just answered.
describe('a dialer call the agent just answered', () => {
    const dialerLeg = { name: 'leg parked for a dialer call', options: { telnyxCallControlId: 'agent-leg-b' } };

    it('is not hung up by an end reported for another call', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);
        planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'));

        expect(planPlatformReport(legs, 'Disconnected', 'customer-a', gone('customer-a'))).toEqual({ action: 'none', leg: null });
        expect(planPlatformReport(legs, 'Failed', 'stale-entry', gone('stale-entry'))).toEqual({ action: 'none', leg: null });
        expect(planPlatformReport(legs, 'Disconnected', 'customer-b', gone('customer-b'))).toEqual({ action: 'hangup', leg: dialerLeg });
    });

    it('is not hung up by an end reported for a call the phone never showed, before its own report arrives', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);

        expect(planPlatformReport(legs, 'Disconnected', 'customer-a', gone())).toEqual({ action: 'none', leg: null });
        expect(planPlatformReport(legs, 'Disconnected', 'customer-a')).toEqual({ action: 'none', leg: null });
    });

    it('is not hung up by an active-calls refresh that does not list it yet', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);

        expect(planPlatformReport(legs, null, null, gone())).toEqual({ action: 'none', leg: null });
        expect(planPlatformReport(legs, null)).toEqual({ action: 'none', leg: null });
    });

    it('is not hung up by a refresh that still lists the call it carries', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);
        planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'));

        expect(planPlatformReport(legs, null, null, showing('customer-b'))).toEqual({ action: 'none', leg: null });
    });

    it('is hung up when the call on screen ends before the server reported it live on the leg', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);

        expect(planPlatformReport(legs, 'Disconnected', 'customer-b', gone('customer-b'))).toEqual({ action: 'hangup', leg: dialerLeg });
    });

    it('ties the leg to the first call reported live on it, and holds and resumes it by that call', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);

        expect(planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'))).toEqual({ action: 'resume', leg: dialerLeg });
        expect(boundCallOf(legs, dialerLeg)).toBe('customer-b');
        expect(planPlatformReport(legs, 'OnHold', 'customer-b', showing('customer-b'))).toEqual({ action: 'hold', leg: dialerLeg });
        expect(planPlatformReport(legs, 'Connected', 'stale-entry', showing('customer-b', 'stale-entry'))).toEqual({ action: 'none', leg: null });
        expect(boundCallOf(legs, dialerLeg)).toBe('customer-b');
    });

    it('lets a live report of a new call take the leg over once the call it carried is off the screen', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, dialerLeg);
        planPlatformReport(legs, 'Connected', 'customer-a', showing('customer-a'));

        expect(planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'))).toEqual({ action: 'resume', leg: dialerLeg });
        expect(boundCallOf(legs, dialerLeg)).toBe('customer-b');
    });

    it('keeps each of two platform calls on its own leg', () => {
        const legs = createCallLegs();
        const heldLeg = { name: 'leg of the call on hold', options: { telnyxCallControlId: 'agent-leg-a' } };
        notePlatformLeg(legs, heldLeg);
        planPlatformReport(legs, 'OnHold', 'customer-a', showing('customer-a'));
        notePlatformLeg(legs, dialerLeg);
        planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-a', 'customer-b'));

        expect(planPlatformReport(legs, 'Disconnected', 'customer-a', showing('customer-b'))).toEqual({ action: 'hangup', leg: heldLeg });
        forgetLeg(legs, heldLeg);
        expect(boundCallOf(legs, heldLeg)).toBe('');
        expect(planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'))).toEqual({ action: 'resume', leg: dialerLeg });
    });
});

describe('unreportedLiveLegs', () => {
    // The microphone stayed off from the last call until the server reported the join, so the agent's "hello" on a
    // dialer leg never reached the customer.
    it('lists an answered platform leg until the server reports a call on it', () => {
        const legs = createCallLegs();
        const leg = { name: 'dialer leg', state: 'active' };
        const isActive = candidate => candidate.state === 'active';
        notePlatformLeg(legs, leg);

        expect(unreportedLiveLegs(legs, isActive)).toEqual([leg]);
        planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'));
        expect(unreportedLiveLegs(legs, isActive)).toEqual([]);
    });

    it('leaves out a leg that is not up and never lists a keypad call', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, { name: 'ringing leg', state: 'ringing' });
        noteBrowserLeg(legs, { name: 'keypad call', state: 'active' });

        expect(unreportedLiveLegs(legs, candidate => candidate.state === 'active')).toEqual([]);
    });
});

describe('replaceLeg', () => {
    // A call the provider recovers across a reconnect arrives as a new object; left out of the legs, the dead object kept
    // answering for the call and the live one was never ended.
    it('puts a recovered call in the place of the one it replaces, with the call it carried', () => {
        const legs = createCallLegs();
        const before = { name: 'call before the reconnect' };
        const after = { name: 'recovered call' };
        notePlatformLeg(legs, before);
        planPlatformReport(legs, 'Connected', 'customer-b', showing('customer-b'));

        replaceLeg(legs, before, after);

        expect(legs.platform).toBe(after);
        expect(allLegs(legs)).toEqual([after]);
        expect(boundCallOf(legs, after)).toBe('customer-b');
        expect(planPlatformReport(legs, 'Disconnected', 'customer-b', gone('customer-b'))).toEqual({ action: 'hangup', leg: after });
    });
});
