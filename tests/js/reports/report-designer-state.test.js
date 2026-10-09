import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-designer/designer-state.js';
import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-charts.js';

const designer = globalThis.CrestAppsReportDesigner;
const charts = globalThis.CrestAppsReportCharts;

const field = (key, dataType, extra = {}) => ({ key, label: key, dataType, ...extra });

function design() {
    return {
        query: {
            dataSets: [
                { alias: 'Customer', source: 'Contents', dataSet: 'Customer', displayName: 'Customer' },
                { alias: 'Order', source: 'Contents', dataSet: 'Order', displayName: 'Order' },
            ],
            joins: [{ alias: 'Order', type: 'Inner', conditions: [{ leftField: 'Customer.ContentItemId', rightField: 'Order.Order.Customer' }] }],
            calculatedFields: [],
            filters: [],
            columns: [],
            sorts: [],
        },
        visuals: [],
    };
}

describe('type rules', () => {
    it('offers sums and averages only for numbers', () => {
        expect(designer.aggregatesFor('Decimal')).toContain('Sum');
        expect(designer.aggregatesFor('Text')).not.toContain('Sum');
        expect(designer.aggregatesFor('Date')).toContain('Max');
        expect(designer.aggregatesFor('Boolean')).toEqual(['None', 'Count', 'CountDistinct']);
    });

    it('offers date transforms only for dates and hours only for date-times', () => {
        expect(designer.transformsFor('Date')).toContain('Month');
        expect(designer.transformsFor('Date')).not.toContain('Hour');
        expect(designer.transformsFor('DateTime')).toContain('Hour');
        expect(designer.transformsFor('Text')).toContain('Upper');
        expect(designer.transformsFor('Integer')).toEqual(['None', 'Round']);
    });

    it('offers text matching only for text and day windows only for dates', () => {
        expect(designer.operatorsFor('Text')).toContain('Contains');
        expect(designer.operatorsFor('Integer')).not.toContain('Contains');
        expect(designer.operatorsFor('DateTime')).toContain('InLastDays');
        expect(designer.operatorsFor('Text')).not.toContain('InLastDays');
    });

    it('makes a control carry the operator it needs', () => {
        expect(designer.operatorForControl('DateRange', 'Equals')).toBe('Between');
        expect(designer.operatorForControl('MultiSelect', 'NotIn')).toBe('NotIn');
        expect(designer.operatorForControl('MultiSelect', 'Equals')).toBe('In');
        expect(designer.operatorForControl('Text', 'Contains')).toBeNull();
    });
});

describe('aliases and identifiers', () => {
    it('turns a data set name into a unique formula-safe alias', () => {
        const existing = [{ alias: 'SalesOrder' }];

        expect(designer.aliasFor('Sales Order', existing)).toBe('SalesOrder2');
        expect(designer.aliasFor('2024 data', [])).toBe('D2024data');
        expect(designer.aliasFor('Customer', [])).toBe('Customer');
    });

    it('creates identifiers that are not in use', () => {
        expect(designer.newId('c', ['c1', 'c2', 'c4'])).toBe('c3');
    });
});

