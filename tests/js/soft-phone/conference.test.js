import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-legs.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/conference.js';

const {
    planMerge,
    conferenceAfterMerge,
    buildActiveCallsHtml,
    createConferenceMemory,
    rememberConference,
    applyConferenceMemory,
    markParticipantLeft,
    forgetConferenceCall,
    conferenceMembers,
    visibleConferenceCalls,
    isOneConference,
    conferenceLegsWithoutParticipants
} = globalThis.CrestAppsSoftPhone;

const escapeHtml = value => String(value == null ? '' : value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

const held = { callId: 'a', state: 'OnHold' };
const live = { callId: 'b', state: 'Connected' };
const third = { callId: 'c', state: 'OnHold' };

// With several calls up, the agent picks the calls to join by ticking the box beside each line; Merge is offered as soon
// as two lines could be joined, and does something only once two or more are ticked.
describe('planMerge', () => {
    it('offers a merge for two calls that can be joined, but waits for the agent to tick them', () => {
        expect(planMerge([held, live], {})).toMatchObject({
            offered: true,
            canMerge: false,
            selectedCount: 0,
            callIds: [],
            eligibleCallIds: ['a', 'b'],
            allSelected: false
        });
    });

    it('does not merge a single ticked call', () => {
        expect(planMerge([held, live], { a: true })).toMatchObject({ offered: true, canMerge: false, selectedCount: 1 });
    });

    it('merges the two calls once both are ticked', () => {
        expect(planMerge([held, live], { a: true, b: true })).toMatchObject({
            canMerge: true,
            selectedCount: 2,
            callIds: ['a', 'b'],
            calls: [held, live],
            addsToConference: false,
            allSelected: true
        });
    });

    it('merges only the ticked calls out of three, and all three when all are ticked', () => {
        expect(planMerge([held, live, third], { a: true, c: true }).callIds).toEqual(['a', 'c']);
        expect(planMerge([held, live, third], { a: true, b: true, c: true })).toMatchObject({ canMerge: true, selectedCount: 3, callIds: ['a', 'b', 'c'] });
    });

    it('offers nothing with a single call', () => {
        expect(planMerge([live], {}).offered).toBe(false);
    });

    it('ignores a tick left on a call that has since ended', () => {
        expect(planMerge([held, live], { a: true, gone: true })).toMatchObject({ canMerge: false, selectedCount: 1 });
    });

    // A call dialed from this browser runs in the provider SDK alone, so the server cannot join it to anything.
    it('never counts a call this browser placed itself, and says why nothing can be merged', () => {
        const browserCall = { callId: 'x', state: 'Connected', browserOriginated: true };

        expect(planMerge([held, browserCall], { a: true, x: true })).toMatchObject({ offered: false, canMerge: false, blocked: 'browser-call' });
        expect(planMerge([held, live, browserCall], {})).toMatchObject({ offered: true, blocked: 'browser-call', eligibleCallIds: ['a', 'b'] });
        expect(planMerge([held, live], {}).blocked).toBe('');
    });

    it('does not count a call that is still ringing or connecting', () => {
        expect(planMerge([held, { callId: 'r', state: 'Ringing' }], {}).offered).toBe(false);
    });

    it('offers nothing once the calls are already one conference', () => {
        const merged = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true } },
            { callId: 'b', state: 'Connected', metadata: { isConference: 'true' } }
        ];

        expect(planMerge(merged, { a: true, b: true })).toMatchObject({ offered: false, canMerge: false });
    });

    describe('with a conference already running', () => {
        const calls = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'b', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'n', state: 'OnHold' }
        ];

        it('counts the conference as one line to join', () => {
            expect(planMerge(calls, {})).toMatchObject({ offered: true, canMerge: false });
            expect(planMerge(calls, { a: true })).toMatchObject({ canMerge: false, selectedCount: 1 });
        });

        it('adds a ticked call to the conference, asking the provider for the conference it already made', () => {
            expect(planMerge(calls, { b: true, n: true })).toMatchObject({
                canMerge: true,
                selectedCount: 2,
                addsToConference: true,
                conferenceName: 'conf-a',
                callIds: ['a', 'n']
            });
        });

        it('names the conference by the call it was made from', () => {
            const madeFromB = calls.map(call => call.metadata
                ? { ...call, metadata: { ...call.metadata, conferencePrimaryCallId: 'b' } }
                : call);

            expect(planMerge(madeFromB, { a: true, n: true }).callIds).toEqual(['b', 'n']);
        });

        // A provider's report of the held participant replaces the call, and with it what the phone noted on it.
        it('reads the conference\'s own call and name from whichever participant still carries them', () => {
            const reported = [
                { callId: 'a', state: 'OnHold', metadata: { isConference: true } },
                { callId: 'b', state: 'Connected', metadata: { isConference: true, conferencePrimaryCallId: 'b', conferenceName: 'conf-b' } },
                { callId: 'n', state: 'Connected' }
            ];

            expect(planMerge(reported, { a: true, n: true })).toMatchObject({ callIds: ['b', 'n'], conferenceName: 'conf-b' });
        });
    });

    it('reads the state and conference flag through the accessors it is given', () => {
        const plan = planMerge([{ callId: 'a', state: 3 }, { callId: 'b', state: 4 }], { a: true, b: true }, {
            stateOf: call => (call.state === 3 ? 'Connected' : 'OnHold'),
            isConference: () => false
        });

        expect(plan.canMerge).toBe(true);
    });
});

