/*
 * The canvas of the report builder: the Columns and Filters shelves that fields are dropped on, the sort and row
 * limit, the properties of the selected column or filter, and the visuals of the report.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner;
    var ui = designer.ui;
    var h = ui.h;
    var app = designer.app;

    var PILL_MIME = 'application/x-report-pill';

    function dropIndex(zone, event) {
        var pills = Array.prototype.slice.call(zone.querySelectorAll('[data-pill-index]'));

        for (var index = 0; index < pills.length; index++) {
            var box = pills[index].getBoundingClientRect();

            if (event.clientY < box.top || (event.clientY <= box.bottom && event.clientX < box.left + box.width / 2)) {
                return index;
            }
        }

        return pills.length;
    }

    function shelf(kind, title, hint, items, renderPill) {
        var zone = h('div', {
            className: 'report-designer-shelf-zone d-flex flex-wrap gap-1 align-items-center',
            dataset: { shelf: kind },
            ondragover: function (event) {
                var types = Array.prototype.slice.call(event.dataTransfer.types || []);

                if (types.indexOf(designer.FIELD_MIME) >= 0 || types.indexOf(PILL_MIME) >= 0) {
                    event.preventDefault();
                    zone.classList.add('is-over');
                }
            },
            ondragleave: function () {
                zone.classList.remove('is-over');
            },
            ondrop: function (event) {
                event.preventDefault();
                zone.classList.remove('is-over');

                var fieldKey = event.dataTransfer.getData(designer.FIELD_MIME);
                var pill = event.dataTransfer.getData(PILL_MIME);
                var index = dropIndex(zone, event);
                var fields = app.fields();

                if (fieldKey && fields[fieldKey]) {
                    if (kind === 'columns') {
                        app.selection = { kind: 'column', id: designer.addColumn(app.design.query, fields[fieldKey], index).id };
                    } else {
                        app.selection = { kind: 'filter', id: designer.addFilter(app.design.query, fields[fieldKey]).id };
                    }

                    app.changed();

                    return;
                }

                if (pill) {
                    var moved = JSON.parse(pill);
                    var list = kind === 'columns' ? app.design.query.columns : app.design.query.filters;

                    if (moved.shelf === kind) {
                        designer.move(list, moved.index, moved.index < index ? index - 1 : index);
                        app.changed();
                    } else if (kind === 'filters' && moved.shelf === 'columns') {
                        var column = app.design.query.columns[moved.index];
                        var field = column && fields[column.field];

                        if (field) {
                            app.selection = { kind: 'filter', id: designer.addFilter(app.design.query, field).id };
                            app.changed();
                        }
                    }
                }
            }
        });

        items.forEach(function (item, index) {
            var pill = renderPill(item, index);

            pill.dataset.pillIndex = String(index);
            pill.setAttribute('draggable', 'true');
            pill.addEventListener('dragstart', function (event) {
                event.dataTransfer.setData(PILL_MIME, JSON.stringify({ shelf: kind, index: index }));
                event.dataTransfer.effectAllowed = 'move';
            });
            zone.appendChild(pill);
        });

        if (!items.length) {
            zone.appendChild(h('span', { className: 'text-muted small' }, hint));
        }

        return h('div', { className: 'report-designer-shelf mb-2' },
            h('div', { className: 'report-designer-shelf-label small fw-semibold text-muted text-uppercase' }, title),
            zone);
    }

    function pill(label, badge, selected, isMeasure, onSelect, onRemove) {
        return h('span', {
            className: 'report-designer-pill btn-group btn-group-sm' + (selected ? ' is-selected' : '') + (isMeasure ? ' is-measure' : '')
        },
        h('button', { type: 'button', className: 'btn ' + (isMeasure ? 'btn-success' : 'btn-primary'), onclick: onSelect },
            badge ? h('span', { className: 'badge text-bg-light me-1' }, badge) : null, label),
        h('button', { type: 'button', className: 'btn ' + (isMeasure ? 'btn-success' : 'btn-primary'), 'aria-label': app.t('Remove') + ': ' + label, onclick: onRemove }, ui.icon('fa-xmark')));
    }

    app.renderShelves = function () {
        var target = app.elements.shelves;
        var query = app.design.query;
        var fields = app.fields();
        var columns = app.columns();
        var selection = app.selection || {};

        ui.clear(target);

        if (query.dataSets.length > 1) {
            target.appendChild(h('div', { className: 'report-designer-shelf mb-2' },
                h('div', { className: 'report-designer-shelf-label small fw-semibold text-muted text-uppercase' }, app.t('Joins')),
                h('div', { className: 'report-designer-shelf-zone rd-joins-zone d-flex flex-wrap gap-1 align-items-center' }, query.dataSets.slice(1).map(function (dataSet) {
                    var summary = app.joinSummary(dataSet.alias);
                    var join = designer.findJoin(query, dataSet.alias) || { type: 'Inner' };
                    var selected = selection.kind === 'join' && selection.id === dataSet.alias;

                    return h('button', {
                        type: 'button',
                        className: 'btn btn-sm rd-join-pill ' + (summary.complete ? 'btn-outline-primary' : 'btn-outline-danger') + (selected ? ' active' : ''),
                        title: summary.complete ? summary.text : app.t('Add the columns that must match.'),
                        onclick: function () {
                            app.editJoin(dataSet.alias);
                        }
                    }, ui.icon(summary.complete ? 'fa-link' : 'fa-link-slash'), ' ',
                    h('span', { className: 'fw-semibold' }, app.dataSetLabel(dataSet.alias)), ' ',
                    h('span', { className: 'badge text-bg-light' }, app.config.joinLabels[join.type] || join.type),
                    summary.complete ? h('span', { className: 'small ms-1 rd-join-summary' }, summary.text) : h('span', { className: 'small ms-1' }, app.t('Not joined yet')));
                }))));
        }

        target.appendChild(shelf('columns', app.t('Columns'), app.t('Drag fields here. Numbers are summed; add a dimension to group them.'), query.columns, function (column, index) {
            var field = fields[column.field];
            var aggregate = column.aggregate && column.aggregate !== 'None' ? app.config.aggregateLabels[column.aggregate] || column.aggregate : null;

            return pill(columns[index].label, aggregate, selection.kind === 'column' && selection.id === column.id, columns[index].isMeasure, function () {
                app.select('column', column.id);
            }, function () {
                designer.removeColumn(app.design, column.id);
                app.selection = null;
                app.changed();
            }, field);
        }));

        target.appendChild(shelf('filters', app.t('Filters'), app.t('Drag fields here to filter the data.'), query.filters, function (filter) {
            var label = filter.label || (filter.stage === 'Result'
                ? (columns.filter(function (column) {
                    return column.id === filter.field;
                })[0] || {}).label
                : (fields[filter.field] || {}).label) || filter.field;

            return pill(label, filter.exposed ? app.t('Viewer') : null, selection.kind === 'filter' && selection.id === filter.id, false, function () {
                app.select('filter', filter.id);
            }, function () {
                query.filters = query.filters.filter(function (other) {
                    return other.id !== filter.id;
                });
                app.selection = null;
                app.changed();
            });
        }));

        var sorts = h('div', { className: 'd-flex flex-wrap gap-1 align-items-center' });

        query.sorts.forEach(function (sort, index) {
            var column = columns.filter(function (candidate) {
                return candidate.id === sort.columnId;
            })[0];

            sorts.appendChild(h('span', { className: 'btn-group btn-group-sm' },
                h('button', { type: 'button', className: 'btn btn-outline-secondary', title: app.t('Change direction'), onclick: function () {
                    sort.descending = !sort.descending;
                    app.changed();
                } }, ui.icon(sort.descending ? 'fa-arrow-down-wide-short' : 'fa-arrow-up-short-wide'), ' ', column ? column.label : sort.columnId),
                h('button', { type: 'button', className: 'btn btn-outline-secondary', 'aria-label': app.t('Remove'), onclick: function () {
                    query.sorts.splice(index, 1);
                    app.changed();
                } }, ui.icon('fa-xmark'))));
        });

        var unsorted = columns.filter(function (column) {
            return !query.sorts.some(function (sort) {
                return sort.columnId === column.id;
            });
        });

        if (unsorted.length) {
            sorts.appendChild(ui.select([{ value: '', text: app.t('Add sort…') }].concat(unsorted.map(function (column) {
                return { value: column.id, text: column.label };
            })), '', {
                className: 'form-select form-select-sm w-auto',
                'aria-label': app.t('Add sort'),
                onchange: function (event) {
                    if (event.target.value) {
                        query.sorts.push({ columnId: event.target.value, descending: false });
                        app.changed();
                    }
                }
            }));
        }

        target.appendChild(h('div', { className: 'd-flex flex-wrap gap-3 align-items-center mt-2' },
            h('div', { className: 'd-flex gap-2 align-items-center' }, h('span', { className: 'small fw-semibold text-muted text-uppercase' }, app.t('Sort')), sorts),
            h('div', { className: 'd-flex gap-2 align-items-center' },
                h('label', { className: 'small fw-semibold text-muted text-uppercase', for: 'report-designer-limit' }, app.t('Top rows')),
                h('input', {
                    id: 'report-designer-limit',
                    type: 'number',
                    min: '1',
                    className: 'form-control form-control-sm',
                    style: { width: '6rem' },
                    value: query.limit == null ? '' : String(query.limit),
                    placeholder: app.t('All'),
                    onchange: function (event) {
                        var value = parseInt(event.target.value, 10);

                        query.limit = value > 0 ? value : null;
                        app.changed({ render: false });
                    }
                }))));
    };

    function labelled(text, control, hint) {
        return h('div', { className: 'mb-2' },
            h('label', { className: 'form-label small mb-1' }, text),
            control,
            hint ? h('div', { className: 'form-text' }, hint) : null);
    }

    function check(text, value, onChange, hint) {
        var id = 'rd-' + Math.random().toString(36).slice(2, 9);

        return h('div', { className: 'form-check mb-2' },
            h('input', { type: 'checkbox', className: 'form-check-input', id: id, checked: !!value, onchange: function (event) {
                onChange(event.target.checked);
            } }),
            h('label', { className: 'form-check-label', for: id }, text),
            hint ? h('span', { className: 'hint dashed' }, hint) : null);
    }

    function textInput(value, onChange, attrs) {
        return h('input', Object.assign({
            type: 'text',
            className: 'form-control form-control-sm',
            value: value || '',
            onchange: function (event) {
                onChange(event.target.value.trim() || null);
            }
        }, attrs || {}));
    }

    function columnProperties(column) {
        var field = app.fields()[column.field] || { dataType: 'Text', label: column.field };
        var labels = app.config.aggregateLabels;
        var transforms = app.config.transformLabels;

        return [
            h('div', { className: 'small text-muted mb-2' }, app.t('Field') + ': ', h('code', null, column.field)),
            labelled(app.t('Header'), textInput(column.label, function (value) {
                column.label = value;
                app.changed();
            }, { placeholder: designer.columnLabel(column, field, labels), maxlength: '200' })),
            field.isAggregate ? null : labelled(app.t('Aggregate'), ui.select(designer.aggregatesFor(field.dataType).map(function (value) {
                return { value: value, text: labels[value] || value };
            }), column.aggregate || 'None', {
                onchange: function (event) {
                    column.aggregate = event.target.value;
                    app.changed();
                }
            }), app.t('Pick None to group the result by this column.')),
            labelled(app.t('Transform'), ui.select(designer.transformsFor(field.dataType).map(function (value) {
                return { value: value, text: transforms[value] || value };
            }), column.transform || 'None', {
                onchange: function (event) {
                    column.transform = event.target.value;
                    app.changed();
                }
            })),
            labelled(app.t('Format'), textInput(column.format, function (value) {
                column.format = value;
                app.changed({ render: false });
            }, { placeholder: 'N2, C2, P1, yyyy-MM-dd', list: 'report-designer-formats' }), app.t('A .NET format, such as N0 for whole numbers, C2 for currency, or MMM yyyy for months.')),
            check(app.t('Hide from tables'), column.hidden, function (value) {
                column.hidden = value;
                app.changed({ render: false });
            }, app.t('A hidden column still groups the data and can feed charts.'))
        ];
    }

    app.periodLabel = function (days) {
        switch (days) {
            case '':
                return app.t('All time');
            case '1':
                return app.t('Today');
            case '365':
                return app.t('Last 12 months');
            default:
                return app.t('Last') + ' ' + days + ' ' + app.t('days');
        }
    };

    function valueEditor(filter, dataType) {
        var arity = designer.valueArity(filter.operator);
        var inputType = designer.isTemporal(dataType) ? (dataType === 'DateTime' ? 'datetime-local' : 'date') : (designer.isNumeric(dataType) ? 'number' : 'text');

        if (filter.operator === 'InLastDays' || filter.operator === 'InNextDays') {
            inputType = 'number';
        }

        if (arity === 0) {
            return null;
        }

        // A recent period: its value is the default the report opens with, which viewers can change.
        if (filter.control === 'RelativeDate') {
            return labelled(app.t('Default period'), ui.select(designer.RELATIVE_PERIODS.map(function (days) {
                return { value: days, text: app.periodLabel(days) };
            }), (filter.values || [])[0] || '', {
                onchange: function (event) {
                    filter.values = event.target.value ? [event.target.value] : [];
                    app.changed();
                }
            }), app.t('The people who run the report can pick another period.'));
        }

        if (dataType === 'Boolean') {
            return labelled(app.t('Value'), ui.select([
                { value: '', text: app.t('Any') },
                { value: 'true', text: app.t('Yes') },
                { value: 'false', text: app.t('No') }
            ], filter.values[0] || '', {
                onchange: function (event) {
                    filter.values = event.target.value ? [event.target.value] : [];
                    app.changed({ render: false });
                }
            }));
        }

        if (arity === 'many') {
            return labelled(app.t('Values'), h('textarea', {
                className: 'form-control form-control-sm',
                rows: '3',
                onchange: function (event) {
                    filter.values = event.target.value.split('\n').map(function (value) {
                        return value.trim();
                    }).filter(Boolean);
                    app.changed({ render: false });
                }
            }, filter.values.join('\n')), app.t('One value per line.'));
        }

        var input = function (index, placeholder) {
            return h('input', {
                type: inputType,
                className: 'form-control form-control-sm',
                value: filter.values[index] || '',
                placeholder: placeholder,
                onchange: function (event) {
                    var values = filter.values.slice();

                    while (values.length <= index) {
                        values.push('');
                    }

                    values[index] = event.target.value;
                    filter.values = values;
                    app.changed({ render: false });
                }
            });
        };

        if (arity === 2) {
            return labelled(app.t('Between'), h('div', { className: 'd-flex gap-1' }, input(0, app.t('From')), input(1, app.t('To'))), app.t('Leave a side empty to leave it open.'));
        }

        return labelled(filter.operator === 'InLastDays' || filter.operator === 'InNextDays' ? app.t('Days') : app.t('Value'), input(0));
    }

    function filterProperties(filter) {
        var fields = app.fields();
        var columns = app.columns();
        var dataType = filter.stage === 'Result'
            ? ((columns.filter(function (column) {
                return column.id === filter.field;
            })[0] || {}).dataType || 'Text')
            : ((fields[filter.field] || {}).dataType || 'Text');
        var operators = app.config.operatorLabels;

        return [
            labelled(app.t('Applies to'), ui.select([
                { value: 'Rows', text: app.t('Rows, before grouping') },
                { value: 'Result', text: app.t('Result, after grouping') }
            ], filter.stage || 'Rows', {
                onchange: function (event) {
                    filter.stage = event.target.value;
                    filter.field = filter.stage === 'Result' ? (columns[0] || {}).id : null;
                    filter.values = [];
                    app.changed();
                }
            })),
            labelled(filter.stage === 'Result' ? app.t('Column') : app.t('Field'), filter.stage === 'Result'
                ? ui.select(columns.map(function (column) {
                    return { value: column.id, text: column.label };
                }), filter.field, {
                    onchange: function (event) {
                        filter.field = event.target.value;
                        app.changed();
                    }
                })
                : h('code', { className: 'd-block small' }, filter.field)),
            labelled(app.t('Condition'), ui.select(designer.operatorsFor(dataType).map(function (value) {
                return { value: value, text: operators[value] || value };
            }), filter.operator, {
                onchange: function (event) {
                    filter.operator = event.target.value;
                    filter.control = 'Auto';
                    app.changed();
                }
            })),
            valueEditor(filter, dataType),
            check(app.t('Let viewers change this filter'), filter.exposed, function (value) {
                filter.exposed = value;
                app.changed();
            }, app.t('Shows the filter above the report. The values above become its defaults.')),
            filter.exposed ? labelled(app.t('Filter label'), textInput(filter.label, function (value) {
                filter.label = value;
                app.changed();
            }, { maxlength: '200' })) : null,
            filter.exposed ? labelled(app.t('Control'), ui.select(designer.controlsFor(dataType).map(function (value) {
                return { value: value, text: app.config.controlLabels[value] || value };
            }), filter.control || 'Auto', {
                onchange: function (event) {
                    filter.control = event.target.value;
                    filter.operator = designer.operatorForControl(filter.control, filter.operator) || filter.operator;
                    app.changed();
                }
            })) : null
        ];
    }

    app.renderProperties = function () {
        var target = app.elements.properties;
        var selection = app.selection;
        var query = app.design.query;
        var body = null;
        var title = app.t('Properties');

        ui.clear(target);

        if (selection && selection.kind === 'join') {
            title = app.t('Join');
            body = app.joinEditor(selection.id);
        } else if (selection && selection.kind === 'column') {
            var column = query.columns.filter(function (candidate) {
                return candidate.id === selection.id;
            })[0];

            if (column) {
                title = app.t('Column');
                body = columnProperties(column);
            }
        } else if (selection && selection.kind === 'filter') {
            var filter = query.filters.filter(function (candidate) {
                return candidate.id === selection.id;
            })[0];

            if (filter) {
                title = app.t('Filter');
                body = filterProperties(filter);
            }
        }

        target.appendChild(h('div', { className: 'card mb-3' },
            h('div', { className: 'card-header py-1 fw-semibold' }, title),
            h('div', { className: 'card-body p-2' }, body || h('p', { className: 'text-muted small mb-0' }, app.t('Select a join, column, or filter to change it.')))));
    };

    function checkboxList(options, selected, onChange) {
        return h('div', { className: 'report-designer-checklist' }, options.map(function (option) {
            return check(option.text, selected.indexOf(option.value) >= 0, function (value) {
                var next = selected.filter(function (id) {
                    return id !== option.value;
                });

                if (value) {
                    next.push(option.value);
                }

                onChange(next);
            });
        }));
    }

    // A well a field can be dropped on to put it in a visual, like a chart's categories or values.
    function well(label, role, control, onField) {
        var zone = h('div', {
            className: 'rd-well',
            ondragover: function (event) {
                if (Array.prototype.indexOf.call(event.dataTransfer.types || [], designer.FIELD_MIME) >= 0) {
                    event.preventDefault();
                    zone.classList.add('is-over');
                }
            },
            ondragleave: function () {
                zone.classList.remove('is-over');
            },
            ondrop: function (event) {
                event.preventDefault();
                zone.classList.remove('is-over');

                var field = app.fields()[event.dataTransfer.getData(designer.FIELD_MIME)];

                if (field) {
                    onField(designer.ensureColumn(app.design.query, field, role));
                    app.changed();
                }
            }
        }, control, h('div', { className: 'rd-well-hint' }, ui.icon('fa-arrow-down'), ' ', app.t('Drop a field here')));

        return labelled(label, zone);
    }

    function visualProperties(visual) {
        var columns = app.columns();
        var dimensions = columns.filter(function (column) {
            return !column.isMeasure;
        }).map(function (column) {
            return { value: column.id, text: column.label };
        });
        var numbers = columns.filter(function (column) {
            return designer.isNumeric(column.dataType);
        }).map(function (column) {
            return { value: column.id, text: column.label };
        });
        var all = columns.map(function (column) {
            return { value: column.id, text: column.label };
        });
        var none = [{ value: '', text: app.t('None') }];
        var set = function (name, render) {
            return function (value) {
                visual[name] = value;
                app.changed({ render: render !== false });
            };
        };
        var parts = [
            labelled(app.t('Title'), textInput(visual.title, set('title', false), { maxlength: '200' })),
            labelled(app.t('Width'), ui.select([
                { value: '3', text: app.t('Quarter') },
                { value: '4', text: app.t('Third') },
                { value: '6', text: app.t('Half') },
                { value: '8', text: app.t('Two thirds') },
                { value: '12', text: app.t('Full') }
            ], String(visual.width || 12), {
                onchange: function (event) {
                    visual.width = parseInt(event.target.value, 10);
                    app.changed({ render: false });
                }
            }))
        ];

        switch (visual.type) {
            case 'Chart':
                parts.push(
                    labelled(app.t('Chart type'), ui.select(['Bar', 'HorizontalBar', 'Line', 'Area', 'Pie', 'Doughnut'].map(function (value) {
                        return { value: value, text: app.config.chartLabels[value] || value };
                    }), visual.chartType || 'Bar', {
                        onchange: function (event) {
                            visual.chartType = event.target.value;
                            app.changed({ render: false });
                        }
                    })),
                    well(app.t('Categories'), 'dimension', ui.select(none.concat(dimensions), visual.categoryColumnId || '', {
                        onchange: function (event) {
                            visual.categoryColumnId = event.target.value || null;
                            app.changed({ render: false });
                        }
                    }), function (column) {
                        visual.categoryColumnId = column.id;
                    }),
                    well(app.t('Values'), 'measure', checkboxList(numbers, visual.valueColumnIds || [], set('valueColumnIds')), function (column) {
                        visual.valueColumnIds = (visual.valueColumnIds || []).filter(function (id) {
                            return id !== column.id;
                        }).concat([column.id]);
                    }),
                    well(app.t('Split into series by'), 'dimension', ui.select(none.concat(dimensions), visual.seriesColumnId || '', {
                        onchange: function (event) {
                            visual.seriesColumnId = event.target.value || null;
                            app.changed({ render: false });
                        }
                    }), function (column) {
                        visual.seriesColumnId = column.id;
                    }),
                    h('div', { className: 'form-text mb-2' }, app.t('Draws one series per value of this column, using the first value column.')),
                    check(app.t('Stack series'), visual.stacked, set('stacked', false)),
                    check(app.t('Show legend'), visual.showLegend !== false, set('showLegend', false)));
                break;

            case 'Metrics':
                parts.push(well(app.t('Values'), 'measure', checkboxList(numbers, visual.valueColumnIds || [], set('valueColumnIds')), function (column) {
                    visual.valueColumnIds = (visual.valueColumnIds || []).filter(function (id) {
                        return id !== column.id;
                    }).concat([column.id]);
                }));
                break;

            case 'Pivot':
                parts.push(
                    well(app.t('Rows'), 'dimension', checkboxList(dimensions, visual.columnIds || [], set('columnIds')), function (column) {
                        visual.columnIds = (visual.columnIds || []).filter(function (id) {
                            return id !== column.id;
                        }).concat([column.id]);
                    }),
                    well(app.t('Columns across'), 'dimension', ui.select(none.concat(dimensions), visual.seriesColumnId || '', {
                        onchange: function (event) {
                            visual.seriesColumnId = event.target.value || null;
                            app.changed({ render: false });
                        }
                    }), function (column) {
                        visual.seriesColumnId = column.id;
                    }),
                    well(app.t('Value'), 'measure', ui.select(none.concat(numbers), (visual.valueColumnIds || [])[0] || '', {
                        onchange: function (event) {
                            visual.valueColumnIds = event.target.value ? [event.target.value] : [];
                            app.changed({ render: false });
                        }
                    }), function (column) {
                        visual.valueColumnIds = [column.id];
                    }),
                    check(app.t('Show totals'), visual.showTotals, set('showTotals', false)));
                break;

            default:
                parts.push(
                    labelled(app.t('Columns shown'), checkboxList(all, visual.columnIds || [], set('columnIds')), app.t('Leave all unchecked to show every visible column.')),
                    check(app.t('Show totals'), visual.showTotals, set('showTotals', false)),
                    check(app.t('Show subtotals'), visual.showSubtotals, set('showSubtotals', false), app.t('Adds a subtotal after each group of the leading dimensions, such as per user within each role, then per role.')));
                break;
        }

        return parts;
    }

    app.renderVisuals = function () {
        var target = app.elements.visuals;

        if (!target || app.isView()) {
            return;
        }

        var visuals = app.design.visuals;
        var selection = app.selection || {};
        var columns = app.columns();
        var add = function (type) {
            var measures = columns.filter(function (column) {
                return column.isMeasure;
            });
            var dimensions = columns.filter(function (column) {
                return !column.isMeasure;
            });

            if (!measures.length) {
                measures = columns.filter(function (column) {
                    return designer.isNumeric(column.dataType);
                });
            }

            var visual = {
                id: designer.newId('v', visuals.map(function (item) {
                    return item.id;
                })),
                type: type,
                title: null,
                width: type === 'Table' || type === 'Metrics' ? 12 : 6,
                chartType: 'Bar',
                categoryColumnId: type === 'Chart' && dimensions[0] ? dimensions[0].id : null,
                seriesColumnId: type === 'Pivot' && dimensions[1] ? dimensions[1].id : null,
                valueColumnIds: measures.slice(0, type === 'Pivot' ? 1 : 3).map(function (column) {
                    return column.id;
                }),
                columnIds: type === 'Pivot' && dimensions[0] ? [dimensions[0].id] : [],
                showTotals: type !== 'Chart',
                stacked: false,
                showLegend: true
            };

            visuals.push(visual);
            app.selection = { kind: 'visual', id: visual.id };
            app.changed();
        };
        var icons = { Table: 'fa-table', Chart: 'fa-chart-column', Metrics: 'fa-gauge', Pivot: 'fa-table-cells' };
        var list = h('div', { className: 'list-group list-group-flush' });

        visuals.forEach(function (visual, index) {
            var selected = selection.kind === 'visual' && selection.id === visual.id;

            list.appendChild(h('div', { className: 'list-group-item d-flex align-items-center gap-2 py-1' + (selected ? ' active' : '') },
                ui.icon(icons[visual.type] || 'fa-table'),
                h('button', { type: 'button', className: 'btn btn-link btn-sm p-0 flex-grow-1 text-start text-reset text-decoration-none', onclick: function () {
                    app.select('visual', visual.id);
                } }, visual.title || app.config.visualLabels[visual.type] || visual.type),
                h('button', { type: 'button', className: 'btn btn-link btn-sm p-0 text-reset', 'aria-label': app.t('Move up'), disabled: index === 0, onclick: function () {
                    designer.move(visuals, index, index - 1);
                    app.changed();
                } }, ui.icon('fa-arrow-up')),
                h('button', { type: 'button', className: 'btn btn-link btn-sm p-0 text-reset', 'aria-label': app.t('Remove'), onclick: function () {
                    visuals.splice(index, 1);
                    app.selection = null;
                    app.changed();
                } }, ui.icon('fa-xmark'))));
        });

        var selectedVisual = selection.kind === 'visual'
            ? visuals.filter(function (visual) {
                return visual.id === selection.id;
            })[0]
            : null;

        ui.clear(target);
        target.appendChild(h('div', { className: 'card' },
            h('div', { className: 'card-header py-1 fw-semibold' }, app.t('Visuals')),
            visuals.length ? list : h('div', { className: 'card-body p-2 small text-muted' }, app.t('Without visuals the report shows one table of the result.')),
            h('div', { className: 'card-body p-2 d-flex flex-wrap gap-1' }, ['Table', 'Chart', 'Metrics', 'Pivot'].map(function (type) {
                return h('button', { type: 'button', className: 'btn btn-sm btn-outline-primary', onclick: function () {
                    add(type);
                } }, ui.icon(icons[type]), ' ', app.config.visualLabels[type] || type);
            })),
            selectedVisual ? h('div', { className: 'card-body p-2 border-top' }, visualProperties(selectedVisual)) : null));
    };
})(typeof window !== 'undefined' ? window : globalThis);
