import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Runtime/scorm2004-api.js';

const scorm = globalThis.CrestAppsScorm2004;

function cmi(values = {}) {
    return {
        Edition: 4, LearnerId: 'u1', LearnerName: 'Ana', Location: null, SuspendData: null, LaunchData: null,
        CompletionStatus: 'unknown', SuccessStatus: 'unknown', CompletionThreshold: null, ScaledPassingScore: null,
        ProgressMeasure: null, ScoreScaled: null, ScoreRaw: null, ScoreMin: null, ScoreMax: null, Credit: 'credit',
        Mode: 'normal', Entry: 'ab-initio', Exit: '', SessionTime: null, TotalTime: 'PT0H0M0S', MaxTimeAllowed: null,
        TimeLimitAction: 'continue,no message', AudioLevel: '1', Language: '', DeliverySpeed: '1', AudioCaptioning: '0',
        NavRequest: '_none_', Objectives: [], Interactions: [], CommentsFromLearner: [], CommentsFromLms: [],
        ...values,
    };
}

function started(values, sent = []) {
    const api = scorm.createApi(cmi(values), message => sent.push(message));

    expect(api.Initialize('')).toBe('true');

    return api;
}

function expectError(api, call, code) {
    call();
    expect(api.GetLastError()).toBe(code);
}

