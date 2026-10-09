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
    var newLinkUrl = null;

    function field(label, control, hint) {
        return h('div', { className: 'mb-3' },
            h('label', { className: 'form-label' }, label),
            control,
            hint ? h('div', { className: 'form-text' }, hint) : null);
    }

    function checkbox(label, checked, onChange, hint, disabled) {
        var id = 'rd-' + Math.random().toString(36).slice(2, 9);

        return h('div', { className: 'form-check mb-2' },
            h('input', { type: 'checkbox', className: 'form-check-input', id: id, checked: !!checked, disabled: !!disabled, onchange: function (event) {
                onChange(event.target.checked);
            } }),
            h('label', { className: 'form-check-label', for: id }, label),
            hint ? h('span', { className: 'hint dashed' }, hint) : null);
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
        ui.append(target, [h('div', { className: 'row' }, h('div', { className: 'col-12 col-lg-8' },
            field(app.isView() ? app.t('View name') : app.t('Report title'), h('input', {
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
                className: 'form-control',
                rows: '3',
                oninput: function (event) {
                    design.description = event.target.value;
                    touch();
                }
            }, design.description || ''), app.isView() ? app.t('Tells other report builders what the view prepares.') : app.t('Shown above the report.')),
            app.isView() ? null : field(app.t('Category'), h('input', {
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
            })))]);
    };

    function userPicker() {
        var design = app.design;
        var chips = h('div', { className: 'd-flex flex-wrap gap-1 mb-2' });
        var results = h('div', { className: 'list-group position-absolute w-100 shadow report-designer-user-results' });
        var input = h('input', { type: 'search', className: 'form-control', placeholder: app.t('Search people by user name or email'), 'aria-label': app.t('Search people') });
        var renderChips = function () {
            ui.clear(chips);
            design.sharedUserNames.forEach(function (userName) {
                chips.appendChild(h('span', { className: 'badge text-bg-secondary d-inline-flex align-items-center gap-1' }, userName,
                    h('button', { type: 'button', className: 'btn-close btn-close-white btn-sm', 'aria-label': app.t('Remove') + ': ' + userName, onclick: function () {
                        design.sharedUserNames = design.sharedUserNames.filter(function (other) {
                            return other !== userName;
                        });
                        app.touched();
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
                        if (design.sharedUserNames.indexOf(user.value) < 0) {
                            design.sharedUserNames.push(user.value);
                            app.touched();
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

            return checkbox(role, shared, function (value) {
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

        var name = h('input', { type: 'text', className: 'form-control', placeholder: app.t('What the link is for'), maxlength: '200' });
        var expires = h('input', { type: 'datetime-local', className: 'form-control' });
        var allowExport = h('input', { type: 'checkbox', className: 'form-check-input', id: 'rd-link-export' });
        var requireSignIn = h('input', { type: 'checkbox', className: 'form-check-input', id: 'rd-link-signin' });
        var table = h('div');
        var renderLinks = function () {
            ui.clear(table);

            if (newLinkUrl) {
                var urlInput = h('input', { type: 'text', className: 'form-control', readonly: true, value: newLinkUrl });

                table.appendChild(h('div', { className: 'alert alert-success' },
                    h('div', { className: 'fw-semibold mb-1' }, app.t('Copy the link now. For security it is not shown again.')),
                    h('div', { className: 'input-group' }, urlInput,
                        h('button', { type: 'button', className: 'btn btn-outline-secondary', onclick: function () {
                            urlInput.select();

                            if (root.navigator.clipboard) {
                                root.navigator.clipboard.writeText(newLinkUrl);
                            }
                        } }, ui.icon('fa-copy'), ' ', app.t('Copy')))));
            }

            if (!linkCache || !linkCache.length) {
                table.appendChild(h('p', { className: 'text-muted' }, app.t('The report has no share links.')));

                return;
            }

            table.appendChild(h('div', { className: 'table-responsive' }, h('table', { className: 'table table-sm align-middle' },
                h('thead', null, h('tr', null,
                    h('th', null, app.t('Link')),
                    h('th', null, app.t('Expires')),
                    h('th', null, app.t('Allows')),
                    h('th', null, app.t('Status')),
                    h('th'))),
                h('tbody', null, linkCache.map(function (link) {
                    var expired = link.expiresUtc && new Date(link.expiresUtc) <= new Date();
                    var status = link.revokedUtc ? app.t('Revoked') : (expired ? app.t('Expired') : app.t('Active'));

                    return h('tr', null,
                        h('td', null, h('div', null, link.name || app.t('Unnamed link')), h('code', { className: 'small' }, '…/' + link.tokenHint + '…')),
                        h('td', null, formatDate(link.expiresUtc)),
                        h('td', { className: 'small' }, [link.allowExport ? app.t('Export') : null, link.requireSignIn ? app.t('Signed-in people only') : app.t('Anyone with the link')].filter(Boolean).join(', ')),
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

        ui.append(container, [
            h('div', { className: 'row g-2 align-items-end mb-3' },
                h('div', { className: 'col-12 col-md-4' }, h('label', { className: 'form-label' }, app.t('Note')), name),
                h('div', { className: 'col-12 col-md-3' }, h('label', { className: 'form-label' }, app.t('Expires')), expires),
                h('div', { className: 'col-12 col-md-3' },
                    h('div', { className: 'form-check' }, allowExport, h('label', { className: 'form-check-label', for: 'rd-link-export' }, app.t('Allow export'))),
                    h('div', { className: 'form-check' }, requireSignIn, h('label', { className: 'form-check-label', for: 'rd-link-signin' }, app.t('Require sign-in')))),
                h('div', { className: 'col-12 col-md-2' }, h('button', { type: 'button', className: 'btn btn-primary w-100', onclick: function () {
                    var expiry = expires.value ? new Date(expires.value) : null;

                    ui.request(app.url('createLink', { id: design.id }), {
                        body: {
                            name: name.value,
                            expiresUtc: expiry && !isNaN(expiry.getTime()) ? expiry.toISOString() : null,
                            allowExport: allowExport.checked,
                            requireSignIn: requireSignIn.checked
                        }
                    }).then(function (result) {
                        newLinkUrl = result.url;
                        name.value = '';
                        expires.value = '';
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
            h('div', { className: 'row g-4' },
                h('div', { className: 'col-12 col-lg-6' }, h('h2', { className: 'h6' }, app.t('People')), userPicker()),
                h('div', { className: 'col-12 col-lg-6' }, h('h2', { className: 'h6' }, app.t('Roles')), rolesList())),
            h('h2', { className: 'h6 mt-4' }, app.t('Share links')),
            h('p', { className: 'text-muted small' }, app.t('A share link opens the report for anyone who has it, until it expires or you revoke it.')),
            linksSection()
        ]);
    };
})(typeof window !== 'undefined' ? window : globalThis);
