import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Checks/Assets/js/check-layout-designer.js';

const designer = globalThis.CrestAppsCheckDesigner;

describe('clampToCheck', () => {
    it('keeps a field on the check', () => {
        expect(designer.clampToCheck({ x: -10, y: -4, width: 100, size: 10 })).toEqual({ x: 0, y: 0, width: 100, size: 10 });
        expect(designer.clampToCheck({ x: 600, y: 300, width: 100, size: 10 })).toEqual({ x: 512, y: 254, width: 100, size: 10 });
    });

    it('rounds to half a point and keeps the text size between 4 and 36', () => {
        expect(designer.clampToCheck({ x: 10.26, y: 20.74, width: 0, size: 2 })).toEqual({ x: 10.5, y: 20.5, width: 0, size: 4 });
        expect(designer.clampToCheck({ x: 10, y: 20, width: 0, size: 60 }).size).toBe(36);
    });
});

describe('moveBy', () => {
    it('moves a field and stops it at the edge', () => {
        expect(designer.moveBy({ x: 100, y: 50, width: 200, size: 10 }, 12, -5)).toEqual({ x: 112, y: 45, width: 200, size: 10 });
        expect(designer.moveBy({ x: 400, y: 50, width: 200, size: 10 }, 50, 0).x).toBe(412);
    });
});

describe('the clear band', () => {
    it('is the bottom 5/8 inch of the check', () => {
        expect(designer.CLEAR_BAND_TOP).toBe(264 - 45);
    });

    it('is entered by a field whose lines reach below its top', () => {
        expect(designer.entersClearBand({ y: 204, size: 9 }, 1)).toBe(false);
        expect(designer.entersClearBand({ y: 212, size: 9 }, 1)).toBe(true);
        expect(designer.entersClearBand({ y: 150, size: 9 }, 4)).toBe(false);
        expect(designer.entersClearBand({ y: 180, size: 9 }, 4)).toBe(true);
    });
});

describe('micrLeftOf', () => {
    it('counts 1/8 inch positions from 5/16 inch left of the right edge, as the printer does', () => {
        expect(designer.micrLeftOf(1)).toBe(612 - 22.5 - 9);
        expect(designer.micrLeftOf(43)).toBe(612 - 22.5 - (43 * 9));
    });
});

describe('parsePoints', () => {
    it('reads measures typed with a period and falls back on blank or wrong text', () => {
        expect(designer.parsePoints('12.5', 0)).toBe(12.5);
        expect(designer.parsePoints('', 10)).toBe(10);
        expect(designer.parsePoints('abc', 3)).toBe(3);
    });
});
