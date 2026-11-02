import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-designer/designer-state.js';
import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-charts.js';
import '../../../src/Modules/CrestApps.OrchardCore.Reports/Assets/js/report-interactions.js';

const designer = globalThis.CrestAppsReportDesigner;
const charts = globalThis.CrestAppsReportCharts;
const interactions = globalThis.CrestAppsReportInteractions;

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

describe('filter stage', () => {
    it('restores the field of a filter switched to the result and back to the rows', () => {
        const value = design();
        const filter = designer.addFilter(value.query, field('Order.Channel', 'Text'));

        designer.setFilterStage(filter, 'Result', [{ id: 'total' }, { id: 'count' }]);
        expect(filter).toMatchObject({ stage: 'Result', field: 'total', values: [] });

        designer.setFilterStage(filter, 'Rows', []);
        expect(filter).toMatchObject({ stage: 'Rows', field: 'Order.Channel', values: [] });
    });

    it('leaves a filter switched to the stage it already has unchanged', () => {
        const value = design();
        const filter = designer.addFilter(value.query, field('Order.Channel', 'Text'));
        filter.values = ['Phone'];

        designer.setFilterStage(filter, 'Rows', []);
        expect(filter).toMatchObject({ stage: 'Rows', field: 'Order.Channel', values: ['Phone'] });
    });
});

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

describe('shelves', () => {
    it('adds a field to the Rows shelf and keeps each shelf in its own order', () => {
        const value = design();
        const total = designer.addColumn(value.query, field('Order.Total', 'Decimal'));
        const region = designer.addColumn(value.query, field('Customer.Region', 'Text'), 0, 'Rows');
        const name = designer.addColumn(value.query, field('Customer.Name', 'Text'), 0, 'Rows');
        const status = designer.addColumn(value.query, field('Order.Status', 'Text'), 0);

        expect(designer.shelfColumns(value.query, 'Rows').map(column => column.id)).toEqual([name.id, region.id]);
        expect(designer.shelfColumns(value.query, 'Columns').map(column => column.id)).toEqual([status.id, total.id]);
        expect(region.shelf).toBe('Rows');
        expect(designer.shelfOf({})).toBe('Columns');
    });

    it('moves a column to the other shelf at a position among its columns', () => {
        const value = design();
        const total = designer.addColumn(value.query, field('Order.Total', 'Decimal'));
        const region = designer.addColumn(value.query, field('Customer.Region', 'Text'), null, 'Rows');
        const status = designer.addColumn(value.query, field('Order.Status', 'Text'));

        designer.placeColumn(value.query, status.id, 'Rows', 0);

        expect(designer.shelfColumns(value.query, 'Rows').map(column => column.id)).toEqual([status.id, region.id]);
        expect(designer.shelfColumns(value.query, 'Columns').map(column => column.id)).toEqual([total.id]);
        expect(designer.placeColumn(value.query, 'missing', 'Rows', 0)).toBeNull();
    });

    it('sorts by a column first and keeps the other sorts after it', () => {
        const value = design();

        value.query.sorts = [{ columnId: 'a', descending: false }, { columnId: 'b', descending: true }];
        designer.sortBy(value.query, 'b', false);

        expect(value.query.sorts).toEqual([{ columnId: 'b', descending: false }, { columnId: 'a', descending: false }]);
    });
});

describe('column colors', () => {
    it('adds red, orange and green bands that fill the cells', () => {
        const column = { id: 'c1' };

        designer.addTrafficLightRules(column, 20, 50);

        expect(column.colors.rules).toEqual([
            { operator: 'LessThan', values: ['20'], color: '#F8D7DA', target: 'Background' },
            { operator: 'Between', values: ['20', '50'], color: '#FFE5D0', target: 'Background' },
            { operator: 'GreaterThanOrEqual', values: ['50'], color: '#D1E7DD', target: 'Background' },
        ]);
    });

    it('drops colors that no longer color anything', () => {
        const column = { id: 'c1' };

        designer.ensureColors(column).color = '#DC3545';
        designer.pruneColors(column);
        expect(column.colors.color).toBe('#DC3545');

        column.colors.color = null;
        designer.pruneColors(column);
        expect(column.colors).toBeNull();
    });

    it('compares values with single values and ranges only', () => {
        expect(designer.colorOperatorsFor('Decimal')).toContain('Between');
        expect(designer.colorOperatorsFor('Decimal')).not.toContain('In');
        expect(designer.colorOperatorsFor('Date')).not.toContain('InLastDays');
        expect(designer.colorOperatorsFor('Text')).toContain('Contains');
    });
});

