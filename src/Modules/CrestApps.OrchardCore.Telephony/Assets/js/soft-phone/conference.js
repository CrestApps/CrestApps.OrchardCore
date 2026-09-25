/*
 * Merging calls into a conference, and showing the conference once it exists.
 *
 * With one call held and a second call up, the only way to join them was to tick a checkbox beside each call in the
 * active-call list and then find an unlabelled icon among the call buttons, which only appeared once two boxes were
 * ticked. Agents did not find it. The phone now offers a labelled Merge action whenever two or more calls can be joined,
 * naming the calls it will join, and lists a conference's participants under their own heading, each with a hang-up.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function defaultStateOf(call) {
        return call ? String(call.state || '') : '';
    }

    function defaultIsConference(call) {
        var value = call && call.metadata ? call.metadata.isConference : false;

        return value === true || value === 1 || value === 'true' || value === 'True';
    }

    // Merging is a server command over calls the server tracks (see soft-phone/call-legs.js); a call this browser
    // placed itself is never part of one.
    function canJoin(call) {
        return typeof softPhone.canConferenceCall === 'function'
            ? softPhone.canConferenceCall(call)
            : !!(call && call.callId && !call.browserOriginated);
    }

    // Which calls a Merge would join.
    //   calls      - the active calls.
    //   selections - { callId: true } for the calls the agent ticked.
    //   options    - { stateOf(call) -> 'Connected' | 'OnHold' | ..., isConference(call) -> bool }.
    // Returns { available, callIds, calls, usesSelection }. Two or more ticked calls are merged on their own; otherwise
    // every call that can join is. Nothing is offered when the calls are already one conference.
    function planMerge(calls, selections, options) {
        options = options || {};

        var stateOf = options.stateOf || defaultStateOf;
        var isConference = options.isConference || defaultIsConference;
        var eligible = (calls || []).filter(function (call) {
            var state = stateOf(call);

            return canJoin(call) && (state === 'Connected' || state === 'OnHold');
        });
        var selected = eligible.filter(function (call) {
            return !!(selections && selections[call.callId]);
        });
        var usesSelection = selected.length >= 2;
        var chosen = usesSelection ? selected : eligible;
        var alreadyMerged = chosen.length > 0 && chosen.every(isConference);

        return {
            available: chosen.length >= 2 && !alreadyMerged,
            callIds: chosen.map(function (call) { return call.callId; }),
            calls: chosen,
            usesSelection: usesSelection
        };
    }

    function format(template, value) {
        return String(template).replace('{0}', value);
    }

    function row(item, strings, escapeHtml, inConference) {
        var check = item.selectable
            ? '<input type="checkbox" class="telephony-soft-phone__active-call-check" data-telephony-conference-call="' +
                escapeHtml(item.callId) + '"' + (item.selected ? ' checked' : '') + ' aria-label="' +
                escapeHtml(format(strings.selectCall || 'Select {0}', item.number)) + '" />'
            : '';
        var hangup = inConference && item.canHangup
            ? '<button type="button" class="telephony-soft-phone__participant-hangup" data-telephony-participant-hangup="' +
                escapeHtml(item.callId) + '" title="' + escapeHtml(format(strings.hangupParticipant || 'Hang up {0}', item.number)) +
                '" aria-label="' + escapeHtml(format(strings.hangupParticipant || 'Hang up {0}', item.number)) + '">' +
                '<i class="fa-solid fa-phone-slash" aria-hidden="true"></i></button>'
            : '';

        return '<div class="telephony-soft-phone__active-call' + (item.current ? ' is-current' : '') +
            (inConference ? ' is-participant' : '') + '"' + (inConference ? ' data-telephony-conference-participant="' + escapeHtml(item.callId) + '"' : '') + '>' +
            check +
            '<button type="button" class="telephony-soft-phone__active-call-select" data-telephony-call-select="' +
            escapeHtml(item.callId) + '">' +
            '<span class="telephony-soft-phone__active-call-number">' + escapeHtml(item.number) + '</span>' +
            '<span class="telephony-soft-phone__active-call-state">' + escapeHtml(item.state) + '</span>' +
            '</button>' + hangup + '</div>';
    }

    // The active-call list: the conference's participants under their own heading, the other calls, and -- when calls
    // can be joined -- the Merge action naming them.
    //   model - { calls: [{ callId, number, state, current, selectable, selected, inConference, canHangup }],
    //             merge: { available, numbers: [] } }
    function buildActiveCallsHtml(model, strings, escapeHtml) {
        model = model || {};
        strings = strings || {};

        var calls = model.calls || [];
        var participants = calls.filter(function (call) { return call.inConference; });
        var others = calls.filter(function (call) { return !call.inConference; });
        var html = '';

        if (participants.length) {
            html += '<div class="telephony-soft-phone__conference" data-telephony-conference role="group" aria-label="' +
                escapeHtml(format(strings.conferenceParticipants || 'Conference · {0} participants', participants.length)) + '">' +
                '<div class="telephony-soft-phone__conference-heading">' +
                '<i class="fa-solid fa-users" aria-hidden="true"></i> ' +
                escapeHtml(format(strings.conferenceParticipants || 'Conference · {0} participants', participants.length)) +
                '</div>' +
                participants.map(function (call) { return row(call, strings, escapeHtml, true); }).join('') +
                '</div>';
        }

        html += others.map(function (call) { return row(call, strings, escapeHtml, false); }).join('');

        var merge = model.merge || {};

        if (merge.available) {
            var names = (merge.numbers || []).join(' + ');

            html += '<div class="telephony-soft-phone__merge-bar">' +
                '<span class="telephony-soft-phone__merge-summary" data-telephony-merge-summary>' +
                escapeHtml(format(strings.mergeSummary || 'Join {0} in one conference.', names)) + '</span>' +
                '<button type="button" class="btn btn-sm btn-primary telephony-soft-phone__merge-calls" data-telephony-merge-calls>' +
                '<i class="fa-solid fa-code-merge" aria-hidden="true"></i> ' + escapeHtml(strings.mergeCalls || 'Merge calls') +
                '</button></div>';
        }

        return html;
    }

    softPhone.planMerge = planMerge;
    softPhone.buildActiveCallsHtml = buildActiveCallsHtml;
}(typeof globalThis !== 'undefined' ? globalThis : window));
