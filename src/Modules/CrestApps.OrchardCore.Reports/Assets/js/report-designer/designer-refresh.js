/*
 * The refresh schedule of a view, in the Settings tab of the view builder. A live view runs every time a report reads
 * it; a scheduled view stores its result on the schedule, and reports read the stored result. For a saved scheduled
 * view the tab shows when the stored result was last refreshed, or why the last refresh failed, and refreshes it now.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner = root.CrestAppsReportDesigner || {};

    // The schedules a view offers, in minutes; 0 is live. A schedule set elsewhere, such as by a recipe, is added so it
    // stays selected. "app" provides the localized texts.
    designer.refreshOptions = function (app, current) {
        var options = [
            { value: 0, text: app.t('Live') },
            { value: 15, text: app.t('Every 15 minutes') },
            { value: 60, text: app.t('Every hour') },
            { value: 360, text: app.t('Every 6 hours') },
            { value: 1440, text: app.t('Every day') }
        ];
        var minutes = parseInt(current, 10) || 0;

        if (!options.some(function (option) { return option.value === minutes; })) {
            options.push({ value: minutes, text: app.t('Every {minutes} minutes').replace('{minutes}', String(minutes)) });
        }

        return options;
    };

    // Describes a view's stored result: when it was last refreshed and how many rows it holds, or why the last refresh
    // failed. "app" provides the localized texts and the time format.
    designer.describeSnapshot = function (app, snapshot) {
        if (snapshot && snapshot.lastError) {
            return {
                failed: true,
                text: app.t('The last refresh failed') +
                    (snapshot.lastErrorUtc ? ' (' + app.formatTime(snapshot.lastErrorUtc) + ')' : '') + ': ' + snapshot.lastError +
                    (snapshot.refreshedUtc ? ' ' + app.t('Reports read the rows of the last successful refresh.') : '')
            };
        }

        if (!snapshot || !snapshot.refreshedUtc) {
            return { failed: false, text: app.t('Not refreshed yet. Reports run the view until its first refresh.') };
        }

        return {
            failed: false,
            text: app.t('Last refreshed {time} ({rows} rows)')
                .replace('{time}', app.formatTime(snapshot.refreshedUtc))
                .replace('{rows}', String(snapshot.rowCount || 0))
        };
    };

    var app = designer.app;
    var ui = designer.ui;

    if (!app || !ui) {
        return;
    }

    var h = ui.h;

    function status() {
        var design = app.design;
        var described = designer.describeSnapshot(app, app.config.snapshot);
        var button = h('button', { type: 'button', className: 'btn btn-sm btn-outline-secondary', onclick: function () {
            button.disabled = true;
            ui.request(app.url('refreshView', { id: design.id }), { method: 'POST', body: {} }).then(function (result) {
                // Only a refresh that ran changes the stored result; a busy or live view keeps what it had.
                if (result.status === 'Refreshed' || result.status === 'Failed') {
                    app.config.snapshot = {
                        refreshedUtc: result.refreshedUtc,
                        rowCount: result.rowCount,
                        lastError: result.error,
                        lastErrorUtc: result.lastErrorUtc
                    };
                }

                if (result.error) {
                    app.showMessage('danger', app.t('The view could not be refreshed.'), [result.error]);
                }

                // The settings are not redrawn while something in them has the focus.
                button.blur();
                app.renderSettings();
            }).catch(function () {
                button.disabled = false;
                app.showMessage('danger', app.t('The view could not be refreshed.'));
            });
        } }, ui.icon('fa-rotate'), ' ', app.t('Refresh now'));

        return h('div', { className: 'd-flex flex-wrap align-items-center gap-2 mb-3' },
            h('span', { className: 'small ' + (described.failed ? 'text-danger' : 'text-muted') }, described.text),
            button);
    }

    app.renderRefresh = function () {
        var design = app.design;
        var current = parseInt(design.refreshIntervalMinutes, 10) || 0;

        if (app.savedRefreshIntervalMinutes === undefined) {
            app.savedRefreshIntervalMinutes = design.id ? current : 0;
        }

        var select = ui.select(designer.refreshOptions(app, current), current, {
            className: 'form-select',
            onchange: function (event) {
                design.refreshIntervalMinutes = parseInt(event.target.value, 10) || 0;
                app.touched();
            }
        });

        return h('div', { className: 'mb-3' },
            h('label', { className: 'form-label' }, app.t('Refresh')),
            select,
            h('div', { className: 'form-text' }, app.t('Live runs the view every time a report reads it. A schedule stores the result, so reports read it quickly; the data is as old as the last refresh.')),
            // The status is of the saved view, so it shows only once the view is saved with a schedule.
            design.id && app.savedRefreshIntervalMinutes > 0 ? h('div', { className: 'mt-2' }, status()) : null);
    };
})(typeof window !== 'undefined' ? window : globalThis);