describe('conferenceAfterMerge', () => {
    it('makes the merged calls one conference, named after the call the provider made it from', () => {
        const plan = planMerge([held, live, third], { a: true, c: true });

        expect(conferenceAfterMerge([held, live, third], plan, { call: { callId: 'a', metadata: { conferenceName: 'conf-a' } } }))
            .toEqual({ callIds: ['a', 'c'], primaryCallId: 'a', conferenceName: 'conf-a' });
    });

    it('keeps the running conference and the call it was made from when a call is added to it', () => {
        const calls = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'b', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'n', state: 'OnHold' }
        ];
        const plan = planMerge(calls, { a: true, n: true });

        expect(conferenceAfterMerge(calls, plan, { call: { callId: 'a' } }))
            .toEqual({ callIds: ['a', 'b', 'n'], primaryCallId: 'a', conferenceName: 'conf-a' });
    });

    it('falls back to the first merged call when the provider named no call', () => {
        const plan = planMerge([held, live], { a: true, b: true });

        expect(conferenceAfterMerge([held, live], plan, {})).toEqual({ callIds: ['a', 'b'], primaryCallId: 'a', conferenceName: '' });
    });
});

describe('buildActiveCallsHtml', () => {
    const strings = {
        mergeSummary: 'Join {0} in one conference.',
        mergeSelectHint: 'Tick two or more calls to merge them.',
        mergeSelectedCalls: 'Merge {0} calls',
        mergeCalls: 'Merge calls',
        addToConference: 'Add to conference',
        addToConferenceSummary: 'Add {0} to the conference.',
        selectAllCalls: 'Select all',
        conferenceParticipants: 'Conference · {0} participants',
        hangupParticipant: 'Hang up {0}',
        selectCall: 'Select {0}',
        cannotMergeBrowserCall: 'A call dialed from this phone cannot be merged.'
    };

    const twoCalls = [
        { callId: 'a', number: '(555) 123-4567', state: 'On hold', selectable: true },
        { callId: 'b', number: '(555) 765-4321', state: 'In call', current: true, selectable: true }
    ];

    it('puts a checkbox beside every call line', () => {
        const html = buildActiveCallsHtml({ calls: twoCalls, merge: { offered: true } }, strings, escapeHtml);

        expect(html).toContain('data-telephony-conference-call="a"');
        expect(html).toContain('data-telephony-conference-call="b"');
        expect(html).toContain('aria-label="Select (555) 765-4321"');
    });

    it('offers Merge disabled until two calls are ticked, and says what to do', () => {
        const html = buildActiveCallsHtml({ calls: twoCalls, merge: { offered: true, canMerge: false, selectedCount: 1, numbers: ['(555) 123-4567'] } }, strings, escapeHtml);

        expect(html).toMatch(/<button[^>]*data-telephony-merge-calls[^>]*disabled/);
        expect(html).toContain('Tick two or more calls to merge them.');
        expect(html).toContain('Merge calls</button>');
    });

    it('names how many calls Merge joins, and which, once they are ticked', () => {
        const html = buildActiveCallsHtml({
            calls: twoCalls.map(call => ({ ...call, selected: true })),
            merge: { offered: true, canMerge: true, selectedCount: 2, allSelected: true, numbers: ['(555) 123-4567', '(555) 765-4321'] }
        }, strings, escapeHtml);

        expect(html).not.toMatch(/<button[^>]*data-telephony-merge-calls[^>]*disabled/);
        expect(html).toContain('Merge 2 calls');
        expect(html).toContain('Join (555) 123-4567 + (555) 765-4321 in one conference.');
        expect(html).toMatch(/data-telephony-conference-call="a"[^>]*checked/);
        expect(html).toMatch(/data-telephony-merge-select-all[^>]*checked/);
    });

    it('offers to add the ticked call to a running conference', () => {
        const html = buildActiveCallsHtml({
            calls: twoCalls,
            merge: { offered: true, canMerge: true, selectedCount: 2, addsToConference: true, numbers: ['(555) 123-4567'] }
        }, strings, escapeHtml);

        expect(html).toContain('Add to conference');
        expect(html).toContain('Add (555) 123-4567 to the conference.');
    });

    it('leaves the Merge action out when there is nothing to merge', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: 'a', number: '1', state: 'In call', selectable: true }],
            merge: { offered: false, numbers: [] }
        }, strings, escapeHtml);

        expect(html).not.toContain('data-telephony-merge-calls');
        expect(html).not.toContain('data-telephony-merge-select-all');
    });

    it('shows a call that cannot be merged with a disabled checkbox that says why', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'x', number: '(702) 499-3350', state: 'In call', selectable: false, unselectableReason: 'browser-call' },
                { callId: 'y', number: '(702) 555-0100', state: 'On hold', selectable: false, unselectableReason: 'browser-call' }
            ],
            merge: { offered: false, blocked: 'browser-call' }
        }, strings, escapeHtml);

        expect(html).toMatch(/data-telephony-conference-call="x"[^>]*disabled/);
        expect(html).toContain('title="A call dialed from this phone cannot be merged."');
        expect(html).toContain('data-telephony-merge-blocked');
        expect(html).not.toContain('data-telephony-merge-calls');
    });

    it('lists a conference under its own heading, with a hang-up for each participant', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: '(555) 123-4567', state: 'In conference', inConference: true, canHangup: true, selectable: true },
                { callId: 'b', number: '(555) 765-4321', state: 'In conference', inConference: true, canHangup: true, selectable: true }
            ],
            merge: { offered: false }
        }, strings, escapeHtml);

        expect(html).toContain('Conference · 2 participants');
        expect(html).toContain('data-telephony-conference-participant="a"');
        expect(html).toContain('data-telephony-participant-hangup="b"');
        expect(html).toContain('aria-label="Hang up (555) 765-4321"');
    });

    // Live: after a merge the conference's calls still carried a checkbox each, so Merge was pressed again on the calls
    // already in it, and the provider refused ("Participant must not join the same conference twice").
    it('draws no checkbox when there is nothing left to merge with', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: 'Jane Doe · ext 2', state: 'In conference', inConference: true, canHangup: true, selectable: true },
                { callId: 'b', number: '(702) 499-3350', state: 'In conference', inConference: true, canHangup: true, selectable: true }
            ],
            merge: { offered: false }
        }, strings, escapeHtml);

        expect(html).not.toContain('data-telephony-conference-call');
        expect(html).toContain('data-telephony-participant-hangup="a"');
        expect(html).toContain('aria-label="Hang up Jane Doe · ext 2"');
        expect(html).toContain('aria-label="Hang up (702) 499-3350"');
    });

    it('offers no participant hang-up when the provider cannot hang up', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: 'a', number: '1', state: 'In conference', inConference: true, canHangup: false }]
        }, strings, escapeHtml);

        expect(html).not.toContain('data-telephony-participant-hangup');
    });

    it('escapes what it shows', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: '"><x', number: '<b>', state: 'In call', selectable: true }],
            merge: { offered: true, numbers: ['<i>'] }
        }, strings, escapeHtml);

        expect(html).not.toContain('<b>');
        expect(html).not.toContain('<i>');
        expect(html).not.toContain('"><x');
    });
});

