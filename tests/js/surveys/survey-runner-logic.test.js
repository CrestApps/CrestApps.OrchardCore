import { describe, expect, it } from 'vitest';
import {
    evaluateRules,
    formatTimeLeft,
    isQuestionHidden,
    isRetryable,
    lockAttribute,
    readAnswers,
    retryDelay,
} from '../../../src/Modules/CrestApps.OrchardCore.Surveys/Assets/js/survey-runner-logic.js';

const showWhen = (source, value) => ({ Match: 'All', Conditions: [{ Source: source, Operator: 'Is', Values: [value] }] });

describe('readAnswers', () => {
    it('groups values by question and row and ignores other fields', () => {
        const answers = readAnswers([
            ['q.a', 'yes'],
            ['q.b', 'x'],
            ['q.b', 'y'],
            ['q.m.row1', '3'],
            ['q.a.other', 'free text'],
            ['pageId', 'p1'],
            ['q.c', '  '],
        ]);

        expect(answers.get('a')).toEqual(['yes']);
        expect(answers.get('b')).toEqual(['x', 'y']);
        expect(answers.get('m.row1')).toEqual(['3']);
        expect(answers.get('c')).toEqual([]);
        expect(answers.has('pageId')).toBe(false);
        expect(answers.has('a.other')).toBe(false);
    });
});

describe('evaluateRules', () => {
    it('hides a question whose rule is not met, and closes a chain', () => {
        const rules = {
            'q:b': showWhen('a', 'yes'),
            'q:c': showWhen('b', 'yes'),
        };

        const hidden = evaluateRules(rules, new Map([['a', ['no']], ['b', ['yes']]]), new Set(['a', 'b', 'c']));

        expect([...hidden.questions].sort()).toEqual(['b', 'c']);
    });

    it('shows a question whose rule is met', () => {
        const hidden = evaluateRules({ 'q:b': showWhen('a', 'yes') }, new Map([['a', ['yes']]]), new Set(['a', 'b']));

        expect(hidden.questions.size).toBe(0);
    });

    it('hides choices by question', () => {
        const hidden = evaluateRules({ 'c:b:x': showWhen('a', 'yes') }, new Map([['a', ['no']]]), new Set(['a', 'b']));

        expect(hidden.choices.get('b')).toEqual(new Set(['x']));
    });

    it('ignores sources that are not on the page', () => {
        const hidden = evaluateRules({ 'q:b': showWhen('earlier', 'yes') }, new Map(), new Set(['b']));

        expect(hidden.questions.size).toBe(0);
    });
});

describe('isQuestionHidden', () => {
    it('follows the rules for an answer that is not locked', () => {
        expect(isQuestionHidden('q2', false, false, new Set(['q2']))).toBe(true);
        expect(isQuestionHidden('q2', false, true, new Set())).toBe(false);
    });

    it('keeps a locked answer as the server showed it, whatever the rules say', () => {
        // The server refuses a change that would hide a checked answer, so the page must not hide it either.
        expect(isQuestionHidden('q2', true, false, new Set(['q2']))).toBe(false);
        expect(isQuestionHidden('q2', true, true, new Set())).toBe(true);
    });
});

describe('lockAttribute', () => {
    it('disables choice controls, where readonly does nothing', () => {
        expect(lockAttribute('INPUT', 'radio')).toBe('disabled');
        expect(lockAttribute('INPUT', 'checkbox')).toBe('disabled');
        expect(lockAttribute('SELECT', '')).toBe('disabled');
    });

    it('makes text-like controls read-only and leaves hidden inputs alone', () => {
        expect(lockAttribute('INPUT', 'text')).toBe('readonly');
        expect(lockAttribute('TEXTAREA', '')).toBe('readonly');
        expect(lockAttribute('INPUT', 'hidden')).toBeNull();
    });
});

describe('formatTimeLeft', () => {
    it('formats minutes and hours', () => {
        expect(formatTimeLeft(65_000)).toBe('1:05');
        expect(formatTimeLeft(3_725_000)).toBe('1:02:05');
        expect(formatTimeLeft(-5)).toBe('0:00');
    });
});

describe('retryDelay', () => {
    it('waits the Retry-After seconds of the server, within one and thirty seconds', () => {
        expect(retryDelay('4', 1)).toBe(4000);
        expect(retryDelay('0', 1)).toBe(1000);
        expect(retryDelay('120', 1)).toBe(30000);
    });

    it('doubles the wait with each attempt when the server sends no Retry-After', () => {
        expect(retryDelay(null, 1)).toBe(2000);
        expect(retryDelay(null, 2)).toBe(4000);
        expect(retryDelay('soon', 3)).toBe(8000);
        expect(retryDelay(null, 10)).toBe(30000);
    });
});

describe('isRetryable', () => {
    it('retries only a request the server did not handle', () => {
        expect(isRetryable(429)).toBe(true);
        expect(isRetryable(503)).toBe(true);
        expect(isRetryable(200)).toBe(false);
        expect(isRetryable(400)).toBe(false);
        expect(isRetryable(500)).toBe(false);
    });
});
