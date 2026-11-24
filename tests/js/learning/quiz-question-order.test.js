import { describe, expect, it } from 'vitest';

import { mergeOrder, move } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Assessments/Assets/js/quiz-question-order-helpers.js';

// The quiz editor lists the fixed questions in the order every attempt asks them. The author moves a question up or
// down; a question picked later goes last, and one taken out of the picker leaves the list.
describe('quiz question order', () => {
    it('keeps the order set for the questions still chosen and adds new ones last', () => {
        expect(mergeOrder(['c', 'a', 'b'], ['a', 'b', 'c', 'd'])).toEqual(['c', 'a', 'b', 'd']);
        expect(mergeOrder(['c', 'a', 'b'], ['b', 'c'])).toEqual(['c', 'b']);
        expect(mergeOrder([], ['z', 'y'])).toEqual(['z', 'y']);
        expect(mergeOrder(null, null)).toEqual([]);
    });

    it('moves a question up or down, and never past either end', () => {
        expect(move(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
        expect(move(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
        expect(move(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
        expect(move(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'b', 'c']);
    });
});
