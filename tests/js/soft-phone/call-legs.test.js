import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-legs.js';

const {
    createCallLegs,
    notePlatformLeg,
    noteBrowserLeg,
    forgetLeg,
    planPlatformReport,
    legAfterEnd,
    allLegs,
    canConferenceCall,
} = globalThis.CrestAppsSoftPhone;

const predialedLeg = { name: 'pre-dialed agent leg' };
const browserCall = { name: 'call placed from the keypad' };

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
        forgetLeg(legs, browserCall);

        expect(planPlatformReport(legs, 'Disconnected')).toEqual({ action: 'hangup', leg: predialedLeg });
        expect(planPlatformReport(legs, null)).toEqual({ action: 'hangup', leg: predialedLeg });
        expect(planPlatformReport(legs, 'Failed')).toEqual({ action: 'hangup', leg: predialedLeg });
    });

    it('hangs the platform leg up while a keypad call is still live, and leaves the keypad call alone', () => {
        const legs = createCallLegs();
        notePlatformLeg(legs, predialedLeg);
        noteBrowserLeg(legs, browserCall);

        expect(planPlatformReport(legs, null)).toEqual({ action: 'hangup', leg: predialedLeg });
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
