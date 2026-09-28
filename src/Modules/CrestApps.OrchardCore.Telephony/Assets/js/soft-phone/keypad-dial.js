/*
 * How a number typed on the keypad is placed, and how the phone's one microphone follows several calls at once.
 *
 * A call the browser dials itself through the provider's SDK is invisible to the platform on some providers (Telnyx: the
 * credential connection reports nothing), so it can be neither transferred nor merged. A provider that can connect the
 * dial on the server instead (TelephonyCapabilities.BridgedDial) is asked to: it rings this browser's own registered
 * credential, which the phone answers without ringing, and dials the number from there. When the provider says it
 * cannot -- and only then, because nothing was dialed -- the phone dials the number itself, as before.
 *
 * With calls placed that way the phone holds several calls the platform tracks, each on a leg of its own, all sending
 * the same microphone track. The track used to follow whichever call was reported last, so holding one call muted the
 * agent on the call they had just resumed.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Must match CrestApps.OrchardCore.Telephony.TelephonyConstants.
    var BRIDGED_DIAL_LEG_CAPABILITY = 'bridged-dial-leg';
    var BRIDGE_UNAVAILABLE = 'bridge-unavailable';
    var SOFT_PHONE_CREDENTIAL_ID = 'softPhoneCredentialId';

    function canOriginate(session) {
        return !!(session && session.canOriginate && typeof session.originate === 'function');
    }

    // How to place a keypad dial:
    //   bridgedDial  - whether the provider connects keypad dials itself (the BridgedDial capability)
    //   session      - the browser audio session, or null when the phone has none
    //   credentialId - the credential this phone is registered on
    // Returns 'bridge' (ask the provider, naming this phone), 'browser' (dial it here) or 'server' (a plain Dial).
    function planKeypadDial(options) {
        options = options || {};

        if (options.bridgedDial && options.session && options.credentialId) {
            return 'bridge';
        }

        return canOriginate(options.session) ? 'browser' : 'server';
    }

    // The Dial request that asks the provider to connect the number through this phone's own leg.
    function bridgedDialRequest(number, credentialId) {
        var metadata = {};

        metadata[SOFT_PHONE_CREDENTIAL_ID] = credentialId;

        return { to: number, isExtension: false, metadata: metadata };
    }

    // Whether the provider's answer to a bridged dial means the phone should dial the number itself: the provider said it
    // could not connect it and created no leg. Any other failure -- a refused number, a call the provider may have placed
    // -- is shown, never dialed a second time.
    function shouldDialFromBrowser(result, session) {
        return !!(result && result.succeeded === false && result.errorCode === BRIDGE_UNAVAILABLE && canOriginate(session));
    }

    // Whether the shared microphone track should be live after a report about one call.
    //   reported - the call the report is about: { state, isMuted }
    //   others   - every other call the phone shows: { state, isMuted, isOnHold, browserOriginated }
    //   isHeld   - optional; whether the agent holds a call (a browser-audio hold happens here, not on the server)
    // A connected call speaks for itself -- muting it mutes the agent. A call that is held, ringing or over leaves the
    // microphone to the others: live while the agent is talking on any of them, or a call this browser placed is up.
    function sharedMicrophoneEnabled(reported, others, isHeld) {
        var held = typeof isHeld === 'function' ? isHeld : function () { return false; };

        if (reported && reported.state === 'Connected' && !reported.isOnHold && !held(reported)) {
            return !reported.isMuted;
        }

        return (others || []).some(function (call) {
            if (!call) {
                return false;
            }

            // A call placed here is muted through its own session, which mutes this same track.
            if (call.browserOriginated) {
                return !call.isMuted;
            }

            return call.state === 'Connected' && !call.isMuted && !call.isOnHold && !held(call);
        });
    }

    softPhone.BRIDGED_DIAL_LEG_CAPABILITY = BRIDGED_DIAL_LEG_CAPABILITY;
    softPhone.BRIDGE_UNAVAILABLE = BRIDGE_UNAVAILABLE;
    softPhone.planKeypadDial = planKeypadDial;
    softPhone.bridgedDialRequest = bridgedDialRequest;
    softPhone.shouldDialFromBrowser = shouldDialFromBrowser;
    softPhone.sharedMicrophoneEnabled = sharedMicrophoneEnabled;
}(typeof globalThis !== 'undefined' ? globalThis : window));
