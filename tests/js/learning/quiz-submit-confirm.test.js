import { describe, expect, it } from 'vitest';

import { isAnswered, unansweredText } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Assessments/Assets/js/quiz-submit-confirm.js';

const texts = {
    one: '1 question is not answered.',
    many: '{0} questions are not answered.',
    allAnswered: 'You answered every question.',
};

// Submitting a quiz asks in the site's modal and says how many questions are still unanswered.
describe('quiz submit confirmation', () => {
    it('says how many questions are unanswered', () => {
        expect(unansweredText(0, texts)).toBe('You answered every question.');
        expect(unansweredText(1, texts)).toBe('1 question is not answered.');
        expect(unansweredText(3, texts)).toBe('3 questions are not answered.');
    });

    it('counts a ticked choice, a typed text or a chosen option as an answer', () => {
        expect(isAnswered([{ type: 'radio', checked: false }, { type: 'radio', checked: true }])).toBe(true);
        expect(isAnswered([{ type: 'radio', checked: false }, { type: 'radio', checked: false }])).toBe(false);
        expect(isAnswered([{ type: 'text', value: '  ' }])).toBe(false);
        expect(isAnswered([{ type: 'textarea', value: 'An answer' }])).toBe(true);
        expect(isAnswered([{ type: 'select-one', value: 'b' }])).toBe(true);
        expect(isAnswered([{ type: 'hidden', value: 'order' }])).toBe(false);
    });
});
