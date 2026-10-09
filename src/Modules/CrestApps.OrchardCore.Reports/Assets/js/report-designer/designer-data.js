/*
 * The Data pane of the report designer: the data sets of the design with their draggable fields, the joins between
 * them, and the calculated fields, plus the dialogs that add a data set and edit a formula.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner;
    var ui = designer.ui;
    var h = ui.h;
    var app = designer.app;

    var FIELD_MIME = 'application/x-report-field';
    var fieldSearch = '';

    designer.FIELD_MIME = FIELD_MIME;

    function fieldItem(field) {
        var item = h('li', {
            className: 'report-designer-field list-group-item list-group-item-action d-flex align-items-center gap-2 py-1',
            draggable: 'true',
            title: field.key,
            dataset: { fieldKey: field.key },
            ondragstart: function (event) {
                event.dataTransfer.setData(FIELD_MIME, field.key);
                event.dataTransfer.setData('text/plain', '[' + field.key + ']');
                event.dataTransfer.effectAllowed = 'copy';
            }
        },
        h('span', { className: 'report-designer-type badge text-bg-light' + (field.isAggregate ? ' is-measure' : '') }, designer.typeGlyph(field.dataType)),
        h('span', { className: 'flex-grow-1 text-truncate' }, field.label),
        h('button', {
            type: 'button',
            className: 'btn btn-link btn-sm p-0 report-designer-field-action',
            title: app.t('Add to columns'),
            'aria-label': app.t('Add to columns') + ': ' + field.label,
            onclick: function () {
                var column = designer.addColumn(app.design.query, field);

                app.selection = { kind: 'column', id: column.id };
                app.changed();
            }
        }, ui.icon('fa-table-columns')),
        h('button', {
            type: 'button',
            className: 'btn btn-link btn-sm p-0 report-designer-field-action',
            title: app.t('Add to filters'),
            'aria-label': app.t('Add to filters') + ': ' + field.label,
            onclick: function () {
                var filter = designer.addFilter(app.design.query, field);

                app.selection = { kind: 'filter', id: filter.id };
                app.changed();
            }
        }, ui.icon('fa-filter')));

        return item;
    }

    function matches(field) {
        if (!fieldSearch) {
            return true;
        }

        var text = (field.label + ' ' + field.key + ' ' + (field.group || '')).toLowerCase();

        return text.indexOf(fieldSearch) >= 0;
    }

    function dataSetCard(dataSet, index, fields) {
        var schema = app.schemas[dataSet.alias];
        var own = Object.keys(fields)
            .map(function (key) {
                return fields[key];
            })
            .filter(function (field) {
                return field.alias === dataSet.alias && matches(field);
            });
        var groups = {};

        own.forEach(function (field) {
            var group = field.group || '';

            (groups[group] = groups[group] || []).push(field);
        });

        var list = h('ul', { className: 'list-group list-group-flush report-designer-field-list' });

        Object.keys(groups).forEach(function (group) {
            if (group) {
                list.appendChild(h('li', { className: 'list-group-item py-1 small text-uppercase text-muted report-designer-field-group' }, group));
            }

            groups[group].forEach(function (field) {
                list.appendChild(fieldItem(field));
            });
        });

        if (!schema) {
            list.appendChild(h('li', { className: 'list-group-item small text-muted' }, app.t('Loading…')));
        } else if (schema.errors && schema.errors.length) {
            schema.errors.forEach(function (error) {
                list.appendChild(h('li', { className: 'list-group-item small text-danger' }, error));
            });
        }

        return h('div', { className: 'card mb-2 report-designer-dataset' },
            h('div', { className: 'card-header d-flex align-items-center gap-2 py-1' },
                h('span', { className: 'badge text-bg-primary' }, index === 0 ? app.t('Base') : String(index + 1)),
                h('span', { className: 'fw-semibold text-truncate flex-grow-1', title: dataSet.alias }, dataSet.displayName || dataSet.dataSet),
                h('button', {
                    type: 'button',
                    className: 'btn btn-sm btn-link text-danger p-0',
                    title: app.t('Remove data set'),
                    'aria-label': app.t('Remove data set') + ': ' + (dataSet.displayName || dataSet.dataSet),
                    onclick: function () {
                        designer.removeDataSet(app.design, dataSet.alias);
                        delete app.schemas[dataSet.alias];
                        app.changed();
                    }
                }, ui.icon('fa-xmark'))),
            list);
    }

    function joinEditor(dataSet, index, fields) {
        var query = app.design.query;
        var join = query.joins.filter(function (candidate) {
            return candidate.alias === dataSet.alias;
        })[0];

        if (!join) {
            join = { alias: dataSet.alias, type: 'Inner', conditions: [] };
            query.joins.push(join);
        }

        var earlier = query.dataSets.slice(0, index).map(function (candidate) {
            return candidate.alias;
        });
        var options = function (aliases) {
            return [{ value: '', text: app.t('Pick a field') }].concat(Object.keys(fields)
                .map(function (key) {
                    return fields[key];
                })
                .filter(function (field) {
                    return field.kind === 'DataSetField' && aliases.indexOf(field.alias) >= 0;
                })
                .map(function (field) {
                    var owner = query.dataSets.filter(function (candidate) {
                        return candidate.alias === field.alias;
                    })[0];

                    return { value: field.key, text: (owner.displayName || owner.alias) + ' › ' + field.label };
                }));
        };

        var conditions = h('div');

        join.conditions.forEach(function (condition, conditionIndex) {
            conditions.appendChild(h('div', { className: 'd-flex gap-1 align-items-center mb-1' },
                ui.select(options(earlier), condition.leftField, {
                    'aria-label': app.t('Field before'),
                    onchange: function (event) {
                        condition.leftField = event.target.value;
                        app.changed({ render: false });
                    }
                }),
                h('span', { className: 'text-muted' }, '='),
                ui.select(options([dataSet.alias]), condition.rightField, {
                    'aria-label': app.t('Field of the joined data set'),
                    onchange: function (event) {
                        condition.rightField = event.target.value;
                        app.changed({ render: false });
                    }
                }),
                h('button', {
                    type: 'button',
                    className: 'btn btn-sm btn-link text-danger p-0',
                    'aria-label': app.t('Remove'),
                    onclick: function () {
                        join.conditions.splice(conditionIndex, 1);
                        app.changed();
                    }
                }, ui.icon('fa-xmark'))));
        });

        return h('div', { className: 'card mb-2' }, h('div', { className: 'card-body p-2' },
            h('label', { className: 'form-label small mb-1' }, app.t('Join') + ' ' + (dataSet.displayName || dataSet.alias)),
            ui.select([
                { value: 'Inner', text: app.t('Only rows that match on both sides') },
                { value: 'Left', text: app.t('All rows before, matching rows of this data set') },
                { value: 'Right', text: app.t('All rows of this data set, matching rows before') },
                { value: 'Full', text: app.t('All rows of both sides') }
            ], join.type, {
                className: 'form-select form-select-sm mb-2',
                'aria-label': app.t('Join type'),
                onchange: function (event) {
                    join.type = event.target.value;
                    app.changed({ render: false });
                }
            }),
            conditions,
            h('button', {
                type: 'button',
                className: 'btn btn-sm btn-outline-secondary',
                onclick: function () {
                    join.conditions.push({ leftField: '', rightField: '' });
                    app.changed();
                }
            }, ui.icon('fa-plus'), ' ', app.t('Match fields'))));
    }

    app.renderDataPane = function () {
        var pane = app.elements.dataPane;
        var query = app.design.query;
        var fields = app.fields();
        var search = h('input', {
            type: 'search',
            className: 'form-control form-control-sm mb-2',
            placeholder: app.t('Search fields'),
            'aria-label': app.t('Search fields'),
            value: fieldSearch,
            oninput: function (event) {
                fieldSearch = event.target.value.trim().toLowerCase();
                app.renderDataPane();

                var input = app.elements.dataPane.querySelector('input[type="search"]');

                input.focus();
                input.setSelectionRange(input.value.length, input.value.length);
            }
        });

        ui.clear(pane);
        ui.append(pane, [
            h('div', { className: 'd-flex align-items-center justify-content-between mb-2' },
                h('h2', { className: 'h6 mb-0' }, app.t('Data')),
                h('button', { type: 'button', className: 'btn btn-sm btn-primary', onclick: app.openAddDataSet }, ui.icon('fa-plus'), ' ', app.t('Add data set'))),
            query.dataSets.length ? search : h('p', { className: 'text-muted small' }, app.t('Start by adding a data set, such as a content type.')),
            query.dataSets.map(function (dataSet, index) {
                return dataSetCard(dataSet, index, fields);
            })
        ]);

        if (query.dataSets.length > 1) {
            pane.appendChild(h('h3', { className: 'h6 mt-3' }, app.t('Relationships')));
            query.dataSets.slice(1).forEach(function (dataSet, index) {
                pane.appendChild(joinEditor(dataSet, index + 1, fields));
            });
        }

        var calculated = Object.keys(fields)
            .map(function (key) {
                return fields[key];
            })
            .filter(function (field) {
                return field.kind !== 'DataSetField' && matches(field);
            });
        var calculatedList = h('ul', { className: 'list-group list-group-flush report-designer-field-list' });

        calculated.forEach(function (field) {
            var item = fieldItem(field);
            var definition = query.calculatedFields.filter(function (candidate) {
                return candidate.name === field.key;
            })[0];

            if (definition) {
                item.appendChild(h('button', {
                    type: 'button',
                    className: 'btn btn-link btn-sm p-0 report-designer-field-action',
                    title: app.t('Edit formula'),
                    'aria-label': app.t('Edit formula') + ': ' + field.label,
                    onclick: function () {
                        app.openFormula(definition);
                    }
                }, ui.icon('fa-pen')));
            }

            calculatedList.appendChild(item);
        });

        if (query.dataSets.length) {
            pane.appendChild(h('div', { className: 'card mt-3' },
                h('div', { className: 'card-header d-flex align-items-center justify-content-between py-1' },
                    h('span', { className: 'fw-semibold' }, app.t('Calculated fields')),
                    h('button', { type: 'button', className: 'btn btn-sm btn-link p-0', onclick: function () {
                        app.openFormula(null);
                    } }, ui.icon('fa-plus'), ' ', app.t('New'))),
                calculatedList));
        }
    };

    app.openAddDataSet = function () {
        var sourceSelect = ui.select([{ value: '', text: app.t('Pick a data source') }].concat(app.sources.map(function (source) {
            return { value: source.name, text: source.displayName };
        })), '', { className: 'form-select mb-2', 'aria-label': app.t('Data source') });
        var filter = h('input', { type: 'search', className: 'form-control mb-2', placeholder: app.t('Search data sets'), 'aria-label': app.t('Search data sets') });
        var list = h('div', { className: 'list-group report-designer-dataset-picker' });
        var dataSets = [];
        var modal;

        var renderList = function () {
            var text = filter.value.trim().toLowerCase();

            ui.clear(list);
            dataSets.filter(function (dataSet) {
                return !text || (dataSet.displayName + ' ' + dataSet.name).toLowerCase().indexOf(text) >= 0;
            }).forEach(function (dataSet) {
                list.appendChild(h('button', {
                    type: 'button',
                    className: 'list-group-item list-group-item-action',
                    onclick: function () {
                        addDataSet(sourceSelect.value, dataSet);
                        modal.close();
                    }
                }, h('div', { className: 'fw-semibold' }, dataSet.displayName || dataSet.name),
                dataSet.description ? h('div', { className: 'small text-muted' }, dataSet.description) : null));
            });

            if (!list.firstChild) {
                list.appendChild(h('div', { className: 'text-muted small p-2' }, sourceSelect.value ? app.t('No data sets found.') : app.t('Pick a data source first.')));
            }
        };

        sourceSelect.addEventListener('change', function () {
            dataSets = [];
            renderList();

            if (!sourceSelect.value) {
                return;
            }

            ui.request(app.url('dataSets') + '?source=' + encodeURIComponent(sourceSelect.value)).then(function (result) {
                dataSets = result || [];
                renderList();
            });
        });
        filter.addEventListener('input', renderList);

        if (app.sources.length === 1) {
            sourceSelect.value = app.sources[0].name;
            sourceSelect.dispatchEvent(new root.Event('change'));
        }

        renderList();
        modal = ui.modal(app.t('Add data set'), [sourceSelect, filter, list]);
    };

    function addDataSet(source, descriptor) {
        var query = app.design.query;
        var reference = {
            alias: designer.aliasFor(descriptor.displayName || descriptor.name, query.dataSets),
            source: source,
            dataSet: descriptor.name,
            displayName: descriptor.displayName || descriptor.name
        };

        query.dataSets.push(reference);
        app.render();

        app.loadSchema(reference).then(function () {
            if (query.dataSets.length > 1) {
                var earlier = query.dataSets.slice(0, -1).map(function (dataSet) {
                    return { dataSet: dataSet, fields: (app.schemas[dataSet.alias] || {}).fields || [] };
                });
                var suggestion = designer.suggestJoin(earlier, { dataSet: reference, fields: app.schemas[reference.alias].fields || [] });

                query.joins.push({
                    alias: reference.alias,
                    type: 'Inner',
                    conditions: suggestion ? [suggestion] : [{ leftField: '', rightField: '' }]
                });
            }

            app.changed();
        });
    }

    app.openFormula = function (existing) {
        var query = app.design.query;
        var fields = app.fields();
        var editing = existing || { name: '', label: '', expression: '' };
        var label = h('input', { type: 'text', className: 'form-control mb-2', value: editing.label || '', placeholder: app.t('Label, such as Profit margin'), maxlength: '200' });
        var name = h('input', { type: 'text', className: 'form-control mb-2', value: editing.name || '', placeholder: app.t('Name used in formulas, such as ProfitMargin'), pattern: '[A-Za-z][A-Za-z0-9_]*', disabled: !!existing });
        var formula = h('textarea', { className: 'form-control font-monospace mb-2', rows: '5', spellcheck: 'false', placeholder: 'SUM([Order.Total]) / COUNTD([Customer.ContentItemId])' }, editing.expression || '');
        var feedback = h('div', { className: 'small mb-2' });
        var insert = function (text) {
            var start = formula.selectionStart || 0;
            var end = formula.selectionEnd || 0;

            formula.value = formula.value.slice(0, start) + text + formula.value.slice(end);
            formula.focus();
            formula.setSelectionRange(start + text.length, start + text.length);
        };
        var fieldList = h('div', { className: 'list-group list-group-flush report-designer-formula-list' }, Object.keys(fields)
            .map(function (key) {
                return fields[key];
            })
            .filter(function (field) {
                return field.key !== editing.name;
            })
            .map(function (field) {
                return h('button', { type: 'button', className: 'list-group-item list-group-item-action py-1 small', title: field.key, onclick: function () {
                    insert('[' + field.key + ']');
                } }, h('span', { className: 'badge text-bg-light me-1' }, designer.typeGlyph(field.dataType)), field.label);
            }));
        var functionList = h('div', { className: 'list-group list-group-flush report-designer-formula-list' }, app.functions.map(function (fn) {
            return h('button', { type: 'button', className: 'list-group-item list-group-item-action py-1 small', title: fn.description, onclick: function () {
                insert(fn.name + '(');
            } }, h('code', null, fn.signature), h('div', { className: 'text-muted' }, fn.description));
        }));

        label.addEventListener('input', function () {
            if (!existing && !name.dataset.edited) {
                name.value = designer.aliasFor(label.value || 'Field', []);
            }
        });
        name.addEventListener('input', function () {
            name.dataset.edited = 'true';
        });

        var candidate = function () {
            return { name: name.value.trim(), label: label.value.trim(), expression: formula.value };
        };
        var check = function () {
            var field = candidate();
            var probe = JSON.parse(JSON.stringify(query));

            probe.calculatedFields = probe.calculatedFields.filter(function (other) {
                return other.name !== field.name && other.name !== editing.name;
            });
            probe.calculatedFields.push(field);
            probe.columns = [];

            return ui.request(app.url('plan'), { body: probe }).then(function (plan) {
                var own = (plan.errors || []).filter(function (error) {
                    return error.indexOf(field.label || field.name) >= 0 || error.indexOf(field.name) >= 0;
                });
                var described = (plan.fields || []).filter(function (candidateField) {
                    return candidateField.key === field.name;
                })[0];

                ui.clear(feedback);

                if (own.length || !described) {
                    feedback.className = 'small mb-2 text-danger';
                    feedback.textContent = own.length ? own.join(' ') : app.t('Enter a valid name and formula.');

                    return false;
                }

                feedback.className = 'small mb-2 text-success';
                feedback.textContent = app.t('The formula is valid.') + ' ' + app.t('Result type') + ': ' + described.dataType +
                    (described.isAggregate ? ' (' + app.t('aggregated per group') + ')' : '');

                return true;
            });
        };

        var modal = ui.modal(existing ? app.t('Edit calculated field') : app.t('New calculated field'), h('div', { className: 'row g-3' },
            h('div', { className: 'col-12 col-md-7' },
                h('label', { className: 'form-label' }, app.t('Label')), label,
                h('label', { className: 'form-label' }, app.t('Name')), name,
                h('label', { className: 'form-label' }, app.t('Formula')), formula,
                feedback,
                h('p', { className: 'small text-muted mb-0' }, app.t('Write fields in square brackets. Use an aggregate function such as SUM or COUNTD to calculate one value per group.'))),
            h('div', { className: 'col-12 col-md-5' },
                h('div', { className: 'small fw-semibold' }, app.t('Fields')), fieldList,
                h('div', { className: 'small fw-semibold mt-2' }, app.t('Functions')), functionList)), [
            existing ? h('button', { type: 'button', className: 'btn btn-outline-danger me-auto', onclick: function () {
                query.calculatedFields = query.calculatedFields.filter(function (other) {
                    return other.name !== existing.name;
                });
                modal.close();
                app.changed();
            } }, app.t('Delete')) : null,
            h('button', { type: 'button', className: 'btn btn-outline-secondary', onclick: check }, app.t('Check')),
            h('button', { type: 'button', className: 'btn btn-primary', onclick: function () {
                check().then(function (valid) {
                    if (!valid) {
                        return;
                    }

                    var field = candidate();

                    if (existing) {
                        existing.label = field.label;
                        existing.expression = field.expression;
                    } else {
                        query.calculatedFields.push(field);
                    }

                    modal.close();
                    app.changed();
                });
            } }, app.t('Apply'))
        ], 'modal-xl');
    };
})(typeof window !== 'undefined' ? window : globalThis);
