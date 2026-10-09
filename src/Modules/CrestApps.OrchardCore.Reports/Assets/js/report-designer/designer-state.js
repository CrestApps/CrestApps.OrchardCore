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
            return ['Auto', 'DateRange', 'Text'];
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

    designer.addColumn = function (query, field, index) {
        query.columns = query.columns || [];

        var column = {
            id: designer.newId('c', ids(query.columns)),
            field: field.key,
            label: null,
            aggregate: designer.defaultAggregate(field),
            transform: 'None',
            format: null,
            hidden: false
        };
        var position = typeof index === 'number' && index >= 0 && index <= query.columns.length ? index : query.columns.length;

        query.columns.splice(position, 0, column);

        return column;
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

        (design.visuals || []).forEach(function (visual) {
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
