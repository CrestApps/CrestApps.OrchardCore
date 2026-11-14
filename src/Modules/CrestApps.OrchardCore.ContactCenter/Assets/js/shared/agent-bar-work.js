// @ts-check
/*
 * When the docked agent bar opens itself, and when it opens the record of the agent's call, from the workspace state.
 *
 * A call the dialer places for an agent (Power, Progressive, Predictive) has no offer: the agent never accepts it, so the
 * bar, which opened itself only for an offer, stayed tucked away when the agent was connected, and tucked itself away
 * again at wrap-up, hiding "Complete activity". The record opened only from the one push saying the agent joined the
 * call; a page that missed it (loading, reconnecting) never opened it, and agents were left on a call with no record and
 * no idea what to do.
 *
 * The bar now opens once for each call that has a record -- when it connects, and again when it ends so the agent can
 * complete the activity -- and opens the record of an automatic dial from any state refresh that shows the agent is on
 * it, once per call in the tab, so an agent who moved on from the record is never sent back to it.
 *
 * An ES module: the agent bar imports it, the asset build bundles it in, and the unit tests import it directly.
 */

// What the bar opens itself for in this state, or '' for nothing: the ringing offer, else the agent's call that has a
// record to complete -- live, then ended. Each key opens the bar once, so the agent can still close it.
//   state - the workspace state: { offer, activeInteraction }.
export function autoOpenWorkKey(state) {
    var offer = state && state.offer;

    if (offer && offer.reservationId) {
        return 'offer:' + offer.reservationId;
    }

    var active = state && state.activeInteraction;

    if (!active || !active.interactionId || !active.activityItemId) {
        return '';
    }

    return 'call:' + active.interactionId + (isEndedInteraction(active) ? ':ended' : '');
}

export function isEndedInteraction(active) {
    return !!active && (active.status === 'Ended' || active.status === 'Failed');
}

// The activity to open now for an automatic dial the agent is connected to, or '' when there is none or it was already
// opened in this tab (`poppedActivityIds`, a set of ids as an object).
export function activityToAutoOpen(state, poppedActivityIds) {
    var active = state && state.activeInteraction;

    if (!active || active.autoOpenActivity !== true || !active.activityItemId) {
        return '';
    }

    var id = String(active.activityItemId);

    return poppedActivityIds && Object.prototype.hasOwnProperty.call(poppedActivityIds, id) ? '' : id;
}

// Whether an offer notification names this user as the agent the offer was made to. The server also sends every
// offer's notifications to the offer's queue and to that queue's supervisors, for their views, so one arriving here is
// not proof it is this agent's. Unlike a refresh, opening a record is only ever done for the agent's own offer: an
// unknown owner on either side is never taken as this agent's.
//   notification - the offer notification: { userId }.
//   ownUserId    - the signed-in user, from the workspace state.
export function isOfferForUser(notification, ownUserId) {
    return !!notification && !!notification.userId && !!ownUserId &&
        String(notification.userId) === String(ownUserId);
}

// The activity to open because an offer was accepted, or '' for none. Only the agent who accepted it lands on its
// record. Live: administrators who had once signed in to a queue were taken off the page they were on, to the record
// of a call another agent had just answered -- two of them to the same record at once -- because every screen that
// heard the acceptance opened it. An automatic dial is accepted by the dialer before the call is placed, so its record
// waits until the agent is connected.
//   notification - the OfferRevoked notification: { userId, reason, kind, activityItemId }.
export function acceptedOfferActivityToOpen(notification, ownUserId) {
    if (!isOfferForUser(notification, ownUserId) ||
        notification.reason !== 'Accepted' ||
        notification.kind === 'AutoDial' ||
        !notification.activityItemId) {
        return '';
    }

    return String(notification.activityItemId);
}

// The activities this tab already opened, kept across the page loads a pop causes. Storage can be missing or refuse
// access (a private window, blocked site data); the bar then only remembers them for the page.
var POPPED_STORAGE_KEY = 'cc-agent-bar:popped-activities';
var POPPED_LIMIT = 50;

export function readPoppedActivities(storage) {
    try {
        var raw = storage ? storage.getItem(POPPED_STORAGE_KEY) : null;
        var ids = raw ? JSON.parse(raw) : [];
        var set = {};

        if (Array.isArray(ids)) {
            ids.forEach(function (id) {
                if (id) {
                    set[String(id)] = true;
                }
            });
        }

        return set;
    } catch (error) {
        return {};
    }
}

// Remembers `activityId` as opened, keeping only the most recent ones.
export function rememberPoppedActivity(storage, poppedActivityIds, activityId) {
    if (!activityId) {
        return;
    }

    var id = String(activityId);

    delete poppedActivityIds[id];
    poppedActivityIds[id] = true;

    var ids = Object.keys(poppedActivityIds);

    while (ids.length > POPPED_LIMIT) {
        delete poppedActivityIds[ids.shift()];
    }

    try {
        if (storage) {
            storage.setItem(POPPED_STORAGE_KEY, JSON.stringify(Object.keys(poppedActivityIds)));
        }
    } catch (error) { /* best effort */ }
}
