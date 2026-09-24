/*
 * What the dial button may call.
 *
 * While a call is held the keypad opens for adding a call, and the number field keeps showing the held call's number
 * as a display. The dial button used to take whatever the field held. For a call the platform bridged to the agent
 * that display was the tenant's own caller id, and the dial button appears first in the row -- exactly where the Hold
 * button had been a moment earlier -- so an agent reaching to resume called the tenant from the tenant: a second call
 * on top of the held one, which came straight back in as a new inbound call.
 *
 * The dial button now dials only a number the agent entered, and never adds a call to the tenant's own outbound caller
 * id; and it is not offered at all while the field only shows the held call's number.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function digitsOf(value) {
        return value == null ? '' : String(value).replace(/\D+/g, '');
    }

    // Whether two numbers are the same number, however each is written.
    function isSameNumber(left, right) {
        var a = digitsOf(left);

        return !!a && a === digitsOf(right);
    }

    // The number to dial.
    //   number        - what the number field holds.
    //   isCallDisplay - whether the field is only showing the current call's number, not something the agent entered.
    //   liveCall      - whether a call is already in progress (the dial adds a call to it).
    //   ownNumbers    - the tenant's own outbound caller ids.
    // Returns { number, refused }: the number to dial, or '' with why it was refused ('call-display' | 'own-number').
    function resolveDialTarget(options) {
        options = options || {};

        var number = options.number ? String(options.number) : '';

        if (!number) {
            return { number: '', refused: '' };
        }

        if (options.isCallDisplay) {
            return { number: '', refused: 'call-display' };
        }

        if (options.liveCall && (options.ownNumbers || []).some(function (own) { return isSameNumber(number, own); })) {
            return { number: '', refused: 'own-number' };
        }

        return { number: number, refused: '' };
    }

    // Whether to show the dial button: with no call, or on hold once the agent has entered a number to add.
    function shouldOfferDial(options) {
        options = options || {};

        if (!options.callActive) {
            return true;
        }

        return options.stateName === 'OnHold' && !options.numberIsCallDisplay;
    }

    // Whether the number field should show the current call's number. On a held call the field is where the agent enters
    // the number to add, and the dial button only appears once they have; writing the held call's number back over that
    // entry on the next render would hide the button again and throw away what they typed.
    //   stateName    - the current call's state (normalized: 'Connected', 'OnHold', ...).
    //   agentEntered - whether the field holds something the agent entered since it last showed a call.
    function shouldShowCallNumber(options) {
        options = options || {};

        return !(options.stateName === 'OnHold' && options.agentEntered);
    }

    // The other party's number, for the number field and the active-call list. The agent's leg of a call the platform
    // bridged here is placed from the tenant's own number, so its "from" is the tenant itself; the other side is tried
    // next, and nothing is shown rather than the tenant's own number.
    //   call       - the call ({ direction, from, to }).
    //   ownNumbers - the tenant's own outbound caller ids.
    function resolvePeerNumber(call, ownNumbers) {
        if (!call) {
            return '';
        }

        var inbound = call.direction === 1 || call.direction === 'Inbound';
        var candidates = inbound ? [call.from, call.to] : [call.to, call.from];
        var own = ownNumbers || [];

        for (var i = 0; i < candidates.length; i++) {
            var candidate = candidates[i] ? String(candidates[i]) : '';

            if (candidate && !own.some(isOwn(candidate))) {
                return candidate;
            }
        }

        return '';
    }

    function isOwn(candidate) {
        return function (number) {
            return isSameNumber(candidate, number);
        };
    }

    softPhone.isSameNumber = isSameNumber;
    softPhone.resolvePeerNumber = resolvePeerNumber;
    softPhone.resolveDialTarget = resolveDialTarget;
    softPhone.shouldOfferDial = shouldOfferDial;
    softPhone.shouldShowCallNumber = shouldShowCallNumber;
}(typeof globalThis !== 'undefined' ? globalThis : window));