// The same rules as the server's Scorm2004DataModel and Scorm2004Api, so the content gets the error codes the server gives.
describe('SCORM 2004 API of the package frame', () => {
    it('keeps the state rules of the API', () => {
        const api = scorm.createApi(cmi());

        expectError(api, () => expect(api.GetValue('cmi.location')).toBe(''), '122');
        expectError(api, () => expect(api.SetValue('cmi.location', 'a')).toBe('false'), '132');
        expectError(api, () => expect(api.Commit('')).toBe('false'), '142');
        expectError(api, () => expect(api.Terminate('')).toBe('false'), '112');
        expectError(api, () => expect(api.Initialize('x')).toBe('false'), '201');
        expect(api.Initialize('')).toBe('true');
        expectError(api, () => expect(api.Initialize('')).toBe('false'), '103');
        expectError(api, () => expect(api.Commit('x')).toBe('false'), '201');
        expect(api.Terminate('')).toBe('true');
        expectError(api, () => expect(api.GetValue('cmi.location')).toBe(''), '123');
        expectError(api, () => expect(api.SetValue('cmi.location', 'a')).toBe('false'), '133');
        expectError(api, () => expect(api.Commit('')).toBe('false'), '143');
        expectError(api, () => expect(api.Terminate('')).toBe('false'), '113');
        expectError(api, () => expect(api.Initialize('')).toBe('false'), '104');
    });

    it('answers the error codes of the data model', () => {
        const api = started();
        const sets = [
            ['', 'x', '351'],
            ['cmi.learner_id', 'x', '404'],
            ['cmi._version', '2.0', '404'],
            ['cmi.objectives._count', '1', '404'],
            ['cmi.unknown', 'x', '401'],
            ['cmi.completion_status', 'passed', '406'],
            ['cmi.score.scaled', '1.5', '407'],
            ['cmi.score.raw', 'ten', '406'],
            ['cmi.progress_measure', '-0.1', '407'],
            ['cmi.session_time', '00:10:00', '406'],
            ['cmi.location', 'l'.repeat(1001), '406'],
            ['cmi.suspend_data', 's'.repeat(64001), '406'],
            ['cmi.objectives.0.success_status', 'passed', '408'],
            ['cmi.objectives.1.id', 'o', '351'],
            ['cmi.interactions.0.type', 'choice', '408'],
            ['adl.nav.request', 'jumpto', '406'],
            ['adl.nav.request_valid.continue', 'true', '404'],
        ];

        for (const [element, value, code] of sets) {
            expect(api.SetValue(element, value), element).toBe('false');
            expect(api.GetLastError(), element).toBe(code);
        }

        const gets = [
            ['', '301'],
            ['cmi.exit', '405'],
            ['cmi.session_time', '405'],
            ['cmi.location', '403'],
            ['cmi.score.scaled', '403'],
            ['cmi.location._children', '301'],
            ['cmi.score._count', '301'],
            ['cmi.unknown', '401'],
            ['cmi.objectives.0.id', '301'],
        ];

        for (const [element, code] of gets) {
            expect(api.GetValue(element), element).toBe('');
            expect(api.GetLastError(), element).toBe(code);
        }

        expect(api.GetErrorString('406')).toBe('Data Model Element Type Mismatch');
        expect(api.GetDiagnostic('')).toBe('General Get Failure');
        expect(api.GetErrorString('999')).toBe('');
    });

    it('reads the keywords and the defaults', () => {
        const api = started({ CommentsFromLms: [{ Comment: 'Welcome', Location: 'intro', Timestamp: '2026-11-16T10:00:00Z' }] });

        expect(api.GetValue('cmi._version')).toBe('1.0');
        expect(api.GetValue('cmi.score._children')).toBe('scaled,raw,min,max');
        expect(api.GetValue('cmi.objectives._children')).toBe('id,score,success_status,completion_status,progress_measure,description');
        expect(api.GetValue('cmi.interactions._children')).toBe('id,type,objectives,timestamp,correct_responses,weighting,learner_response,result,latency,description');
        expect(api.GetValue('cmi.learner_preference._children')).toBe('audio_level,language,delivery_speed,audio_captioning');
        expect(api.GetValue('cmi.comments_from_lms._count')).toBe('1');
        expect(api.GetValue('cmi.comments_from_lms.0.comment')).toBe('Welcome');
        expect(api.GetValue('cmi.completion_status')).toBe('unknown');
        expect(api.GetValue('cmi.success_status')).toBe('unknown');
        expect(api.GetValue('cmi.total_time')).toBe('PT0H0M0S');
        expect(api.GetValue('cmi.entry')).toBe('ab-initio');
        expect(api.GetValue('cmi.learner_name')).toBe('Ana');
        expect(api.GetValue('adl.nav.request')).toBe('_none_');
        expect(api.GetValue('adl.nav.request_valid.continue')).toBe('unknown');
        expect(api.GetValue('adl.nav.request_valid.choice.{target=intro}')).toBe('unknown');
        expect(api.GetValue('adl.data._count')).toBe('0');
    });

    it('evaluates the completion and success statuses on a read', () => {
        const api = started({ CompletionThreshold: '0.8', ScaledPassingScore: '0.6' });

        expect(api.SetValue('cmi.completion_status', 'completed')).toBe('true');
        expect(api.SetValue('cmi.success_status', 'passed')).toBe('true');
        expect(api.GetValue('cmi.completion_status')).toBe('unknown');
        expect(api.GetValue('cmi.success_status')).toBe('unknown');
        expect(api.SetValue('cmi.progress_measure', '0.79')).toBe('true');
        expect(api.SetValue('cmi.score.scaled', '0.59')).toBe('true');
        expect(api.GetValue('cmi.completion_status')).toBe('incomplete');
        expect(api.GetValue('cmi.success_status')).toBe('failed');
        expect(api.SetValue('cmi.progress_measure', '0.8')).toBe('true');
        expect(api.SetValue('cmi.score.scaled', '0.6')).toBe('true');
        expect(api.GetValue('cmi.completion_status')).toBe('completed');
        expect(api.GetValue('cmi.success_status')).toBe('passed');
    });

    it('keeps objective ids unique and adds entries by id', () => {
        const api = started();

        expect(api.SetValue('cmi.objectives.0.id', 'obj-1')).toBe('true');
        expect(api.SetValue('cmi.objectives.1.id', 'obj-1')).toBe('false');
        expect(api.GetLastError()).toBe('351');
        expect(api.SetValue('cmi.objectives.0.id', 'renamed')).toBe('false');
        expect(api.GetLastError()).toBe('351');
        expect(api.SetValue('cmi.objectives.0.score.scaled', '0.5')).toBe('true');
        expect(api.GetValue('cmi.objectives.0.score.scaled')).toBe('0.5');
        expect(api.GetValue('cmi.objectives.0.success_status')).toBe('unknown');
        expect(api.GetValue('cmi.objectives._count')).toBe('1');
    });

    it('checks interactions in order and by type', () => {
        const api = started();

        expect(api.SetValue('cmi.interactions.0.id', 'q1')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.learner_response', 'true')).toBe('false');
        expect(api.GetLastError()).toBe('408');
        expect(api.SetValue('cmi.interactions.0.type', 'true-false')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.learner_response', 'yes')).toBe('false');
        expect(api.GetLastError()).toBe('406');
        expect(api.SetValue('cmi.interactions.0.learner_response', 'true')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.correct_responses.0.pattern', 'false')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.correct_responses.1.pattern', 'true')).toBe('false');
        expect(api.GetLastError()).toBe('351');
        expect(api.SetValue('cmi.interactions.0.objectives.0.id', 'obj-1')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.objectives.1.id', 'obj-1')).toBe('false');
        expect(api.GetLastError()).toBe('351');
        expect(api.SetValue('cmi.interactions.0.timestamp', '2026-11-16T10:30:00.5+02:00')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.timestamp', '2039-01-01')).toBe('false');
        expect(api.GetLastError()).toBe('406');
        expect(api.SetValue('cmi.interactions.0.latency', 'PT12.5S')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.result', 'incorrect')).toBe('true');
        expect(api.SetValue('cmi.interactions.0.description', '{lang=xyz9}bad')).toBe('false');
        expect(api.GetLastError()).toBe('406');
        expect(api.GetValue('cmi.interactions.0.correct_responses._count')).toBe('1');

        expect(api.SetValue('cmi.interactions.1.id', 'q2')).toBe('true');
        expect(api.SetValue('cmi.interactions.1.type', 'choice')).toBe('true');
        expect(api.SetValue('cmi.interactions.1.correct_responses.0.pattern', 'a[,]a')).toBe('false');
        expect(api.GetLastError()).toBe('406');
        expect(api.SetValue('cmi.interactions.1.correct_responses.0.pattern', 'a[,]b')).toBe('true');
        expect(api.SetValue('cmi.interactions.1.correct_responses.1.pattern', 'c')).toBe('true');
    });

    it('takes 4,000 characters of suspend data in the 3rd Edition and no jump request', () => {
        const api = started({ Edition: 3 });

        expect(api.SetValue('cmi.suspend_data', 's'.repeat(4000))).toBe('true');
        expect(api.SetValue('cmi.suspend_data', 's'.repeat(4001))).toBe('false');
        expect(api.GetLastError()).toBe('406');
        expect(api.SetValue('adl.nav.request', '{target=intro}jump')).toBe('false');
        expect(api.SetValue('adl.nav.request', '{target=intro}choice')).toBe('true');
    });

    it('checks ISO 8601 durations and timestamps as the server does', () => {
        expect(scorm.isDuration('PT1H30M')).toBe(true);
        expect(scorm.isDuration('P1Y2M3DT4H5M6.78S')).toBe(true);
        expect(scorm.isDuration('P')).toBe(false);
        expect(scorm.isDuration('PT')).toBe(false);
        expect(scorm.isDuration('PT1.5M')).toBe(false);
        expect(scorm.isTimestamp('2026-02-30')).toBe(false);
        expect(scorm.isTimestamp('2026-11-16T10:30')).toBe(true);
    });

    it('tells the player what it set, commits and terminates, and not what it refused', () => {
        const sent = [];
        const api = started({}, sent);

        expect(api.SetValue('cmi.location', 'slide-2')).toBe('true');
        expect(api.SetValue('cmi.completion_status', 'done')).toBe('false');
        expect(api.SetValue('cmi.exit', 'suspend')).toBe('true');
        expect(api.SetValue('cmi.session_time', 'PT2M30S')).toBe('true');
        expect(api.Commit('')).toBe('true');
        expect(api.Terminate('')).toBe('true');

        expect(sent).toEqual([
            { type: 'initialize' },
            { type: 'set', element: 'cmi.location', value: 'slide-2' },
            { type: 'set', element: 'cmi.exit', value: 'suspend' },
            { type: 'set', element: 'cmi.session_time', value: 'PT2M30S' },
            { type: 'commit' },
            { type: 'finish' },
        ]);
    });
});
