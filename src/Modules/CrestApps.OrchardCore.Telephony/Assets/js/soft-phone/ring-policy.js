// @ts-check
/*
 * One ringing call at a time.
 *
 * A person takes one call at a time, so the soft phone rings for one incoming call at most. A provider leg nobody told
 * the phone to expect -- a colleague dialing this agent's extension, a call handed over, or the platform's leg of a call
 * the phone did not know of -- used to take over the prompt even while another call was ringing. The earlier leg was
 * only dropped from the screen: its call was never declined, so it kept ringing at the provider, and the desktop host
 * that notifies for the page heard the Contact Center offer from its own connection while the page reported the leg.
 * The agent saw two ringing calls at once.
 *
 * Now the call already ringing keeps the prompt and a new leg is declined, as one is while the agent is on a call. A
 * Contact Center offer is the work routing chose for the agent, so it wins over a leg still ringing here: that leg is
 * declined, not hidden.
 *
 * Part of the soft phone: an ES module that soft-phone.js, or another of its helpers, imports, and that the unit tests
 * import directly.
 */

export var INBOUND_RING_ACTIONS = {
    ring: 'ring',
    decline: 'decline'
};

// What to do with an incoming leg that would ring as a call of its own.
//   state.onCall    - the agent is on a call (anything active that is not an incoming call still ringing).
//   state.ringing   - another incoming call is ringing on this phone now (an offer or an earlier leg).
//   state.answering - the agent has already answered the call that is ringing, and the answer is still under way. A
//                     leg arriving then is most likely that call's own, come later than the phone expected it, so it is
//                     never declined: declining it would drop the call the agent is taking.
export function inboundRingAction(state) {
    state = state || {};

    if (state.onCall) {
        return INBOUND_RING_ACTIONS.decline;
    }

    return state.ringing && !state.answering ? INBOUND_RING_ACTIONS.decline : INBOUND_RING_ACTIONS.ring;
}

// Whether a Contact Center offer arriving now declines the leg ringing here.
//   ringCallId  - the id of the leg ringing on this phone that the agent has not answered, or '' for none.
//   offerCallId - the id of the offer's call.
export function offerDeclinesRing(ringCallId, offerCallId) {
    return !!ringCallId && ringCallId !== offerCallId;
}
