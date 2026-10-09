/*
 * The Data model tab of the report designer: every data set as a card on a canvas, with a line for each pair of
 * columns that joins two of them. Dragging a column from one card onto a column of another card joins the two data
 * sets on that pair; clicking a line or its badge opens the join so the rows to keep and more pairs can be set.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner;
    var ui = designer.ui;
    var h = ui.h;
    var app = designer.app;

    var MODEL_MIME = 'application/x-report-model-field';
    var SVG = 'http://www.w3.org/2000/svg';
    var CARD_WIDTH = 248;
    var positions = {};
    var cardElements = {};

    // Places cards that the user has not moved yet on a grid, left to right.
    function placeCards(aliases, width) {
        var perRow = Math.max(1, Math.floor((width - 40) / (CARD_WIDTH + 96)));
        var placed = 0;

        aliases.forEach(function (alias) {
            if (!positions[alias]) {
                positions[alias] = {
                    x: 32 + (placed % perRow) * (CARD_WIDTH + 96),
                    y: 32 + Math.floor(placed / perRow) * 340
                };
            }

            placed++;
        });
    }

    function fieldRow(alias, field, usedKeys) {
        var key = alias + '.' + field.name;
        var row = h('li', {
            className: 'rd-model-field d-flex align-items-center gap-2' + (usedKeys[key] ? ' is-joined' : ''),
            draggable: 'true',
            title: app.t('Drag onto a column of another data set to join on it.'),
            dataset: { fieldKey: key },
            ondragstart: function (event) {
                event.dataTransfer.setData(MODEL_MIME, key);
                event.dataTransfer.effectAllowed = 'link';
                app.elements.model.classList.add('is-linking');
            },
            ondragend: function () {
                app.elements.model.classList.remove('is-linking');
            },
            ondragover: function (event) {
                if (Array.prototype.indexOf.call(event.dataTransfer.types || [], MODEL_MIME) >= 0) {
                    event.preventDefault();
                    row.classList.add('is-over');
                }
            },
            ondragleave: function () {
                row.classList.remove('is-over');
            },
            ondrop: function (event) {
                event.preventDefault();
                row.classList.remove('is-over');
                app.elements.model.classList.remove('is-linking');

                var source = event.dataTransfer.getData(MODEL_MIME);
                var joined = source ? designer.connect(app.design.query, source, key) : null;

                if (joined) {
                    app.selection = { kind: 'join', id: joined };
                    app.changed();
                }
            }
        },
        h('span', { className: 'report-designer-type badge text-bg-light' }, designer.typeGlyph(field.dataType)),
        h('span', { className: 'flex-grow-1 text-truncate' }, field.displayName || field.name),
        field.isIdentifier ? h('i', { className: 'fa-solid fa-key text-warning', title: app.t('Key column'), 'aria-hidden': 'true' }) : null);

        return row;
    }

    function startMove(alias, card, event) {
        if (event.button !== 0 || event.target.closest('button')) {
            return;
        }

        var start = { x: event.clientX, y: event.clientY, left: positions[alias].x, top: positions[alias].y };

        var move = function (moveEvent) {
            positions[alias] = {
                x: Math.max(0, start.left + moveEvent.clientX - start.x),
                y: Math.max(0, start.top + moveEvent.clientY - start.y)
            };
            card.style.left = positions[alias].x + 'px';
            card.style.top = positions[alias].y + 'px';
            app.drawModelLines();
        };

        var stop = function () {
            root.removeEventListener('pointermove', move);
            root.removeEventListener('pointerup', stop);
            card.classList.remove('is-moving');
        };

        card.classList.add('is-moving');
        root.addEventListener('pointermove', move);
        root.addEventListener('pointerup', stop);
        event.preventDefault();
    }

    function modelCard(dataSet, index, usedKeys, joined) {
        var schema = app.schemas[dataSet.alias];
        var list = h('ul', { className: 'rd-model-fields list-unstyled mb-0', onscroll: function () {
            app.drawModelLines();
        } });
        var fields = (schema && schema.fields || []).slice().sort(function (left, right) {
            return (right.isIdentifier ? 1 : 0) - (left.isIdentifier ? 1 : 0);
        });

        fields.forEach(function (field) {
            list.appendChild(fieldRow(dataSet.alias, field, usedKeys));
        });

        var card = h('div', {
            className: 'rd-model-card card shadow-sm',
            style: { left: positions[dataSet.alias].x + 'px', top: positions[dataSet.alias].y + 'px', width: CARD_WIDTH + 'px' },
            dataset: { alias: dataSet.alias }
        },
        h('div', {
            className: 'card-header d-flex align-items-center gap-2 py-1 rd-model-card-header',
            title: app.t('Drag to move'),
            onpointerdown: function (event) {
                startMove(dataSet.alias, card, event);
            }
        },
        ui.icon(dataSet.source === 'ReportViews' ? 'fa-layer-group' : 'fa-table'),
        h('span', { className: 'fw-semibold text-truncate flex-grow-1' }, dataSet.displayName || dataSet.dataSet),
        index === 0
            ? h('span', { className: 'badge text-bg-primary' }, app.t('Base'))
            : (joined ? null : h('span', { className: 'badge text-bg-danger', title: app.t('Add the columns that must match.') }, app.t('Not joined')))),
        list);

        cardElements[dataSet.alias] = card;

        return card;
    }

    // The point on a card's edge where a line for a column starts: beside the column's row when it is visible in the
    // card's list, else at the card's header.
    function anchor(key, towardRight) {
        var alias = designer.aliasOf(key);
        var card = cardElements[alias];

        if (!card) {
            return null;
        }

        var canvas = app.elements.modelCanvas.getBoundingClientRect();
        var cardBox = card.getBoundingClientRect();
        var row = card.querySelector('[data-field-key="' + (root.CSS && root.CSS.escape ? root.CSS.escape(key) : key) + '"]');
        var list = card.querySelector('.rd-model-fields');
        var y = cardBox.top + 16;

        if (row && list) {
            var rowBox = row.getBoundingClientRect();
            var listBox = list.getBoundingClientRect();

            y = Math.min(Math.max(rowBox.top + rowBox.height / 2, listBox.top + 4), listBox.bottom - 4);
        }

        return {
            x: (towardRight ? cardBox.right : cardBox.left) - canvas.left + app.elements.modelCanvas.scrollLeft,
            y: y - canvas.top + app.elements.modelCanvas.scrollTop
        };
    }

    app.drawModelLines = function () {
        var svg = app.elements.modelLines;
        var badges = app.elements.modelBadges;

        if (!svg || !app.elements.modelCanvas.offsetParent) {
            return;
        }

        while (svg.firstChild) {
            svg.removeChild(svg.firstChild);
        }

        ui.clear(badges);

        (app.design.query.joins || []).forEach(function (join) {
            var selected = app.selection && app.selection.kind === 'join' && app.selection.id === join.alias;

            (join.conditions || []).forEach(function (condition, index) {
                if (!condition.leftField || !condition.rightField) {
                    return;
                }

                var leftCard = cardElements[designer.aliasOf(condition.leftField)];
                var rightCard = cardElements[designer.aliasOf(condition.rightField)];

                if (!leftCard || !rightCard) {
                    return;
                }

                var leftIsWest = leftCard.getBoundingClientRect().left <= rightCard.getBoundingClientRect().left;
                var start = anchor(condition.leftField, leftIsWest);
                var end = anchor(condition.rightField, !leftIsWest);

                if (!start || !end) {
                    return;
                }

                var bend = Math.max(40, Math.abs(end.x - start.x) / 2);
                var direction = leftIsWest ? 1 : -1;
                var d = 'M ' + start.x + ' ' + start.y +
                    ' C ' + (start.x + bend * direction) + ' ' + start.y + ', ' + (end.x - bend * direction) + ' ' + end.y + ', ' + end.x + ' ' + end.y;
                var open = function () {
                    app.selection = { kind: 'join', id: join.alias };
                    app.render();
                };
                var hit = root.document.createElementNS(SVG, 'path');
                var line = root.document.createElementNS(SVG, 'path');

                hit.setAttribute('d', d);
                hit.setAttribute('class', 'rd-model-hit');
                hit.addEventListener('click', open);
                line.setAttribute('d', d);
                line.setAttribute('class', 'rd-model-line' + (selected ? ' is-selected' : ''));
                svg.appendChild(hit);
                svg.appendChild(line);

                if (index === 0) {
                    badges.appendChild(h('button', {
                        type: 'button',
                        className: 'btn btn-sm rd-model-badge ' + (selected ? 'btn-primary' : 'btn-light border'),
                        style: { left: ((start.x + end.x) / 2) + 'px', top: ((start.y + end.y) / 2) + 'px' },
                        title: app.t('Edit join'),
                        onclick: open
                    }, ui.icon('fa-link'), ' ', app.config.joinLabels[join.type] || join.type,
                    join.conditions.length > 1 ? ' ×' + join.conditions.length : ''));
                }
            });
        });
    };

    app.renderModel = function () {
        var target = app.elements.model;

        if (!target) {
            return;
        }

        var query = app.design.query;
        var usedKeys = {};
        var joinedAliases = {};

        (query.joins || []).forEach(function (join) {
            (join.conditions || []).forEach(function (condition) {
                if (condition.leftField && condition.rightField) {
                    usedKeys[condition.leftField] = true;
                    usedKeys[condition.rightField] = true;
                    joinedAliases[join.alias] = true;
                }
            });
        });

        var canvas = app.elements.modelCanvas;
        var scrollLeft = canvas ? canvas.scrollLeft : 0;
        var scrollTop = canvas ? canvas.scrollTop : 0;

        cardElements = {};
        ui.clear(target);

        canvas = app.elements.modelCanvas = h('div', { className: 'rd-model-canvas', onscroll: function () {
            app.drawModelLines();
        } });
        app.elements.modelLines = root.document.createElementNS(SVG, 'svg');
        app.elements.modelLines.setAttribute('class', 'rd-model-lines');
        app.elements.modelBadges = h('div', { className: 'rd-model-badges' });

        var stage = h('div', { className: 'rd-model-stage' }, app.elements.modelLines, app.elements.modelBadges);

        canvas.appendChild(stage);
        placeCards(query.dataSets.map(function (dataSet) {
            return dataSet.alias;
        }), app.elements.root ? app.elements.root.clientWidth - 360 : 900);

        Object.keys(positions).forEach(function (alias) {
            if (!query.dataSets.some(function (dataSet) {
                return dataSet.alias === alias;
            })) {
                delete positions[alias];
            }
        });

        var right = 0;
        var bottom = 0;

        query.dataSets.forEach(function (dataSet, index) {
            stage.appendChild(modelCard(dataSet, index, usedKeys, !!joinedAliases[dataSet.alias]));
            right = Math.max(right, positions[dataSet.alias].x + CARD_WIDTH + 64);
            bottom = Math.max(bottom, positions[dataSet.alias].y + 380);
        });

        stage.style.width = right + 'px';
        stage.style.height = bottom + 'px';

        if (!query.dataSets.length) {
            stage.appendChild(h('div', { className: 'report-designer-empty text-muted' },
                h('div', { className: 'mb-2' }, ui.icon('fa-diagram-project fa-2x')),
                app.t('Add data sets, then drag a column of one onto the matching column of another to join them.')));
        }

        var side = h('div', { className: 'rd-model-side' },
            h('div', { className: 'rd-pane-header d-flex align-items-center gap-2' },
                h('span', { className: 'rd-pane-title flex-grow-1' }, app.t('Join'))),
            h('div', { className: 'rd-pane-scroll' }, app.selection && app.selection.kind === 'join'
                ? app.joinEditor(app.selection.id)
                : h('div', { className: 'small text-muted' },
                    h('p', null, app.t('Drag a column from one data set onto the matching column of another to join them, such as an order\'s customer onto the customer\'s id.')),
                    h('p', null, app.t('Click a line or its badge to choose which rows to keep, or to match on more columns.')),
                    h('p', { className: 'mb-0' }, app.t('Key columns are marked with a key and listed first.')))));

        ui.append(target, [
            h('div', { className: 'rd-model-main' },
                h('div', { className: 'rd-pane-header d-flex align-items-center gap-2' },
                    h('span', { className: 'rd-pane-title flex-grow-1' }, app.t('Data model')),
                    h('button', { type: 'button', className: 'btn btn-sm btn-primary text-nowrap', onclick: app.openAddDataSet }, ui.icon('fa-plus'), ' ', app.t('Add data set'))),
                canvas),
            side
        ]);

        canvas.scrollLeft = scrollLeft;
        canvas.scrollTop = scrollTop;
        root.requestAnimationFrame(app.drawModelLines);
    };
})(typeof window !== 'undefined' ? window : globalThis);
