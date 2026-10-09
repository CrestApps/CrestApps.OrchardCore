/*
 * The report designer page: holds the design being edited, keeps the field catalog and the server's check of the query
 * current, refreshes the preview, and saves. The panels are built by designer-data.js, designer-canvas.js, and
 * designer-sharing.js, which add their render functions to the same app object.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner = root.CrestAppsReportDesigner || {};
    var ui = designer.ui;
    var h = ui.h;

    var app = designer.app = {
        config: null,
        design: null,
        schemas: {},
        plan: { fields: [], columns: [], errors: [] },
        functions: [],
        sources: [],
        selection: null,
        dirty: false,
        filterValues: null,
        elements: {}
    };

    app.t = function (key) {
        var text = app.config && app.config.text;

        return (text && text[key]) || key;
    };

    app.url = function (name, replacements) {
        var url = app.config.urls[name] || '';

        Object.keys(replacements || {}).forEach(function (key) {
            url = url.replace('__' + key + '__', encodeURIComponent(replacements[key]));
        });

        return url;
    };

    app.isView = function () {
        return app.config.mode === 'view';
    };

    // Every field the design can use, by key: data set fields from the schemas, then calculated fields and the row
    // count as the server last described them.
    app.fields = function () {
        var fields = {};

        (app.design.query.dataSets || []).forEach(function (dataSet) {
            var schema = app.schemas[dataSet.alias];

            (schema && schema.fields || []).forEach(function (field) {
                var key = dataSet.alias + '.' + field.name;

                fields[key] = {
                    key: key,
                    name: field.name,
                    label: field.displayName || field.name,
                    dataType: field.dataType,
                    group: field.group,
                    alias: dataSet.alias,
                    isIdentifier: !!field.isIdentifier,
                    isAggregate: false,
                    kind: 'DataSetField'
                };
            });
        });

        (app.plan.fields || []).forEach(function (field) {
            if (field.kind !== 'DataSetField') {
                fields[field.key] = {
                    key: field.key,
                    label: field.label,
                    dataType: field.dataType,
                    isAggregate: field.isAggregate,
                    kind: field.kind
                };
            }
        });

        if (!fields[designer.ROW_COUNT_FIELD]) {
            fields[designer.ROW_COUNT_FIELD] = {
                key: designer.ROW_COUNT_FIELD,
                label: app.t('Number of rows'),
                dataType: 'Integer',
                isAggregate: true,
                kind: 'AggregateCalculation'
            };
        }

        return fields;
    };

    // The result columns as the server last described them, falling back to the design while a check is pending.
    app.columns = function () {
        var described = {};
        var fields = app.fields();

        (app.plan.columns || []).forEach(function (column) {
            described[column.id] = column;
        });

        return (app.design.query.columns || []).map(function (column) {
            var field = fields[column.field];
            var known = described[column.id];

            return {
                id: column.id,
                label: known ? known.label : designer.columnLabel(column, field, app.config.aggregateLabels),
                dataType: known ? known.dataType : (field ? field.dataType : 'Text'),
                isMeasure: known ? known.isMeasure : designer.isMeasure(column, field)
            };
        });
    };

    app.changed = function (options) {
        app.dirty = true;
        app.renderStatus();

        if (!options || options.render !== false) {
            app.render();
        }

        app.schedulePlan();
        app.schedulePreview();
    };

    app.loadSchema = function (dataSet) {
        var url = app.url('schema') + '?source=' + encodeURIComponent(dataSet.source) + '&dataSet=' + encodeURIComponent(dataSet.dataSet);

        return ui.request(url).then(function (schema) {
            if (schema && schema.errors) {
                app.schemas[dataSet.alias] = { fields: [], errors: schema.errors };
            } else {
                app.schemas[dataSet.alias] = schema || { fields: [] };
            }
        }).catch(function () {
            app.schemas[dataSet.alias] = { fields: [], errors: [app.t('This data set is not available to you.')] };
        });
    };

    app.refreshPlan = function () {
        return ui.request(app.url('plan'), { body: app.design.query }).then(function (plan) {
            app.plan = plan || { fields: [], columns: [], errors: [] };
            app.renderIssues();
            app.renderDataPane();
            app.renderProperties();
            app.renderVisuals();
        }).catch(function () {
            app.plan.errors = [app.t('The design could not be checked. Try again.')];
            app.renderIssues();
        });
    };

    app.refreshPreview = function () {
        var target = app.elements.preview;

        if (!target) {
            return;
        }

        if (!(app.design.query.dataSets || []).length || !(app.design.query.columns || []).length) {
            ui.clear(target);
            target.appendChild(h('div', { className: 'report-designer-empty text-muted' }, app.t('Add a data set, then drag fields to Columns to see a preview.')));

            return;
        }

        target.classList.add('is-loading');

        var payload = app.payload();

        payload.filterValues = app.filterValues;

        ui.request(app.url('preview') + (app.isView() ? '?view=true' : ''), { body: payload, html: true }).then(function (html) {
            target.classList.remove('is-loading');
            target.innerHTML = html;
            app.bindPreview(target);

            if (root.CrestAppsReportCharts) {
                root.CrestAppsReportCharts.render(target);
            }
        }).catch(function () {
            target.classList.remove('is-loading');
            ui.clear(target);
            target.appendChild(h('div', { className: 'alert alert-danger' }, app.t('The preview could not be loaded.')));
        });
    };

    // The preview renders the exposed filters as a form; submitting it re-runs the preview with those values.
    app.bindPreview = function (target) {
        var form = target.querySelector('form[data-preview-filters]');

        if (!form) {
            return;
        }

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            app.filterValues = designer.filterValuesFromEntries(Array.from(new root.FormData(form).entries()));
            app.refreshPreview();
        });
    };

    app.payload = function () {
        var design = app.design;

        return {
            id: design.id,
            displayText: design.displayText,
            description: design.description,
            category: design.category,
            query: design.query,
            visuals: design.visuals,
            showInAdminMenu: design.showInAdminMenu,
            allowExport: design.allowExport,
            sharedUserNames: design.sharedUserNames,
            sharedRoles: design.sharedRoles
        };
    };

    app.save = function () {
        var button = app.elements.saveButton;

        button.disabled = true;

        return ui.request(app.url(app.isView() ? 'saveView' : 'save'), { body: app.payload() }).then(function (result) {
            button.disabled = false;

            if (!result.saved) {
                app.showMessage('danger', app.t('The design was not saved.'), result.errors);

                return;
            }

            var isNew = !app.design.id;

            app.design.id = result.id;
            app.dirty = false;
            app.renderStatus();

            if (result.warnings && result.warnings.length) {
                app.showMessage('warning', app.t('Saved. Fix these problems before people run it:'), result.warnings);
            } else {
                app.showMessage('success', app.t('Saved.'));
            }

            if (isNew && result.editUrl) {
                root.history.replaceState(null, '', result.editUrl);
                app.config.runUrl = result.runUrl;
                app.render();
            }
        }).catch(function () {
            button.disabled = false;
            app.showMessage('danger', app.t('The design was not saved.'));
        });
    };

    app.showMessage = function (kind, title, items) {
        var target = app.elements.messages;

        ui.clear(target);
        target.appendChild(h('div', { className: 'alert alert-' + kind + ' alert-dismissible fade show', role: 'alert' },
            h('div', { className: 'fw-semibold' }, title),
            items && items.length ? h('ul', { className: 'mb-0 mt-1' }, items.map(function (item) {
                return h('li', null, item);
            })) : null,
            h('button', { type: 'button', className: 'btn-close', 'data-bs-dismiss': 'alert', 'aria-label': app.t('Close') })));
    };

    app.renderStatus = function () {
        var status = app.elements.status;

        if (status) {
            status.textContent = app.dirty ? app.t('Unsaved changes') : '';
        }

        if (app.elements.runLink) {
            app.elements.runLink.classList.toggle('d-none', !app.design.id || app.isView());
            app.elements.runLink.href = app.design.id ? app.url('run', { id: app.design.id }) : '#';
        }
    };

    app.renderIssues = function () {
        var target = app.elements.issues;
        var errors = (app.plan.errors || []).slice();

        Object.keys(app.schemas).forEach(function (alias) {
            (app.schemas[alias].errors || []).forEach(function (error) {
                errors.push(error);
            });
        });

        ui.clear(target);

        if (errors.length && (app.design.query.dataSets || []).length) {
            target.appendChild(h('div', { className: 'alert alert-warning py-2 mb-2 small' },
                h('div', { className: 'fw-semibold' }, app.t('This design has problems:')),
                h('ul', { className: 'mb-0' }, errors.map(function (error) {
                    return h('li', null, error);
                }))));
        }
    };

    app.select = function (kind, id) {
        app.selection = kind ? { kind: kind, id: id } : null;
        app.render();
    };

    app.render = function () {
        app.renderDataPane();
        app.renderShelves();
        app.renderProperties();
        app.renderVisuals();
        app.renderSettings();
        app.renderSharing();
        app.renderStatus();
    };

    app.buildLayout = function (container) {
        var e = app.elements;

        e.title = h('input', {
            type: 'text',
            className: 'form-control form-control-lg report-designer-title',
            value: app.design.displayText || '',
            placeholder: app.isView() ? app.t('View name') : app.t('Report title'),
            'aria-label': app.isView() ? app.t('View name') : app.t('Report title'),
            maxlength: '200',
            oninput: function (event) {
                app.design.displayText = event.target.value;
                app.dirty = true;
                app.renderStatus();
            }
        });
        e.status = h('span', { className: 'text-muted small' });
        e.saveButton = h('button', { type: 'button', className: 'btn btn-primary', onclick: app.save }, ui.icon('fa-floppy-disk'), ' ', app.t('Save'));
        e.runLink = h('a', { className: 'btn btn-outline-secondary d-none', href: '#' }, ui.icon('fa-play'), ' ', app.t('Run report'));
        e.messages = h('div');
        e.issues = h('div');
        e.dataPane = h('div', { className: 'report-designer-data' });
        e.shelves = h('div', { className: 'report-designer-shelves' });
        e.properties = h('div', { className: 'report-designer-properties' });
        e.visuals = h('div', { className: 'report-designer-visuals' });
        e.preview = h('div', { className: 'report-designer-preview' });
        e.settings = h('div', { className: 'report-designer-settings' });
        e.sharing = h('div', { className: 'report-designer-sharing' });

        var tabs = [
            { id: 'design', label: app.t('Design'), body: h('div', { className: 'row g-3' },
                h('div', { className: 'col-12 col-lg-3' }, e.dataPane),
                h('div', { className: 'col-12 col-lg-6' }, e.issues, e.shelves, h('div', { className: 'd-flex align-items-center justify-content-between mt-3 mb-2' },
                    h('h2', { className: 'h6 mb-0' }, app.t('Preview')),
                    h('button', { type: 'button', className: 'btn btn-sm btn-outline-secondary', onclick: function () {
                        app.refreshPreview();
                    } }, ui.icon('fa-rotate'), ' ', app.t('Refresh'))), e.preview),
                h('div', { className: 'col-12 col-lg-3' }, e.properties, app.isView() ? null : e.visuals)) },
            { id: 'settings', label: app.t('Settings'), body: e.settings }
        ];

        if (!app.isView()) {
            tabs.push({ id: 'sharing', label: app.t('Sharing'), body: e.sharing });
        }

        var nav = h('ul', { className: 'nav nav-tabs mb-3', role: 'tablist' });
        var panes = h('div', { className: 'tab-content' });

        tabs.forEach(function (tab, index) {
            var paneId = 'report-designer-' + tab.id;

            nav.appendChild(h('li', { className: 'nav-item', role: 'presentation' },
                h('button', {
                    type: 'button',
                    className: 'nav-link' + (index === 0 ? ' active' : ''),
                    'data-bs-toggle': 'tab',
                    'data-bs-target': '#' + paneId,
                    role: 'tab',
                    'aria-controls': paneId,
                    'aria-selected': index === 0 ? 'true' : 'false'
                }, tab.label)));
            panes.appendChild(h('div', { className: 'tab-pane fade' + (index === 0 ? ' show active' : ''), id: paneId, role: 'tabpanel' }, tab.body));
        });

        ui.append(container, [
            h('div', { className: 'report-designer-toolbar card mb-3' }, h('div', { className: 'card-body d-flex flex-wrap gap-2 align-items-center' },
                h('div', { className: 'flex-grow-1' }, e.title),
                e.status,
                e.runLink,
                e.saveButton)),
            e.messages,
            nav,
            panes
        ]);
    };

    app.start = function (container) {
        var configElement = root.document.getElementById(container.dataset.config);

        app.config = JSON.parse(configElement.textContent);
        app.design = app.config.payload;
        app.design.query = app.design.query || {};

        ['dataSets', 'joins', 'calculatedFields', 'filters', 'columns', 'sorts'].forEach(function (name) {
            app.design.query[name] = app.design.query[name] || [];
        });

        app.design.visuals = app.design.visuals || [];
        app.design.sharedUserNames = app.design.sharedUserNames || [];
        app.design.sharedRoles = app.design.sharedRoles || [];
        app.schedulePlan = ui.debounce(app.refreshPlan, 350);
        app.schedulePreview = ui.debounce(app.refreshPreview, 900);

        app.buildLayout(container);
        app.render();

        Promise.all([
            ui.request(app.url('sources')).then(function (sources) {
                app.sources = sources || [];
            }),
            ui.request(app.url('functions')).then(function (functions) {
                app.functions = functions || [];
            })
        ].concat(app.design.query.dataSets.map(app.loadSchema))).catch(function () {
            return null;
        }).then(function () {
            app.render();

            return app.refreshPlan();
        }).then(app.refreshPreview);

        root.addEventListener('beforeunload', function (event) {
            if (app.dirty) {
                event.preventDefault();
                event.returnValue = '';
            }
        });

        root.document.addEventListener('keydown', function (event) {
            if ((event.ctrlKey || event.metaKey) && event.key === 's') {
                event.preventDefault();
                app.save();
            }
        });
    };

    var boot = function () {
        var container = root.document.querySelector('[data-report-designer]');

        if (container && !container.dataset.started) {
            container.dataset.started = 'true';
            app.start(container);
        }
    };

    if (root.document && root.document.readyState === 'loading') {
        root.document.addEventListener('DOMContentLoaded', boot);
    } else if (root.document) {
        root.setTimeout(boot, 0);
    }
})(typeof window !== 'undefined' ? window : globalThis);
