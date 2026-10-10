/*
 * The report builder page: holds the design being edited, keeps the field catalog and the server's check of the query
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

    // The address of a builder endpoint or page, from the server's root-relative URLs.
    app.url = function (name, replacements) {
        var url = app.config.urls[name] || '';

        Object.keys(replacements || {}).forEach(function (key) {
            url = url.replace('__' + key + '__', encodeURIComponent(replacements[key]));
        });

        return ui.localUrl(url);
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
        app.queueAutosave();
    };

    // Records an edit that needs no re-render, such as typing in a text box, and saves it a moment later.
    app.touched = function () {
        app.dirty = true;
        app.renderStatus();
        app.queueAutosave();
    };

    // Saves the change into the report's draft a moment later, when the report exists (see designer-history.js).
    app.queueAutosave = function () {
        if (app.scheduleAutosave && app.canAutosave && app.canAutosave()) {
            app.scheduleAutosave();
        }
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
            sharedRoles: design.sharedRoles,
            refreshIntervalMinutes: app.isView() ? (parseInt(design.refreshIntervalMinutes, 10) || 0) : undefined
        };
    };

    // Publishes a report (see designer-history.js), or saves a view.
    app.save = function () {
        if (!app.isView() && app.publish) {
            return app.publish();
        }

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

            // Saving a view changes its schedule, and a changed query drops its stored result.
            if (app.isView()) {
                app.savedRefreshIntervalMinutes = parseInt(app.design.refreshIntervalMinutes, 10) || 0;
                app.config.snapshot = result.snapshot || null;
                app.renderSettings();
            }

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

        if (kind === 'success') {
            root.setTimeout(function () {
                ui.clear(target);
            }, 3000);
        }
    };

    app.renderStatus = function () {
        var status = app.elements.status;

        if (status) {
            status.textContent = app.historyStatus ? app.historyStatus() : (app.dirty ? app.t('Unsaved changes') : '');
        }

        if (app.elements.runLink) {
            // A report that was never published has nothing to run yet.
            app.elements.runLink.classList.toggle('d-none', !app.design.id || app.isView() || !!(app.history && app.history.unpublished));
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

    // Opens a join in the Properties pane, expanding the pane when it is collapsed.
    app.editJoin = function (alias) {
        app.selection = { kind: 'join', id: alias };

        if (app.layout.side) {
            app.toggle('side', false);
        }

        app.render();
    };

    app.select = function (kind, id) {
        app.selection = kind ? { kind: kind, id: id } : null;
        app.render();
    };

    app.render = function () {
        app.renderModel();
        app.renderDataPane();
        app.renderShelves();
        app.renderProperties();
        app.renderVisuals();
        app.renderSettings();
        app.renderSharing();
        app.renderStatus();
    };

    var STORAGE_KEY = 'crestapps-report-designer-layout';

    function readLayout() {
        try {
            return JSON.parse(root.localStorage.getItem(STORAGE_KEY)) || {};
        } catch (e) {
            return {};
        }
    }

    function writeLayout(layout) {
        try {
            root.localStorage.setItem(STORAGE_KEY, JSON.stringify(layout));
        } catch (e) {
            // Remembering the layout is a convenience; private windows may refuse it.
        }
    }

    app.layout = { data: false, side: false, shelves: false };

    // Collapses or expands a part of the workspace and remembers the choice for the next visit.
    app.toggle = function (part, collapsed) {
        app.layout[part] = typeof collapsed === 'boolean' ? collapsed : !app.layout[part];
        writeLayout(app.layout);
        app.applyLayout();
    };

    app.applyLayout = function () {
        var e = app.elements;

        e.workspace.classList.toggle('is-data-collapsed', !!app.layout.data);
        e.workspace.classList.toggle('is-side-collapsed', !!app.layout.side);
        e.workspace.classList.toggle('is-shelves-collapsed', !!app.layout.shelves);
        e.dataToggle.setAttribute('aria-expanded', app.layout.data ? 'false' : 'true');
        e.sideToggle.setAttribute('aria-expanded', app.layout.side ? 'false' : 'true');
        e.shelvesToggle.setAttribute('aria-expanded', app.layout.shelves ? 'false' : 'true');
        e.shelvesToggle.firstChild.className = 'fa-solid ' + (app.layout.shelves ? 'fa-chevron-down' : 'fa-chevron-up');
    };

    // Sizes the designer to the window so the page itself never scrolls; each pane scrolls on its own instead. On a
    // narrow screen the panes stack and the page scrolls normally.
    app.fit = function () {
        var container = app.elements.root;

        if (!container) {
            return;
        }

        if (root.innerWidth < 992) {
            container.style.height = '';

            return;
        }

        var top = container.getBoundingClientRect().top + root.scrollY;
        var height = Math.max(420, root.innerHeight - top);

        container.style.height = height + 'px';

        var overflow = root.document.documentElement.scrollHeight - root.innerHeight;

        if (overflow > 0) {
            container.style.height = Math.max(420, height - overflow) + 'px';
        }
    };

    function paneHeader(title, toggle, extra) {
        return h('div', { className: 'rd-pane-header d-flex align-items-center gap-2' },
            h('span', { className: 'rd-pane-title flex-grow-1 text-truncate' }, title),
            extra || null,
            toggle || null);
    }

    function collapseButton(part, label, icon) {
        return h('button', {
            type: 'button',
            className: 'btn btn-sm btn-link text-reset p-0 rd-collapse',
            title: label,
            'aria-label': label,
            onclick: function () {
                app.toggle(part);
            }
        }, h('i', { className: 'fa-solid ' + icon, 'aria-hidden': 'true' }));
    }

    function rail(part, label, icon) {
        return h('button', {
            type: 'button',
            className: 'rd-rail btn btn-link text-reset',
            title: label,
            'aria-label': label,
            onclick: function () {
                app.toggle(part, false);
            }
        }, h('i', { className: 'fa-solid ' + icon, 'aria-hidden': 'true' }), h('span', { className: 'rd-rail-label' }, label));
    }

    app.buildLayout = function (container) {
        var e = app.elements;

        e.root = container;
        e.status = h('span', { className: 'text-muted small text-nowrap' });
        e.saveButton = app.isView()
            ? h('button', { type: 'button', className: 'btn btn-sm btn-primary text-nowrap', onclick: app.save }, ui.icon('fa-floppy-disk'), ' ', app.t('Save'))
            : h('button', { type: 'button', className: 'btn btn-sm btn-primary text-nowrap', onclick: app.save, title: app.t('Publish (Ctrl+S)') }, ui.icon('fa-cloud-arrow-up'), ' ', app.t('Publish'));
        e.versionsButton = app.isView() ? null : h('button', {
            type: 'button',
            className: 'btn btn-sm btn-outline-secondary text-nowrap d-none',
            onclick: function () {
                app.openVersions();
            }
        }, ui.icon('fa-clock-rotate-left'), ' ', app.t('Versions'));
        e.presence = h('span', { className: 'rd-presence d-none' });
        e.historyBar = h('div', { className: 'rd-history', 'aria-live': 'polite' });
        e.runLink = h('a', { className: 'btn btn-sm btn-outline-secondary text-nowrap d-none', href: '#' }, ui.icon('fa-play'), ' ', app.t('Run report'));
        e.messages = h('div', { className: 'report-designer-messages' });
        e.issues = h('div');
        e.dataPane = h('div', { className: 'report-designer-data' });
        e.shelves = h('div', { className: 'report-designer-shelves' });
        e.properties = h('div', { className: 'report-designer-properties' });
        e.visuals = h('div', { className: 'report-designer-visuals' });
        e.preview = h('div', { className: 'report-designer-preview' });
        e.settings = h('div', { className: 'report-designer-settings' });
        e.sharing = h('div', { className: 'report-designer-sharing' });
        e.model = h('div', { className: 'rd-model' });
        e.dataToggle = collapseButton('data', app.t('Collapse the data pane'), 'fa-angles-left');
        e.sideToggle = collapseButton('side', app.t('Collapse the properties pane'), 'fa-angles-right');
        e.shelvesToggle = collapseButton('shelves', app.t('Collapse the columns and filters'), 'fa-chevron-up');

        var addDataSet = h('button', { type: 'button', className: 'btn btn-sm btn-primary text-nowrap', onclick: app.openAddDataSet }, ui.icon('fa-plus'), ' ', app.t('Add data set'));
        var refresh = h('button', {
            type: 'button',
            className: 'btn btn-sm btn-outline-secondary text-nowrap',
            onclick: function () {
                app.refreshPreview();
            }
        }, ui.icon('fa-rotate'), ' ', app.t('Refresh'));
        var sideTitle = app.isView() ? app.t('Properties') : app.t('Properties and visuals');

        e.workspace = h('div', { className: 'rd-workspace' },
            h('aside', { className: 'rd-pane rd-pane-data', 'aria-label': app.t('Data') },
                rail('data', app.t('Data'), 'fa-database'),
                h('div', { className: 'rd-pane-content' },
                    paneHeader(app.t('Data'), e.dataToggle, addDataSet),
                    h('div', { className: 'rd-pane-scroll' }, e.dataPane))),
            h('section', { className: 'rd-center', 'aria-label': app.t('Design') },
                h('div', { className: 'rd-shelves-panel' },
                    paneHeader(app.t('Columns and filters'), e.shelvesToggle),
                    h('div', { className: 'rd-shelves-body' }, e.issues, e.shelves)),
                h('div', { className: 'rd-preview-panel' },
                    paneHeader(app.t('Preview'), null, refresh),
                    h('div', { className: 'rd-pane-scroll rd-preview-scroll' }, e.preview))),
            h('aside', { className: 'rd-pane rd-pane-side', 'aria-label': sideTitle },
                rail('side', sideTitle, 'fa-sliders'),
                h('div', { className: 'rd-pane-content' },
                    paneHeader(sideTitle, e.sideToggle),
                    h('div', { className: 'rd-pane-scroll' }, e.properties, app.isView() ? null : e.visuals))));

        // Settings (the title and description) come first; the builder opens on Design, where the work happens.
        var tabs = [
            { id: 'settings', label: app.t('Settings'), icon: 'fa-gear', body: h('div', { className: 'rd-tab-scroll' }, e.settings) },
            { id: 'design', label: app.t('Design'), icon: 'fa-pen-ruler', body: e.workspace, className: 'rd-tab-design', active: true },
            { id: 'model', label: app.t('Data model'), icon: 'fa-diagram-project', body: e.model, className: 'rd-tab-model' }
        ];

        if (!app.isView()) {
            tabs.push({ id: 'sharing', label: app.t('Sharing'), icon: 'fa-share-nodes', body: h('div', { className: 'rd-tab-scroll' }, e.sharing) });
        }

        var nav = h('ul', { className: 'nav nav-tabs card-header-tabs flex-nowrap', role: 'tablist' });
        var panes = h('div', { className: 'tab-content rd-tabs-content' });

        tabs.forEach(function (tab) {
            var paneId = 'report-designer-' + tab.id;

            nav.appendChild(h('li', { className: 'nav-item', role: 'presentation' },
                h('button', {
                    type: 'button',
                    className: 'nav-link text-nowrap' + (tab.active ? ' active' : ''),
                    'data-bs-toggle': 'tab',
                    'data-bs-target': '#' + paneId,
                    role: 'tab',
                    'aria-controls': paneId,
                    'aria-selected': tab.active ? 'true' : 'false'
                }, ui.icon(tab.icon), ' ', tab.label)));
            panes.appendChild(h('div', {
                className: 'tab-pane fade ' + (tab.className || '') + (tab.active ? ' show active' : ''),
                id: paneId,
                role: 'tabpanel'
            }, tab.body));
        });

        container.classList.add('card');
        ui.append(container, [
            h('div', { className: 'card-header rd-header' },
                nav,
                h('div', { className: 'rd-header-tools' }, e.status, e.presence, e.runLink, e.versionsButton, e.saveButton)),
            h('div', { className: 'card-body p-0 rd-body' }, e.historyBar, e.messages, panes)
        ]);

        var saved = readLayout();

        app.layout.data = !!saved.data;
        app.layout.side = !!saved.side;
        app.layout.shelves = !!saved.shelves;
        app.applyLayout();
        app.fit();
        root.addEventListener('resize', ui.debounce(function () {
            app.fit();
            app.drawModelLines();
        }, 100));
        nav.addEventListener('shown.bs.tab', function () {
            app.drawModelLines();
        });
    };

    app.start = function (container) {
        var configElement = root.document.getElementById(container.dataset.config);

        app.config = JSON.parse(configElement.textContent);
        app.design = app.config.payload;
        app.design.query = app.design.query || {};

        ['dataSets', 'joins', 'calculatedFields', 'filters', 'columns', 'sorts'].forEach(function (name) {
            app.design.query[name] = app.design.query[name] || [];
        });

        // A design saved with two joins for one data set cannot run; merge them.
        designer.mergeJoins(app.design.query);

        app.design.visuals = app.design.visuals || [];
        app.design.sharedUserNames = app.design.sharedUserNames || [];
        app.design.sharedRoles = app.design.sharedRoles || [];
        app.schedulePlan = ui.debounce(app.refreshPlan, 350);
        app.schedulePreview = ui.debounce(app.refreshPreview, 900);

        app.buildLayout(container);
        app.render();

        if (app.startHistory) {
            app.startHistory();
        }

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

        // Unsaved changes of a report are sent on their way as the page unloads; only what cannot be (a view) asks first.
        root.addEventListener('beforeunload', function (event) {
            var saved = app.flushOnUnload ? app.flushOnUnload() : !app.dirty;

            if (!saved) {
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