// Live: a merge's conference lasted on screen until the phone next read its calls. The provider keeps no conference flag
// on a call, so every refresh put the calls back as separate lines -- each with a checkbox and Merge offered again.
describe('the conference memory', () => {
    const merged = { callIds: ['ext-leg', 'cell-leg'], primaryCallId: 'ext-leg', conferenceName: 'ext-ext-leg' };
    const report = callId => ({ callId, state: 'Connected', metadata: {} });

    it('stamps a merged call\'s conference back onto a report that no longer carries it', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);

        const call = applyConferenceMemory(memory, report('cell-leg'));

        expect(call.metadata).toMatchObject({
            isConference: true,
            conferencePrimaryCallId: 'ext-leg',
            conferenceName: 'ext-ext-leg',
            participantCount: 2
        });
    });

    it('leaves a call that was never merged as it is', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);

        expect(applyConferenceMemory(memory, report('other')).metadata).toEqual({});
    });

    it('offers no second merge of the calls it already joined', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);
        const calls = [report('ext-leg'), report('cell-leg')].map(call => applyConferenceMemory(memory, call));

        expect(planMerge(calls, {}).offered).toBe(false);
        expect(isOneConference(memory, ['cell-leg', 'ext-leg'])).toBe(true);
        expect(isOneConference(memory, ['cell-leg', 'third'])).toBe(false);
        expect(isOneConference(memory, ['cell-leg'])).toBe(false);
    });

    it('still offers to add a new call to the conference', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);
        const calls = [report('ext-leg'), report('cell-leg'), report('third')].map(call => applyConferenceMemory(memory, call));

        expect(planMerge(calls, { 'cell-leg': true, third: true })).toMatchObject({
            offered: true,
            canMerge: true,
            addsToConference: true,
            callIds: ['ext-leg', 'third'],
            conferenceName: 'ext-ext-leg'
        });
    });

    it('adds the calls a later merge joins to the same conference', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);
        rememberConference(memory, { callIds: ['ext-leg', 'cell-leg', 'third'], primaryCallId: 'ext-leg', conferenceName: 'ext-ext-leg' });

        expect(conferenceMembers(memory, 'third').sort()).toEqual(['cell-leg', 'ext-leg', 'third']);
        expect(applyConferenceMemory(memory, report('third')).metadata.participantCount).toBe(3);
    });

    // A participant whose party was hung up on its own (the colleague of the extension call the conference was made
    // from) leaves its leg behind as the agent's own way into the conference: it is no longer listed.
    it('hides a participant who left while its leg carries on, and counts only those still there', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);
        markParticipantLeft(memory, 'ext-leg');
        const calls = [report('ext-leg'), report('cell-leg')].map(call => applyConferenceMemory(memory, call));

        expect(visibleConferenceCalls(memory, calls).map(call => call.callId)).toEqual(['cell-leg']);
        expect(calls[1].metadata.participantCount).toBe(1);
        expect(conferenceLegsWithoutParticipants(memory, ['ext-leg', 'cell-leg'])).toEqual([]);
    });

    it('names the legs left behind once nobody is left in the conference', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);
        markParticipantLeft(memory, 'ext-leg');
        forgetConferenceCall(memory, 'cell-leg');

        expect(conferenceLegsWithoutParticipants(memory, ['ext-leg'])).toEqual(['ext-leg']);
        expect(conferenceLegsWithoutParticipants(memory, [])).toEqual([]);
    });

    it('forgets a call that ended, and the conference with its last call', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, merged);
        forgetConferenceCall(memory, 'cell-leg');

        expect(applyConferenceMemory(memory, report('cell-leg')).metadata).toEqual({});
        expect(applyConferenceMemory(memory, report('ext-leg')).metadata.participantCount).toBe(1);

        forgetConferenceCall(memory, 'ext-leg');
        expect(conferenceMembers(memory, 'ext-leg')).toEqual([]);
    });
});

