// @ts-check
/*
 * Standing by for an over-dialing campaign: answering, at once, the leg of a call this agent was just claimed for.
 *
 * An over-dialing campaign places calls before any agent is chosen, and claims a free agent only when a person answers.
 * The platform then rings the agent's leg straight away, and the push telling this phone about the claim travels
 * separately: when the invite won the race, the phone rang the leg as an unsolicited call (or refused it as busy) while
 * the person who answered listened to silence, and the two-second limit on connecting them ran out.
 *
 * While the agent is Available and signed in to a campaign, the phone stands by. A leg the platform rings for a claim
 * carries two SIP headers -- the claim (reservation) and the user it was made for -- and a standing-by phone answers a
 * leg carrying both, naming its own user, without waiting for the push. Nothing else is answered on the strength of the
 * standby: a leg without the tag, a leg for another user, a colleague's call to this phone and a call handed over all
 * ring as before, and inbound offers keep their accept.
 *
 * The claim also takes the agent off Available, and that push can arrive a moment before the invite, so a standby just
 * dropped still answers a tagged leg for a few seconds. A leg answered or seen on standby is remembered for a minute: the
 * push about its claim arriving afterwards must not arm the phone to answer whatever leg comes next.
 *
 * Part of the soft phone: an ES module that imports the header and destination-leg readers of offer-leg.js and
 * auto-answer.js. The soft phone and the Contact Center soft phone import it, and the unit tests import it directly.
 */
import { isDestinationLeg } from './auto-answer.js';
import { readProviderHeader } from './offer-leg.js';

// The SIP headers the platform tags a claimed agent's leg with (see TelnyxConstants on the server).
var STANDBY_RESERVATION_HEADER = 'x-cc-reservation';
var STANDBY_AGENT_USER_HEADER = 'x-cc-agent-user';

// How long a standby just dropped still answers a tagged leg.
export var STANDBY_GRACE_MS = 5000;

// How long a tagged leg is remembered once seen.
export var STANDBY_LEG_MEMORY_MS = 60000;

export function createPredictiveStandby() {
    return { armed: false, until: 0, userId: '', legs: {} };
}

// Whether the agent should stand by: Available, and signed in to at least one campaign. Only a campaign can claim the
// agent for a call without offering it; a queue offers its calls.
export function isStandbyEligible(presenceStatus, campaignIds) {
    return presenceStatus === 'Available' && Array.isArray(campaignIds) && campaignIds.some(function (id) {
        return !!id;
    });
}

// Stands by (armed true) for `userId`, or stops standing by after the grace period. Returns whether it stands by.
export function setPredictiveStandby(standby, armed, userId, now, graceMs) {
    if (!standby) {
        return false;
    }

    if (userId) {
        standby.userId = String(userId);
    }

    if (armed) {
        standby.armed = true;
        standby.until = 0;

        return true;
    }

    if (standby.armed) {
        standby.armed = false;
        standby.until = now + (typeof graceMs === 'number' && graceMs >= 0 ? graceMs : STANDBY_GRACE_MS);
    }

    return false;
}

export function isStandingBy(standby, now) {
    return !!standby && (standby.armed || now < standby.until);
}

// What a leg's SIP headers say about a standby claim: { reservationId, agentUserId }, both '' for an untagged leg.
export function readStandbyLegTag(options) {
    var readHeader = readProviderHeader;
    var headers = options ? (options.customHeaders || options.custom_headers) : null;

    if (typeof readHeader !== 'function' || !headers) {
        return { reservationId: '', agentUserId: '' };
    }

    return {
        reservationId: readHeader(headers, STANDBY_RESERVATION_HEADER),
        agentUserId: readHeader(headers, STANDBY_AGENT_USER_HEADER)
    };
}

function forgetOldLegs(standby, now) {
    Object.keys(standby.legs).forEach(function (id) {
        if (now - standby.legs[id] > STANDBY_LEG_MEMORY_MS) {
            delete standby.legs[id];
        }
    });
}

// Remembers that the leg of a claim reached this phone, answered or not.
export function noteStandbyLeg(standby, reservationId, now) {
    if (!standby || !reservationId) {
        return;
    }

    forgetOldLegs(standby, now);
    standby.legs[reservationId] = now;
}

// Whether the leg of this claim already reached this phone: an accept arriving after it must not arm the phone for a
// leg that will never come.
export function wasStandbyLegSeen(standby, reservationId, now) {
    if (!standby || !reservationId || typeof standby.legs[reservationId] !== 'number') {
        return false;
    }

    return now - standby.legs[reservationId] <= STANDBY_LEG_MEMORY_MS;
}

// Decides whether an incoming leg is answered on standby. Returns the claim's reservation id when it is, '' when the
// phone's usual rules apply.
//   leg     - the incoming leg's options: { customHeaders | custom_headers, clientState, transferLeg }.
//   context - { ownUserId, onCall }: this phone's user (when known better than the standby's), and whether the phone
//             is already on a call.
export function shouldStandbyAnswerLeg(standby, leg, now, context) {
    if (!leg || !isStandingBy(standby, now)) {
        return '';
    }

    // Somebody's call to this phone, or a call handed over, rings whatever it carries.
    if (leg.transferLeg || (typeof isDestinationLeg === 'function' && isDestinationLeg(leg))) {
        return '';
    }

    if (context && context.onCall) {
        return '';
    }

    var tag = readStandbyLegTag(leg);

    if (!tag.reservationId || !tag.agentUserId) {
        return '';
    }

    var ownUserId = (context && context.ownUserId) || standby.userId;

    if (!ownUserId || String(ownUserId) !== tag.agentUserId) {
        return '';
    }

    return tag.reservationId;
}
