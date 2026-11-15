import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Invoicing/Assets/js/payment-schedule-editor.js';

const schedule = globalThis.CrestAppsPaymentSchedule;

// A payment schedule splits the invoice into parts all given the same way: amounts, or percents. The editor picks the
// way once for the whole schedule and shows what is left to allocate; the server checks the parts on save.
describe('detectMode', () => {
    it('is percent when the saved parts were given as percents', () => {
        expect(schedule.detectMode([{ amount: null, percent: 50 }, { amount: null, percent: 50 }])).toBe('percent');
    });

    it('is amount otherwise, also for a new schedule', () => {
        expect(schedule.detectMode([{ amount: 100, percent: null }])).toBe('amount');
        expect(schedule.detectMode([])).toBe('amount');
    });
});

describe('allocation', () => {
    it('adds the amounts and says what is left of the total', () => {
        expect(schedule.allocation([400, 300], 'amount', 1000, 2)).toEqual({ allocated: 700, left: 300 });
    });

    it('adds the percents and says what is left of 100', () => {
        expect(schedule.allocation([50, 25], 'percent', 1000, 2)).toEqual({ allocated: 75, left: 25 });
    });

    it('ignores parts with no value yet', () => {
        expect(schedule.allocation([null, 250], 'amount', 500, 2)).toEqual({ allocated: 250, left: 250 });
    });

    it('rounds amounts to the currency', () => {
        expect(schedule.allocation([333.333, 333.333], 'amount', 1000, 2)).toEqual({ allocated: 666.67, left: 333.33 });
    });

    it('shows an over-allocation as a negative amount left', () => {
        expect(schedule.allocation([60, 50], 'percent', 0, 2)).toEqual({ allocated: 110, left: -10 });
    });
});

describe('valueName', () => {
    it('posts the value as the amount or the percent of its part', () => {
        expect(schedule.valueName(2, 'amount')).toBe('Schedule[2].Amount');
        expect(schedule.valueName(0, 'percent')).toBe('Schedule[0].Percent');
    });
});