describe('quick formulas', () => {
    const functions = ['SUM', 'AVG', 'MIN', 'MAX', 'MEDIAN', 'COUNT', 'COUNTD', 'ROUND', 'ABS', 'UPPER', 'LOWER', 'TRIM', 'LEN', 'YEAR', 'DATETRUNC', 'ISBLANK']
        .map(name => ({ name }));

    it('offers the aggregates and functions that suit the type of a field', () => {
        const number = designer.quickFormulas(field('Order.Total', 'Decimal'), functions).map(formula => formula.expression);
        const text = designer.quickFormulas(field('Order.Status', 'Text'), functions).map(formula => formula.expression);
        const date = designer.quickFormulas(field('Order.Placed', 'DateTime'), functions).map(formula => formula.expression);

        expect(number.slice(0, 3)).toEqual(['SUM([Order.Total])', 'AVG([Order.Total])', 'MIN([Order.Total])']);
        expect(number).toContain('ROUND([Order.Total], 2)');
        expect(text).not.toContain('SUM([Order.Status])');
        expect(text).toEqual(expect.arrayContaining(['MIN([Order.Status])', 'COUNTD([Order.Status])', 'UPPER([Order.Status])']));
        expect(date).toContain('DATETRUNC(\'month\', [Order.Placed])');
    });

    it('offers only the functions the catalog has', () => {
        const expressions = designer.quickFormulas(field('Order.Placed', 'DateTime'), functions).map(formula => formula.expression);

        expect(expressions.some(expression => expression.startsWith('MONTH('))).toBe(false);
        expect(expressions.some(expression => expression.startsWith('DATEDIFF('))).toBe(false);
    });

    it('does not aggregate a field that is already aggregated', () => {
        const formulas = designer.quickFormulas(field('Margin', 'Decimal', { isAggregate: true }), functions);

        expect(formulas.some(formula => formula.isAggregate)).toBe(false);
        expect(designer.quickFormulas(field(designer.ROW_COUNT_FIELD, 'Integer', { isAggregate: true }), functions)).toEqual([]);
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

describe('default date filter', () => {
    const fields = [{ name: 'CreatedUtc', displayName: 'Created', dataType: 'DateTime' }, { name: 'Name', dataType: 'Text' }];

    it('filters a new report on the last 30 days of the main date, as a recent period viewers can change', () => {
        const filter = designer.defaultDateFilter({ filters: [] }, { alias: 'Activities', defaultDateField: 'CreatedUtc' }, fields);

        expect(filter).toMatchObject({
            field: 'Activities.CreatedUtc',
            stage: 'Rows',
            operator: 'InLastDays',
            values: ['30'],
            exposed: true,
            label: 'Created',
            control: 'RelativeDate',
        });
    });

    it('adds nothing without a main date, when the field is missing or not a date, or when the report already filters', () => {
        expect(designer.defaultDateFilter({ filters: [] }, { alias: 'A' }, fields)).toBeNull();
        expect(designer.defaultDateFilter({ filters: [] }, { alias: 'A', defaultDateField: 'Missing' }, fields)).toBeNull();
        expect(designer.defaultDateFilter({ filters: [] }, { alias: 'A', defaultDateField: 'Name' }, fields)).toBeNull();
        expect(designer.defaultDateFilter({ filters: [{ id: 'f1' }] }, { alias: 'A', defaultDateField: 'CreatedUtc' }, fields)).toBeNull();
    });

    it('offers recent periods for date fields', () => {
        expect(designer.controlsFor('DateTime')).toContain('RelativeDate');
        expect(designer.operatorForControl('RelativeDate', 'Between')).toBe('InLastDays');
    });
});

describe('one join per data set', () => {
    it('finds a join without creating one', () => {
        const query = { joins: [] };

        expect(designer.findJoin(query, 'Customer')).toBeUndefined();
        expect(query.joins).toEqual([]);
    });

    it('merges two joins of the same data set, keeping the complete pairs once', () => {
        const query = {
            joins: [
                { alias: 'Customer', type: 'Left', conditions: [{ leftField: '', rightField: '' }] },
                { alias: 'Customer', type: 'Inner', conditions: [{ leftField: 'Users.UserId', rightField: 'Customer.Owner' }, { leftField: 'Users.UserId', rightField: 'Customer.Owner' }] },
                { alias: 'Order', type: 'Inner', conditions: [] },
            ],
        };

        designer.mergeJoins(query);

        expect(query.joins).toEqual([
            { alias: 'Customer', type: 'Left', conditions: [{ leftField: 'Users.UserId', rightField: 'Customer.Owner' }] },
            { alias: 'Order', type: 'Inner', conditions: [] },
        ]);
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

describe('containers', () => {
    it('adds containers with unused identifiers', () => {
        const value = design();

        const first = designer.addContainer(value);
        const second = designer.addContainer(value);

        expect(first).toMatchObject({ id: 'k1', width: 12, height: 400 });
        expect(second.id).toBe('k2');
        expect(value.containers).toHaveLength(2);
    });

    it('shows the visuals of a removed container on their own', () => {
        const value = design();
        const container = designer.addContainer(value);

        value.visuals = [{ id: 'v1', containerId: container.id }, { id: 'v2', containerId: 'other' }];
        designer.removeContainer(value, container.id);

        expect(value.containers).toEqual([]);
        expect(value.visuals.map((visual) => visual.containerId)).toEqual([null, 'other']);
    });
});

describe('queries', () => {
    it('adds a query that starts as a copy of another', () => {
        const value = design();
        value.query.filters.push({ id: 'f1', field: 'Order.PlacedUtc', exposed: true });

        const added = designer.addQuery(value, value.query);

        expect(added.id).toBe('q1');
        expect(added.query).toEqual(value.query);
        expect(added.query).not.toBe(value.query);
        expect(added.query.filters[0].id).toBe('f1');
    });

    it('removes a query with its visuals and the clicks that filtered them', () => {
        const value = design();
        const added = designer.addQuery(value, value.query);

        value.visuals = [
            { id: 'v1', action: { columnId: 'c1', targets: [{ visualId: 'v2' }, { visualId: 'v3' }] } },
            { id: 'v2', queryId: added.id },
            { id: 'v3' },
        ];
        designer.removeQuery(value, added.id);

        expect(value.queries).toEqual([]);
        expect(value.visuals.map((visual) => visual.id)).toEqual(['v1', 'v3']);
        expect(value.visuals[0].action.targets).toEqual([{ visualId: 'v3' }]);
    });

    it('removes a column only from the visuals of the query being edited', () => {
        const value = design();
        const column = designer.addColumn(value.query, field('Order.Total', 'Decimal'));

        value.visuals = [
            { id: 'v1', valueColumnIds: [column.id] },
            { id: 'v2', queryId: 'q1', valueColumnIds: [column.id] },
        ];
        designer.removeColumn(value, column.id);

        expect(value.visuals[0].valueColumnIds).toEqual([]);
        expect(value.visuals[1].valueColumnIds).toEqual([column.id]);
    });
});

describe('click actions', () => {
    const columns = [
        { id: 'agent', isMeasure: false },
        { id: 'day', isMeasure: false },
        { id: 'weekday', isMeasure: false },
        { id: 'secret', isMeasure: false, hidden: true },
        { id: 'score', isMeasure: true },
    ];
    const definitions = [
        { id: 'agent', transform: 'None' },
        { id: 'day', transform: 'Day' },
        { id: 'weekday', transform: 'DayOfWeek' },
        { id: 'secret', transform: 'None' },
        { id: 'score', transform: 'None' },
    ];

    it('offers the dimensions a table shows whose values filter their field', () => {
        const all = designer.clickableColumns({ type: 'Table', columnIds: [] }, columns, definitions);
        const some = designer.clickableColumns({ type: 'Table', columnIds: ['day', 'score'] }, columns, definitions);

        expect(all.map((column) => column.id)).toEqual(['agent', 'day']);
        expect(some.map((column) => column.id)).toEqual(['day']);
    });

    it('offers the categories and series of a chart, and nothing for metrics', () => {
        const chart = designer.clickableColumns({ type: 'Chart', categoryColumnId: 'day', seriesColumnId: 'agent' }, columns, definitions);
        const metrics = designer.clickableColumns({ type: 'Metrics' }, columns, definitions);

        expect(chart.map((column) => column.id)).toEqual(['agent', 'day']);
        expect(metrics).toEqual([]);
    });

    it('sets the column a click picks and the visuals it filters', () => {
        const visual = { id: 'v1' };

        designer.setActionColumn(visual, 'agent');
        designer.toggleActionTarget(visual, 'v2', true);
        designer.toggleActionTarget(visual, 'v3', true);
        designer.toggleActionTarget(visual, 'v1', true);
        designer.toggleActionTarget(visual, 'v2', false);

        expect(visual.action).toEqual({ columnId: 'agent', targets: [{ visualId: 'v3', field: null }] });

        designer.setActionColumn(visual, '');
        expect(visual.action).toBeNull();
    });

    it('drops a click action when its column is removed', () => {
        const value = design();
        const column = designer.addColumn(value.query, field('Order.Channel', 'Text'));

        value.visuals = [{ id: 'v1', action: { columnId: column.id, targets: [{ visualId: 'v2' }] } }];
        designer.removeColumn(value, column.id);

        expect(value.visuals[0].action).toBeNull();
    });
});

describe('picking values', () => {
    it('picks only the value clicked, and clears it when clicked again', () => {
        expect(interactions.nextKeys([], 'a', false)).toEqual(['a']);
        expect(interactions.nextKeys(['a', 'b'], 'c', false)).toEqual(['c']);
        expect(interactions.nextKeys(['a'], 'a', false)).toEqual([]);
        expect(interactions.nextKeys(['a', 'b'], 'a', false)).toEqual(['a']);
    });

    it('adds or removes the value clicked with Ctrl', () => {
        expect(interactions.nextKeys(['a'], 'b', true)).toEqual(['a', 'b']);
        expect(interactions.nextKeys(['a', 'b'], 'a', true)).toEqual(['b']);
    });

    it('finds the key of the category or series clicked', () => {
        expect(charts.keyAt({ keys: ['x', 'y'] }, { index: 1, datasetIndex: 0 })).toBe('y');
        expect(charts.keyAt({ seriesKeys: ['s1', 's2'] }, { index: 0, datasetIndex: 1 })).toBe('s2');
        expect(charts.keyAt({}, { index: 0, datasetIndex: 0 })).toBeUndefined();
    });

    it('fades the categories a pick leaves out', () => {
        const model = { type: 'bar', labels: ['A', 'B'], keys: ['a', 'b'], selectedKeys: ['b'], datasets: [{ label: 'Total', data: [1, 2] }] };
        const config = charts.buildConfig(model, '#000', '#ccc');
        const colors = config.data.datasets[0].backgroundColor;

        expect(colors).toHaveLength(2);
        expect(colors[0]).not.toBe(colors[1]);
        expect(colors[0].startsWith(colors[1])).toBe(true);
        expect(typeof config.options.onClick).toBe('function');
    });

    it('leaves a chart nobody can click as it was', () => {
        const config = charts.buildConfig({ type: 'bar', labels: ['A'], datasets: [{ label: 'Total', data: [1] }] }, '#000', '#ccc');

        expect(config.options.onClick).toBeUndefined();
        expect(typeof config.data.datasets[0].backgroundColor).toBe('string');
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

describe('quick table calculations', () => {
    it('offers a calculation on numeric measures, or on any number of a result that groups nothing', () => {
        const measure = { id: 'total', dataType: 'Decimal', isMeasure: true };
        const dimension = { id: 'region', dataType: 'Text', isMeasure: false };
        const count = { id: 'count', dataType: 'Integer', isMeasure: false };

        expect(designer.canShowAs(measure, [dimension, measure])).toBe(true);
        expect(designer.canShowAs(dimension, [dimension, measure])).toBe(false);
        expect(designer.canShowAs(count, [dimension, count, measure])).toBe(false);
        expect(designer.canShowAs(count, [dimension, count])).toBe(true);
        expect(designer.canShowAs(undefined, [])).toBe(false);
    });

    it('sets a calculation and its direction, and forgets both when showing values again', () => {
        const column = { id: 'total' };

        designer.setShowAs(column, 'PercentOfTotal');
        expect(column).toMatchObject({ showAs: 'PercentOfTotal', showAsDirection: 'Down' });

        designer.setShowAs(column, 'RunningTotal', 'Across');
        expect(column).toMatchObject({ showAs: 'RunningTotal', showAsDirection: 'Across' });

        designer.setShowAs(column, 'Rank');
        expect(column.showAsDirection).toBe('Across');

        designer.setShowAs(column, 'Rank', 'Sideways');
        expect(column.showAsDirection).toBe('Down');

        designer.setShowAs(column, 'Value');
        expect(column).toEqual({ id: 'total' });

        designer.setShowAs(column, 'Unknown');
        expect(column).toEqual({ id: 'total' });
    });

    it('badges the pill of a calculated column', () => {
        expect(designer.showAsBadge({ showAs: 'PercentOfTotal' })).toBe('%');
        expect(designer.showAsBadge({ showAs: 'DifferenceFromPrevious' })).toBe('Δ');
        expect(designer.showAsBadge({ showAs: 'Rank' })).toBe('#');
        expect(designer.showAsBadge({})).toBeNull();
    });
});

describe('reference lines', () => {
    it('adds the average of the first value column', () => {
        const visual = { type: 'Chart', valueColumnIds: ['total', 'count'] };

        const line = designer.addReferenceLine(visual);

        expect(line).toEqual({ columnId: 'total', kind: 'Average', value: null, label: '' });
        expect(visual.referenceLines).toEqual([line]);
    });

    it('drops the lines of a column the chart no longer plots, keeping constants', () => {
        const visual = {
            valueColumnIds: ['total'],
            referenceLines: [
                { columnId: 'total', kind: 'Median' },
                { columnId: 'count', kind: 'Average' },
                { columnId: 'count', kind: 'Constant', value: 5 },
            ],
        };

        designer.pruneReferenceLines(visual);

        expect(visual.referenceLines).toEqual([
            { columnId: 'total', kind: 'Median' },
            { columnId: null, kind: 'Constant', value: 5 },
        ]);
    });

    it('prunes the lines of a removed column', () => {
        const value = design();
        value.query.columns = [{ id: 'total', field: 'Order.Total' }];
        value.visuals = [{ id: 'v1', type: 'Chart', valueColumnIds: ['total'], referenceLines: [{ columnId: 'total', kind: 'Average' }] }];

        designer.removeColumn(value, 'total');

        expect(value.visuals[0].referenceLines).toEqual([]);
    });

    it('stretches the value axis to every line, and draws none on a pie', () => {
        const model = {
            type: 'bar',
            labels: ['A', 'B'],
            datasets: [{ label: 'Total', data: [10, 20] }],
            referenceLines: [{ label: 'Target: 50', value: 50, datasetIndex: null }, { label: 'Floor', value: -5, datasetIndex: 0 }],
        };

        const config = charts.buildConfig(model, '#000', '#ccc');
        const pie = charts.buildConfig({ ...model, type: 'pie' }, '#000', '#ccc');

        expect(config.options.scales.y).toMatchObject({ suggestedMin: -5, suggestedMax: 50 });
        expect(config.options.plugins.reportReferenceLines.lines).toHaveLength(2);
        expect(config.plugins).toEqual([charts.referenceLinesPlugin]);
        expect(pie.plugins).toEqual([]);
        expect(charts.referenceLineRange([], true)).toEqual({ suggestedMin: undefined, suggestedMax: 100 });
        expect(charts.referenceLineRange([{ value: 140 }], true).suggestedMax).toBe(140);
    });

    it('draws each line across the plot in the color of its dataset', () => {
        const calls = [];
        const ctx = new Proxy({}, {
            get: (target, name) => (name in target ? target[name] : (...args) => calls.push([name, ...args])),
            set: (target, name, value) => {
                target[name] = value;
                calls.push(['set ' + String(name), value]);
                return true;
            },
        });
        const chart = {
            ctx,
            chartArea: { left: 10, right: 210, top: 0, bottom: 100 },
            scales: { y: { getPixelForValue: (value) => 100 - value } },
        };

        charts.referenceLinesPlugin.afterDatasetsDraw(chart, {}, {
            lines: [{ label: 'Average: 40', value: 40, datasetIndex: 1 }],
            horizontal: false,
            textColor: '#111',
        });

        expect(calls).toContainEqual(['moveTo', 10, 60]);
        expect(calls).toContainEqual(['lineTo', 210, 60]);
        expect(calls).toContainEqual(['set strokeStyle', '#14b8a6']);
        expect(calls).toContainEqual(['fillText', 'Average: 40', 206, 57]);
        expect(charts.referenceLineColor({ datasetIndex: null }, '#111')).toBe('#111');
    });
});
