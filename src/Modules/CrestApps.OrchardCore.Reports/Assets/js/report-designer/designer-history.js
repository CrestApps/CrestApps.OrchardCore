/*
 * Autosave, versions, and live presence of the report builder. Changes to a saved report are saved into its draft a
 * moment after each edit; Publish makes the draft the report that runs. Every save states the revision it is based on,
 * and the server refuses it when someone else changed the report since, so nobody overwrites another person's work
 * without choosing to. When OrchardCore.SignalR is enabled, the page also shows who else has the report open and
 * offers to reload when they change it.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner = root.CrestAppsReportDesigner || {};

    // Decides what a change announced by the hub means for this page: "ignore" a change this page already has (its own
    // saves, or older ones), "deleted" when the report is gone, otherwise "changed".
    designer.remoteChangeAction = function (message, revision) {
        if (!message) {
            return 'ignore';
        }

        if (message.kind === 'Deleted') {
            return 'deleted';
        }

        return Number(message.revision) > Number(revision || 0) ? 'changed' : 'ignore';
    };

    // Connects to the reports hub and follows one report: its changes and who else has it open. "factory" builds the
    // connection; by default it uses the SignalR client the page loaded. Resolves to null when real time is not
    // available, so every failure here is quiet.
    designer.startRealtime = function (options) {
        var factory = options.factory || function (url) {
            var signalR = root.signalR;

            return signalR ? new signalR.HubConnectionBuilder().withUrl(url).withAutomaticReconnect().build() : null;
        };
        var connection = options.url ? factory(options.url) : null;

        if (!connection) {
            return Promise.resolve(null);
        }

        var others = {};
        var publish = function () {
            if (options.onPresence) {
                options.onPresence(Object.keys(others).map(function (id) {
                    return others[id];
                }));
            }
        };

        connection.on('ReportDesignChanged', function (message) {
            if (options.onChange) {
                options.onChange(message);
            }
        });

        // Someone arrived: they learn who is here from each of the others.
        connection.on('PresenceJoined', function (presence) {
            others[presence.connectionId] = presence;
            publish();
            connection.invoke('AnnouncePresence', options.designId, presence.connectionId).catch(function () {
                return null;
            });
        });

        connection.on('PresenceHere', function (presence) {
            others[presence.connectionId] = presence;
            publish();
        });

        connection.on('PresenceLeft', function (connectionId) {
            if (others[connectionId]) {
                delete others[connectionId];
                publish();
            }
        });

        var subscribe = function () {
            return connection.invoke('Subscribe', options.designId);
        };

        // A reconnected connection has a new id and lost its groups: subscribe again, and the others announce
        // themselves again.
        connection.onreconnected(function () {
            others = {};
            publish();
            subscribe().catch(function () {
                return null;
            });
        });

        return connection.start().then(subscribe).then(function () {
            return {
                stop: function () {
                    return connection.stop().catch(function () {
                        return null;
                    });
                }
            };
        }).catch(function () {
            return null;
        });
    };

    // The names of the people in a presence list, once each, in the order they arrived.
    designer.presenceNames = function (presence) {
        var names = [];

        (presence || []).forEach(function (person) {
            var name = person.userName || person.userId;

            if (name && names.indexOf(name) < 0) {
                names.push(name);
            }
        });

        return names;
    };

    var app = designer.app;

    if (!app) {
        return;
    }

    var ui = designer.ui;
    var h = ui.h;

    app.history = {
        revision: 0,
        hasDraft: false,
        unpublished: false,
        modifiedBy: null,
        modifiedUtc: null,
        state: 'idle',
        saving: null,
        pending: false,
        force: false,
        conflict: null,
        remote: null,
        presence: [],
        queued: []
    };

    // Whether changes are saved automatically: every report. A new report is saved as a draft from its first change.
    app.canAutosave = function () {
        return !app.isView();
    };

    // Whether the report exists on the server: published, or saved as a draft.
    app.isSaved = function () {
        return !!app.design.id;
    };

    // The request that saves the current changes: into the report's draft, or, for a report that was never saved,
    // into a new draft.
    app.draftRequest = function (options) {
        var history = app.history;
        var body = app.payload();

        body.revision = history.revision;
        body.force = history.force;

        var url = app.isSaved() ? app.url('draft', { id: app.design.id }) : app.url('newDraft');

        // A save still on its way when the page unloads (a refresh right after a change) must not be dropped; browsers
        // let such requests outlive the page up to 64 KB.
        var settings = { body: body, keepalive: JSON.stringify(body).length < 60000 };

        return ui.request(url, Object.assign(settings, options || {}));
    };

    // Called when the first save of a new report created its draft: the page now edits that report.
    app.adoptDraft = function (result) {
        app.design.id = result.id;
        app.history.unpublished = true;

        if (result.editUrl) {
            root.history.replaceState(null, '', result.editUrl);
        }

        app.render();
        app.startLive();
    };

    app.formatTime = function (value) {
        if (!value) {
            return '';
        }

        var date = new Date(value);

        return isNaN(date.getTime()) ? '' : date.toLocaleString();
    };

    app.autosave = function () {
        var history = app.history;

        if (!app.canAutosave() || !app.dirty || history.conflict || history.remote === 'deleted') {
            return Promise.resolve();
        }

        if (history.saving) {
            history.pending = true;

            return history.saving;
        }

        var isNew = !app.isSaved();

        app.dirty = false;
        history.state = 'saving';
        app.renderHistory();

        history.saving = app.draftRequest().then(function (result) {
            if (isNew) {
                app.adoptDraft(result);
            }

            history.revision = result.revision;
            history.hasDraft = true;
            history.modifiedBy = result.modifiedBy;
            history.modifiedUtc = result.modifiedUtc;
            history.force = false;
            history.state = 'saved';
        }).catch(function (error) {
            app.dirty = true;

            if (error && error.status === 409) {
                history.state = 'conflict';
                history.conflict = error.body || {};
            } else {
                history.state = 'error';
            }
        }).then(function () {
            history.saving = null;
            app.flushRemoteChanges();
            app.renderHistory();

            if (history.pending) {
                history.pending = false;

                return app.autosave();
            }

            return null;
        });

        return history.saving;
    };

    app.scheduleAutosave = ui.debounce(function () {
        app.autosave();
    }, 800);

    // Sends the changes not saved yet while the page unloads. Answers false when that is not possible (a view, or a
    // design too large for a request that outlives the page), so the page asks before leaving.
    app.flushOnUnload = function () {
        if (!app.dirty) {
            return true;
        }

        if (!app.canAutosave() || app.history.conflict || app.history.remote === 'deleted') {
            return false;
        }

        // Browsers limit requests that outlive the page to 64 KB.
        if (JSON.stringify(app.payload()).length > 60000) {
            return false;
        }

        app.scheduleAutosave.cancel();
        app.draftRequest({ keepalive: true }).catch(function () {
            return null;
        });

        return true;
    };

    // Publishes the report. A new report is created and gets its first version.
    app.publish = function () {
        var history = app.history;
        var button = app.elements.saveButton;

        app.scheduleAutosave.cancel();
        button.disabled = true;

        var body = app.payload();

        body.revision = history.revision;
        body.force = history.force;

        return ui.request(app.url('save'), { body: body }).then(function (result) {
            button.disabled = false;

            if (!result.saved) {
                app.showMessage('danger', app.t('The report was not published.'), result.errors);

                return;
            }

            var isNew = !app.design.id;
            var wasUnpublished = history.unpublished;

            app.design.id = result.id;
            app.dirty = false;
            history.revision = result.revision;
            history.hasDraft = false;
            history.unpublished = false;
            history.force = false;
            history.conflict = null;
            history.remote = null;
            history.state = 'published';

            var title = result.versionNumber
                ? app.t('Published as version') + ' ' + result.versionNumber + '.'
                : app.t('Published. Nothing changed since the last version.');

            if (result.warnings && result.warnings.length) {
                app.showMessage('warning', title + ' ' + app.t('Fix these problems before people run it:'), result.warnings);
            } else {
                app.showMessage('success', title);
            }

            if (isNew && result.editUrl) {
                root.history.replaceState(null, '', result.editUrl);
                app.config.runUrl = result.runUrl;
                app.startLive();
            }

            if (isNew || wasUnpublished) {
                app.render();
            }

            app.renderHistory();
        }).catch(function (error) {
            button.disabled = false;

            if (error && error.status === 409) {
                history.state = 'conflict';
                history.conflict = error.body || {};
                app.renderHistory();

                return;
            }

            app.showMessage('danger', app.t('The report was not published.'));
        });
    };

    app.discardDraft = function () {
        var question = app.history.unpublished
            ? app.t('Delete this report? It was never published, so nothing else is kept.')
            : app.t('Discard the unpublished changes? The report goes back to its published version.');

        if (!root.confirm(question)) {
            return;
        }

        app.scheduleAutosave.cancel();

        ui.request(app.url('discardDraft', { id: app.design.id }), { body: { revision: app.history.revision, force: app.history.force } }).then(function (result) {
            if (result && result.deleted) {
                app.dirty = false;
                root.location.href = ui.localUrl(result.listUrl || app.url('list'));

                return;
            }

            app.reload();
        }).catch(function (error) {
            if (error && error.status === 409) {
                app.history.state = 'conflict';
                app.history.conflict = error.body || {};
                app.renderHistory();
            } else {
                app.showMessage('danger', app.t('The changes could not be discarded.'));
            }
        });
    };

    // Reloads the page without the unsaved-changes prompt.
    app.reload = function () {
        app.dirty = false;
        root.location.reload();
    };

    // Keeps this page's changes over someone else's: the next save overwrites theirs.
    app.overwrite = function () {
        app.history.force = true;
        app.history.conflict = null;
        app.history.remote = null;
        app.history.state = 'idle';
        app.dirty = true;
        app.renderHistory();
        app.autosave();
    };

    app.openVersions = function () {
        var list = h('div', { className: 'list-group' });
        var status = h('div', { className: 'text-muted small' }, app.t('Loading…'));
        var modal = ui.modal(app.t('Versions'), h('div', null,
            h('p', { className: 'text-muted small' }, app.t('Publishing keeps a version when the report changed. Restoring a version copies it into the draft, where you can check it and publish it.')),
            status,
            list), null, 'modal-lg');

        ui.request(app.url('versions', { id: app.design.id })).then(function (versions) {
            status.textContent = versions.length ? '' : app.t('This report has no versions yet. Publish it to keep one.');

            versions.forEach(function (version) {
                list.appendChild(h('div', { className: 'list-group-item d-flex align-items-center gap-3 flex-wrap' },
                    h('span', { className: 'badge text-bg-secondary' }, app.t('Version') + ' ' + version.number),
                    h('div', { className: 'flex-grow-1' },
                        h('div', { className: 'fw-semibold' }, version.displayText || app.t('Untitled report')),
                        h('div', { className: 'text-muted small' },
                            app.formatTime(version.createdUtc),
                            version.createdByName ? ' · ' + version.createdByName : '',
                            version.restoredFrom ? ' · ' + app.t('Restored from version') + ' ' + version.restoredFrom : '')),
                    version.isCurrent ? h('span', { className: 'badge text-bg-success' }, app.t('Published')) : null,
                    h('button', {
                        type: 'button',
                        className: 'btn btn-sm btn-outline-secondary',
                        onclick: function () {
                            app.previewVersion(version.number);
                        }
                    }, ui.icon('fa-eye'), ' ', app.t('Preview')),
                    version.isCurrent ? null : h('button', {
                        type: 'button',
                        className: 'btn btn-sm btn-outline-primary',
                        onclick: function () {
                            modal.close();
                            app.restoreVersion(version.number);
                        }
                    }, ui.icon('fa-clock-rotate-left'), ' ', app.t('Restore'))));
            });
        }).catch(function () {
            status.textContent = app.t('The versions could not be loaded.');
        });
    };

    app.previewVersion = function (number) {
        var body = h('div', { className: 'report-designer-preview' }, h('div', { className: 'text-muted small' }, app.t('Loading…')));

        ui.modal(app.t('Version') + ' ' + number, body, null, 'modal-xl');

        ui.request(app.url('version', { id: app.design.id, number: number })).then(function (version) {
            return ui.request(app.url('preview'), { body: version.design, html: true });
        }).then(function (html) {
            body.innerHTML = html;
            ui.initPickers(body);

            if (root.CrestAppsReportCharts) {
                root.CrestAppsReportCharts.render(body);
            }
        }).catch(function () {
            ui.clear(body);
            body.appendChild(h('div', { className: 'alert alert-danger' }, app.t('The preview could not be loaded.')));
        });
    };

    app.restoreVersion = function (number) {
        var message = app.t('Restore version') + ' ' + number + '? ' + app.t('It replaces the unpublished changes; publish it to make it the report people run.');

        if (!root.confirm(message)) {
            return;
        }

        app.scheduleAutosave.cancel();

        ui.request(app.url('restore', { id: app.design.id, number: number }), { body: { revision: app.history.revision, force: app.history.force } }).then(function () {
            app.reload();
        }).catch(function (error) {
            if (error && error.status === 409) {
                app.history.state = 'conflict';
                app.history.conflict = error.body || {};
                app.renderHistory();
            } else {
                app.showMessage('danger', app.t('The version could not be restored.'));
            }
        });
    };

    // A change announced by the hub. It waits while this page is saving, since the answer to the save tells which
    // revision is this page's own.
    app.onRemoteChange = function (message) {
        app.history.queued.push(message);

        if (!app.history.saving) {
            app.flushRemoteChanges();
        }
    };

    app.flushRemoteChanges = function () {
        var history = app.history;
        var messages = history.queued;

        history.queued = [];

        messages.forEach(function (message) {
            var action = designer.remoteChangeAction(message, history.revision);

            if (action === 'deleted') {
                history.remote = 'deleted';
                history.remoteBy = message.userName;
            } else if (action === 'changed') {
                history.remote = message.kind;
                history.remoteBy = message.userName;
            }
        });

        app.renderHistory();
    };

    app.startLive = function () {
        if (app.live || !app.config.hubUrl || !app.design.id || app.isView()) {
            return;
        }

        app.live = true;
        designer.startRealtime({
            url: app.config.hubUrl,
            designId: app.design.id,
            onChange: app.onRemoteChange,
            onPresence: function (presence) {
                app.history.presence = presence;
                app.renderHistory();
            }
        });
    };

    function bar(kind, icon, text, actions) {
        return h('div', { className: 'alert alert-' + kind + ' d-flex align-items-center gap-2 flex-wrap py-2 px-3 mb-0 rounded-0 border-0 border-bottom small rd-history-bar' },
            ui.icon(icon),
            h('span', { className: 'flex-grow-1' }, text),
            actions);
    }

    function actionButton(label, className, action) {
        return h('button', { type: 'button', className: 'btn btn-sm ' + className, onclick: action }, label);
    }

    app.renderHistory = function () {
        var e = app.elements;
        var history = app.history;

        app.renderStatus();

        if (e.presence) {
            var names = designer.presenceNames(history.presence);

            ui.clear(e.presence);
            e.presence.classList.toggle('d-none', !names.length);
            e.presence.title = names.length ? app.t('Also editing:') + ' ' + names.join(', ') : '';

            names.slice(0, 3).forEach(function (name) {
                e.presence.appendChild(h('span', { className: 'rd-avatar', 'aria-hidden': 'true' }, name.charAt(0).toUpperCase()));
            });

            if (names.length) {
                e.presence.appendChild(h('span', { className: 'visually-hidden' }, e.presence.title));
            }
        }

        if (e.versionsButton) {
            e.versionsButton.classList.toggle('d-none', !app.isSaved() || history.unpublished);
        }

        if (!e.historyBar) {
            return;
        }

        ui.clear(e.historyBar);

        var who = function (name) {
            return name || app.t('Someone');
        };

        if (history.remote === 'deleted') {
            e.historyBar.appendChild(bar('danger', 'fa-trash', who(history.remoteBy) + ' ' + app.t('deleted this report. Your changes are not saved.'), null));
        } else if (history.conflict) {
            var conflict = history.conflict;
            var text = conflict.busy
                ? app.t('Someone else is saving this report. Try again in a moment.')
                : who(conflict.modifiedBy) + ' ' + app.t('changed this report') + (conflict.modifiedUtc ? ' (' + app.formatTime(conflict.modifiedUtc) + ')' : '') + '. ' + app.t('Your latest changes are not saved.');

            e.historyBar.appendChild(bar('warning', 'fa-triangle-exclamation', text, [
                actionButton(app.t('Reload their changes'), 'btn-warning', app.reload),
                conflict.busy ? null : actionButton(app.t('Keep mine'), 'btn-outline-dark', app.overwrite)
            ]));
        } else if (history.remote) {
            var verb = history.remote === 'Published'
                ? app.t('published this report.')
                : history.remote === 'DraftDiscarded'
                    ? app.t('discarded the unpublished changes.')
                    : history.remote === 'Restored'
                        ? app.t('restored a version of this report.')
                        : app.t('changed this report.');

            e.historyBar.appendChild(bar('info', 'fa-user-pen', who(history.remoteBy) + ' ' + verb + ' ' + app.t('Reload to get their changes.'), [
                actionButton(app.t('Reload'), 'btn-info', app.reload)
            ]));
        } else if (history.presence.length && app.canAutosave()) {
            e.historyBar.appendChild(bar('secondary', 'fa-users', designer.presenceNames(history.presence).join(', ') + ' ' + app.t('also has this report open. Changes you both make can conflict.'), null));
        }

        if (history.unpublished && history.remote !== 'deleted') {
            e.historyBar.appendChild(bar('light', 'fa-file-pen', app.t('This report is saved as a draft and was never published. Only you and the people who manage every report can see it.'), [
                actionButton(app.t('Delete draft'), 'btn-outline-secondary', app.discardDraft)
            ]));
        } else if (history.hasDraft && history.remote !== 'deleted') {
            var by = history.modifiedBy ? ' ' + app.t('by') + ' ' + history.modifiedBy : '';
            var when = history.modifiedUtc ? ' (' + app.formatTime(history.modifiedUtc) + ')' : '';

            e.historyBar.appendChild(bar('light', 'fa-file-pen', app.t('Unpublished changes') + by + when + '. ' + app.t('People who run the report still see the published version.'), [
                actionButton(app.t('Discard changes'), 'btn-outline-secondary', app.discardDraft)
            ]));
        }
    };

    // The words next to the title: what happened to the latest changes.
    app.historyStatus = function () {
        var history = app.history;

        if (!app.canAutosave()) {
            return app.dirty ? app.t('Unsaved changes') : '';
        }

        switch (history.state) {
            case 'saving':
                return app.t('Saving…');
            case 'error':
                return app.t('The draft could not be saved. It is retried with your next change.');
            case 'conflict':
                return app.t('Not saved');
            case 'saved':
                return app.dirty ? app.t('Unsaved changes') : app.t('Draft saved');
            case 'published':
                return app.dirty ? app.t('Unsaved changes') : app.t('Published');
            default:
                return app.dirty ? app.t('Unsaved changes') : '';
        }
    };

    app.startHistory = function () {
        var payload = app.config.payload || {};

        app.history.revision = payload.revision || 0;
        app.history.hasDraft = !!payload.hasDraft;
        app.history.unpublished = !!payload.isUnpublished;
        app.history.modifiedBy = payload.draftModifiedBy || null;
        app.history.modifiedUtc = payload.draftModifiedUtc || null;
        app.renderHistory();
        app.startLive();
    };
})(typeof window !== 'undefined' ? window : globalThis);
