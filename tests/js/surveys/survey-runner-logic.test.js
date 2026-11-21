import { describe, expect, it } from 'vitest';
import {
    evaluateRules,
    formatTimeLeft,
    readAnswers,
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

describe('formatTimeLeft', () => {
    it('formats minutes and hours', () => {
        expect(formatTimeLeft(65_000)).toBe('1:05');
        expect(formatTimeLeft(3_725_000)).toBe('1:02:05');
        expect(formatTimeLeft(-5)).toBe('0:00');
    });
});
