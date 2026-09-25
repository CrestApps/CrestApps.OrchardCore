/*
 * Keeping a mute the agent placed until the agent unmutes.
 *
 * With a provider that delivers the call's audio to this browser, muting happens here: the phone disables the
 * microphone track it sends, and the provider never hears of it. The server answers the Mute command with a muted call,
 * but it keeps nothing, so everything it says about the call afterwards -- the periodic active-call refresh, a state
 * push when anything else about the call changes -- describes it as unmuted. The phone took each of those reports at its
 * word and handed it to the media adapter, which turned the microphone back on about five seconds after the agent
 * pressed Mute (the next refresh), with the button flipping back to Mute as if the agent had never pressed it.
 *
 * The phone now remembers the mutes the agent placed. Where the mute is performed in this browser, only the agent
 * unmuting, or the call ending, ends it; where the provider mutes the call itself, the provider's report is the
 * authority.
 *
 * The phone sends one microphone track on every call it holds, so a mute is about the conversation the agent is in, not
 * one leg: calls merged into a conference share it, and a report about any of them must not unmute the others.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Remembers (muted true) or forgets (false) the agent's mute on a call.
    function rememberAgentMute(mutes, callId, muted) {
        if (!mutes || !callId) {
            return;
        }

        if (muted) {
            mutes[callId] = true;
        } else {
            delete mutes[callId];
        }
    }

    function isAgentMuted(mutes, callId) {
        return !!(mutes && callId && mutes[callId] === true);
    }

    // Forgets the mutes on every call that is no longer live, so the memory cannot outlast the calls it is about.
    function pruneAgentMutes(mutes, liveCallIds) {
        if (!mutes) {
            return;
        }

        var live = {};

        (liveCallIds || []).forEach(function (callId) {
            live[callId] = true;
        });

        Object.keys(mutes).forEach(function (callId) {
            if (!live[callId]) {
                delete mutes[callId];
            }
        });
    }

    // Whether the phone should show a call a report describes as muted.
    //   reportedMuted - the report's own mute flag.
    //   agentMuted    - whether the agent muted this call and has not unmuted it.
    //   muteIsLocal   - whether this browser performs the mute (the provider cannot see it).
    // Returns { isMuted, agentMuted }: the flag to apply, and whether the agent's mute still stands afterwards.
    function reconcileAgentMute(reportedMuted, agentMuted, muteIsLocal) {
        // This browser is the only one that knows: a report either way is an echo of a command, or knows nothing.
        if (muteIsLocal) {
            return { isMuted: !!agentMuted, agentMuted: !!agentMuted };
        }

        return { isMuted: !!reportedMuted, agentMuted: !!reportedMuted };
    }

    function metadataValue(call, key) {
        var metadata = call && call.metadata;

        return metadata && metadata[key] !== undefined && metadata[key] !== null ? metadata[key] : null;
    }

    function isConferenceCall(call) {
        var value = metadataValue(call, 'isConference');

        return value === true || value === 'true' || value === 'True';
    }

    // What names the conference a call is part of: its primary call, or else its conference's name.
    function conferenceKey(call) {
        var primary = metadataValue(call, 'conferencePrimaryCallId');

        if (primary) {
            return 'primary:' + String(primary);
        }

        var name = metadataValue(call, 'conferenceName');

        return name ? 'name:' + String(name) : '';
    }

    // The calls a mute of `call` applies to: the call itself and, when it is part of a conference, every other call
    // in that conference -- the agent speaks into all of them through the same microphone.
    function muteGroupCallIds(calls, call) {
        if (!call || !call.callId) {
            return [];
        }

        var ids = [call.callId];

        if (!isConferenceCall(call)) {
            return ids;
        }

        var key = conferenceKey(call);

        (calls || []).forEach(function (other) {
            if (!other || !other.callId || other.callId === call.callId || !isConferenceCall(other)) {
                return;
            }

            // Two conferences can never be up at once on one phone, so a conference call that names none is this one.
            var otherKey = conferenceKey(other);

            if (!key || !otherKey || otherKey === key) {
                ids.push(other.callId);
            }
        });

        return ids;
    }

    // Whether the calls just merged into a conference are muted: the agent was talking on one of them, and the
    // conference carries on as that call was -- muted or not -- so the button shown and the microphone agree.
    //   callIds       - the calls in the conference.
    //   mutes         - the agent's remembered mutes.
    //   talkingCallId - the call the agent was on when merging (the one the phone showed).
    function conferenceMuted(callIds, mutes, talkingCallId) {
        var ids = callIds || [];

        if (talkingCallId && ids.indexOf(talkingCallId) >= 0) {
            return isAgentMuted(mutes, talkingCallId);
        }

        return ids.some(function (callId) {
            return isAgentMuted(mutes, callId);
        });
    }

    softPhone.rememberAgentMute = rememberAgentMute;
    softPhone.isAgentMuted = isAgentMuted;
    softPhone.pruneAgentMutes = pruneAgentMutes;
    softPhone.reconcileAgentMute = reconcileAgentMute;
    softPhone.muteGroupCallIds = muteGroupCallIds;
    softPhone.conferenceMuted = conferenceMuted;
}(typeof globalThis !== 'undefined' ? globalThis : window));
