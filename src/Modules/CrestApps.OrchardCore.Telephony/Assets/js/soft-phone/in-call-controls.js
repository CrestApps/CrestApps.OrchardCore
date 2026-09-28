/*
 * Which in-call controls the phone shows, and how.
 *
 * The phone used to put every in-call action in one row of identical round icons -- hold, mute, transfer, a dark red
 * "disconnect all" and a red hang-up -- over a keypad that stayed open for the whole call. Agents holding a caller to
 * dial someone else could not tell which icon did what, and two red buttons side by side made "hang up" a guess. Phones
 * people already know (the iOS and Android in-call screens, Zoom Phone, RingCentral, Teams, Webex, Dialpad) split them:
 * the agent's own call controls -- Mute, Hold, Keypad and a single red Hang up -- in one labelled row, and the actions
 * that bring someone else in or hand the call over -- Transfer, Add call, and ending every call -- in a second, quieter
 * row. The keypad opens only when asked for (or while a held call is waiting for the number to add), and Add call holds
 * the current call and opens an empty number field with a way back to the held call.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // What the phone shows for the current call.
    //   options - {
    //     active, stateName ('Idle' | 'Connecting' | 'Ringing' | 'Connected' | 'OnHold' | ...), muted,
    //     ringingOffer     - an incoming offer rings, answered or declined on its own card,
    //     lineCount        - how many calls are up,
    //     isConference     - the current call is in a conference,
    //     conferenceParties - how many other parties are still in it,
    //     keypadOpen       - the agent opened the keypad for digits,
    //     addingCall       - the agent pressed Add call and is entering the number to add,
    //     transferOpen     - the transfer panel is open,
    //     numberIsCallDisplay - the number field only shows the current call's number,
    //     canHold, canResume, canMute, canHangup, canTransfer, canAddCall, canDial - what the provider can do
    //   }
    // Returns {
    //   callControls, options - whether the primary and the secondary row are shown,
    //   mute, hold, keypad    - { visible, pressed },
    //   hangup                - { visible, kind: 'hangup' | 'leave' },
    //   transfer              - whether Transfer is offered,
    //   addCall               - { visible, disabledReason: '' | 'hold-unavailable' },
    //   endAll                - { visible, kind: 'calls' | 'conference' },
    //   dial, cancelAdd       - whether the dial button and Back to call are shown,
    //   showKeypad            - whether the keypad is shown
    // }
    function planInCallControls(options) {
        options = options || {};

        var stateName = String(options.stateName || 'Idle');
        var active = !!options.active;
        var connected = active && stateName === 'Connected';
        var held = active && stateName === 'OnHold';
        var live = connected || held;
        var inCall = active && !options.ringingOffer;
        var adding = inCall && !!options.addingCall;
        var parties = Number(options.conferenceParties) || 0;
        var dialOffered = typeof softPhone.shouldOfferDial === 'function'
            ? softPhone.shouldOfferDial({ callActive: active, stateName: stateName, numberIsCallDisplay: !!options.numberIsCallDisplay })
            : (!active || (held && !options.numberIsCallDisplay));
        var controls = inCall && !adding;
        var addCallVisible = controls && live && !!options.canDial;

        return {
            callControls: controls,
            options: controls && live,
            mute: { visible: controls && connected && !!options.canMute, pressed: !!options.muted },
            hold: {
                visible: controls && ((connected && !!options.canHold) || (held && !!options.canResume)),
                pressed: held
            },
            // The transfer panel takes the keypad's place; its own button goes back to the call.
            keypad: { visible: controls && connected && !options.transferOpen, pressed: !!options.keypadOpen },
            hangup: {
                visible: controls && !!options.canHangup,
                kind: options.isConference && parties >= 2 ? 'leave' : 'hangup'
            },
            transfer: controls && live && !!options.canTransfer,
            addCall: {
                visible: addCallVisible,
                // A connected call is held before the number is entered; a provider that cannot hold cannot add one.
                disabledReason: addCallVisible && connected && !options.canHold ? 'hold-unavailable' : ''
            },
            endAll: {
                visible: controls && (Number(options.lineCount) || 0) > 1 && !!options.canHangup,
                kind: options.isConference ? 'conference' : 'calls'
            },
            dial: !!options.canDial && (adding || dialOffered),
            cancelAdd: adding,
            showKeypad: !options.transferOpen && (!active || adding || held || (connected && !!options.keypadOpen))
        };
    }

    softPhone.planInCallControls = planInCallControls;
}(typeof globalThis !== 'undefined' ? globalThis : window));
