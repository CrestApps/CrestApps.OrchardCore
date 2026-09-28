import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/consult-state.js';

const { consultView, consultEndedMessage, CONSULT_POLL_INTERVAL_MS } = globalThis.CrestAppsSoftPhone;

const strings = {
    consultRinging: 'Calling {0}...',
    consultConnected: 'Talking to {0}. The caller is on hold.',
    consultCompleted: 'The call was handed to {0}.',
    consultCancelled: 'The caller is back with you.',
    consultTargetLeft: '{0} did not take the call. The caller is back with you.',
    consultCallerLeft: 'The caller hung up.'
};

describe('consultView', () => {
    it('lets the agent cancel while the destination rings, but not complete', () => {
        expect(consultView({ id: 'k1', status: 'ringing', live: true }, 'Bea', strings)).toEqual({
            status: 'ringing',
            live: true,
            message: 'Calling Bea...',
            canComplete: false,
            canCancel: true
        });
    });

    it('lets the agent complete or cancel once the destination has answered', () => {
        expect(consultView({ id: 'k1', status: 'connected', live: true }, 'Bea', strings)).toMatchObject({
            message: 'Talking to Bea. The caller is on hold.',
            canComplete: true,
            canCancel: true
        });
    });

    it('offers nothing once the consult is over', () => {
        expect(consultView({ id: 'k1', status: 'completed', live: false }, 'Bea', strings)).toMatchObject({
            live: false,
            message: 'The call was handed to Bea.',
            canComplete: false,
            canCancel: false
        });
        expect(consultView({ id: 'k1', status: 'cancelled', live: false }, 'Bea', strings)).toMatchObject({
            message: 'The caller is back with you.',
            canComplete: false,
            canCancel: false
        });
    });

    it('reads a missing consult as over', () => {
        expect(consultView(null, 'Bea', strings)).toMatchObject({ status: 'cancelled', live: false, canCancel: false });
    });
});

describe('consultEndedMessage', () => {
    it('says the destination walked away when a live consult ended without the agent asking', () => {
        expect(consultEndedMessage('connected', { status: 'cancelled', live: false }, 'Bea', strings, { callEnded: false }))
            .toBe('Bea did not take the call. The caller is back with you.');
        expect(consultEndedMessage('ringing', { status: 'cancelled', live: false }, 'Bea', strings, { callEnded: false }))
            .toBe('Bea did not take the call. The caller is back with you.');
    });

    it('says the caller hung up when the call itself is gone', () => {
        expect(consultEndedMessage('connected', { status: 'cancelled', live: false }, 'Bea', strings, { callEnded: true }))
            .toBe('The caller hung up.');
    });

    it('says nothing while the consult is still going', () => {
        expect(consultEndedMessage('ringing', { status: 'connected', live: true }, 'Bea', strings, {})).toBe('');
    });

    it('polls often enough that the agent sees the answer within a couple of seconds', () => {
        expect(CONSULT_POLL_INTERVAL_MS).toBeGreaterThan(0);
        expect(CONSULT_POLL_INTERVAL_MS).toBeLessThanOrEqual(2000);
    });
});
