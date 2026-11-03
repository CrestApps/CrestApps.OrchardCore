/*
 * Lets people click the rows and charts of a report to filter the visuals they are linked to, like picking an agent in
 * a scorecard to show only that agent's calls and trend. A clickable section carries data-report-selectable, the
 * visuals it filters in data-report-targets, and its picked values in data-report-selected; its rows carry
 * data-report-key, and its chart reports clicks with a report-chart-select event.
 *
 * Picking a value raises report-selection-change on the section. On a report page (a form with
 * data-report-interactive) the picked values are kept in the form, so filtering and exporting keep them, and the
 * visuals they filter are loaded again from the same page. The report builder preview listens for the event itself.
 */
(function (root) {
    'use strict';

    var SECTION = '[data-report-selectable]';
    var pending = typeof WeakMap === 'function' ? new WeakMap() : null;

    function selectedKeys(section) {
        try {
            var keys = JSON.parse(section.getAttribute('data-report-selected') || '[]');

            return Array.isArray(keys) ? keys.map(String) : [];
        } catch (e) {
            return [];
        }
    }

    function targetsOf(section) {
        return (section.getAttribute('data-report-targets') || '').split(' ').filter(Boolean);
    }

    // The picked values after a click on a value: a plain click picks only that value, or clears the pick when it is
    // the only value picked; with Ctrl (or Cmd) the click adds the value or removes it.
    function nextKeys(current, key, additive) {
        var has = current.indexOf(key) >= 0;

        if (additive) {
            return has
                ? current.filter(function (other) {
                    return other !== key;
                })
                : current.concat([key]);
        }

        return has && current.length === 1 ? [] : [key];
    }

    function clearButton(section) {
        var button = root.document.createElement('button');

        button.type = 'button';
        button.className = 'btn btn-sm btn-outline-secondary mb-2 report-selection-clear';
        button.innerHTML = '<i class="fa-solid fa-xmark" aria-hidden="true"></i> ';
        button.appendChild(root.document.createTextNode(section.getAttribute('data-report-clear-text') || 'Clear selection'));

        return button;
    }

    // Shows the picked values in the section: the rows picked stand out, the chart dims what isn't picked, and a button
    // clears the pick.
    function highlight(section, keys) {
        section.setAttribute('data-report-selected', JSON.stringify(keys));
        section.classList.toggle('has-selection', keys.length > 0);

        section.querySelectorAll('tr[data-report-key]').forEach(function (row) {
            var picked = keys.indexOf(row.getAttribute('data-report-key')) >= 0;

            row.classList.toggle('is-selected', picked);
            row.setAttribute('aria-selected', picked ? 'true' : 'false');
        });

        var canvas = section.querySelector('canvas[data-report-chart]');

        if (canvas && root.CrestAppsReportCharts && root.CrestAppsReportCharts.select) {
            root.CrestAppsReportCharts.select(canvas, keys);
        }

        var button = section.querySelector(':scope > .report-selection-clear');

        if (keys.length && !button) {
            section.insertBefore(clearButton(section), section.firstChild);
        } else if (!keys.length && button) {
            button.remove();
        }
    }

    function change(section, keys) {
        highlight(section, keys);
        section.dispatchEvent(new root.CustomEvent('report-selection-change', {
            bubbles: true,
            detail: {
                visualId: section.getAttribute('data-report-visual'),
                keys: keys,
                targets: targetsOf(section)
            }
        }));
    }

    function pick(section, key, additive) {
        change(section, nextKeys(selectedKeys(section), String(key), additive));
    }

    function escape(value) {
        return root.CSS && root.CSS.escape ? root.CSS.escape(value) : String(value).replace(/["\\]/g, '\\$&');
    }

    // Keeps a visual's picked values in the form, one hidden field per value.
    function keepInForm(form, visualId, keys) {
        var holder = form.querySelector('[data-report-selections]');

        if (!holder) {
            return;
        }

        var name = 'sel.' + visualId;

        holder.querySelectorAll('input[name="' + escape(name) + '"]').forEach(function (input) {
            input.remove();
        });

        keys.forEach(function (key) {
            var input = root.document.createElement('input');

            input.type = 'hidden';
            input.name = name;
            input.value = key;
            holder.appendChild(input);
        });
    }

    function formUrl(form) {
        var url = new root.URL(form.getAttribute('action') || root.location.href, root.location.href);
        var params = new root.URLSearchParams();

        new root.FormData(form).forEach(function (value, name) {
            if (typeof value === 'string') {
                params.append(name, value);
            }
        });

        url.search = params.toString();

        return url;
    }

    // Loads the visuals a pick filters again, from the same page with the form's values, and swaps them in. The address
    // keeps the picked values, so reloading or sharing it shows the same view.
    function refresh(form, targets) {
        var sections = targets.map(function (id) {
            return form.querySelector('[data-report-visual="' + escape(id) + '"]');
        }).filter(Boolean);
        var url = formUrl(form);

        if (root.history && root.history.replaceState) {
            root.history.replaceState(root.history.state, '', url.toString());
        }

        if (!sections.length) {
            return;
        }

        var previous = pending && pending.get(form);

        if (previous) {
            previous.abort();
        }

        var controller = typeof root.AbortController === 'function' ? new root.AbortController() : null;

        if (pending && controller) {
            pending.set(form, controller);
        }

        sections.forEach(function (section) {
            section.classList.add('is-refreshing');
            section.setAttribute('aria-busy', 'true');
        });

        url.searchParams.set('rv', targets.join(','));

        root.fetch(url.toString(), {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            signal: controller ? controller.signal : undefined
        }).then(function (response) {
            if (!response.ok) {
                throw new Error(response.statusText);
            }

            return response.text();
        }).then(function (html) {
            var page = new root.DOMParser().parseFromString(html, 'text/html');

            sections.forEach(function (section) {
                var fresh = page.querySelector('[data-report-visual="' + escape(section.getAttribute('data-report-visual')) + '"]');

                if (!fresh) {
                    section.classList.remove('is-refreshing');
                    section.removeAttribute('aria-busy');

                    return;
                }

                var node = root.document.importNode(fresh, true);

                section.replaceWith(node);

                if (root.CrestAppsReportCharts) {
                    root.CrestAppsReportCharts.render(node);
                }
            });
        }).catch(function (error) {
            if (error && error.name === 'AbortError') {
                return;
            }

            sections.forEach(function (section) {
                section.classList.remove('is-refreshing');
                section.removeAttribute('aria-busy');
            });
        });
    }

    function bind(document) {
        document.addEventListener('click', function (event) {
            var clear = event.target.closest('.report-selection-clear');

            if (clear) {
                var cleared = clear.closest(SECTION);

                if (cleared) {
                    event.preventDefault();
                    change(cleared, []);
                }

                return;
            }

            var row = event.target.closest(SECTION + ' tr[data-report-key]');

            if (!row || event.target.closest('a, button, input, select, textarea, label')) {
                return;
            }

            pick(row.closest(SECTION), row.getAttribute('data-report-key'), event.ctrlKey || event.metaKey);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key !== 'Enter' && event.key !== ' ') {
                return;
            }

            var row = event.target.closest && event.target.closest(SECTION + ' tr[data-report-key]');

            if (row && row === event.target) {
                event.preventDefault();
                pick(row.closest(SECTION), row.getAttribute('data-report-key'), event.ctrlKey || event.metaKey);
            }
        });

        document.addEventListener('report-chart-select', function (event) {
            var section = event.target.closest(SECTION);

            if (section && event.detail) {
                pick(section, event.detail.key, !!event.detail.additive);
            }
        });

        document.addEventListener('report-selection-change', function (event) {
            var form = event.target.closest('form[data-report-interactive]');

            if (!form) {
                return;
            }

            keepInForm(form, event.detail.visualId, event.detail.keys);
            refresh(form, event.detail.targets);
        });
    }

    root.CrestAppsReportInteractions = {
        nextKeys: nextKeys,
        highlight: highlight
    };

    if (root.document && !root.CrestAppsReportInteractionsBound) {
        root.CrestAppsReportInteractionsBound = true;
        bind(root.document);

        // A page opened with picked values shows them, with the button that clears them.
        var start = function () {
            root.document.querySelectorAll(SECTION).forEach(function (section) {
                var keys = selectedKeys(section);

                if (keys.length) {
                    highlight(section, keys);
                }
            });
        };

        if (root.document.readyState === 'loading') {
            root.document.addEventListener('DOMContentLoaded', start);
        } else {
            start();
        }
    }
})(typeof window !== 'undefined' ? window : globalThis);
