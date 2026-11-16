import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Runtime/scorm12-api.js';

const scorm = globalThis.CrestAppsScorm12;

function cmi(values = {}) {
    return {
        StudentId: 'u1', StudentName: 'Ana', LessonLocation: '', Credit: 'credit', LessonStatus: 'not attempted', Entry: 'ab-initio',
        ScoreRaw: '', ScoreMin: '', ScoreMax: '', TotalTime: '0000:00:00.00', LessonMode: 'normal', Exit: '', SessionTime: '',
        SuspendData: '', LaunchData: '', Comments: '', CommentsFromLms: '', MasteryScore: '80', MaxTimeAllowed: '', TimeLimitAction: '',
        Audio: '0', Language: '', Speed: '0', Text: '0', Objectives: [], Interactions: [],
        ...values,
    };
}

function started(values, sent = []) {
    const api = scorm.createApi(cmi(values), message => sent.push(message));

    api.LMSInitialize('');

    return api;
}

// The same rules as the server's Scorm12DataModel and Scorm12Api, so the content gets the error codes the server gives.
describe('SCORM 1.2 API of the package frame', () => {
    it('refuses every call before LMSInitialize with 301', () => {
        const api = scorm.createApi(cmi());

        expect(api.LMSGetValue('cmi.core.lesson_status')).toBe('');
        expect(api.LMSGetLastError()).toBe('301');
        expect(api.LMSSetValue('cmi.core.lesson_status', 'completed')).toBe('false');
        expect(api.LMSCommit('')).toBe('false');
        expect(api.LMSFinish('')).toBe('false');
        expect(api.LMSGetLastError()).toBe('301');
    });

    it('starts once, takes no argument and refuses a second start with 101', () => {
        const api = scorm.createApi(cmi());

        expect(api.LMSInitialize('x')).toBe('false');
        expect(api.LMSGetLastError()).toBe('201');
        expect(api.LMSInitialize('')).toBe('true');
        expect(api.LMSInitialize('')).toBe('false');
        expect(api.LMSGetLastError()).toBe('101');
    });

    it('answers the error codes of the data model', () => {
        const api = started();
        const cases = [
            ['cmi.core.student_id', 'x', '403'],
            ['cmi.core._children', 'x', '402'],
            ['cmi.objectives._count', '1', '402'],
            ['cmi.core.lesson_status', 'not attempted', '405'],
            ['cmi.core.score.raw', '101', '405'],
            ['cmi.core.session_time', '1:00:00', '405'],
            ['cmi.suspend_data', 's'.repeat(4097), '405'],
            ['cmi.objectives.1.id', 'o', '201'],
            ['cmi.unknown', 'x', '201'],
        ];

        for (const [element, value, code] of cases) {
            expect(api.LMSSetValue(element, value), element).toBe('false');
            expect(api.LMSGetLastError(), element).toBe(code);
        }

        expect(api.LMSGetValue('cmi.core.exit')).toBe('');
        expect(api.LMSGetLastError()).toBe('404');
        expect(api.LMSGetValue('cmi.core.student_id._children')).toBe('');
        expect(api.LMSGetLastError()).toBe('202');
        expect(api.LMSGetValue('cmi.core.score._count')).toBe('');
        expect(api.LMSGetLastError()).toBe('203');
        expect(api.LMSGetErrorString('405')).toBe('Incorrect Data Type');
        expect(api.LMSGetDiagnostic('')).toBe('Element not an array - cannot have count');
    });

    it('reads the learner values and its own writes, and tells the player what it set', () => {
        const sent = [];
        const api = started({ Entry: 'resume', SuspendData: 'page=2' }, sent);

        expect(api.LMSGetValue('cmi.core.student_name')).toBe('Ana');
        expect(api.LMSGetValue('cmi.core.entry')).toBe('resume');
        expect(api.LMSGetValue('cmi.suspend_data')).toBe('page=2');
        expect(api.LMSGetValue('cmi.student_data.mastery_score')).toBe('80');
        expect(api.LMSSetValue('cmi.core.lesson_status', 'passed')).toBe('true');
        expect(api.LMSSetValue('cmi.objectives.0.id', 'obj-1')).toBe('true');
        expect(api.LMSSetValue('cmi.interactions.0.id', 'q1')).toBe('true');
        expect(api.LMSGetValue('cmi.objectives._count')).toBe('1');
        expect(api.LMSGetValue('cmi.interactions._count')).toBe('1');
        expect(api.LMSGetValue('cmi.core.lesson_status')).toBe('passed');
        expect(api.LMSCommit('')).toBe('true');
        expect(api.LMSFinish('')).toBe('true');
        expect(api.LMSGetValue('cmi.core.lesson_status')).toBe('');
        expect(api.LMSGetLastError()).toBe('301');

        expect(sent).toEqual([
            { type: 'initialize' },
            { type: 'set', element: 'cmi.core.lesson_status', value: 'passed' },
            { type: 'set', element: 'cmi.objectives.0.id', value: 'obj-1' },
            { type: 'set', element: 'cmi.interactions.0.id', value: 'q1' },
            { type: 'commit' },
            { type: 'finish' },
        ]);
    });

    it('does not tell the player about a refused value', () => {
        const sent = [];
        const api = started({}, sent);

        api.LMSSetValue('cmi.core.lesson_status', 'done');

        expect(sent).toEqual([{ type: 'initialize' }]);
    });
});
