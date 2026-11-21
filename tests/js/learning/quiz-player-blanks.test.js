import { describe, expect, it } from 'vitest';

import { choiceLabel, dropTarget } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Assessments/Assets/js/quiz-player-blanks.js';

// A drag into text question: the learner drags a word onto a blank, or picks it in the blank's list. Each blank is a
// <select> that posts the chosen word's id, so the drag only sets the list's value.
describe('quiz player drag into text', () => {
    const select = (name, options) => ({
        tagName: 'SELECT',
        name,
        options: options.map(value => ({ value, textContent: 'Word ' + value })),
        closest: () => null,
    });

    it('drops a word only on a blank that offers it', () => {
        const blank = select('a1_b0', ['', 'c0', 'c1']);

        expect(dropTarget(blank, 'c1')).toBe(blank);
        expect(dropTarget(blank, 'forged')).toBeNull();
        expect(dropTarget({ tagName: 'DIV', closest: () => null }, 'c1')).toBeNull();
        expect(dropTarget(null, 'c1')).toBeNull();
    });

    it('finds the blank a drop lands inside', () => {
        const blank = select('a1_b0', ['', 'c0']);
        const inner = { tagName: 'SPAN', closest: () => blank };

        expect(dropTarget(inner, 'c0')).toBe(blank);
    });

    it('names the word a blank holds, for screen readers', () => {
        const blank = select('a1_b0', ['', 'c0']);

        expect(choiceLabel(blank, 'c0')).toBe('Word c0');
        expect(choiceLabel(blank, 'nothing')).toBe('');
    });
});
