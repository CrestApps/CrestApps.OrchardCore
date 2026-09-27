import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/registration.js';

const {
    shouldStartRegistration,
    answerClickAction,
    isAnswerInProgress,
    planRegistrationSelfHeal,
    planMicrophoneLossRecovery,
    guardRegistration,
    REGISTRATION_TIMEOUT_MS,
    REGISTRATION_RETRY_MS,
    registrationRetryDelay,
    shouldReportRegistrationFailure,
} = globalThis.CrestAppsSoftPhone;

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

// Live (2026-09-26), a supervisor's Bluetooth microphone kept dropping. Each drop, and each call that found the capture
// dead, registered the phone again: a new SIP credential every few minutes. One of them landed while the supervisor was
// listening -- a monitor leg is no call of the phone's own, so the phone took itself for idle -- and hung the leg up; the
// next engagement rang the address being replaced and was refused (480). A registration is only rebuilt for what needs a
// new one, never while media is up, and a dead capture is replaced where it is.
describe('healing the registration before it is used', () => {
    const healthy = { expiring: false, captureDead: false, reregisterRequested: false, liveMedia: false };

    it('keeps a healthy registration', () => {
        expect(planRegistrationSelfHeal(healthy)).toBe('keep');
    });

    it('never rebuilds it while a call or a monitor leg is up', () => {
        expect(planRegistrationSelfHeal({ ...healthy, liveMedia: true, captureDead: true })).toBe('keep');
        expect(planRegistrationSelfHeal({ ...healthy, liveMedia: true, expiring: true })).toBe('keep');
        expect(planRegistrationSelfHeal({ ...healthy, liveMedia: true, reregisterRequested: true })).toBe('keep');
    });

    it('registers again for a credential near expiry, or a change that needs a new provider client', () => {
        expect(planRegistrationSelfHeal({ ...healthy, expiring: true })).toBe('reregister');
        expect(planRegistrationSelfHeal({ ...healthy, reregisterRequested: true, captureDead: true })).toBe('reregister');
    });

    it('replaces a dead capture without registering again', () => {
        expect(planRegistrationSelfHeal({ ...healthy, captureDead: true })).toBe('replace-capture');
    });
});

describe('recovering a microphone that stopped', () => {
    it('replaces the capture in place, under whatever is up', () => {
        expect(planMicrophoneLossRecovery({ liveMedia: true }).first).toBe('replace-capture');
        expect(planMicrophoneLossRecovery({ liveMedia: false }).first).toBe('replace-capture');
    });

    it('registers again only when an idle phone could not replace it', () => {
        expect(planMicrophoneLossRecovery({ liveMedia: false }).onFailure).toBe('reregister');
        expect(planMicrophoneLossRecovery({ liveMedia: true }).onFailure).toBe('warn');
    });
});

// Live (2026-09-26), a supervisor's phone asked for its registration config at the moment the tunnel in front of the
// server was replaced. The reply never arrived, nothing timed out, and the phone stayed "registering" for good: every
// later attempt joined the stalled one, so it never registered again and every supervisor leg rang the credential it had
// registered before the restart (refused, SIP 480). A registration is given up on, said, and tried again.
describe('guarding a registration attempt', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('settles with the session a registration produces in time', async () => {
        await expect(guardRegistration(Promise.resolve('session'), 1000, () => 'provider login')).resolves.toBe('session');
    });

    it('fails with the error the registration itself failed with', async () => {
        await expect(guardRegistration(Promise.reject(new Error('No microphone was found.')), 1000, () => 'microphone'))
            .rejects.toThrow('No microphone was found.');
    });

    it('gives up on one that has not finished, saying what it was waiting for', async () => {
        let stage = 'credential';
        const stalled = new Promise(() => { });
        const outcome = guardRegistration(stalled, 1000, () => stage).catch(error => error);

        stage = 'registration config';
        vi.advanceTimersByTime(999);
        vi.advanceTimersByTime(1);
        const error = await outcome;

        expect(error.registrationTimedOut).toBe(true);
        expect(error.stage).toBe('registration config');
        expect(error.message).toContain('waiting for the registration config');
    });

    it('lets go of a session that arrives after it was given up on', async () => {
        let finish;
        const late = vi.fn();
        const guarded = guardRegistration(new Promise(resolve => { finish = resolve; }), 1000, () => 'provider login', late);
        const outcome = guarded.catch(error => error);

        vi.advanceTimersByTime(1000);
        await outcome;
        finish('late-session');
        await Promise.resolve();
        await Promise.resolve();

        expect(late).toHaveBeenCalledWith('late-session');
        await expect(guarded).rejects.toMatchObject({ registrationTimedOut: true });
    });

    it('waits long enough for a slow login and retries soon, not only at the minute heartbeat', () => {
        expect(REGISTRATION_TIMEOUT_MS).toBeGreaterThanOrEqual(15000);
        expect(REGISTRATION_TIMEOUT_MS).toBeLessThanOrEqual(60000);
        expect(REGISTRATION_RETRY_MS).toBeGreaterThan(0);
        expect(REGISTRATION_RETRY_MS).toBeLessThan(60000);
    });
});

// Live (overnight 2026-09-26), an agent's browser never answered the microphone request a registration made. The
// registration guard gave up and started another every 35 seconds all night: 682 attempts, each opening another
// microphone request behind the first, and about 1,400 warnings in the server log. The next morning, once the phone waited
// for the open request, it registered the moment the microphone answered.
describe('a registration waiting on the microphone', () => {
    it('does not start another while the microphone request it made is still open', () => {
        expect(shouldStartRegistration({ ...idle, captureInFlight: true })).toBe(false);
        expect(shouldStartRegistration({ ...idle, captureInFlight: false })).toBe(true);
    });
});

describe('retrying a registration that failed', () => {
    it('tries again soon, then backs off to a couple of minutes', () => {
        expect([1, 2, 3, 4, 5, 6, 50].map(registrationRetryDelay)).toEqual([5000, 15000, 30000, 60000, 120000, 120000, 120000]);
        expect(registrationRetryDelay(0)).toBe(5000);
    });

    it('logs the first failure of a run, one that fails differently, and every tenth -- not every one', () => {
        expect(shouldReportRegistrationFailure(1, 'stalled', '')).toBe(true);
        expect(shouldReportRegistrationFailure(2, 'stalled', 'stalled')).toBe(false);
        expect(shouldReportRegistrationFailure(3, 'refused', 'stalled')).toBe(true);
        expect(shouldReportRegistrationFailure(10, 'stalled', 'stalled')).toBe(true);
        expect(shouldReportRegistrationFailure(11, 'stalled', 'stalled')).toBe(false);
    });
});
