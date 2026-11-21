import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Runtime/scorm12-api.js';
import '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Runtime/scorm2004-api.js';

const PLAYER = 'https://learn.example.com';
const CONTENT = 'https://packages.example.com';

// A window of the frame page: the player as its parent, and the listeners it adds.
function page(origin) {
    const sent = [];
    const listeners = [];
    const root = {
        name: 'crestapps-package-frame',
        location: { origin },
        parent: { postMessage: (message, targetOrigin) => sent.push({ message, targetOrigin }) },
        addEventListener: (type, listener) => listeners.push(listener),
        postMessage: () => {},
    };

    root.dispatch = event => listeners.forEach(listener => listener(event));

    return { root, sent };
}

function cmi12() {
    return {
        StudentId: 'u1', StudentName: 'Ana', LessonLocation: '', Credit: 'credit', LessonStatus: 'not attempted', Entry: 'ab-initio',
        ScoreRaw: '', ScoreMin: '', ScoreMax: '', TotalTime: '0000:00:00.00', LessonMode: 'normal', Exit: '', SessionTime: '',
        SuspendData: '', LaunchData: '', Comments: '', CommentsFromLms: '', MasteryScore: '', MaxTimeAllowed: '', TimeLimitAction: '',
        Audio: '0', Language: '', Speed: '0', Text: '0', Objectives: [], Interactions: [],
    };
}

function cmi2004() {
    return {
        Edition: 4, LearnerId: 'u1', LearnerName: 'Ana', Location: null, SuspendData: null, LaunchData: null,
        CompletionStatus: 'unknown', SuccessStatus: 'unknown', CompletionThreshold: null, ScaledPassingScore: null,
        ProgressMeasure: null, ScoreScaled: null, ScoreRaw: null, ScoreMin: null, ScoreMax: null, Credit: 'credit',
        Mode: 'normal', Entry: 'ab-initio', Exit: '', SessionTime: null, TotalTime: 'PT0H0M0S', MaxTimeAllowed: null,
        TimeLimitAction: 'continue,no message', AudioLevel: '1', Language: '', DeliverySpeed: '1', AudioCaptioning: '0',
        NavRequest: '_none_', Objectives: [], Interactions: [], CommentsFromLearner: [], CommentsFromLms: [],
    };
}

// The frame page on the content host: the API forwards to the player's origin only, and passes up only what its own
// same-origin children send.
describe('SCORM bridge on the package content host', () => {
    it('defines window.API and posts every call to the player origin only', () => {
        const { root, sent } = page(CONTENT);
        const api = globalThis.CrestAppsScorm12.connect(root, { launchId: 'l1', playerOrigin: PLAYER, cmi: cmi12(), bridge: true });

        expect(root.API).toBe(api);
        expect(api.LMSInitialize('')).toBe('true');
        expect(api.LMSSetValue('cmi.core.lesson_status', 'completed')).toBe('true');
        expect(api.LMSCommit('')).toBe('true');

        expect(sent.map(item => item.targetOrigin)).toEqual([PLAYER, PLAYER, PLAYER]);
        expect(sent.map(item => item.message.type)).toEqual(['initialize', 'set', 'commit']);
        expect(sent[1].message).toMatchObject({ protocol: 'crestapps-scorm12', launchId: 'l1', element: 'cmi.core.lesson_status', value: 'completed' });
    });

    it('passes up the messages of its own children from its own origin only', () => {
        const { root, sent } = page(CONTENT);

        globalThis.CrestAppsScorm12.connect(root, { launchId: 'l1', playerOrigin: PLAYER, cmi: cmi12(), bridge: true });

        const child = { parent: root };
        const data = { protocol: 'crestapps-scorm12', launchId: 'l1', type: 'commit' };

        root.dispatch({ origin: 'null', data, source: child });
        root.dispatch({ origin: 'https://evil.example.com', data, source: child });
        root.dispatch({ origin: CONTENT, data: { ...data, launchId: 'l0' }, source: child });
        root.dispatch({ origin: CONTENT, data, source: { parent: {} } });
        expect(sent).toEqual([]);

        root.dispatch({ origin: CONTENT, data, source: child });
        expect(sent).toHaveLength(1);
        expect(sent[0]).toMatchObject({ targetOrigin: PLAYER, message: { type: 'commit', launchId: 'l1' } });
    });

    it('in strict mode passes up messages of opaque children only', () => {
        const { root, sent } = page('null');

        globalThis.CrestAppsScorm12.connect(root, { launchId: 'l1', playerOrigin: PLAYER, cmi: cmi12() });

        const child = { parent: root };
        const data = { protocol: 'crestapps-scorm12', launchId: 'l1', type: 'commit' };

        root.dispatch({ origin: CONTENT, data, source: child });
        expect(sent).toEqual([]);

        root.dispatch({ origin: 'null', data, source: child });
        expect(sent).toHaveLength(1);
    });

    it('defines API_1484_11 for SCORM 2004 and posts to the player origin only', () => {
        const { root, sent } = page(CONTENT);
        const api = globalThis.CrestAppsScorm2004.connect(root, { launchId: 'l2', playerOrigin: PLAYER, cmi: cmi2004(), bridge: true });

        expect(root.API_1484_11).toBe(api);
        expect(api.Initialize('')).toBe('true');
        expect(sent).toHaveLength(1);
        expect(sent[0]).toMatchObject({ targetOrigin: PLAYER, message: { protocol: 'crestapps-scorm2004', launchId: 'l2' } });
    });
});
