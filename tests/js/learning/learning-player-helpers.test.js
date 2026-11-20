import { describe, expect, it } from 'vitest';

import { progressText } from '../../../src/Modules/CrestApps.OrchardCore.Learning/Assets/js/learning-player-helpers.js';

// The player's progress bar never stands alone: beside it, "3 of 7 complete · 43%", from a localized template.
describe('learning player progress text', () => {
    it('fills the localized template with the counts and the percent', () => {
        expect(progressText('{0} of {1} complete · {2}%', 3, 7, 43)).toBe('3 of 7 complete · 43%');
        expect(progressText('{2} % — {0}/{1}', 0, 5, 0)).toBe('0 % — 0/5');
    });
});
