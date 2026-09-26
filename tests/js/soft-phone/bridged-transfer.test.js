import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/offer-leg.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/bridged-transfer.js';

const {
    readTransferLegTag,
    providerTransferRequest,
    consultFromCall,
    consultRequest,
    consultCommandResult,
    shouldHoldBeforeTransfer
} = globalThis.CrestAppsSoftPhone;

const encode = value => Buffer.from(JSON.stringify(value)).toString('base64');

describe('readTransferLegTag', () => {
    it('recognizes a leg rung to hand this agent a call, from its client state', () => {
        const options = {
            telnyxCallControlId: 'v3:xfer',
            clientState: encode({ i: 'ob-xfer', p: 'v3:party', h: 'v3:agent', m: '+17025550101', n: 'Agent One' })
        };

        expect(readTransferLegTag(options)).toEqual({ legId: 'v3:xfer', partyNumber: '+17025550101', transferredBy: 'Agent One' });
    });

    it('recognizes it from its SIP header when the SDK does not expose the client state', () => {
        const options = { telnyxCallControlId: 'v3:xfer', customHeaders: [{ name: 'X-Transfer-Leg', value: '+17025550101' }] };

        expect(readTransferLegTag(options)).toEqual({ legId: 'v3:xfer', partyNumber: '+17025550101', transferredBy: '' });
    });

    it('leaves any other leg to the usual rules', () => {
        expect(readTransferLegTag({ telnyxCallControlId: 'v3:ext', clientState: encode({ i: 'ob-dest', p: 'v3:agent' }) })).toBeNull();
        expect(readTransferLegTag({ telnyxCallControlId: 'v3:ext', customHeaders: [] })).toBeNull();
        expect(readTransferLegTag({ telnyxCallControlId: 'v3:cc', clientState: encode({ i: 'cc-predial', r: 'res-1' }) })).toBeNull();
    });

    it('cannot take over a leg it cannot name', () => {
        expect(readTransferLegTag({ clientState: encode({ i: 'ob-xfer', m: '+17025550101' }) })).toBeNull();
    });
});

describe('providerTransferRequest', () => {
    it('names the phone the consult rings back, and says an extension is one', () => {
        expect(providerTransferRequest('call-1', { destination: '2', isExtension: true }, 1, 'credential-1')).toEqual({
            callId: 'call-1',
            to: '2',
            mode: 1,
            isExtension: true,
            metadata: { softPhoneCredentialId: 'credential-1' }
        });
    });

    it('sends no metadata without a registered phone', () => {
        expect(providerTransferRequest('call-1', { destination: '+17025550199' }, 0, '')).toEqual({
            callId: 'call-1',
            to: '+17025550199',
            mode: 0,
            isExtension: false
        });
    });
});

describe('consultFromCall', () => {
    it('follows a blind transfer on the call being transferred', () => {
        const call = { callId: 'call-1', metadata: { consultId: 'xfer-1', consultStatus: 'ringing', consultLive: true, consultCallEnded: false } };

        expect(consultFromCall(call)).toEqual({ id: 'xfer-1', callId: 'call-1', status: 'ringing', live: true, callEnded: false, blind: true });
    });

    it('follows a warm transfer on its consult call, against the call being consulted about', () => {
        const call = { callId: 'consult-1', metadata: { consultId: 'consult-1', consultOf: 'call-1', consultStatus: 'ringing', consultLive: true } };

        expect(consultFromCall(call)).toMatchObject({ id: 'consult-1', callId: 'call-1', live: true, blind: false });
    });

    it('is nothing for a transfer the provider carried out at once', () => {
        expect(consultFromCall({ callId: 'call-1', state: 'Disconnected', metadata: {} })).toBeNull();
        expect(consultFromCall(null)).toBeNull();
    });

    it('reads a status the hub serialized without the live flag', () => {
        expect(consultFromCall({ callId: 'call-1', metadata: { consultId: 'k', consultStatus: 'completed' } }).live).toBe(false);
        expect(consultFromCall({ callId: 'call-1', metadata: { consultId: 'k', consultStatus: 'connected' } }).live).toBe(true);
    });
});

describe('consultCommandResult', () => {
    it('keeps what the panel knew about the consult it followed', () => {
        const previous = { id: 'consult-1', callId: 'call-1', blind: false };
        const result = { succeeded: true, call: { callId: 'call-1', metadata: { consultId: 'consult-1', consultStatus: 'connected', consultLive: true } } };

        expect(consultCommandResult(result, previous).consult).toMatchObject({ status: 'connected', blind: false, callId: 'call-1' });
    });

    it('says why a command was refused', () => {
        expect(consultCommandResult({ succeeded: false, error: 'They have not answered yet.' })).toEqual({ succeeded: false, error: 'They have not answered yet.' });
    });

    it('reports the call the caller left', () => {
        const result = { succeeded: true, call: { callId: 'call-1', metadata: { consultId: 'consult-1', consultStatus: 'cancelled', consultLive: false, consultCallEnded: true } } };

        expect(consultCommandResult(result, { id: 'consult-1', callId: 'call-1' }).consult).toMatchObject({ live: false, callEnded: true });
    });
});

describe('consultRequest', () => {
    it('names the call and the transfer leg', () => {
        expect(consultRequest({ id: 'xfer-1', callId: 'call-1' })).toEqual({ callId: 'call-1', consultCallId: 'xfer-1' });
    });
});

describe('shouldHoldBeforeTransfer', () => {
    const call = { callId: 'call-1', state: 'Connected', isOnHold: false };

    it('holds a call the provider connected, so the caller hears the hold tone while the destination rings', () => {
        expect(shouldHoldBeforeTransfer({ bridgedDial: true, call })).toBe(true);
    });

    it('leaves every other call as it is', () => {
        expect(shouldHoldBeforeTransfer({ bridgedDial: false, call })).toBe(false);
        expect(shouldHoldBeforeTransfer({ bridgedDial: true, call: { ...call, isOnHold: true } })).toBe(false);
        expect(shouldHoldBeforeTransfer({ bridgedDial: true, call: { ...call, browserOriginated: true } })).toBe(false);
        expect(shouldHoldBeforeTransfer({ bridgedDial: true, call, serviceCall: true })).toBe(false);
        expect(shouldHoldBeforeTransfer({ bridgedDial: true, call: null })).toBe(false);
    });
});
