// @ts-check
/*
 * Transferring a call the provider connected on the server (a number dialed from the keypad on Telnyx), and taking one
 * over from a colleague.
 *
 * A blind transfer of such a call used to hand the other party straight to the colleague. Their phone got a leg nobody
 * had recorded, so it treated the call as one it had dialed itself and could not transfer it again; a warm transfer was
 * refused outright. Now the provider rings the destination on a leg of its own and the call stays here, held, until
 * that leg answers -- the transfer panel follows it as a consult, blind or warm -- and a colleague's phone recognizes
 * the leg it is rung on, rings it with Answer and Decline, and follows it as a call the platform tracks.
 *
 * These are the pure decisions; soft-phone.js wires them to the hub and the media adapter.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */
import { readProviderClientState, readProviderHeader } from './offer-leg.js';

// Must match CrestApps.OrchardCore.Telnyx.Services.TelnyxOutboundBridgeState.TransferLegIntent and
// TelnyxTransferCommands.TransferLegSipHeader.
export var TRANSFER_LEG_INTENT = 'ob-xfer';
var TRANSFER_LEG_HEADER = 'x-transfer-leg';

// Must match CrestApps.OrchardCore.Telephony.TelephonyConstants.CallMetadata and ConsultStatuses.
var LIVE_STATUSES = ['ringing', 'connected'];

function text(value) {
    return value == null ? '' : String(value);
}

function isTrue(value) {
    return value === true || value === 'true' || value === 'True';
}

// What an incoming provider leg says about a call a colleague is handing over: { legId, partyNumber, transferredBy },
// or null for any other leg. The leg names itself in its client state and in a SIP header, for an SDK that exposes
// only one of them.
export function readTransferLegTag(options) {
    options = options || {};

    var legId = text(options.telnyxCallControlId || options.callControlId);
    var readState = readProviderClientState;
    var readHeader = readProviderHeader;
    var state = typeof readState === 'function' ? readState(options.clientState || options.client_state) : null;
    var header = typeof readHeader === 'function' ? readHeader(options.customHeaders || options.custom_headers, TRANSFER_LEG_HEADER) : '';

    if (!legId) {
        return null;
    }

    if (state && state.i === TRANSFER_LEG_INTENT) {
        return { legId: legId, partyNumber: text(state.m), transferredBy: text(state.n) };
    }

    if (header) {
        return { legId: legId, partyNumber: header === '1' ? '' : header, transferredBy: '' };
    }

    return null;
}

// The Transfer request for a call the provider carries out itself. The phone names the credential it is registered on,
// so a warm transfer's consult rings this phone -- the server checks that it is the caller's.
export function providerTransferRequest(callId, target, mode, credentialId) {
    var request = {
        callId: callId,
        to: target && target.destination,
        mode: mode,
        isExtension: !!(target && target.isExtension)
    };

    if (credentialId) {
        request.metadata = { softPhoneCredentialId: credentialId };
    }

    return request;
}

// The transfer a command's call reports, as the transfer panel follows it: { id, callId, status, live, callEnded,
// blind }, or null when the call carries none (the provider handed the call over at once, the old way).
//   call - the call the command returned: the call being transferred, or (warm) the consult's own call.
export function consultFromCall(call) {
    var metadata = call && call.metadata;

    if (!metadata || !metadata.consultId) {
        return null;
    }

    var status = text(metadata.consultStatus) || 'cancelled';
    var consultOf = text(metadata.consultOf);

    return {
        id: text(metadata.consultId),
        callId: consultOf || text(call.callId),
        status: status,
        live: metadata.consultLive === undefined ? LIVE_STATUSES.indexOf(status) >= 0 : isTrue(metadata.consultLive),
        callEnded: isTrue(metadata.consultCallEnded),
        blind: !consultOf && text(metadata.consultId) !== text(call.callId)
    };
}

// The follow-up request for a consult the panel is showing.
export function consultRequest(consult) {
    return { callId: consult ? consult.callId : '', consultCallId: consult ? consult.id : '' };
}

// A GetConsult/CompleteConsult/CancelConsult result as the transfer panel reads one. A consult followed from a blind
// transfer stays blind.
export function consultCommandResult(result, previous) {
    if (!result || result.succeeded === false) {
        return { succeeded: false, error: result && result.error ? result.error : '' };
    }

    var consult = consultFromCall(result.call);

    if (consult && previous) {
        consult.blind = !!previous.blind;
        consult.callId = previous.callId || consult.callId;
    }

    return { succeeded: true, consult: consult };
}

// Whether the call is held before a provider transfer: the party waits on this call's own hold tone while the
// destination is rung (and, warm, while the agent talks to them on the consult). Only a provider that connects calls
// on the server does this, and only for a call it tracks.
export function shouldHoldBeforeTransfer(options) {
    options = options || {};

    var call = options.call;

    return !!(options.bridgedDial && call && call.callId && !call.browserOriginated && !call.isOnHold && !options.serviceCall);
}
