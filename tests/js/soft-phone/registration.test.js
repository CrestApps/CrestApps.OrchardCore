import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/registration.js';

const { shouldStartRegistration, answerClickAction, isAnswerInProgress } = globalThis.CrestAppsSoftPhone;

const idle = {
    browserAudioEnabled: true,
    registered: false,
    registering: false,
    microphoneBlocked: false,
    hubConnected: true,
};

// Bug: an Available agent's soft phone was not registered with the provider when a direct inbound offer rang. The
// platform rang its leg for the offer at a credential nothing was registered on (SIP 480) and the phone only started
// registering when the agent clicked Answer, 9 to 24 seconds later. Registration must start the moment the phone
// knows it is needed -- an offer on screen, or the heartbeat noticing it lapsed -- not on the click.
describe('shouldStartRegistration', () => {
    it('registers a phone that uses browser audio and holds no registration', () => {
        expect(shouldStartRegistration(idle)).toBe(true);
    });

    it('leaves a registered phone alone', () => {
        expect(shouldStartRegistration({ ...idle, registered: true })).toBe(false);
    });

    it('does not start a second registration while one is in flight', () => {
        expect(shouldStartRegistration({ ...idle, registering: true })).toBe(false);
    });

    it('does nothing when the provider does not deliver audio to the browser', () => {
        expect(shouldStartRegistration({ ...idle, browserAudioEnabled: false })).toBe(false);
    });

    // A blocked or missing microphone fails every attempt the same way; retrying on a timer would only repeat the
    // guidance the agent is already looking at. The Retry button is the way back.
    it('does not retry on its own while the microphone is blocked', () => {
        expect(shouldStartRegistration({ ...idle, microphoneBlocked: true })).toBe(false);
    });

    it('waits for the hub, which hands out the credentials', () => {
        expect(shouldStartRegistration({ ...idle, hubConnected: false })).toBe(false);
    });
});

// Bug: clicking Answer on an unregistered phone started a registration that took several seconds and changed nothing
// on screen -- the ringtone carried on and the buttons stayed live -- so the agent clicked again and again, sure the
// machine had frozen. The first click must be the one that counts, and every later click a no-op.
describe('answerClickAction', () => {
    const ringing = { browserAudioEnabled: true, registered: false, acceptPending: false, registeringForAnswer: false };

    it('registers first when the phone is not registered', () => {
        expect(answerClickAction(ringing)).toBe('register');
    });

    it('accepts straight away when the phone is registered', () => {
        expect(answerClickAction({ ...ringing, registered: true })).toBe('accept');
    });

    it('accepts straight away when the provider does not deliver audio to the browser', () => {
        expect(answerClickAction({ ...ringing, browserAudioEnabled: false })).toBe('accept');
    });

    it('ignores a click while the registration for an earlier one is still running', () => {
        expect(answerClickAction({ ...ringing, registeringForAnswer: true })).toBe('ignore');
        expect(answerClickAction({ ...ringing, registered: true, registeringForAnswer: true })).toBe('ignore');
    });

    it('ignores a click while an accept is already on its way', () => {
        expect(answerClickAction({ ...ringing, registered: true, acceptPending: true })).toBe('ignore');
    });
});

describe('isAnswerInProgress', () => {
    it('is in progress from the first click, while the phone registers for it', () => {
        expect(isAnswerInProgress({ acceptPending: false, registeringForAnswer: true })).toBe(true);
    });

    it('is in progress while the accept round-trips', () => {
        expect(isAnswerInProgress({ acceptPending: true, registeringForAnswer: false })).toBe(true);
    });

    it('is not in progress before the agent clicks', () => {
        expect(isAnswerInProgress({ acceptPending: false, registeringForAnswer: false })).toBe(false);
    });
});
