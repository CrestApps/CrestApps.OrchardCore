/*
 * The decisions the report builder makes about a design, kept free of the DOM so they can be tested: which aggregates,
 * transforms, operators, and controls suit a field type, how columns, filters, data sets, and joins are added and
 * removed without leaving dangling references, and which fields to suggest for a join.
 *
 * Concatenated ahead of the designer page script by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner = root.CrestAppsReportDesigner || {};

    var NUMERIC = ['Integer', 'Decimal'];
    var TEMPORAL = ['Date', 'DateTime'];

    designer.ROW_COUNT_FIELD = '$count';

    function isNumeric(dataType) {
        return NUMERIC.indexOf(dataType) >= 0;
    }

    function isTemporal(dataType) {
        return TEMPORAL.indexOf(dataType) >= 0;
    }

    designer.isNumeric = isNumeric;
    designer.isTemporal = isTemporal;

    // The aggregates a column can apply to a field of the given type, as the engine accepts them.
    designer.aggregatesFor = function (dataType) {
        var aggregates = ['None', 'Count', 'CountDistinct'];

        if (isNumeric(dataType)) {
            aggregates.push('Sum', 'Average', 'Min', 'Max', 'Median');
        } else if (isTemporal(dataType)) {
            aggregates.push('Min', 'Max', 'Median');
        } else if (dataType === 'Text') {
            aggregates.push('Min', 'Max');
        }

        return aggregates;
    };

    // The transforms a column can apply to a field of the given type.
    designer.transformsFor = function (dataType) {
        if (dataType === 'Text') {
            return ['None', 'Upper', 'Lower', 'Trim', 'Length'];
        }

        if (isNumeric(dataType)) {
            return ['None', 'Round'];
        }

        if (isTemporal(dataType)) {
            var transforms = ['None', 'Year', 'Quarter', 'Month', 'Week', 'Day', 'DayOfWeek', 'MonthOfYear'];

            if (dataType === 'DateTime') {
                transforms.push('Hour');
            }

            return transforms;
        }

        return ['None'];
    };

    // The filter operators that make sense for a field type.
    designer.operatorsFor = function (dataType) {
        var common = ['Equals', 'NotEquals', 'In', 'NotIn', 'IsEmpty', 'IsNotEmpty'];

        if (dataType === 'Boolean') {
            return ['Equals', 'NotEquals', 'IsEmpty', 'IsNotEmpty'];
        }

        if (isNumeric(dataType)) {
            return common.concat(['GreaterThan', 'GreaterThanOrEqual', 'LessThan', 'LessThanOrEqual', 'Between']);
        }

        if (isTemporal(dataType)) {
            return ['Equals', 'NotEquals', 'GreaterThan', 'GreaterThanOrEqual', 'LessThan', 'LessThanOrEqual', 'Between', 'InLastDays', 'InNextDays', 'IsEmpty', 'IsNotEmpty'];
        }

        return common.concat(['Contains', 'NotContains', 'StartsWith', 'EndsWith']);
    };

    // How many values an operator takes: 0, 1, 2 (a range), or 'many'.
    designer.valueArity = function (operator) {
        switch (operator) {
            case 'IsEmpty':
            case 'IsNotEmpty':
                return 0;
            case 'Between':
                return 2;
            case 'In':
            case 'NotIn':
                return 'many';
            default:
                return 1;
        }
    };

    // The controls an exposed filter may use for a field type, the automatic choice first.
    designer.controlsFor = function (dataType) {
        if (dataType === 'Boolean') {
            return ['Auto', 'Boolean'];
        }

        if (isTemporal(dataType)) {
            return ['Auto', 'RelativeDate', 'DateRange', 'Text'];
        }

        if (isNumeric(dataType)) {
            return ['Auto', 'NumberRange', 'Text', 'Select', 'MultiSelect'];
        }

        return ['Auto', 'Text', 'Select', 'MultiSelect'];
    };

    // The operator a control requires, or null when the control works with the current operator.
    designer.operatorForControl = function (control, operator) {
        switch (control) {
            case 'DateRange':
            case 'NumberRange':
                return 'Between';
            case 'MultiSelect':
                return operator === 'NotIn' ? 'NotIn' : 'In';
            case 'Select':
                return operator === 'NotEquals' ? 'NotEquals' : 'Equals';
            case 'Boolean':
                return 'Equals';
            case 'RelativeDate':
                return 'InLastDays';
            default:
                return null;
        }
    };

    // A short glyph that tells field types apart in the field list.
    designer.typeGlyph = function (dataType) {
        switch (dataType) {
            case 'Integer':
            case 'Decimal':
                return '#';
            case 'Boolean':
                return 'T/F';
            case 'Date':
            case 'DateTime':
                return 'Date';
            default:
                return 'Abc';
        }
    };

    // A new identifier with the prefix that is not in use yet.
    designer.newId = function (prefix, existing) {
        var used = {};

        (existing || []).forEach(function (id) {
            used[id] = true;
        });

        for (var index = 1; ; index++) {
            var candidate = prefix + index;

            if (!used[candidate]) {
                return candidate;
            }
        }
    };

    // An alias for a new data set: its name reduced to letters, digits, and underscores, made unique.
    designer.aliasFor = function (name, dataSets) {
        var base = String(name || 'Data').replace(/[^A-Za-z0-9_]/g, '');

        if (!/^[A-Za-z]/.test(base)) {
            base = 'D' + base;
        }

        base = base.slice(0, 40);

        var taken = {};

        (dataSets || []).forEach(function (dataSet) {
            taken[String(dataSet.alias).toLowerCase()] = true;
        });

        if (!taken[base.toLowerCase()]) {
            return base;
        }

        for (var index = 2; ; index++) {
            if (!taken[(base + index).toLowerCase()]) {
                return base + index;
            }
        }
    };

    designer.aliasOf = function (fieldKey) {
        var dot = String(fieldKey || '').indexOf('.');

        return dot > 0 ? fieldKey.slice(0, dot) : null;
    };

    // The aggregate a field gets when it is dropped on the columns: numbers are summed, everything else is a dimension.
    designer.defaultAggregate = function (field) {
        if (!field || field.isAggregate || field.key === designer.ROW_COUNT_FIELD) {
            return 'None';
        }

        return isNumeric(field.dataType) && !field.isIdentifier ? 'Sum' : 'None';
    };

    designer.isMeasure = function (column, field) {
        return !!column && ((column.aggregate && column.aggregate !== 'None') || !!(field && field.isAggregate));
    };

    function ids(list) {
        return (list || []).map(function (item) {
            return item.id;
        });
    }

    // Adds a column for a field to a shelf (Columns when none is given), at a position among that shelf's columns.
    designer.addColumn = function (query, field, index, shelf) {
        query.columns = query.columns || [];

        var column = {
            id: designer.newId('c', ids(query.columns)),
            field: field.key,
            label: null,
            aggregate: designer.defaultAggregate(field),
            transform: 'None',
            format: null,
            hidden: false,
            shelf: 'Columns'
        };

        query.columns.push(column);
        designer.placeColumn(query, column.id, shelf || 'Columns', index);

        return column;
    };

    // The shelf a column is on: Rows, or Columns for every other column.
    designer.shelfOf = function (column) {
        return column && column.shelf === 'Rows' ? 'Rows' : 'Columns';
    };

    // The columns placed on a shelf, in order.
    designer.shelfColumns = function (query, shelf) {
        return (query.columns || []).filter(function (column) {
            return designer.shelfOf(column) === shelf;
        });
    };

    // Moves a column onto a shelf, at a position among the columns already on it (after the last one when the position
    // is missing or past the end). The other columns keep their order. Returns the column, or null when there is none.
    designer.placeColumn = function (query, columnId, shelf, index) {
        var columns = query.columns || [];
        var column = columns.filter(function (candidate) {
            return candidate.id === columnId;
        })[0];

        if (!column) {
            return null;
        }

        var rest = columns.filter(function (candidate) {
            return candidate !== column;
        });
        var onShelf = rest.filter(function (candidate) {
            return designer.shelfOf(candidate) === shelf;
        });
        var position;

        if (typeof index === 'number' && index >= 0 && index < onShelf.length) {
            position = rest.indexOf(onShelf[index]);
        } else {
            position = onShelf.length ? rest.indexOf(onShelf[onShelf.length - 1]) + 1 : rest.length;
        }

        column.shelf = shelf === 'Rows' ? 'Rows' : 'Columns';
        rest.splice(position, 0, column);
        columns.length = 0;
        Array.prototype.push.apply(columns, rest);
        query.columns = columns;

        return column;
    };

    // Sorts the result by one column first, keeping the other sorts after it.
    designer.sortBy = function (query, columnId, descending) {
        query.sorts = (query.sorts || []).filter(function (sort) {
            return sort.columnId !== columnId;
        });
        query.sorts.unshift({ columnId: columnId, descending: !!descending });
    };

    // The operators a color rule can compare a column's values with.
    designer.colorOperatorsFor = function (dataType) {
        return designer.operatorsFor(dataType).filter(function (operator) {
            return ['In', 'NotIn', 'InLastDays', 'InNextDays'].indexOf(operator) < 0;
        });
    };

    // The colors offered next to a color box: strong colors for text and soft ones for cell backgrounds.
    designer.COLOR_SWATCHES = ['#DC3545', '#FD7E14', '#FFC107', '#198754', '#0D6EFD', '#6F42C1', '#6C757D',
        '#F8D7DA', '#FFE5D0', '#FFF3CD', '#D1E7DD', '#CFE2FF', '#E2D9F3', '#E9ECEF'];

    // The colors of a column, created empty when it has none.
    designer.ensureColors = function (column) {
        column.colors = column.colors || { headerColor: null, headerBackgroundColor: null, color: null, backgroundColor: null, scale: null, rules: [] };
        column.colors.rules = column.colors.rules || [];

        return column.colors;
    };

    // Drops a column's colors when they no longer color anything, so the design stays as it was before.
    designer.pruneColors = function (column) {
        var colors = column.colors;

        if (colors && !colors.headerColor && !colors.headerBackgroundColor && !colors.color && !colors.backgroundColor &&
            !colors.scale && !(colors.rules || []).length) {
            column.colors = null;
        }
    };

    // Adds the three bands of a traffic light to a column: red below the low value, orange up to the high value, green
    // from it. The values are left for the user to fill when they are not given.
    designer.addTrafficLightRules = function (column, low, high, target) {
        var colors = designer.ensureColors(column);
        var paint = target === 'Text' ? 'Text' : 'Background';
        var strong = paint === 'Text';
        var lowValue = low == null ? '' : String(low);
        var highValue = high == null ? '' : String(high);

        colors.rules.push(
            { operator: 'LessThan', values: [lowValue], color: strong ? '#DC3545' : '#F8D7DA', target: paint },
            { operator: 'Between', values: [lowValue, highValue], color: strong ? '#FD7E14' : '#FFE5D0', target: paint },
            { operator: 'GreaterThanOrEqual', values: [highValue], color: strong ? '#198754' : '#D1E7DD', target: paint });

        return colors.rules;
    };

    // The ready-made color scales: low, middle (or null), and high colors.
    designer.COLOR_SCALES = {
        RedToGreen: ['#F8D7DA', '#FFF3CD', '#D1E7DD'],
        GreenToRed: ['#D1E7DD', '#FFF3CD', '#F8D7DA'],
        WhiteToBlue: ['#FFFFFF', null, '#6EA8FE'],
        WhiteToRed: ['#FFFFFF', null, '#EA868F']
    };

    // The common formulas offered for a field, by its type, so the user picks one instead of typing it. Each has the
    // name of the function it needs, a label key, and the formula with the field in it. Aggregates come first, as in a
    // column's aggregate list; only the functions the catalog has are offered.
    designer.quickFormulas = function (field, functions) {
        if (!field || field.key === designer.ROW_COUNT_FIELD) {
            return [];
        }

        var key = '[' + field.key + ']';
        var available = {};
        var formulas = [];

        (functions || []).forEach(function (fn) {
            available[String(fn.name).toUpperCase()] = true;
        });

        // A field that is already aggregated, such as the number of rows, cannot be aggregated again.
        var add = function (fn, label, expression, aggregate) {
            if ((!functions || available[fn]) && !(aggregate && field.isAggregate)) {
                formulas.push({ fn: fn, label: label, expression: expression, isAggregate: !!aggregate });
            }
        };

        if (isNumeric(field.dataType)) {
            add('SUM', 'Sum', 'SUM(' + key + ')', true);
            add('AVG', 'Average', 'AVG(' + key + ')', true);
            add('MIN', 'Minimum', 'MIN(' + key + ')', true);
            add('MAX', 'Maximum', 'MAX(' + key + ')', true);
            add('MEDIAN', 'Median', 'MEDIAN(' + key + ')', true);
        } else if (isTemporal(field.dataType) || field.dataType === 'Text') {
            add('MIN', isTemporal(field.dataType) ? 'Earliest' : 'Minimum', 'MIN(' + key + ')', true);
            add('MAX', isTemporal(field.dataType) ? 'Latest' : 'Maximum', 'MAX(' + key + ')', true);
        }

        add('COUNT', 'Count', 'COUNT(' + key + ')', true);
        add('COUNTD', 'Count (distinct)', 'COUNTD(' + key + ')', true);

        if (field.dataType === 'Boolean') {
            add('SUM', 'Count of yes', 'SUM(IF(' + key + ', 1, 0))', true);
        }

        if (isNumeric(field.dataType)) {
            add('ROUND', 'Round to 2 decimals', 'ROUND(' + key + ', 2)');
            add('ABS', 'Absolute value', 'ABS(' + key + ')');
        } else if (isTemporal(field.dataType)) {
            add('YEAR', 'Year', 'YEAR(' + key + ')');
            add('MONTH', 'Month number', 'MONTH(' + key + ')');
            add('DATETRUNC', 'Start of the month', 'DATETRUNC(\'month\', ' + key + ')');
            add('DATEDIFF', 'Days until today', 'DATEDIFF(\'day\', ' + key + ', TODAY())');
        } else if (field.dataType === 'Text') {
            add('UPPER', 'Upper case', 'UPPER(' + key + ')');
            add('LOWER', 'Lower case', 'LOWER(' + key + ')');
            add('PROPER', 'Capitalize words', 'PROPER(' + key + ')');
            add('TRIM', 'Trim spaces', 'TRIM(' + key + ')');
            add('LEN', 'Number of characters', 'LEN(' + key + ')');
        }

        add('ISBLANK', 'Is empty', 'ISBLANK(' + key + ')');

        return formulas;
    };

    // Finds the column that shows a field in a role, adding one when there is none, so a field dropped on a visual's
    // well becomes a dimension (categories, series, pivot rows) or a measure (values). A measure of a field that cannot
    // be summed counts it instead.
    designer.ensureColumn = function (query, field, role) {
        query.columns = query.columns || [];

        var wantsMeasure = role === 'measure';
        var existing = query.columns.filter(function (column) {
            var isMeasure = designer.isMeasure(column, field);

            return column.field === field.key && isMeasure === wantsMeasure && (column.transform || 'None') === 'None';
        })[0];

        if (existing) {
            return existing;
        }

        var column = designer.addColumn(query, field);

        if (wantsMeasure) {
            if (!field.isAggregate && column.aggregate === 'None') {
                column.aggregate = isNumeric(field.dataType) && !field.isIdentifier ? 'Sum' : 'Count';
            }
        } else {
            column.aggregate = 'None';
        }

        return column;
    };

    // Joins two data sets on a pair of columns dropped on each other in the data model. The data set listed later is
    // the one attached by the join, so the pair is stored on its join, oriented from the earlier data set. An empty
    // pair left by the designer is filled first. Returns the alias of the join, or null when both fields belong to the
    // same data set.
    designer.connect = function (query, sourceKey, targetKey) {
        var sourceAlias = designer.aliasOf(sourceKey);
        var targetAlias = designer.aliasOf(targetKey);
        var aliases = (query.dataSets || []).map(function (dataSet) {
            return dataSet.alias;
        });
        var sourceIndex = aliases.indexOf(sourceAlias);
        var targetIndex = aliases.indexOf(targetAlias);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex === targetIndex) {
            return null;
        }

        var later = sourceIndex > targetIndex ? sourceAlias : targetAlias;
        var condition = sourceIndex < targetIndex
            ? { leftField: sourceKey, rightField: targetKey }
            : { leftField: targetKey, rightField: sourceKey };

        query.joins = query.joins || [];

        var join = query.joins.filter(function (candidate) {
            return candidate.alias === later;
        })[0];

        if (!join) {
            join = { alias: later, type: 'Inner', conditions: [] };
            query.joins.push(join);
        }

        join.conditions = join.conditions || [];

        var duplicate = join.conditions.some(function (existing) {
            return existing.leftField === condition.leftField && existing.rightField === condition.rightField;
        });

        if (!duplicate) {
            var empty = join.conditions.filter(function (existing) {
                return !existing.leftField || !existing.rightField;
            })[0];

            if (empty) {
                empty.leftField = condition.leftField;
                empty.rightField = condition.rightField;
            } else {
                join.conditions.push(condition);
            }
        }

        return later;
    };

    // The recent periods a relative date filter offers, in days; an empty value keeps every row.
    designer.RELATIVE_PERIODS = ['', '1', '7', '30', '90', '365'];

    // The filter a new report starts with on its first data set's main date: the last 30 days, which the people who run
    // the report can change. Reading only that period keeps a report over a large data set fast and complete. Returns
    // null when the data set has no main date, the field is not there, or the report already filters.
    designer.defaultDateFilter = function (query, dataSet, fields) {
        var name = dataSet && dataSet.defaultDateField;
        var field = (fields || []).filter(function (candidate) {
            return candidate.name === name;
        })[0];

        if (!name || !field || (query.filters || []).length > 0 || !isTemporal(field.dataType)) {
            return null;
        }

        return {
            id: designer.newId('f', ids(query.filters || [])),
            field: dataSet.alias + '.' + field.name,
            stage: 'Rows',
            operator: 'InLastDays',
            values: ['30'],
            exposed: true,
            label: field.displayName || field.name,
            control: 'RelativeDate'
        };
    };

    designer.addFilter = function (query, field) {
        query.filters = query.filters || [];

        var filter = {
            id: designer.newId('f', ids(query.filters)),
            field: field.key,
            stage: field.isAggregate ? 'Result' : 'Rows',
            operator: field.dataType === 'Text' ? 'Contains' : 'Equals',
            values: [],
            exposed: false,
            label: field.label || null,
            control: 'Auto'
        };

        query.filters.push(filter);

        return filter;
    };

    // The field a filter had before it was switched to the result, so switching back to the rows restores it.
    var rowFields = typeof WeakMap === 'function' ? new WeakMap() : null;

    // Switches a filter between the rows (a field) and the result (a column). A result filter starts on the first
    // column; switching back to the rows restores the field the filter had, since a column id is not a field.
    designer.setFilterStage = function (filter, stage, columns) {
        if (!filter || filter.stage === stage) {
            return filter;
        }

        if (stage === 'Result') {
            if (rowFields && filter.field) {
                rowFields.set(filter, filter.field);
            }

            filter.field = ((columns || [])[0] || {}).id || null;
        } else {
            filter.field = (rowFields && rowFields.get(filter)) || null;
        }

        filter.stage = stage;
        filter.values = [];

        return filter;
    };

    // Moves an item within a list, returning the list.
    designer.move = function (list, from, to) {
        if (!list || from < 0 || from >= list.length) {
            return list;
        }

        var target = Math.max(0, Math.min(to, list.length - 1));
        var item = list.splice(from, 1)[0];

        list.splice(target, 0, item);

        return list;
    };

    // The visuals that read the query being edited: design.activeQueryId names it, and is empty for the main query.
    designer.visualsOfQuery = function (design, queryId) {
        var id = queryId == null ? (design.activeQueryId || '') : queryId;

        return (design.visuals || []).filter(function (visual) {
            return (visual.queryId || '') === id;
        });
    };

    // Adds a query that starts as a copy of another, so it reads the same data sets and keeps the same viewer filters,
    // which then share one control on the report. Returns the new entry.
    designer.addQuery = function (design, source, name) {
        design.queries = design.queries || [];

        var entry = {
            id: designer.newId('q', ids(design.queries)),
            name: name || null,
            query: JSON.parse(JSON.stringify(source || {}))
        };

        design.queries.push(entry);

        return entry;
    };

    // Removes a query and the visuals that read it, with every reference to those visuals.
    designer.removeQuery = function (design, queryId) {
        var removed = {};

        design.queries = (design.queries || []).filter(function (entry) {
            return entry.id !== queryId;
        });
        design.visuals = (design.visuals || []).filter(function (visual) {
            if (visual.queryId === queryId) {
                removed[visual.id] = true;

                return false;
            }

            return true;
        });

        designer.forgetVisuals(design, removed);
    };

    // The transforms whose values still filter their field: text in another case, and dates kept to a period.
    designer.SELECTABLE_TRANSFORMS = ['None', 'Upper', 'Lower', 'Day', 'Week', 'Month', 'Year', 'Hour'];

    // The columns clicking a visual can pick a value of: the dimensions a table shows, or a chart's categories and
    // series. `columns` are the query's columns as described ({ id, isMeasure }) and `definitions` the design's columns.
    designer.clickableColumns = function (visual, columns, definitions) {
        var transforms = {};

        (definitions || []).forEach(function (column) {
            transforms[column.id] = column.transform || 'None';
        });

        return (columns || []).filter(function (column) {
            if (column.isMeasure || designer.SELECTABLE_TRANSFORMS.indexOf(transforms[column.id] || 'None') < 0) {
                return false;
            }

            if (visual.type === 'Chart') {
                return column.id === visual.categoryColumnId || column.id === visual.seriesColumnId;
            }

            if (visual.type === 'Table') {
                return (visual.columnIds || []).length ? visual.columnIds.indexOf(column.id) >= 0 : !column.hidden;
            }

            return false;
        });
    };

    // Sets the column clicking a visual picks; clicking it does nothing once the column is cleared.
    designer.setActionColumn = function (visual, columnId) {
        if (!columnId) {
            visual.action = null;

            return visual;
        }

        visual.action = visual.action || { columnId: null, targets: [] };
        visual.action.columnId = columnId;
        visual.action.targets = visual.action.targets || [];

        return visual;
    };

    // Adds or removes a visual from those a click filters.
    designer.toggleActionTarget = function (visual, targetId, on) {
        if (!visual.action) {
            return visual;
        }

        visual.action.targets = (visual.action.targets || []).filter(function (target) {
            return target.visualId !== targetId;
        });

        if (on && targetId !== visual.id) {
            visual.action.targets.push({ visualId: targetId, field: null });
        }

        return visual;
    };

    // Drops the click actions' references to visuals that no longer exist, by identifier.
    designer.forgetVisuals = function (design, removed) {
        (design.visuals || []).forEach(function (visual) {
            if (visual.action && visual.action.targets) {
                visual.action.targets = visual.action.targets.filter(function (target) {
                    return !removed[target.visualId];
                });
            }
        });
    };

    // Removes a column and every sort, result filter, and visual reference to it.
    designer.removeColumn = function (design, columnId) {
        var query = design.query;

        query.columns = (query.columns || []).filter(function (column) {
            return column.id !== columnId;
        });
        query.sorts = (query.sorts || []).filter(function (sort) {
            return sort.columnId !== columnId;
        });
        query.filters = (query.filters || []).filter(function (filter) {
            return !(filter.stage === 'Result' && filter.field === columnId);
        });

        designer.visualsOfQuery(design).forEach(function (visual) {
            visual.columnIds = (visual.columnIds || []).filter(function (id) {
                return id !== columnId;
            });
            visual.valueColumnIds = (visual.valueColumnIds || []).filter(function (id) {
                return id !== columnId;
            });

            if (visual.categoryColumnId === columnId) {
                visual.categoryColumnId = null;
            }

            if (visual.seriesColumnId === columnId) {
                visual.seriesColumnId = null;
            }

            if (visual.action && visual.action.columnId === columnId) {
                visual.action = null;
            }
        });
    };

    // The heights a container may have, in pixels; 0 grows with its visuals.
    designer.CONTAINER_HEIGHTS = [0, 240, 320, 400, 480, 600, 800];

    designer.addContainer = function (design) {
        design.containers = design.containers || [];

        var container = {
            id: designer.newId('k', ids(design.containers)),
            title: null,
            width: 12,
            height: 400
        };

        design.containers.push(container);

        return container;
    };

    // Removes a container; its visuals are shown on their own again.
    designer.removeContainer = function (design, containerId) {
        design.containers = (design.containers || []).filter(function (container) {
            return container.id !== containerId;
        });

        (design.visuals || []).forEach(function (visual) {
            if (visual.containerId === containerId) {
                visual.containerId = null;
            }
        });
    };

    // Removes a data set with its join and every column and row filter that reads it. Calculated fields that read it
    // are kept so their formulas are not lost; the designer reports them as invalid.
    // The join that attaches a data set, or undefined. It never creates one, so rendering can use it freely.
    designer.findJoin = function (query, alias) {
        return ((query && query.joins) || []).filter(function (candidate) {
            return candidate && candidate.alias === alias;
        })[0];
    };

    // Merges the joins that attach the same data set into one: the server runs a design only with exactly one join per
    // data set. The first join keeps its type; its column pairs are combined with the others' once each, and pairs with
    // a missing column are dropped when a complete pair exists.
    designer.mergeJoins = function (query) {
        var merged = [];
        var byAlias = {};

        ((query && query.joins) || []).forEach(function (join) {
            if (!join) {
                return;
            }

            var first = byAlias[join.alias];

            if (!first) {
                byAlias[join.alias] = join;
                join.conditions = join.conditions || [];
                merged.push(join);

                return;
            }

            (join.conditions || []).forEach(function (condition) {
                first.conditions.push(condition);
            });
        });

        merged.forEach(function (join) {
            var complete = [];

            join.conditions.forEach(function (condition) {
                var duplicate = complete.some(function (existing) {
                    return existing.leftField === condition.leftField && existing.rightField === condition.rightField;
                });

                if (condition && condition.leftField && condition.rightField && !duplicate) {
                    complete.push(condition);
                }
            });

            if (complete.length) {
                join.conditions = complete;
            }
        });

        if (query) {
            query.joins = merged;
        }

        return merged;
    };

    designer.removeDataSet = function (design, alias) {
        var query = design.query;
        var prefix = alias + '.';
        var reads = function (key) {
            return String(key || '').indexOf(prefix) === 0;
        };

        query.dataSets = (query.dataSets || []).filter(function (dataSet) {
            return dataSet.alias !== alias;
        });
        query.joins = (query.joins || []).filter(function (join) {
            return join.alias !== alias;
        });
        query.joins.forEach(function (join) {
            join.conditions = (join.conditions || []).filter(function (condition) {
                return !reads(condition.leftField) && !reads(condition.rightField);
            });
        });
        query.filters = (query.filters || []).filter(function (filter) {
            return !(filter.stage !== 'Result' && reads(filter.field));
        });

        (query.columns || []).filter(function (column) {
            return reads(column.field);
        }).forEach(function (column) {
            designer.removeColumn(design, column.id);
        });

        // The first remaining data set is the base now, so it must not keep a join.
        if (query.dataSets.length > 0) {
            var base = query.dataSets[0].alias;

            query.joins = query.joins.filter(function (join) {
                return join.alias !== base;
            });
        }
    };

    function words(text) {
        return String(text || '').toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim().split(' ').filter(Boolean);
    }

    function score(field, otherDataSet) {
        var name = words(field.name + ' ' + (field.displayName || ''));
        var other = words(otherDataSet.dataSet + ' ' + (otherDataSet.displayName || '') + ' ' + otherDataSet.alias);
        var points = field.isIdentifier ? 1 : 0;

        other.forEach(function (word) {
            if (word.length > 1 && name.indexOf(word) >= 0) {
                points += 3;
            }
        });

        return points;
    }

    // Suggests the fields to match when a data set is joined to the ones before it: an identifier of one side whose
    // name mentions the other side (an order's "Customer" picker and the customer's id), else the two identifiers.
    // `earlier` is a list of { dataSet, fields } and `added` is { dataSet, fields }, where dataSet is a reference.
    // Finds a pair of fields one data set declares as referencing the other, such as an order's customer picker that
    // references the customer type, or a contained item that references its list.
    designer.referencedJoin = function (earlier, added) {
        var refersTo = function (field, dataSet) {
            return (field.references || []).filter(function (reference) {
                return reference.source === dataSet.source && reference.dataSet === dataSet.dataSet;
            })[0];
        };

        for (var index = 0; index < (earlier || []).length; index++) {
            var left = earlier[index];
            var found = null;

            (added.fields || []).some(function (field) {
                var reference = refersTo(field, left.dataSet);

                if (reference) {
                    found = { leftField: left.dataSet.alias + '.' + reference.field, rightField: added.dataSet.alias + '.' + field.name };
                }

                return !!found;
            });

            if (!found) {
                (left.fields || []).some(function (field) {
                    var reference = refersTo(field, added.dataSet);

                    if (reference) {
                        found = { leftField: left.dataSet.alias + '.' + field.name, rightField: added.dataSet.alias + '.' + reference.field };
                    }

                    return !!found;
                });
            }

            if (found) {
                return found;
            }
        }

        return null;
    };

    // Whether two data sets are related: either one's references point at the other.
    designer.isRelated = function (left, right) {
        var refers = function (from, to) {
            return (from.references || []).some(function (reference) {
                return reference.source === to.source && reference.dataSet === to.dataSet;
            });
        };

        return refers(left, right) || refers(right, left);
    };

    designer.suggestJoin = function (earlier, added) {
        var referenced = designer.referencedJoin(earlier, added);

        if (referenced) {
            return referenced;
        }

        var best = null;

        (earlier || []).forEach(function (left) {
            var leftIds = (left.fields || []).filter(function (field) {
                return field.isIdentifier;
            });
            var addedIds = (added.fields || []).filter(function (field) {
                return field.isIdentifier;
            });

            leftIds.forEach(function (leftField) {
                addedIds.forEach(function (rightField) {
                    var points = score(rightField, left.dataSet) + score(leftField, added.dataSet);

                    if (leftField.name === rightField.name) {
                        points -= 1;
                    }

                    if (!best || points > best.points) {
                        best = {
                            points: points,
                            leftField: left.dataSet.alias + '.' + leftField.name,
                            rightField: added.dataSet.alias + '.' + rightField.name
                        };
                    }
                });
            });
        });

        return best ? { leftField: best.leftField, rightField: best.rightField } : null;
    };

    // Turns the entries of a submitted filter form into the values the server expects, by filter identifier. Range
    // controls post f.{id}.from and f.{id}.to; other controls post f.{id}, possibly several times.
    designer.filterValuesFromEntries = function (entries) {
        var values = {};
        var ranges = {};

        (entries || []).forEach(function (entry) {
            var name = entry[0];
            var value = String(entry[1] == null ? '' : entry[1]).trim();
            var match = /^f\.(.+?)(?:\.(from|to))?$/.exec(name);

            if (!match) {
                return;
            }

            var id = match[1];

            if (match[2]) {
                ranges[id] = ranges[id] || { from: '', to: '' };
                ranges[id][match[2]] = value;

                return;
            }

            values[id] = values[id] || [];

            if (value) {
                values[id].push(value);
            }
        });

        Object.keys(ranges).forEach(function (id) {
            var range = ranges[id];
            var to = /T23:59$/.test(range.to) ? range.to + ':59' : range.to;

            values[id] = range.from || to ? [range.from, to] : [];
        });

        return values;
    };

    // A readable label for a column: its own label, else the field label with the aggregate.
    designer.columnLabel = function (column, field, labels) {
        if (column.label) {
            return column.label;
        }

        var base = field ? field.label : column.field;
        var aggregate = column.aggregate && column.aggregate !== 'None' ? ((labels && labels[column.aggregate]) || column.aggregate) + ' ' : '';

        return aggregate + base;
    };
})(typeof window !== 'undefined' ? window : globalThis);