// Live: the agent merged a dialed number and extension 2 into a conference and pressed Hang up; everybody was cut off,
// because the phone hung up every one of the agent's calls in it. A phone system's Hang up leaves a conference and the
// others stay connected; ending it for everyone is its own action.
describe('planConferenceHangup', () => {
    const { planConferenceHangup, planConferenceEnd } = globalThis.CrestAppsSoftPhone;
    const threeWay = { callIds: ['cell-leg', 'ext-leg'], primaryCallId: 'cell-leg', conferenceName: 'conf-cell-leg' };
    const call = (callId, metadata) => ({ callId, state: 'Connected', metadata: metadata || {} });

    it('is nothing to plan for a call in no conference', () => {
        const memory = createConferenceMemory();

        expect(planConferenceHangup(memory, [call('solo')], 'solo')).toMatchObject({ action: 'none' });
    });

    it('leaves a conference two or more others are still in, taking every one of the agent\'s calls out of it', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, threeWay);

        expect(planConferenceHangup(memory, [call('cell-leg'), call('ext-leg')], 'ext-leg')).toEqual({
            action: 'leave',
            leaveCallIds: ['cell-leg', 'ext-leg'],
            keepCallIds: [],
            endCallIds: []
        });
    });

    it('also takes out the agent\'s own way into the conference whose party already left', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, { callIds: ['cell-leg', 'ext-leg', 'third'], primaryCallId: 'cell-leg', conferenceName: 'conf-cell-leg' });
        markParticipantLeft(memory, 'cell-leg');

        expect(planConferenceHangup(memory, [call('cell-leg'), call('ext-leg'), call('third')], 'ext-leg')).toMatchObject({
            action: 'leave',
            leaveCallIds: ['cell-leg', 'ext-leg', 'third']
        });
    });

    it('ends the conference when only one other party is left in it, who would otherwise be alone', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, threeWay);
        markParticipantLeft(memory, 'cell-leg');

        expect(planConferenceHangup(memory, [call('cell-leg'), call('ext-leg')], 'ext-leg')).toEqual({
            action: 'end',
            leaveCallIds: [],
            keepCallIds: [],
            endCallIds: ['cell-leg', 'ext-leg']
        });
    });

    it('never takes a Contact Center caller\'s call down when the agent leaves', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, { callIds: ['cell-leg', 'caller-leg'], primaryCallId: 'cell-leg', conferenceName: 'conf-cell-leg' });
        const calls = [call('cell-leg'), call('caller-leg', { interactionId: 'interaction-1' })];

        expect(planConferenceHangup(memory, calls, 'cell-leg', {
            isContactCenterCall: value => !!(value.metadata && value.metadata.interactionId)
        })).toEqual({
            action: 'leave',
            leaveCallIds: ['cell-leg'],
            keepCallIds: ['caller-leg'],
            endCallIds: []
        });
    });

    it('ignores a remembered call that is no longer up', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, { callIds: ['a', 'b', 'c'], primaryCallId: 'a', conferenceName: 'conf-a' });

        expect(planConferenceHangup(memory, [call('a'), call('b')], 'a')).toMatchObject({ action: 'leave', leaveCallIds: ['a', 'b'] });
    });

    it('ends a conference for everyone from the call it was made from, naming the conference', () => {
        const memory = createConferenceMemory();
        rememberConference(memory, threeWay);

        expect(planConferenceEnd(memory, [call('ext-leg'), call('cell-leg')], 'ext-leg')).toEqual({
            primaryCallId: 'cell-leg',
            conferenceName: 'conf-cell-leg',
            callIds: ['cell-leg', 'ext-leg'],
            partyCount: 2
        });
        expect(planConferenceEnd(memory, [call('solo')], 'solo')).toBeNull();
    });
});

