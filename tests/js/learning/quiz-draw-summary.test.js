import { describe, expect, it } from 'vitest';

import { availableCount, bankTags, renumber } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Assessments/Assets/js/quiz-draw-summary.js';

const questions = [
    { tags: ['Ladders', 'heights'], difficulty: 2 },
    { tags: ['ladders'], difficulty: 3 },
    { tags: ['Fire'], difficulty: 2 },
    { tags: [], difficulty: 5 },
];

// The quiz editor's "Draw at random" rules show the bank's tags and, beside the count, how many questions the bank
// can supply to the rule, counted as an attempt draws: a tag whatever its case, and a difficulty, each when set.
describe('quiz draw summary', () => {
    it('lists the bank tags once each, whatever their case, in alphabetical order', () => {
        expect(bankTags(questions)).toEqual(['Fire', 'heights', 'Ladders']);
        expect(bankTags([])).toEqual([]);
    });

    it('counts the questions a rule can draw', () => {
        expect(availableCount(questions, '', '')).toBe(4);
        expect(availableCount(questions, 'LADDERS', '')).toBe(2);
        expect(availableCount(questions, 'ladders', '2')).toBe(1);
        expect(availableCount(questions, null, 2)).toBe(2);
        expect(availableCount(questions, 'nothing', null)).toBe(0);
    });

    it('renumbers the rule rows so their indexes run from zero', () => {
        const field = name => {
            const attributes = { name };

            return { getAttribute: key => attributes[key], setAttribute: (key, value) => { attributes[key] = value; } };
        };
        const row = fields => ({ querySelectorAll: () => fields });
        const first = [field('QuizPart.DrawRules[3].BankId'), field('other')];
        const second = [field('QuizPart.DrawRules[__index__].Tag')];

        renumber([row(first), row(second)], 'QuizPart.DrawRules');

        expect(first[0].getAttribute('name')).toBe('QuizPart.DrawRules[0].BankId');
        expect(first[1].getAttribute('name')).toBe('other');
        expect(second[0].getAttribute('name')).toBe('QuizPart.DrawRules[1].Tag');
    });
});
