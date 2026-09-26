import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/keypad-dial.js';

const {
    BRIDGED_DIAL_LEG_CAPABILITY,
    BRIDGE_UNAVAILABLE,
    planKeypadDial,
    bridgedDialRequest,
    shouldDialFromBrowser,
    sharedMicrophoneEnabled,
} = globalThis.CrestAppsSoftPhone;

const browserSession = { canOriginate: true, originate: () => ({}) };

// On Telnyx a number the browser dials itself is invisible to the platform, so it could be neither transferred nor
// merged. A provider that connects keypad dials itself is now asked to; the browser dials only when it says it cannot.
describe('planKeypadDial', () => {
    it('asks a provider that connects keypad dials to connect this one, naming this phone', () => {
        expect(planKeypadDial({ bridgedDial: true, session: browserSession, credentialId: 'credential-1' })).toBe('bridge');
    });

    it('dials from the browser on a provider that does not', () => {
        expect(planKeypadDial({ bridgedDial: false, session: browserSession, credentialId: 'credential-1' })).toBe('browser');
    });

    // Without its credential the provider cannot know which of the agent's phones to ring.
    it('dials from the browser when the phone does not know its own credential', () => {
        expect(planKeypadDial({ bridgedDial: true, session: browserSession, credentialId: null })).toBe('browser');
    });

    it('places a plain server dial when the phone has no browser audio', () => {
        expect(planKeypadDial({ bridgedDial: true, session: null, credentialId: 'credential-1' })).toBe('server');
        expect(planKeypadDial({ bridgedDial: false, session: {}, credentialId: 'credential-1' })).toBe('server');
    });
});

describe('bridgedDialRequest', () => {
    it('sends the number as a phone number, with the credential the phone is registered on', () => {
        expect(bridgedDialRequest('+17025550101', 'credential-1')).toEqual({
            to: '+17025550101',
            isExtension: false,
            metadata: { softPhoneCredentialId: 'credential-1' },
        });
    });
});

describe('shouldDialFromBrowser', () => {
    it('dials from the browser when the provider says it could not connect the call and dialed nothing', () => {
        expect(shouldDialFromBrowser({ succeeded: false, errorCode: BRIDGE_UNAVAILABLE }, browserSession)).toBe(true);
    });

    // Dialing again after any other answer could reach the number twice, or reach a number the platform refused.
    it('never dials a refused number, or one the provider may have placed', () => {
        expect(shouldDialFromBrowser({ succeeded: false, error: 'Emergency numbers cannot be dialed.' }, browserSession)).toBe(false);
        expect(shouldDialFromBrowser({ succeeded: false, outcomeUnknown: true }, browserSession)).toBe(false);
        expect(shouldDialFromBrowser({ succeeded: true, call: { callId: 'agent-leg-1' } }, browserSession)).toBe(false);
        expect(shouldDialFromBrowser(null, browserSession)).toBe(false);
    });

    it('cannot dial from a phone that cannot place calls itself', () => {
        expect(shouldDialFromBrowser({ succeeded: false, errorCode: BRIDGE_UNAVAILABLE }, {})).toBe(false);
    });

    it('names the capability the server checks for', () => {
        expect(BRIDGED_DIAL_LEG_CAPABILITY).toBe('bridged-dial-leg');
    });
});

// Every call shares the one microphone track. It used to follow whichever call was reported last: holding the call the
// agent had just left muted them on the call they had just resumed.
describe('sharedMicrophoneEnabled', () => {
    it('follows the call the agent is talking on', () => {
        expect(sharedMicrophoneEnabled({ state: 'Connected', isMuted: false }, [])).toBe(true);
        expect(sharedMicrophoneEnabled({ state: 'Connected', isMuted: true }, [])).toBe(false);
    });

    it('stays live when another call is held while the agent talks on a second one', () => {
        const talking = { callId: 'agent-leg-2', state: 'Connected', isMuted: false };

        expect(sharedMicrophoneEnabled({ callId: 'agent-leg-1', state: 'OnHold' }, [talking])).toBe(true);
    });

    it('goes quiet when the only call is held or over', () => {
        expect(sharedMicrophoneEnabled({ state: 'OnHold' }, [])).toBe(false);
        expect(sharedMicrophoneEnabled({ state: 'Disconnected' }, [{ state: 'OnHold', isOnHold: true }])).toBe(false);
        expect(sharedMicrophoneEnabled(null, [])).toBe(false);
    });

    // A browser-audio hold happens here; the server may go on reporting the held call connected.
    it('treats a call the agent holds here as held, whatever the server reports', () => {
        const held = new Set(['agent-leg-1']);
        const isHeld = call => held.has(call.callId);

        expect(sharedMicrophoneEnabled({ callId: 'agent-leg-1', state: 'Connected' }, [], isHeld)).toBe(false);
        expect(sharedMicrophoneEnabled({ callId: 'agent-leg-3', state: 'Disconnected' }, [{ callId: 'agent-leg-1', state: 'Connected' }], isHeld)).toBe(false);
    });

    // The end of a platform call says nothing about a call this browser placed, which uses the same track.
    it('leaves a call placed from this browser its microphone, unless the agent muted it', () => {
        expect(sharedMicrophoneEnabled({ state: 'Disconnected' }, [{ state: 'Connecting', browserOriginated: true }])).toBe(true);
        expect(sharedMicrophoneEnabled({ state: 'Disconnected' }, [{ state: 'Connected', browserOriginated: true, isMuted: true }])).toBe(false);
    });
});