describe('columns and filters', () => {
    it('sums a dropped number but keeps an identifier as a dimension', () => {
        const value = design();

        const total = designer.addColumn(value.query, field('Order.Total', 'Decimal'));
        const id = designer.addColumn(value.query, field('Order.Number', 'Integer', { isIdentifier: true }), 0);

        expect(total.aggregate).toBe('Sum');
        expect(id.aggregate).toBe('None');
        expect(value.query.columns.map(column => column.field)).toEqual(['Order.Number', 'Order.Total']);
    });

    it('filters an aggregated field on the result', () => {
        const value = design();

        const filter = designer.addFilter(value.query, field('Margin', 'Decimal', { isAggregate: true }));

        expect(filter.stage).toBe('Result');
    });

    // Removing a column used to leave sorts, result filters, and visuals pointing at it, so the saved report failed to run.
    it('removes every reference to a removed column', () => {
        const value = design();
        const column = designer.addColumn(value.query, field('Order.Total', 'Decimal'));

        value.query.sorts.push({ columnId: column.id, descending: true });
        value.query.filters.push({ id: 'f1', field: column.id, stage: 'Result', operator: 'GreaterThan', values: ['5'] });
        value.visuals.push({ id: 'v1', type: 'Chart', categoryColumnId: column.id, seriesColumnId: column.id, valueColumnIds: [column.id], columnIds: [column.id] });

        designer.removeColumn(value, column.id);

        expect(value.query.columns).toEqual([]);
        expect(value.query.sorts).toEqual([]);
        expect(value.query.filters).toEqual([]);
        expect(value.visuals[0]).toMatchObject({ categoryColumnId: null, seriesColumnId: null, valueColumnIds: [], columnIds: [] });
    });

    it('removes a data set with its join, columns, and row filters', () => {
        const value = design();

        designer.addColumn(value.query, field('Customer.Name', 'Text'));
        designer.addColumn(value.query, field('Order.Total', 'Decimal'));
        designer.addFilter(value.query, field('Order.Total', 'Decimal'));

        designer.removeDataSet(value, 'Order');

        expect(value.query.dataSets.map(dataSet => dataSet.alias)).toEqual(['Customer']);
        expect(value.query.joins).toEqual([]);
        expect(value.query.columns.map(column => column.field)).toEqual(['Customer.Name']);
        expect(value.query.filters).toEqual([]);
    });

    it('drops the join of the data set that becomes the base', () => {
        const value = design();

        designer.removeDataSet(value, 'Customer');

        expect(value.query.dataSets.map(dataSet => dataSet.alias)).toEqual(['Order']);
        expect(value.query.joins).toEqual([]);
    });

    it('moves an item within a list', () => {
        expect(designer.move(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
        expect(designer.move(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b']);
    });
});

describe('join suggestions', () => {
    it('matches an order picker that names the customer with the customer id', () => {
        const customer = {
            dataSet: { alias: 'Customer', dataSet: 'Customer', displayName: 'Customer' },
            fields: [
                { name: 'ContentItemId', displayName: 'Content item id', isIdentifier: true },
                { name: 'Owner', displayName: 'Owner', isIdentifier: true },
            ],
        };
        const order = {
            dataSet: { alias: 'Order', dataSet: 'Order', displayName: 'Order' },
            fields: [
                { name: 'ContentItemId', displayName: 'Content item id', isIdentifier: true },
                { name: 'Order.Customer', displayName: 'Customer', isIdentifier: true },
            ],
        };

        expect(designer.suggestJoin([customer], order)).toEqual({
            leftField: 'Customer.ContentItemId',
            rightField: 'Order.Order.Customer',
        });
    });

    it('suggests nothing when either side has no identifier', () => {
        const left = { dataSet: { alias: 'A', dataSet: 'A' }, fields: [{ name: 'Name' }] };
        const right = { dataSet: { alias: 'B', dataSet: 'B' }, fields: [{ name: 'Id', isIdentifier: true }] };

        expect(designer.suggestJoin([left], right)).toBeNull();
    });
});

describe('declared relationships', () => {
    const account = {
        dataSet: { alias: 'Account', source: 'Contents', dataSet: 'Account' },
        fields: [{ name: 'ContentItemId', isIdentifier: true }],
    };
    const contact = {
        dataSet: { alias: 'Contact', source: 'Contents', dataSet: 'Contact' },
        fields: [
            { name: 'ContentItemId', isIdentifier: true },
            {
                name: 'ContainedPart.ListContentItemId',
                isIdentifier: true,
                references: [{ source: 'Contents', dataSet: 'Account', field: 'ContentItemId' }],
            },
        ],
    };

    it('joins a contained item to the list that holds it', () => {
        expect(designer.suggestJoin([account], contact)).toEqual({
            leftField: 'Account.ContentItemId',
            rightField: 'Contact.ContainedPart.ListContentItemId',
        });
    });

    it('joins when the earlier data set holds the reference, even when names do not match', () => {
        const order = {
            dataSet: { alias: 'Order', source: 'Contents', dataSet: 'Order' },
            fields: [{ name: 'Order.Buyer', isIdentifier: true, references: [{ source: 'Contents', dataSet: 'Account', field: 'ContentItemId' }] }],
        };

        expect(designer.referencedJoin([order], account)).toEqual({
            leftField: 'Order.Order.Buyer',
            rightField: 'Account.ContentItemId',
        });
    });

    it('ignores a reference to a data set of another source with the same name', () => {
        const users = {
            dataSet: { alias: 'Account2', source: 'Users', dataSet: 'Account' },
            fields: [{ name: 'UserId', isIdentifier: true }],
        };

        expect(designer.referencedJoin([users], contact)).toBeNull();
    });

    it('tells which data sets are related in either direction', () => {
        const contactSet = { source: 'Contents', dataSet: 'Contact', references: [{ source: 'Contents', dataSet: 'Account' }] };
        const accountSet = { source: 'Contents', dataSet: 'Account', references: [] };
        const userSet = { source: 'Users', dataSet: 'Users' };

        expect(designer.isRelated(contactSet, accountSet)).toBe(true);
        expect(designer.isRelated(accountSet, contactSet)).toBe(true);
        expect(designer.isRelated(accountSet, userSet)).toBe(false);
    });
});

describe('filter form values', () => {
    it('reads single, multiple, and range values and keeps a cleared filter empty', () => {
        const values = designer.filterValuesFromEntries([
            ['applied', '1'],
            ['f.region', 'West'],
            ['f.region', 'East'],
            ['f.name', ''],
            ['f.placed.from', '2026-01-01T00:00'],
            ['f.placed.to', '2026-01-31T23:59'],
            ['f.total.from', ''],
            ['f.total.to', ''],
        ]);

        expect(values).toEqual({
            region: ['West', 'East'],
            name: [],
            placed: ['2026-01-01T00:00', '2026-01-31T23:59:59'],
            total: [],
        });
    });
});

describe('chart rendering', () => {
    it('draws horizontal bars along the y axis and areas filled', () => {
        const horizontal = charts.buildConfig({ type: 'horizontalbar', labels: ['A'], datasets: [{ label: 'Total', data: [1] }] }, '#000', '#ccc');
        const area = charts.buildConfig({ type: 'area', labels: ['A'], datasets: [{ label: 'Total', data: [1] }] }, '#000', '#ccc');
        const pie = charts.buildConfig({ type: 'pie', labels: ['A', 'B'], datasets: [{ label: 'Total', data: [1, 2] }] }, '#000', '#ccc');

        expect(horizontal.type).toBe('bar');
        expect(horizontal.options.indexAxis).toBe('y');
        expect(area.type).toBe('line');
        expect(area.data.datasets[0].fill).toBe(true);
        expect(pie.options.scales).toEqual({});
        expect(pie.data.datasets[0].backgroundColor).toHaveLength(2);
    });
});
