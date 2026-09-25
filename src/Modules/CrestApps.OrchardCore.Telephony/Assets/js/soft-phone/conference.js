/*
 * Merging calls into a conference, and showing the conference once it exists.
 *
 * With one call held and a second call up, the only way to join them was to tick a checkbox beside each call in the
 * active-call list and then find an unlabelled icon among the call buttons, which only appeared once two boxes were
 * ticked. Agents did not find it. The phone then offered a labelled Merge that joined every call it could, which with
 * three or more calls up was not what the agent meant. Now every call line carries its own checkbox, in the keypad view
 * where the lines are listed; Merge is offered as soon as two lines could be joined, names how many it joins, and does
 * something only once two or more are ticked. A running conference counts as one line: ticking it and another call adds
 * that call to the conference. A conference's participants are listed under their own heading, each with a hang-up.
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

    function metadataText(call, key) {
        var value = call && call.metadata ? call.metadata[key] : null;

        return value == null ? '' : String(value);
    }

    // Merging is a server command over calls the server tracks (see soft-phone/call-legs.js); a call this browser
    // placed itself is never part of one.
    function canJoin(call) {
        return typeof softPhone.canConferenceCall === 'function'
            ? softPhone.canConferenceCall(call)
            : !!(call && call.callId && !call.browserOriginated);
    }

    // What a Merge would do with the calls the agent ticked.
    //   calls      - the active calls.
    //   selections - { callId: true } for the calls the agent ticked.
    //   options    - { stateOf(call) -> 'Connected' | 'OnHold' | ..., isConference(call) -> bool }.
    // Returns:
    //   offered          - two or more lines could be joined, so the Merge action is shown.
    //   canMerge         - two or more of them are ticked, so the Merge action does something.
    //   selectedCount    - how many lines are ticked; a running conference counts as one line.
    //   callIds          - the calls the merge command names: the conference's own call first when adding to it.
    //   calls            - the ticked calls.
    //   addsToConference - one of the ticked lines is a running conference the others join.
    //   conferenceName   - that conference's name, when the provider gave it one.
    //   eligibleCallIds  - every call that can be ticked, for Select all.
    //   allSelected      - every call that can be ticked is.
    //   blocked          - 'browser-call' when a call on screen cannot be merged because this browser placed it.
    function planMerge(calls, selections, options) {
        options = options || {};
        selections = selections || {};

        var stateOf = options.stateOf || defaultStateOf;
        var isConference = options.isConference || defaultIsConference;
        var live = (calls || []).filter(function (call) {
            var state = stateOf(call);

            return call && (state === 'Connected' || state === 'OnHold');
        });
        var eligible = live.filter(canJoin);
        var participants = eligible.filter(isConference);
        var others = eligible.filter(function (call) { return !isConference(call); });
        var ticked = function (call) { return !!selections[call.callId]; };
        var conferenceTicked = participants.some(ticked);
        var tickedOthers = others.filter(ticked);
        var units = (participants.length ? 1 : 0) + others.length;
        var selectedCount = (conferenceTicked ? 1 : 0) + tickedOthers.length;
        var canMerge = selectedCount >= 2;
        var primary = null;
        var conferenceName = '';

        if (participants.length) {
            // A provider's report of one participant replaces what the phone noted on it, so the conference's own call
            // and name are read from whichever participant still carries them.
            var primaryId = participants.map(function (call) { return metadataText(call, 'conferencePrimaryCallId'); }).filter(Boolean)[0];

            primary = participants.filter(function (call) { return call.callId === primaryId; })[0] || participants[0];
            conferenceName = metadataText(primary, 'conferenceName') ||
                participants.map(function (call) { return metadataText(call, 'conferenceName'); }).filter(Boolean)[0] || '';
        }

        var chosen = conferenceTicked ? [primary].concat(tickedOthers) : tickedOthers;

        return {
            offered: units >= 2,
            canMerge: canMerge,
            selectedCount: selectedCount,
            callIds: canMerge ? chosen.map(function (call) { return call.callId; }) : [],
            calls: (conferenceTicked ? participants.filter(ticked) : []).concat(tickedOthers),
            addsToConference: canMerge && conferenceTicked,
            conferenceName: conferenceTicked ? conferenceName : '',
            eligibleCallIds: eligible.map(function (call) { return call.callId; }),
            allSelected: eligible.length > 0 && eligible.every(ticked),
            blocked: live.length > eligible.length ? 'browser-call' : ''
        };
    }

    // The conference a merge that succeeded made: every call now in it, the call it was made from and its name, which
    // the next "add to conference" names so the provider joins that conference rather than making another.
    //   calls   - the active calls.
    //   plan    - the plan the merge was sent from (see planMerge).
    //   result  - the provider's answer: its call is the call the conference was made from, and may carry the name.
    //   options - { isConference(call) -> bool }.
    // Returns { callIds, primaryCallId, conferenceName }.
    function conferenceAfterMerge(calls, plan, result, options) {
        options = options || {};

        var isConference = options.isConference || defaultIsConference;
        var callIds = plan.addsToConference
            ? (calls || []).filter(isConference).map(function (call) { return call.callId; })
            : [];

        (plan.callIds || []).forEach(function (callId) {
            if (callIds.indexOf(callId) === -1) {
                callIds.push(callId);
            }
        });

        var resultCall = result && result.call ? result.call : null;

        return {
            callIds: callIds,
            primaryCallId: plan.addsToConference || !resultCall || !resultCall.callId ? (plan.callIds || [])[0] || '' : String(resultCall.callId),
            conferenceName: metadataText(resultCall, 'conferenceName') || plan.conferenceName || ''
        };
    }

    function format(template, value) {
        return String(template).replace('{0}', value);
    }

    function unselectableText(reason, strings) {
        return reason === 'browser-call'
            ? strings.cannotMergeBrowserCall || 'A call dialed from this phone cannot be merged into a conference.'
            : '';
    }

    function row(item, strings, escapeHtml, inConference) {
        var reason = item.selectable ? '' : unselectableText(item.unselectableReason, strings);
        var check = '<input type="checkbox" class="telephony-soft-phone__active-call-check" data-telephony-conference-call="' +
            escapeHtml(item.callId) + '"' + (item.selected && item.selectable ? ' checked' : '') +
            (item.selectable ? '' : ' disabled') + (reason ? ' title="' + escapeHtml(reason) + '"' : '') + ' aria-label="' +
            escapeHtml(format(strings.selectCall || 'Select {0}', item.number)) + '" />';
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

    function mergeBarHtml(merge, strings, escapeHtml) {
        var names = (merge.numbers || []).join(' + ');
        var summary;
        var action;

        if (!merge.canMerge) {
            summary = strings.mergeSelectHint || 'Tick two or more calls to merge them.';
            action = strings.mergeCalls || 'Merge calls';
        } else if (merge.addsToConference) {
            summary = format(strings.addToConferenceSummary || 'Add {0} to the conference.', names);
            action = strings.addToConference || 'Add to conference';
        } else {
            summary = format(strings.mergeSummary || 'Join {0} in one conference.', names);
            action = format(strings.mergeSelectedCalls || 'Merge {0} calls', merge.selectedCount);
        }

        return '<div class="telephony-soft-phone__merge-bar">' +
            '<label class="telephony-soft-phone__merge-select-all">' +
            '<input type="checkbox" data-telephony-merge-select-all' + (merge.allSelected ? ' checked' : '') + ' /> ' +
            '<span>' + escapeHtml(strings.selectAllCalls || 'Select all') + '</span></label>' +
            '<span class="telephony-soft-phone__merge-summary" data-telephony-merge-summary role="status">' + escapeHtml(summary) + '</span>' +
            '<button type="button" class="btn btn-sm btn-primary telephony-soft-phone__merge-calls" data-telephony-merge-calls' +
            (merge.canMerge ? '' : ' disabled') + '>' +
            '<i class="fa-solid fa-code-merge" aria-hidden="true"></i> ' + escapeHtml(action) +
            '</button></div>';
    }

    // The active-call list: a checkbox beside every line, the conference's participants under their own heading, the
    // other calls, and -- when two or more lines could be joined -- the Merge action for the ticked ones.
    //   model - { calls: [{ callId, number, state, current, selectable, unselectableReason, selected, inConference,
    //                       canHangup }],
    //             merge: { offered, canMerge, selectedCount, addsToConference, allSelected, numbers: [], blocked } }
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

        if (merge.blocked) {
            html += '<div class="telephony-soft-phone__merge-note" data-telephony-merge-blocked>' +
                '<i class="fa-solid fa-circle-info" aria-hidden="true"></i> ' +
                escapeHtml(unselectableText(merge.blocked, strings)) + '</div>';
        }

        if (merge.offered) {
            html += mergeBarHtml(merge, strings, escapeHtml);
        }

        return html;
    }

    softPhone.planMerge = planMerge;
    softPhone.conferenceAfterMerge = conferenceAfterMerge;
    softPhone.buildActiveCallsHtml = buildActiveCallsHtml;
}(typeof globalThis !== 'undefined' ? globalThis : window));
