import { describe, expect, it } from 'vitest';

import { createActiveTimer, progressText, swapRefreshed } from '../../../src/Modules/CrestApps.OrchardCore.Learning/Assets/js/learning-player-helpers.js';

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

// When an item completes in the background (minimum time, video, a package), the player swaps in the outline, the
// progress and the Previous/Next buttons from a fresh copy of the page, keeping the sections the learner folded.
describe('learning player refresh', () => {
    const section = open => ({ open });
    const part = (sections = []) => {
        const node = {
            sections,
            replacedWith: null,
            querySelectorAll: () => sections,
            replaceWith(other) {
                node.replacedWith = other;
            },
        };

        return node;
    };

    it('replaces each part the fresh page has, and keeps the folded sections folded', () => {
        const outline = part([section(true), section(false), section(true)]);
        const nav = part();
        const freshOutline = part([section(true), section(true), section(true)]);
        const freshNav = part();

        const replaced = swapRefreshed(new Map([['outline', outline], ['nav', nav], ['index', part()]]), new Map([['outline', freshOutline], ['nav', freshNav]]));

        expect(replaced).toEqual(['outline', 'nav']);
        expect(outline.replacedWith).toBe(freshOutline);
        expect(nav.replacedWith).toBe(freshNav);
        expect(freshOutline.sections.map(candidate => candidate.open)).toEqual([true, false, true]);
    });

    it('leaves the sections as the server drew them when the outline changed shape', () => {
        const outline = part([section(false)]);
        const freshOutline = part([section(true), section(true)]);

        swapRefreshed(new Map([['outline', outline]]), new Map([['outline', freshOutline]]));

        expect(freshOutline.sections.map(candidate => candidate.open)).toEqual([true, true]);
    });
});
