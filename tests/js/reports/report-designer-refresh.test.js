import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-designer/designer-refresh.js';

const designer = globalThis.CrestAppsReportDesigner;

// Stands in for the builder page: texts are their English keys, and times are shown as they were sent.
const page = {
    t: (key) => key,
    formatTime: (value) => `<${value}>`,
};

describe('refresh schedule options', () => {
    it('offers live and the four schedules, in minutes', () => {
        const options = designer.refreshOptions(page, 0);

        expect(options.map((option) => option.value)).toEqual([0, 15, 60, 360, 1440]);
        expect(options[0].text).toBe('Live');
        expect(options[2].text).toBe('Every hour');
    });

    it('keeps a schedule set elsewhere selectable', () => {
        const options = designer.refreshOptions(page, '120');

        expect(options.at(-1)).toEqual({ value: 120, text: 'Every 120 minutes' });
    });
});

describe('stored result status', () => {
    it('says a view was not refreshed yet when it has no stored result', () => {
        const status = designer.describeSnapshot(page, null);

        expect(status.failed).toBe(false);
        expect(status.text).toContain('Not refreshed yet');
    });

    it('shows when the rows were refreshed and how many there are', () => {
        const status = designer.describeSnapshot(page, { refreshedUtc: '2026-03-15T12:00:00Z', rowCount: 42 });

        expect(status).toEqual({ failed: false, text: 'Last refreshed <2026-03-15T12:00:00Z> (42 rows)' });
    });

    it('shows the error of a failed refresh, and that the earlier rows are still read', () => {
        const status = designer.describeSnapshot(page, {
            refreshedUtc: '2026-03-15T11:00:00Z',
            rowCount: 3,
            lastError: 'The data set is gone.',
            lastErrorUtc: '2026-03-15T12:00:00Z',
        });

        expect(status.failed).toBe(true);
        expect(status.text).toBe('The last refresh failed (<2026-03-15T12:00:00Z>): The data set is gone. Reports read the rows of the last successful refresh.');
    });
});
