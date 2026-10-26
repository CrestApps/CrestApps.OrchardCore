/*
 * The Settings and Sharing tabs of the report builder: the description, category, and admin menu placement, the
 * people and roles a report is shared with, and its share links.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner;
    var ui = designer.ui;
    var h = ui.h;
    var app = designer.app;

    var linkCache = null;
    var newLink = null;

    // A field in Orchard Core's admin layout: the label on the left, the control and its hint on the right, one field
    // under the other.
    function field(label, control, hint) {
        var id = control && control.id ? control.id : null;

        return h('div', { className: 'ocat-wrapper' },
            h('label', { className: 'ocat-label' + (control && control.required ? ' ocat-label-required' : ''), for: id }, label),
            h('div', { className: 'ocat-end' },
                control,
                hint ? h('span', { className: 'hint' }, hint) : null));
    }

    // A checkbox on its own line of a list, such as a role.
    function check(label, checked, onChange, hint, disabled) {
        var id = 'rd-' + Math.random().toString(36).slice(2, 9);

        return h('div', { className: 'form-check' },
            h('input', { type: 'checkbox', className: 'form-check-input', id: id, checked: !!checked, disabled: !!disabled, onchange: function (event) {
                onChange(event.target.checked);
            } }),
            h('label', { className: 'form-check-label', for: id }, label),
            hint ? h('span', { className: 'hint dashed' }, hint) : null);
    }

    // A checkbox setting in Orchard Core's admin layout, lined up with the controls of the fields above it.
    function checkbox(label, checked, onChange, hint, disabled) {
        return h('div', { className: 'ocat-wrapper' },
            h('div', { className: 'ocat-end-offset' }, check(label, checked, onChange, hint, disabled)));
    }

    function newId() {
        return 'rd-' + Math.random().toString(36).slice(2, 9);
    }

    app.renderSettings = function () {
        var target = app.elements.settings;
        var design = app.design;
        var touch = app.touched;

        // Redrawing while the person types would lose what they are typing.
        if (target.contains(root.document.activeElement)) {
            return;
        }

        ui.clear(target);
        ui.append(target, [
            field(app.isView() ? app.t('View name') : app.t('Report title'), h('input', {
                id: newId(),
                type: 'text',
                className: 'form-control',
                value: design.displayText || '',
                maxlength: '200',
                required: true,
                placeholder: app.isView() ? app.t('Such as Revenue by region') : app.t('Such as Sales by region'),
                oninput: function (event) {
                    design.displayText = event.target.value;
                    touch();
                }
            }), app.isView() ? app.t('Other reports list the view by this name.') : app.t('Shown at the top of the report and in the report list.')),
            field(app.t('Description'), h('textarea', {
                id: newId(),
                className: 'form-control',
                rows: '3',
                oninput: function (event) {
                    design.description = event.target.value;
                    touch();
                }
            }, design.description || ''), app.isView() ? app.t('Tells other report builders what the view prepares.') : app.t('Shown above the report.')),
            app.isView() ? null : field(app.t('Category'), h('input', {
                id: newId(),
                type: 'text',
                className: 'form-control',
                value: design.category || '',
                maxlength: '200',
                list: 'report-designer-categories',
                oninput: function (event) {
                    design.category = event.target.value;
                    touch();
                }
            }), app.t('Groups the report in the admin menu and the report list.')),
            app.isView() ? null : checkbox(app.t('Show in the admin menu'), design.showInAdminMenu, function (value) {
                design.showInAdminMenu = value;
                touch();
            }, app.t('Adds the report under Reports in the admin menu for everyone who can open it.')),
            app.isView() ? null : checkbox(app.t('Let people the report is shared with export it'), design.allowExport !== false, function (value) {
                design.allowExport = value;
                touch();
            }),
            // A view's refresh schedule (see designer-refresh.js).
            app.isView() && app.renderRefresh ? app.renderRefresh() : null]);
    };

    // Picks users of the site by user name: the names are kept in the given list, and onChange runs after each change.
    function userPicker(userNames, onChange) {
        var chips = h('div', { className: 'd-flex flex-wrap gap-1 mb-2' });
        var results = h('div', { className: 'list-group position-absolute w-100 shadow report-designer-user-results' });
        var input = h('input', { type: 'search', className: 'form-control', placeholder: app.t('Search people by user name or email'), 'aria-label': app.t('Search people') });
        var renderChips = function () {
            ui.clear(chips);
            userNames.forEach(function (userName) {
                chips.appendChild(h('span', { className: 'badge text-bg-secondary d-inline-flex align-items-center gap-1' }, userName,
                    h('button', { type: 'button', className: 'btn-close btn-close-white btn-sm', 'aria-label': app.t('Remove') + ': ' + userName, onclick: function () {
                        userNames.splice(userNames.indexOf(userName), 1);
                        onChange();
                        renderChips();
                    } })));
            });
        };
        var search = ui.debounce(function () {
            var text = input.value.trim();

            ui.clear(results);

            if (text.length < 2) {
                return;
            }

            ui.request(app.url('users') + '?query=' + encodeURIComponent(text)).then(function (users) {
                ui.clear(results);
                (users || []).forEach(function (user) {
                    results.appendChild(h('button', { type: 'button', className: 'list-group-item list-group-item-action', onclick: function () {
                        if (userNames.indexOf(user.value) < 0) {
                            userNames.push(user.value);
                            onChange();
                        }

                        input.value = '';
                        ui.clear(results);
                        renderChips();
                    } }, user.text));
                });
            });
        }, 300);

        input.addEventListener('input', search);
        renderChips();

        return h('div', null, chips, h('div', { className: 'position-relative' }, input, results));
    }

    function rolesList() {
        var design = app.design;

        return h('div', null, app.config.roles.map(function (role) {
            var shared = design.sharedRoles.indexOf(role) >= 0;
            var isAnonymous = role === 'Anonymous';
            var hint = isAnonymous
                ? app.t('Everyone, including visitors who are not signed in, can open the report.')
                : (role === 'Authenticated' ? app.t('Everyone who is signed in can open the report.') : null);

            return check(role, shared, function (value) {
                design.sharedRoles = design.sharedRoles.filter(function (other) {
                    return other !== role;
                });

                if (value) {
                    design.sharedRoles.push(role);
                }

                app.touched();
            }, hint, isAnonymous && !shared && !app.config.canSharePublicly);
        }));
    }

    function formatDate(value) {
        if (!value) {
            return app.t('Never');
        }

        var date = new Date(value);

        return isNaN(date.getTime()) ? value : date.toLocaleString();
    }

    function linksSection() {
        var design = app.design;
        var container = h('div');

        if (!design.id) {
            container.appendChild(h('p', { className: 'text-muted' }, app.t('Save the report to create share links.')));

            return container;
        }

        if (!app.config.canSharePublicly) {
            container.appendChild(h('p', { className: 'text-muted' }, app.t('You are not allowed to create share links.')));

            return container;
        }

        var recipients = [];
        var note = h('input', { id: 'rd-link-note', type: 'text', className: 'form-control', maxlength: '200' });
        var expiresAfterDays = h('input', { id: 'rd-link-days', type: 'number', className: 'form-control', min: '1', value: '7' });
        var maxUses = h('input', { id: 'rd-link-uses', type: 'number', className: 'form-control', min: '0', value: '0' });
        var allowExport = false;
        var notify = true;
        var recipientPicker = h('div');
        var table = h('div');
        var renderRecipients = function () {
            ui.clear(recipientPicker);
            recipientPicker.appendChild(userPicker(recipients, function () { }));
        };
        var renderLinks = function () {
            ui.clear(table);

            if (newLink) {
                var urlInput = h('input', { type: 'text', className: 'form-control', readonly: true, value: newLink.url });

                table.appendChild(h('div', { className: 'alert alert-success' },
                    h('div', { className: 'fw-semibold mb-1' }, newLink.notified && newLink.notified.length
                        ? app.t('The link was emailed to {0}. For security it is not shown again after you leave this page.').replace('{0}', newLink.notified.join(', '))
                        : app.t('Copy the link and send it to its recipients. For security it is not shown again.')),
                    h('div', { className: 'input-group' }, urlInput,
                        h('button', { type: 'button', className: 'btn btn-outline-secondary', onclick: function () {
                            urlInput.select();

                            if (root.navigator.clipboard) {
                                root.navigator.clipboard.writeText(newLink.url);
                            }
                        } }, ui.icon('fa-copy'), ' ', app.t('Copy')))));
            }

            if (!linkCache || !linkCache.length) {
                table.appendChild(h('p', { className: 'text-muted' }, app.t('The report has no share links.')));

                return;
            }

            table.appendChild(h('div', { className: 'table-responsive' }, h('table', { className: 'table table-sm align-middle' },
                h('thead', null, h('tr', null,
                    h('th', null, app.t('Shared with')),
                    h('th', null, app.t('Expires')),
                    h('th', null, app.t('Uses')),
                    h('th', null, app.t('Allows')),
                    h('th', null, app.t('Status')),
                    h('th'))),
                h('tbody', null, linkCache.map(function (link) {
                    var expired = link.expiresUtc && new Date(link.expiresUtc) <= new Date();
                    var status = link.revokedUtc ? app.t('Revoked') : (expired ? app.t('Expired') : (link.isUsedUp ? app.t('Used up') : app.t('Active')));

                    return h('tr', null,
                        h('td', null, h('div', null, (link.recipients || []).join(', ')), link.note ? h('div', { className: 'small text-muted' }, link.note) : null),
                        h('td', null, formatDate(link.expiresUtc)),
                        h('td', null, link.maxUses ? app.t('{0} of {1}').replace('{0}', link.uses).replace('{1}', link.maxUses) : String(link.uses || 0)),
                        h('td', { className: 'small' }, link.allowExport ? app.t('Export') : '-'),
                        h('td', null, h('span', { className: 'badge ' + (status === app.t('Active') ? 'text-bg-success' : 'text-bg-secondary') }, status)),
                        h('td', { className: 'text-end' }, link.revokedUtc ? null : h('button', { type: 'button', className: 'btn btn-sm btn-outline-danger', onclick: function () {
                            ui.request(app.url('revokeLink', { id: design.id, link: link.id }), { method: 'POST', body: {} }).then(loadLinks);
                        } }, app.t('Revoke'))));
                })))));
        };
        var loadLinks = function () {
            ui.request(app.url('links', { id: design.id })).then(function (links) {
                linkCache = links || [];
                renderLinks();
            }).catch(function () {
                linkCache = [];
                renderLinks();
            });
        };

        renderRecipients();

        ui.append(container, [
            field(app.t('Recipients'), recipientPicker, app.t('The users of the site the link is for. Only they can open it, once signed in.')),
            field(app.t('Note'), note, app.t('Optional. What the link is for, sent with the email and shown in the list of links.')),
            field(app.t('Expires after (days)'), expiresAfterDays),
            field(app.t('Most uses'), maxUses, app.t('0 for no limit. Opening the report, changing its filters and exporting it are each a use.')),
            checkbox(app.t('Allow export'), allowExport, function (value) {
                allowExport = value;
            }),
            checkbox(app.t('Email the link to the recipients'), notify, function (value) {
                notify = value;
            }, app.t('Needs the Email feature. Otherwise, copy the link and send it yourself.')),
            h('div', { className: 'ocat-wrapper' }, h('div', { className: 'ocat-end-offset' }, h('button', { type: 'button', className: 'btn btn-primary', onclick: function () {
                    if (!recipients.length) {
                        app.showMessage('warning', app.t('Pick the people the link is for.'));

                        return;
                    }

                    ui.request(app.url('createLink', { id: design.id }), {
                        body: {
                            recipients: recipients.slice(),
                            note: note.value,
                            expiresAfterDays: parseInt(expiresAfterDays.value, 10) || 7,
                            maxUses: parseInt(maxUses.value, 10) || 0,
                            allowExport: allowExport,
                            notifyByEmail: notify
                        }
                    }).then(function (result) {
                        if (result && result.errors && result.errors.length) {
                            app.showMessage('danger', result.errors.join(' '));

                            return;
                        }

                        newLink = result;
                        recipients.length = 0;
                        note.value = '';
                        renderRecipients();
                        loadLinks();
                    }).catch(function () {
                        app.showMessage('danger', app.t('The link could not be created.'));
                    });
                } }, ui.icon('fa-link'), ' ', app.t('Create link')))),
            table
        ]);

        if (linkCache === null) {
            loadLinks();
        } else {
            renderLinks();
        }

        return container;
    }

    app.renderSharing = function () {
        var target = app.elements.sharing;

        // Redrawing while the person types (searching people) would lose what they are typing.
        if (!target || app.isView() || target.contains(root.document.activeElement)) {
            return;
        }

        ui.clear(target);
        ui.append(target, [
            h('p', { className: 'text-muted' }, app.t('A shared report shows its data with your access, so people see what the report shows even if they cannot open that data themselves.')),
            field(app.t('People'), userPicker(app.design.sharedUserNames, app.touched), app.t('The people who can open the report. Search by user name or email.')),
            field(app.t('Roles'), rolesList(), app.t('Everyone in these roles can open the report.')),
            h('h2', { className: 'h6 mt-4' }, app.t('Share links')),
            h('p', { className: 'text-muted small' }, app.t('A share link sends the report to users of the site, who open it once signed in, until it expires, is used as many times as allowed, or you revoke it.')),
            linksSection()
        ]);
    };
})(typeof window !== 'undefined' ? window : globalThis);
