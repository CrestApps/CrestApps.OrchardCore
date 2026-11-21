import { describe, expect, it } from 'vitest';

import { createActiveTimer, progressText } from '../../../src/Modules/CrestApps.OrchardCore.Learning/Assets/js/learning-player-helpers.js';

// The player's progress bar never stands alone: beside it, "3 of 7 complete · 43%", from a localized template.
describe('learning player progress text', () => {
    it('fills the localized template with the counts and the percent', () => {
        expect(progressText('{0} of {1} complete · {2}%', 3, 7, 43)).toBe('3 of 7 complete · 43%');
        expect(progressText('{2} % — {0}/{1}', 0, 5, 0)).toBe('0 % — 0/5');
    });
});

// The transcript showed "Time spent 0:00": an item left before the half-minute call reported nothing. The player now
// sends the visible seconds when the learner leaves the item, and never counts the time the page is hidden.
describe('learning player active timer', () => {
    it('reports the seconds of a short visit when the learner leaves', () => {
        const timer = createActiveTimer(0);

        expect(timer.hide(12_000)).toBe(12);
    });

    it('splits the time between the half-minute calls and the leave, without counting twice', () => {
        const timer = createActiveTimer(0);

        expect(timer.take(30_000)).toBe(30);
        expect(timer.hide(41_000)).toBe(11);
    });

    it('does not count the time the page is hidden', () => {
        const timer = createActiveTimer(0);

        timer.hide(5_000);
        expect(timer.take(65_000)).toBe(0);
        timer.show(120_000);
        expect(timer.take(130_000)).toBe(10);
    });
});