// Live: Merge was pressed while extension 2 was still ringing, and Telnyx refused it. A line that is still connecting or
// ringing cannot be merged, and its checkbox says why.
describe('buildActiveCallsHtml unanswered lines', () => {
    it('draws a disabled checkbox that says the line is waiting to be answered', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: '(555) 123-4567', state: 'On hold', selectable: true },
                { callId: 'b', number: 'Jane Doe · ext 2', state: 'Connecting...', selectable: false, unselectableReason: 'not-answered' }
            ],
            merge: { offered: false }
        }, { cannotMergeUnanswered: 'Waiting for them to answer before this call can be merged.' }, escapeHtml);

        expect(html).toMatch(/data-telephony-conference-call="b"[^>]*disabled/);
        expect(html).toContain('title="Waiting for them to answer before this call can be merged."');
        expect(html).toContain('data-telephony-merge-waiting');
    });
});

describe('buildActiveCallsHtml line details', () => {
    const strings = { hangupParticipant: 'Hang up {0}', conferenceParticipants: 'Conference · {0} participants' };

    it('shows each line\'s state as a chip and its running time', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: '(555) 123-4567', state: 'On hold', stateKind: 'held', elapsed: '1:05' },
                { callId: 'b', number: '(555) 765-4321', state: 'Active', stateKind: 'active', elapsed: '0:12', current: true }
            ]
        }, strings, escapeHtml);

        expect(html).toMatch(/telephony-soft-phone__line-state--held[^>]*>On hold</);
        expect(html).toMatch(/telephony-soft-phone__line-state--active[^>]*>Active</);
        expect(html).toContain('data-telephony-line-timer="a">1:05</span>');
        expect(html).toContain('aria-current="true"');
    });

    it('draws the participant hang-up as a quiet control rather than a second red button', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: 'a', number: '1', state: 'In conference', inConference: true, canHangup: true }]
        }, strings, escapeHtml);

        expect(html).toMatch(/<button[^>]*class="telephony-soft-phone__participant-hangup"[^>]*data-telephony-participant-hangup="a"/);
        expect(html).toContain('fa-user-minus');
    });
});
