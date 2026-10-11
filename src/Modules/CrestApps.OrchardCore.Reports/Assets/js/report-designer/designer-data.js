/*
 * The Data pane of the report builder: the data sets of the design with their draggable fields, the joins between
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
    var collapsed = {};

    function collapsible(key, header, body, className) {
        var isCollapsed = !!collapsed[key] && !fieldSearch;

        return h('div', { className: 'card mb-2 ' + (className || '') },
            h('div', { className: 'card-header d-flex align-items-center gap-2 py-1' },
                h('button', {
                    type: 'button',
                    className: 'btn btn-sm btn-link text-reset p-0',
                    'aria-expanded': isCollapsed ? 'false' : 'true',
                    'aria-label': isCollapsed ? app.t('Expand') : app.t('Collapse'),
                    onclick: function () {
                        collapsed[key] = !collapsed[key];
                        app.renderDataPane();
                    }
                }, ui.icon(isCollapsed ? 'fa-chevron-right' : 'fa-chevron-down')),
                header),
            isCollapsed ? null : body);
    }

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

        return collapsible('dataset:' + dataSet.alias, [
                h('span', { className: 'badge text-bg-primary' }, index === 0 ? app.t('Base') : String(index + 1)),
                h('span', { className: 'fw-semibold text-truncate flex-grow-1', title: dataSet.alias }, dataSet.displayName || dataSet.dataSet),
                h('span', { className: 'badge text-bg-light' }, String(own.length)),
                index === 0 ? null : h('button', {
                    type: 'button',
                    className: 'btn btn-sm btn-link p-0',
                    title: app.t('Edit join'),
                    'aria-label': app.t('Edit join') + ': ' + (dataSet.displayName || dataSet.dataSet),
                    onclick: function () {
                        app.editJoin(dataSet.alias);
                    }
                }, ui.icon('fa-link')),
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
                }, ui.icon('fa-xmark'))
            ], list, 'report-designer-dataset');
    }

    // Finds the join that attaches a data set, creating an inner join with no matching columns when there is none.
    app.joinFor = function (alias) {
        var query = app.design.query;
        var join = query.joins.filter(function (candidate) {
            return candidate.alias === alias;
        })[0];

        if (!join) {
            join = { alias: alias, type: 'Inner', conditions: [] };
            query.joins.push(join);
        }

        return join;
    };

    app.dataSetLabel = function (alias) {
        var dataSet = app.design.query.dataSets.filter(function (candidate) {
            return candidate.alias === alias;
        })[0];

        return dataSet ? (dataSet.displayName || dataSet.dataSet || alias) : alias;
    };

    // The editor of one join, shown in the Properties pane: which rows to keep, and the pairs of columns that must be
    // equal for two rows to match. Several pairs match on several columns at once.
    app.joinEditor = function (alias) {
        var query = app.design.query;
        var index = query.dataSets.map(function (dataSet) {
            return dataSet.alias;
        }).indexOf(alias);

        if (index < 1) {
            return null;
        }

        var fields = app.fields();
        var join = app.joinFor(alias);
        var earlier = query.dataSets.slice(0, index).map(function (candidate) {
            return candidate.alias;
        });
        var options = function (aliases) {
            return [{ value: '', text: app.t('Pick a column') }].concat(Object.keys(fields)
                .map(function (key) {
                    return fields[key];
                })
                .filter(function (field) {
                    return field.kind === 'DataSetField' && aliases.indexOf(field.alias) >= 0;
                })
                .sort(function (left, right) {
                    return (right.isIdentifier ? 1 : 0) - (left.isIdentifier ? 1 : 0);
                })
                .map(function (field) {
                    return { value: field.key, text: app.dataSetLabel(field.alias) + ' › ' + field.label + (field.isIdentifier ? ' 🔑' : '') };
                }));
        };
        var conditions = h('div', { className: 'd-flex flex-column gap-2 mb-2' });

        join.conditions.forEach(function (condition, conditionIndex) {
            conditions.appendChild(h('div', { className: 'rd-join-pair border rounded p-2' },
                h('div', { className: 'd-flex align-items-center justify-content-between mb-1' },
                    h('span', { className: 'small text-muted' }, app.t('Matching columns') + ' ' + (conditionIndex + 1)),
                    h('button', {
                        type: 'button',
                        className: 'btn btn-sm btn-link text-danger p-0',
                        'aria-label': app.t('Remove'),
                        onclick: function () {
                            join.conditions.splice(conditionIndex, 1);
                            app.changed();
                        }
                    }, ui.icon('fa-xmark'))),
                ui.select(options(earlier), condition.leftField, {
                    'aria-label': app.t('Column of the data sets before'),
                    onchange: function (event) {
                        condition.leftField = event.target.value;
                        app.changed();
                    }
                }),
                h('div', { className: 'text-center text-muted small my-1' }, ui.icon('fa-equals')),
                ui.select(options([alias]), condition.rightField, {
                    'aria-label': app.t('Column of the joined data set'),
                    onchange: function (event) {
                        condition.rightField = event.target.value;
                        app.changed();
                    }
                })));
        });

        if (!join.conditions.length) {
            conditions.appendChild(h('div', { className: 'alert alert-warning small py-2 mb-0' }, app.t('Add the columns that must match.')));
        }

        return [
            h('p', { className: 'small text-muted' }, app.t('Joins') + ' ', h('strong', null, app.dataSetLabel(alias)), ' ' + app.t('to') + ' ', h('strong', null, earlier.map(app.dataSetLabel).join(', ')), '.'),
            h('label', { className: 'form-label small mb-1' }, app.t('Keep')),
            ui.select([
                { value: 'Inner', text: app.t('Only rows that match on both sides') },
                { value: 'Left', text: app.t('All rows before, matching rows of this data set') },
                { value: 'Right', text: app.t('All rows of this data set, matching rows before') },
                { value: 'Full', text: app.t('All rows of both sides') }
            ], join.type, {
                className: 'form-select form-select-sm mb-3',
                'aria-label': app.t('Join type'),
                onchange: function (event) {
                    join.type = event.target.value;
                    app.changed();
                }
            }),
            conditions,
            h('button', {
                type: 'button',
                className: 'btn btn-sm btn-outline-primary w-100',
                onclick: function () {
                    join.conditions.push({ leftField: '', rightField: '' });
                    app.changed();
                }
            }, ui.icon('fa-plus'), ' ', app.t('Add matching columns')),
            h('div', { className: 'form-text' }, app.t('Rows match when every pair of columns is equal. Add more pairs to match on several columns. Key columns are marked with a key.'))
        ];
    };

    // A short description of a join for its pill on the Joins shelf.
    app.joinSummary = function (alias) {
        var fields = app.fields();
        var join = designer.findJoin(app.design.query, alias) || { conditions: [] };
        var complete = (join.conditions || []).filter(function (condition) {
            return condition.leftField && condition.rightField;
        });

        return {
            complete: complete.length > 0,
            text: complete.map(function (condition) {
                var left = fields[condition.leftField];
                var right = fields[condition.rightField];

                return (left ? left.label : condition.leftField) + ' = ' + (right ? right.label : condition.rightField);
            }).join(', ')
        };
    };

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
            query.dataSets.length
                ? h('div', { className: 'rd-sticky-search' }, search)
                : h('div', { className: 'report-designer-empty text-muted small' },
                    h('div', { className: 'mb-2' }, ui.icon('fa-database fa-2x')),
                    app.t('Start by adding a data set, such as a content type.')),
            query.dataSets.map(function (dataSet, index) {
                return dataSetCard(dataSet, index, fields);
            })
        ]);

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
            pane.appendChild(collapsible('calculated', [
                h('span', { className: 'fw-semibold flex-grow-1' }, app.t('Calculated fields')),
                h('button', { type: 'button', className: 'btn btn-sm btn-link p-0', onclick: function () {
                    app.openFormula(null);
                } }, ui.icon('fa-plus'), ' ', app.t('New'))
            ], calculatedList, 'mt-3'));
        }
    };

    app.openAddDataSet = function () {
        var filter = h('input', { type: 'search', className: 'form-control mb-3', placeholder: app.t('Filter'), 'aria-label': app.t('Filter data sets'), autocomplete: 'off' });
        var categories = h('nav', { className: 'nav nav-pills flex-nowrap flex-md-column overflow-auto gap-1 pb-2 pb-md-0', 'aria-label': app.t('Data sources') });
        var grid = h('div', { className: 'row row-cols-1 row-cols-md-2 row-cols-xl-3 g-2' });
        var status = h('div', { className: 'text-muted small py-3 text-center' }, app.t('Loading…'));
        var entries = [];
        var selectedSource = '';
        var modal;
        var added = (app.design.query.dataSets || []).map(function (dataSet) {
            return dataSet.source + '\u001f' + dataSet.dataSet;
        });

        // The data sets already in the design, with the references their fields declare, so the picker can tell which
        // data sets are related to them.
        var current = (app.design.query.dataSets || []).map(function (dataSet) {
            var schema = app.schemas[dataSet.alias] || {};
            var references = [];

            (schema.fields || []).forEach(function (field) {
                (field.references || []).forEach(function (reference) {
                    references.push(reference);
                });
            });

            return { source: dataSet.source, dataSet: dataSet.dataSet, label: dataSet.displayName || dataSet.dataSet, references: references };
        });
        var relatedTo = function (entry) {
            var candidate = { source: entry.source.name, dataSet: entry.dataSet.name, references: entry.dataSet.references || [] };

            return current.filter(function (existing) {
                return designer.isRelated(candidate, existing);
            }).map(function (existing) {
                return existing.label;
            });
        };
        var related = '__related';
        var inCategory = function (entry, name) {
            if (name === related) {
                return entry.related.length > 0;
            }

            return !name || entry.source.name === name;
        };

        var renderCategories = function () {
            ui.clear(categories);

            var options = [{ name: '', displayName: app.t('All') }];

            if (entries.some(function (entry) {
                return entry.related.length > 0;
            })) {
                options.push({ name: related, displayName: app.t('Related') });
            }

            options.concat(app.sources).forEach(function (source) {
                var active = source.name === selectedSource;
                var count = entries.filter(function (entry) {
                    return inCategory(entry, source.name);
                }).length;

                categories.appendChild(h('button', {
                    type: 'button',
                    className: 'nav-link text-start text-nowrap d-flex align-items-center gap-2' + (active ? ' active' : ''),
                    'aria-pressed': active ? 'true' : 'false',
                    onclick: function () {
                        selectedSource = source.name;
                        renderCategories();
                        renderCards();
                    }
                }, h('span', { className: 'flex-grow-1' }, source.displayName), h('span', { className: 'badge rounded-pill ' + (active ? 'text-bg-light' : 'text-bg-secondary') }, String(count))));
            });
        };

        var renderCards = function () {
            var text = filter.value.trim().toLowerCase();
            var visible = entries.filter(function (entry) {
                return inCategory(entry, selectedSource) &&
                    (!text || entry.search.indexOf(text) >= 0);
            }).sort(function (left, right) {
                return (right.related.length ? 1 : 0) - (left.related.length ? 1 : 0);
            });

            ui.clear(grid);
            visible.forEach(function (entry) {
                var dataSet = entry.dataSet;
                var isAdded = added.indexOf(entry.source.name + '\u001f' + dataSet.name) >= 0;

                grid.appendChild(h('div', { className: 'col' }, h('div', { className: 'card h-100' },
                    h('div', { className: 'card-body' },
                        h('h5', { className: 'card-title d-flex align-items-baseline gap-2' },
                            h('i', { className: 'fa-solid ' + (entry.source.name === 'ReportViews' ? 'fa-layer-group' : 'fa-table') + ' fa-fw text-primary', 'aria-hidden': 'true' }),
                            h('span', null, dataSet.displayName || dataSet.name)),
                        dataSet.description ? h('p', { className: 'card-text text-body-secondary small mb-0' }, dataSet.description) : null),
                    h('div', { className: 'card-footer d-flex align-items-center gap-2 flex-wrap' },
                        h('span', { className: 'badge text-bg-light' }, entry.source.displayName),
                        entry.related.length
                            ? h('span', { className: 'badge text-bg-success', title: app.t('Joined automatically when added.') }, ui.icon('fa-link'), ' ', app.t('Related to') + ' ' + entry.related.join(', '))
                            : null,
                        h('span', { className: 'me-auto' }),
                        isAdded ? h('span', { className: 'small text-muted' }, app.t('Added')) : null,
                        h('button', {
                            type: 'button',
                            className: 'btn btn-primary btn-sm',
                            onclick: function () {
                                addDataSet(entry.source.name, dataSet);
                                modal.close();
                            }
                        }, isAdded ? app.t('Add again') : app.t('Add'))))));
            });

            status.classList.toggle('d-none', visible.length > 0);
            status.textContent = entries.length ? app.t('No data sets match the filter.') : status.textContent;
        };

        filter.addEventListener('input', renderCards);

        modal = ui.modal(app.t('Add Data Set'), h('div', { className: 'row g-3' },
            h('div', { className: 'col-md-3' }, h('div', { className: 'position-sticky top-0' }, filter, categories)),
            h('div', { className: 'col-md-9' }, grid, status)), null, 'modal-xl');

        renderCategories();

        Promise.all(app.sources.map(function (source) {
            return ui.request(app.url('dataSets') + '?source=' + encodeURIComponent(source.name)).then(function (dataSets) {
                (dataSets || []).forEach(function (dataSet) {
                    var entry = {
                        source: source,
                        dataSet: dataSet,
                        search: [dataSet.displayName, dataSet.name, dataSet.description, dataSet.group, source.displayName].join(' ').toLowerCase()
                    };

                    entry.related = relatedTo(entry);
                    entries.push(entry);
                });
            }).catch(function () {
                return null;
            });
        })).then(function () {
            status.textContent = app.sources.length ? app.t('No data sets are available to you.') : app.t('No data sources are enabled.');

            if (entries.some(function (entry) {
                return entry.related.length > 0;
            })) {
                selectedSource = related;
            }

            renderCategories();
            renderCards();
            filter.focus();
        });
    };

    function addDataSet(source, descriptor) {
        var query = app.design.query;
        var reference = {
            alias: designer.aliasFor(descriptor.displayName || descriptor.name, query.dataSets),
            source: source,
            dataSet: descriptor.name,
            displayName: descriptor.displayName || descriptor.name
        };
        var isFirst = query.dataSets.length === 0;

        query.dataSets.push(reference);
        app.render();

        app.loadSchema(reference).then(function () {
            // A new report starts filtered on the last 30 days of its first data set's main date.
            if (isFirst) {
                var dateFilter = designer.defaultDateFilter(query, {
                    alias: reference.alias,
                    defaultDateField: descriptor.defaultDateField
                }, (app.schemas[reference.alias] || {}).fields);

                if (dateFilter) {
                    query.filters.push(dateFilter);
                }
            }

            if (query.dataSets.length > 1) {
                var earlier = query.dataSets.slice(0, -1).map(function (dataSet) {
                    return { dataSet: dataSet, fields: (app.schemas[dataSet.alias] || {}).fields || [] };
                });
                var suggestion = designer.suggestJoin(earlier, { dataSet: reference, fields: app.schemas[reference.alias].fields || [] });
                var join = app.joinFor(reference.alias);
                var complete = join.conditions.some(function (condition) {
                    return condition.leftField && condition.rightField;
                });

                // The data set may already have a join (the person may have started one while its fields loaded), so
                // the suggestion fills it rather than adding a second join.
                if (!complete) {
                    join.conditions = suggestion ? [suggestion] : [{ leftField: '', rightField: '' }];
                }

                app.editJoin(reference.alias);
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
