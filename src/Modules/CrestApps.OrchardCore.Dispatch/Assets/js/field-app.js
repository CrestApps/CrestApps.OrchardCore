// The technician app in My Work. It keeps the technician's visits (the pack) on the device, so the page works
// offline: every change is applied to the pack at once and kept in an outbox with its own key, then sent in order
// once the device is online. The server applies each command once and refuses changes to a visit given to someone
// else; those come back as notices. With the technician's consent, their position is sent while they work.
(function () {
    'use strict';

    const root = document.getElementById('field-app');

    if (!root) {
        return;
    }

    const texts = JSON.parse(root.dataset.texts || '{}');
    const workerId = root.dataset.workerId;
    const apiUrl = root.dataset.apiUrl;
    const retentionDays = root.dataset.retentionDays;
    const view = root.querySelector('[data-field-view]');
    const syncBadge = root.querySelector('[data-field-sync]');
    const noticeBox = root.querySelector('[data-field-notices]');
    const locationBox = root.querySelector('[data-field-location]');
    const dismissedKey = 'crestapps-field-consent-dismissed-' + workerId;
    const activeStatuses = ['Scheduled', 'Dispatched', 'EnRoute', 'OnSite', 'Paused'];
    const underWay = ['EnRoute', 'OnSite', 'Paused'];
    const syncEveryMs = 30000;

    let pack = null;
    let pending = 0;
    let syncing = false;
    let syncTimer = null;
    let watchId = null;
    let lastPingMs = 0;
    let locationBlocked = false;
    let incompleteFor = null;
    let renderPending = false;
    let signing = false;

    const format = (text, ...values) => values.reduce((result, value, index) => result.replace(`{${index}}`, value), text || '');

    const element = (tag, className, text) => {
        const node = document.createElement(tag);

        if (className) {
            node.className = className;
        }

        if (text !== undefined && text !== null) {
            node.textContent = text;
        }

        return node;
    };

    const button = (className, text, onClick) => {
        const node = element('button', className, text);

        node.type = 'button';
        node.addEventListener('click', onClick);

        return node;
    };

    const newKey = () => (window.crypto && crypto.randomUUID ? crypto.randomUUID() : Date.now().toString(36) + Math.random().toString(36).slice(2));

    // The device's store: the pack per worker, the outbox of commands in the order they were made, and positions.
    const database = new Promise((resolve, reject) => {
        const request = indexedDB.open('crestapps-field', 1);

        request.onupgradeneeded = () => {
            const db = request.result;

            db.createObjectStore('packs', { keyPath: 'workerId' });
            db.createObjectStore('outbox', { keyPath: 'seq', autoIncrement: true }).createIndex('worker', 'workerId');
            db.createObjectStore('pings', { keyPath: 'seq', autoIncrement: true }).createIndex('worker', 'workerId');
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });

    const store = async (name, mode, action) => {
        const db = await database;

        return new Promise((resolve, reject) => {
            const transaction = db.transaction(name, mode);
            const request = action(transaction.objectStore(name));

            transaction.oncomplete = () => resolve(request ? request.result : undefined);
            transaction.onerror = () => reject(transaction.error);
        });
    };

    const mine = (name) => store(name, 'readonly', (objects) => objects.index('worker').getAll(workerId));

    const savePack = (value) => store('packs', 'readwrite', (objects) => objects.put(value));

    const loadPack = () => store('packs', 'readonly', (objects) => objects.get(workerId));

    const remove = (name, seqs) => store(name, 'readwrite', (objects) => {
        seqs.forEach((seq) => objects.delete(seq));

        return null;
    });

    // Times are shown in the dispatch time zone, which is the pack's.
    const timeText = (utc) => new Intl.DateTimeFormat(document.documentElement.lang || undefined, { timeZone: pack.timeZoneId, hour: 'numeric', minute: '2-digit' }).format(new Date(utc));

    const dayKey = (utc) => new Intl.DateTimeFormat('en-CA', { timeZone: pack.timeZoneId, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(utc));

    const dayText = (utc) => {
        const key = dayKey(utc);

        if (key === dayKey(Date.now())) {
            return texts.today;
        }

        if (key === dayKey(Date.now() + 86400000)) {
            return texts.tomorrow;
        }

        return new Intl.DateTimeFormat(document.documentElement.lang || undefined, { timeZone: pack.timeZoneId, weekday: 'long', day: 'numeric', month: 'long' }).format(new Date(utc));
    };

    const statusBadge = (status) => {
        const colors = { EnRoute: 'text-bg-info', OnSite: 'text-bg-primary', Paused: 'text-bg-warning', Completed: 'text-bg-success', Incomplete: 'text-bg-danger', Canceled: 'text-bg-dark' };

        return element('span', 'badge ' + (colors[status] || 'text-bg-secondary'), texts['status.' + status] || status);
    };

    const findVisit = (id) => (pack ? pack.visits.find((visit) => visit.id === id) : null);

    const notice = (text, kind) => {
        const box = element('div', 'alert alert-' + (kind || 'warning') + ' alert-dismissible py-2', text);
        const close = button('btn-close', null, () => box.remove());

        close.setAttribute('aria-label', texts.dismiss);
        box.append(close);
        noticeBox.append(box);
    };

    // Applies a command to a pack as the server will, so the page shows it before it is sent. The pack from the
    // server gets the commands still in the outbox applied the same way.
    const applyCommand = (target, command) => {
        const visit = target.visits.find((item) => item.id === command.visitId);

        if (!visit) {
            return;
        }

        const workOrder = visit.workOrder;

        switch (command.kind) {
            case 'Status':
                visit.status = command.status;
                break;
            case 'Answer': {
                const checklist = (workOrder.checklists || [])[command.checklistIndex];

                if (checklist) {
                    checklist.answers = (checklist.answers || []).filter((answer) => answer.questionId !== command.questionId);

                    if (command.value) {
                        checklist.answers.push({ questionId: command.questionId, value: command.value, utc: command.utc });
                    }
                }

                break;
            }
            case 'Note':
                workOrder.notes = (workOrder.notes || []).concat([{ utc: command.utc, by: target.workerName, text: command.text }]);
                break;
            case 'Part': {
                const line = (workOrder.lines || []).find((item) => item.id === command.lineId);

                if (line) {
                    line.quantityUsed = Number(line.quantityUsed) + Number(command.quantity);
                }

                break;
            }
            case 'Signature':
                workOrder.signedBy = (workOrder.signedBy || []).concat([command.name]);
                break;
            case 'Photo':
                workOrder.photos++;
                break;
            default:
                break;
        }
    };

    // A change is applied to the pack at once, so the page shows it offline, and kept in the outbox to send.
    const queue = async (command) => {
        command.key = newKey();
        command.utc = new Date().toISOString();
        applyCommand(pack, command);
        await savePack(pack);

        await store('outbox', 'readwrite', (objects) => objects.add({ workerId, command }));
        pending++;
        render();

        if (!navigator.onLine) {
            updateBadge();
        }

        scheduleSync(300);
    };

    const updateBadge = () => {
        let text;
        let className = 'badge fw-normal ';

        if (syncing) {
            text = texts.syncing;
            className += 'text-bg-info';
        } else if (!navigator.onLine) {
            text = pending > 0 ? format(texts.offlinePending, pending) : texts.offline;
            className += 'text-bg-warning';
        } else if (pending > 0) {
            text = format(texts.pending, pending);
            className += 'text-bg-warning';
        } else {
            text = pack ? format(texts.updated, timeText(pack.generatedUtc)) + ' · ' + texts.synced : texts.synced;
            className += 'text-bg-success';
        }

        syncBadge.className = className;
        syncBadge.textContent = text;
    };

    const tokenAsync = async () => {
        const response = await fetch(apiUrl + '/token', { credentials: 'same-origin', headers: { Accept: 'application/json' } });

        if (!response.ok) {
            throw new Error('token');
        }

        return response.json();
    };

    const post = async (path, body, token) => {
        const response = await fetch(apiUrl + '/' + path, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', Accept: 'application/json', [token.header]: token.token },
            body: JSON.stringify(body),
        });

        if (!response.ok) {
            throw new Error(path);
        }

        return response.json();
    };

    // Sends the outbox in the order it was made, then the positions, then gets the pack again.
    const sync = async () => {
        if (syncing || !navigator.onLine) {
            updateBadge();

            return;
        }

        syncing = true;
        updateBadge();

        const before = snapshot(pack);

        try {
            const outbox = (await mine('outbox')).sort((a, b) => a.seq - b.seq);
            const pings = await mine('pings');
            let token = null;

            if (outbox.length > 0) {
                token = await tokenAsync();

                const { results } = await post('sync', { commands: outbox.map((item) => item.command) }, token);
                const done = new Set(results.map((result) => result.key));

                await remove('outbox', outbox.filter((item) => done.has(item.command.key)).map((item) => item.seq));
                [...new Set(results.filter((result) => result.outcome === 'Rejected').map((result) => result.reason))].forEach((reason) => notice(format(texts.notKept, reason)));
            }

            if (pings.length > 0 && pack && pack.location.consented) {
                token = token || await tokenAsync();
                await post('pings', { pings: pings.map((item) => item.ping) }, token);
                await remove('pings', pings.map((item) => item.seq));
            }

            const response = await fetch(apiUrl + '/pack', { credentials: 'same-origin', headers: { Accept: 'application/json' } });

            if (!response.ok) {
                throw new Error('pack');
            }

            // Changes made while this sync ran are still in the outbox: they show on the new pack and go next.
            const fresh = await response.json();
            const left = (await mine('outbox')).sort((a, b) => a.seq - b.seq);

            left.forEach((item) => applyCommand(fresh, item.command));
            pack = fresh;
            await savePack(pack);
            pending = left.length;

            if (left.length > 0) {
                scheduleSync(1000);
            }
        } catch {
            // Still offline, or the server is away: everything stays in the outbox for the next try.
        } finally {
            syncing = false;

            // Drawn again only when something changed, and not under the technician's fingers.
            if (snapshot(pack) !== before) {
                renderSoon();
            } else {
                updateBadge();
            }
        }
    };

    const snapshot = (value) => (value ? JSON.stringify([value.visits, value.location, value.stockLocations]) : '');

    const isEditing = () => {
        const active = document.activeElement;

        return signing || (active && view.contains(active) && /^(INPUT|TEXTAREA|SELECT)$/.test(active.tagName) && active.type !== 'button');
    };

    const renderSoon = () => {
        if (isEditing()) {
            renderPending = true;
            updateBadge();

            return;
        }

        renderPending = false;
        render();
    };

    const scheduleSync = (delay) => {
        clearTimeout(syncTimer);
        syncTimer = setTimeout(async () => {
            await sync();
            scheduleSync(syncEveryMs);
        }, delay);
    };

    const directionsUrl = (workOrder) => {
        const place = workOrder.latitude != null ? `${workOrder.latitude},${workOrder.longitude}` : workOrder.address;
        const agent = navigator.userAgent || '';

        if (/Android/i.test(agent)) {
            return 'geo:0,0?q=' + encodeURIComponent(place);
        }

        if (/iPhone|iPad|iPod/i.test(agent)) {
            return 'https://maps.apple.com/?daddr=' + encodeURIComponent(place);
        }

        return 'https://www.openstreetmap.org/directions?route=%3B' + encodeURIComponent(place);
    };

    const renderList = () => {
        const fragment = document.createDocumentFragment();

        if (pack.visits.length === 0) {
            fragment.append(element('div', 'alert alert-info', texts.noVisits));
        }

        let day = null;
        let group = null;

        pack.visits.forEach((visit) => {
            const key = visit.startUtc ? dayKey(visit.startUtc) : '';

            if (key !== day) {
                day = key;
                fragment.append(element('h2', 'h6 text-muted mt-3 mb-2', visit.startUtc ? dayText(visit.startUtc) : ''));
                group = element('div', 'list-group mb-2');
                fragment.append(group);
            }

            const link = element('a', 'list-group-item list-group-item-action d-flex gap-3 align-items-start field-visit-row');
            const when = element('div', 'field-visit-time text-nowrap fw-semibold', visit.startUtc ? timeText(visit.startUtc) : '');
            const body = element('div', 'flex-grow-1 min-w-0');
            const title = element('div', 'd-flex flex-wrap gap-2 align-items-center');

            link.href = '#visit/' + encodeURIComponent(visit.id);
            title.append(element('span', 'fw-semibold', visit.workOrder.summary || visit.workOrder.number), statusBadge(visit.status));
            body.append(title, element('div', 'small text-muted text-truncate', visit.workOrder.address || ''));
            body.append(element('div', 'small text-muted', visit.number + ' · ' + format(texts.workOrder, visit.workOrder.number)));
            link.append(when, body);
            group.append(link);
        });

        view.replaceChildren(fragment);
    };

    const section = (title) => {
        const card = element('div', 'card mb-3');
        const header = element('div', 'card-header fw-semibold', title);
        const body = element('div', 'card-body');

        card.append(header, body);

        return { card, body };
    };

    const renderStatus = (visit, parent) => {
        const next = (pack.nextStatuses[visit.status] || []).filter((status) => status !== 'Incomplete');
        const bar = element('div', 'd-flex flex-wrap gap-2 mb-3');

        next.forEach((status) => {
            const primary = status === 'Completed' || status === 'OnSite' || status === 'EnRoute';

            bar.append(button('btn ' + (primary ? 'btn-success' : 'btn-outline-secondary'), texts['action.' + status], () => queue({ kind: 'Status', visitId: visit.id, status })));
        });

        if ((pack.nextStatuses[visit.status] || []).includes('Incomplete')) {
            bar.append(button('btn btn-outline-danger', texts['action.Incomplete'], () => {
                incompleteFor = visit.id;
                render();
            }));
        }

        parent.append(bar);

        if (incompleteFor === visit.id) {
            const form = element('div', 'card card-body mb-3');
            const label = element('label', 'form-label', texts.reason);
            const reason = element('textarea', 'form-control mb-2');
            const actions = element('div', 'd-flex gap-2');

            reason.id = 'field-incomplete-reason';
            reason.rows = 2;
            label.htmlFor = reason.id;
            actions.append(
                button('btn btn-danger', texts.save, () => {
                    if (!reason.value.trim()) {
                        reason.classList.add('is-invalid');
                        reason.setAttribute('placeholder', texts.reasonRequired);

                        return;
                    }

                    incompleteFor = null;
                    queue({ kind: 'Status', visitId: visit.id, status: 'Incomplete', text: reason.value.trim() });
                }),
                button('btn btn-secondary', texts.cancel, () => {
                    incompleteFor = null;
                    render();
                }));
            form.append(label, reason, actions);
            parent.append(form);
        }
    };

    const renderQuestion = (visit, checklist, index, question) => {
        const row = element('div', 'mb-3');
        const id = `q-${index}-${question.id}`;
        const answer = (checklist.answers || []).find((item) => item.questionId === question.id);
        const value = answer ? answer.value : '';
        const label = element('label', 'form-label d-block', question.text);
        const save = (newValue) => queue({ kind: 'Answer', visitId: visit.id, checklistIndex: index, questionId: question.id, value: newValue });

        label.htmlFor = id;

        if (question.required) {
            label.append(element('span', 'text-danger ms-1', '*'));
        }

        row.append(label);

        const choices = { YesNo: [['Yes', texts.yes], ['No', texts.no]], PassFailNA: [['Pass', texts.pass], ['Fail', texts.fail], ['N/A', texts.na]] }[question.kind];

        if (choices) {
            const group = element('div', 'btn-group flex-wrap');

            group.setAttribute('role', 'group');
            group.setAttribute('aria-label', question.text);
            choices.forEach(([choice, text], position) => {
                const input = element('input', 'btn-check');
                const option = element('label', 'btn btn-outline-primary', text);

                input.type = 'radio';
                input.name = id;
                input.id = id + '-' + position;
                input.checked = value === choice;
                input.addEventListener('change', () => save(choice));
                option.htmlFor = input.id;
                group.append(input, option);
            });
            label.removeAttribute('for');
            row.append(group);
        } else if (question.kind === 'Choice') {
            const select = element('select', 'form-select');

            select.id = id;
            select.append(element('option', null, ''));
            (question.choices || []).forEach((choice) => {
                const option = element('option', null, choice);

                option.value = choice;
                option.selected = value === choice;
                select.append(option);
            });
            select.addEventListener('change', () => save(select.value));
            row.append(select);
        } else if (question.kind === 'MultiChoice') {
            const picked = new Set(value ? value.split('\n') : []);

            (question.choices || []).forEach((choice, position) => {
                const check = element('div', 'form-check');
                const input = element('input', 'form-check-input');
                const text = element('label', 'form-check-label', choice);

                input.type = 'checkbox';
                input.id = id + '-' + position;
                input.checked = picked.has(choice);
                input.addEventListener('change', () => {
                    if (input.checked) {
                        picked.add(choice);
                    } else {
                        picked.delete(choice);
                    }

                    save([...picked].join('\n'));
                });
                text.htmlFor = input.id;
                check.append(input, text);
                row.append(check);
            });
            label.removeAttribute('for');
        } else if (question.kind === 'Photo' || question.kind === 'Signature') {
            row.append(element('div', 'form-text', question.kind === 'Photo' ? texts.usePhotos : texts.useSignature));
        } else {
            const input = element('input', 'form-control');
            const isNumber = question.kind === 'Number' || question.kind === 'MeterReading';

            input.id = id;
            input.type = isNumber ? 'number' : question.kind === 'Date' ? 'date' : 'text';
            input.value = value;

            if (isNumber) {
                input.step = 'any';
                input.inputMode = 'decimal';

                if (question.min != null) {
                    input.min = question.min;
                }

                if (question.max != null) {
                    input.max = question.max;
                }
            }

            input.addEventListener('change', () => save(input.value.trim()));

            if (question.unit) {
                const group = element('div', 'input-group');

                group.append(input, element('span', 'input-group-text', question.unit));
                row.append(group);
            } else {
                row.append(input);
            }
        }

        if (question.help) {
            row.append(element('div', 'form-text', question.help));
        }

        return row;
    };

    const renderChecklists = (visit, parent) => {
        (visit.workOrder.checklists || []).forEach((checklist, index) => {
            const { card, body } = section(checklist.name || texts.checklists);
            let heading = null;

            (checklist.questions || []).forEach((question) => {
                if (question.section && question.section !== heading) {
                    heading = question.section;
                    body.append(element('h3', 'h6 mt-2', heading));
                }

                body.append(renderQuestion(visit, checklist, index, question));
            });

            parent.append(card);
        });
    };

    const renderParts = (visit, parent) => {
        const { card, body } = section(texts.parts);
        const lines = (visit.workOrder.lines || []).filter((line) => line.kind === 'Product');

        if (lines.length === 0) {
            body.append(element('p', 'text-muted mb-0', texts.noParts));
        }

        lines.forEach((line) => {
            const row = element('div', 'border-bottom pb-2 mb-2');
            const controls = element('div', 'd-flex flex-wrap gap-2 align-items-end');
            const quantityId = 'part-' + line.id;
            const quantityBox = element('div');
            const quantityLabel = element('label', 'form-label small mb-1', texts.quantity);
            const quantity = element('input', 'form-control field-quantity');
            let location = null;

            row.append(element('div', 'fw-semibold', line.description), element('div', 'small text-muted mb-1', format(texts.used, line.quantityUsed, line.quantityEstimated)));
            quantity.type = 'number';
            quantity.min = '1';
            quantity.step = '1';
            quantity.value = '1';
            quantity.id = quantityId;
            quantity.inputMode = 'numeric';
            quantityLabel.htmlFor = quantityId;
            quantityBox.append(quantityLabel, quantity);
            controls.append(quantityBox);

            if (line.isStocked && pack.stockLocations.length > 0) {
                const locationBox = element('div');
                const locationLabel = element('label', 'form-label small mb-1', texts.from);

                location = element('select', 'form-select');
                location.id = 'part-location-' + line.id;
                locationLabel.htmlFor = location.id;
                pack.stockLocations.forEach((stock) => {
                    const option = element('option', null, stock.name);

                    option.value = stock.id;
                    location.append(option);
                });
                locationBox.append(locationLabel, location);
                controls.append(locationBox);
            }

            controls.append(button('btn btn-outline-primary', texts.use, () => {
                const count = Number(quantity.value);

                if (!(count > 0)) {
                    quantity.classList.add('is-invalid');

                    return;
                }

                queue({ kind: 'Part', visitId: visit.id, lineId: line.id, quantity: count, locationId: location ? location.value : null });
            }));
            row.append(controls);
            body.append(row);
        });

        parent.append(card);
    };

    const renderNotes = (visit, parent) => {
        const { card, body } = section(texts.notes);
        const list = element('ul', 'list-unstyled small mb-3');
        const text = element('textarea', 'form-control mb-2');

        (visit.workOrder.notes || []).slice(-10).forEach((note) => {
            const item = element('li', 'mb-1');

            item.append(element('span', 'text-muted me-1', timeText(note.utc) + (note.by ? ' · ' + note.by : '')), document.createTextNode(note.text));
            list.append(item);
        });

        text.rows = 2;
        text.placeholder = texts.notePlaceholder;
        text.setAttribute('aria-label', texts.notes);
        body.append(list, text, button('btn btn-outline-primary', texts.addNote, () => {
            const value = text.value.trim();

            if (!value) {
                return;
            }

            queue({ kind: 'Note', visitId: visit.id, text: value });
        }));
        parent.append(card);
    };

    // Photos are made smaller on the device: at most 1600 pixels on the long side, as JPEG.
    const compress = async (file) => {
        const image = await new Promise((resolve, reject) => {
            const img = new Image();
            const url = URL.createObjectURL(file);

            img.onload = () => {
                URL.revokeObjectURL(url);
                resolve(img);
            };
            img.onerror = reject;
            img.src = url;
        });
        const scale = Math.min(1, 1600 / Math.max(image.naturalWidth, image.naturalHeight));
        const canvas = document.createElement('canvas');

        canvas.width = Math.round(image.naturalWidth * scale);
        canvas.height = Math.round(image.naturalHeight * scale);
        canvas.getContext('2d').drawImage(image, 0, 0, canvas.width, canvas.height);

        return canvas.toDataURL('image/jpeg', 0.8);
    };

    const renderPhotos = (visit, parent) => {
        const { card, body } = section(texts.photos);
        const input = element('input', 'form-control');
        const label = element('label', 'form-label', texts.takePhoto);

        body.append(element('p', 'mb-2', format(texts.photoCount, visit.workOrder.photos)));

        if (visit.workOrder.requiredPhotos > visit.workOrder.photos) {
            body.append(element('p', 'small text-warning-emphasis', format(texts.needsPhotos, visit.workOrder.requiredPhotos)));
        }

        input.type = 'file';
        input.accept = 'image/*';
        input.setAttribute('capture', 'environment');
        input.id = 'field-photo';
        label.htmlFor = input.id;
        input.addEventListener('change', async () => {
            const file = input.files && input.files[0];

            if (!file) {
                return;
            }

            const data = await compress(file);

            queue({ kind: 'Photo', visitId: visit.id, data });
        });
        body.append(label, input);
        parent.append(card);
    };

    const renderSignature = (visit, parent) => {
        const { card, body } = section(texts.signature);

        (visit.workOrder.signedBy || []).forEach((name) => body.append(element('p', 'mb-2 text-success', format(texts.signedBy, name))));

        if (visit.workOrder.requiresSignature && (visit.workOrder.signedBy || []).length === 0) {
            body.append(element('p', 'small text-warning-emphasis', texts.needsSignature));
        }

        const nameLabel = element('label', 'form-label', texts.signerName);
        const name = element('input', 'form-control mb-2');
        const canvas = element('canvas', 'field-signature mb-2');
        const context = canvas.getContext('2d');
        let drawn = false;
        let drawing = false;

        name.id = 'field-signer';
        name.autocomplete = 'name';
        nameLabel.htmlFor = name.id;
        canvas.setAttribute('aria-label', texts.signHere);
        canvas.setAttribute('role', 'img');

        const point = (event) => {
            const box = canvas.getBoundingClientRect();

            return [(event.clientX - box.left) * (canvas.width / box.width), (event.clientY - box.top) * (canvas.height / box.height)];
        };

        canvas.addEventListener('pointerdown', (event) => {
            drawing = true;
            signing = true;
            canvas.setPointerCapture(event.pointerId);
            context.beginPath();
            context.moveTo(...point(event));
        });
        canvas.addEventListener('pointermove', (event) => {
            if (!drawing) {
                return;
            }

            drawn = true;
            context.lineTo(...point(event));
            context.stroke();
        });
        canvas.addEventListener('pointerup', () => {
            drawing = false;
        });

        const actions = element('div', 'd-flex flex-wrap gap-2');

        actions.append(
            button('btn btn-outline-secondary', texts.clear, () => {
                context.clearRect(0, 0, canvas.width, canvas.height);
                drawn = false;
                signing = false;
            }),
            button('btn btn-primary', texts.saveSignature, () => {
                const signer = name.value.trim();

                if (!signer) {
                    name.classList.add('is-invalid');

                    return;
                }

                const data = drawn ? canvas.toDataURL('image/png') : null;

                signing = false;

                queue({ kind: 'Signature', visitId: visit.id, name: signer, data });
            }));
        body.append(nameLabel, name, element('div', 'form-text mb-1', texts.signHere), canvas, actions);
        parent.append(card);

        // The canvas draws at its shown size, so lines aren't stretched.
        requestAnimationFrame(() => {
            canvas.width = canvas.clientWidth || 600;
            canvas.height = canvas.clientHeight || 200;
            context.lineWidth = 2.5;
            context.lineCap = 'round';
            context.strokeStyle = '#111';
        });
    };

    const renderVisit = (visit) => {
        const fragment = document.createDocumentFragment();
        const back = element('a', 'd-inline-block mb-2', '‹ ' + texts.back);
        const workOrder = visit.workOrder;
        const header = element('div', 'mb-3');
        const title = element('div', 'd-flex flex-wrap gap-2 align-items-center');

        back.href = '#';
        title.append(element('h2', 'h5 mb-0', workOrder.summary || workOrder.number), statusBadge(visit.status));
        header.append(title, element('div', 'small text-muted', visit.number + ' · ' + format(texts.workOrder, workOrder.number) + (workOrder.typeName ? ' · ' + workOrder.typeName : '')));

        if (visit.startUtc) {
            header.append(element('div', 'small', dayText(visit.startUtc) + ' ' + timeText(visit.startUtc) + (visit.endUtc ? '–' + timeText(visit.endUtc) : '')));
        }

        if (visit.arrivalStartUtc && visit.arrivalEndUtc) {
            header.append(element('div', 'small text-muted', format(texts.arrival, timeText(visit.arrivalStartUtc), timeText(visit.arrivalEndUtc))));
        }

        fragment.append(back, header);
        renderStatus(visit, fragment);

        const place = section(workOrder.address || texts.access);
        const links = element('div', 'd-flex flex-wrap gap-2');

        if (workOrder.address || workOrder.latitude != null) {
            const directions = element('a', 'btn btn-outline-primary btn-sm', texts.directions);

            directions.href = directionsUrl(workOrder);
            directions.target = '_blank';
            directions.rel = 'noopener';
            links.append(directions);
        }

        if (workOrder.contact && workOrder.contact.phone) {
            const call = element('a', 'btn btn-outline-primary btn-sm', texts.call);
            const sms = element('a', 'btn btn-outline-primary btn-sm', texts.text);

            call.href = 'tel:' + workOrder.contact.phone;
            sms.href = 'sms:' + workOrder.contact.phone;
            links.append(call, sms);
        }

        if (workOrder.contact && workOrder.contact.name) {
            place.body.append(element('p', 'mb-2', workOrder.contact.name + (workOrder.contact.phone ? ' · ' + workOrder.contact.phone : '')));
        }

        if (workOrder.accessNotes) {
            place.body.append(element('p', 'mb-2 small', texts.access + ': ' + workOrder.accessNotes));
        }

        if (workOrder.description) {
            place.body.append(element('p', 'mb-2 small text-muted field-description', workOrder.description));
        }

        if (visit.isLead && visit.workOrder) {
            place.body.append(element('p', 'mb-2 small text-muted', texts.lead));
        }

        place.body.append(links);
        fragment.append(place.card);
        renderChecklists(visit, fragment);
        renderParts(visit, fragment);
        renderNotes(visit, fragment);
        renderPhotos(visit, fragment);
        renderSignature(visit, fragment);
        view.replaceChildren(fragment);
    };

    const renderLocation = () => {
        locationBox.replaceChildren();

        if (!pack || !('geolocation' in navigator)) {
            return;
        }

        if (locationBlocked) {
            locationBox.append(element('div', 'alert alert-secondary py-2 small', texts.blocked));

            return;
        }

        if (pack.location.consented) {
            const bar = element('div', 'd-flex flex-wrap align-items-center gap-2 small text-muted mb-3');

            bar.append(element('span', null, texts.sharingOn), button('btn btn-link btn-sm p-0', texts.stop, () => setConsent(false)));
            locationBox.append(bar);

            return;
        }

        let dismissed = false;

        try {
            dismissed = localStorage.getItem(dismissedKey) === pack.location.noticeVersion;
        } catch {
            // Without storage, the question shows each time.
        }

        if (dismissed) {
            const bar = element('div', 'd-flex flex-wrap align-items-center gap-2 small text-muted mb-3');

            bar.append(element('span', null, texts.sharingOff), button('btn btn-link btn-sm p-0', texts.start, () => setConsent(true)));
            locationBox.append(bar);

            return;
        }

        const card = element('div', 'card mb-3 border-info');
        const body = element('div', 'card-body');
        const actions = element('div', 'd-flex flex-wrap gap-2');

        actions.append(
            button('btn btn-primary', texts.allow, () => setConsent(true)),
            button('btn btn-outline-secondary', texts.notNow, () => {
                try {
                    localStorage.setItem(dismissedKey, pack.location.noticeVersion);
                } catch {
                    // Nothing to remember it in.
                }

                renderLocation();
            }));
        body.append(element('h2', 'h6', texts.consentTitle), element('p', 'small', format(texts.consentText, pack.location.intervalSeconds, retentionDays)), actions);
        card.append(body);
        locationBox.append(card);
    };

    const setConsent = async (given) => {
        try {
            const token = await tokenAsync();
            const result = await post('consent', { given }, token);

            pack.location.consented = result.consented;
            await savePack(pack);
        } catch {
            notice(texts.loadFailed);
        }

        render();
    };

    // Positions are taken only with consent and while working: a visit under way, or one still to go to today.
    const isWorking = () => pack && pack.visits.some((visit) => underWay.includes(visit.status)
        || (activeStatuses.includes(visit.status) && visit.startUtc && dayKey(visit.startUtc) === dayKey(Date.now())));

    const updateTracking = () => {
        const shouldTrack = pack && pack.location.consented && isWorking() && 'geolocation' in navigator;

        if (shouldTrack && watchId === null) {
            watchId = navigator.geolocation.watchPosition(async (position) => {
                const now = Date.now();

                if (now - lastPingMs < pack.location.intervalSeconds * 1000) {
                    return;
                }

                lastPingMs = now;
                await store('pings', 'readwrite', (objects) => objects.add({
                    workerId,
                    ping: {
                        utc: new Date(position.timestamp || now).toISOString(),
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracyMeters: position.coords.accuracy,
                        speed: position.coords.speed,
                        heading: position.coords.heading,
                    },
                }));
                scheduleSync(500);
            }, (error) => {
                if (error.code === error.PERMISSION_DENIED) {
                    locationBlocked = true;
                    navigator.geolocation.clearWatch(watchId);
                    watchId = null;
                    renderLocation();
                }
            }, { enableHighAccuracy: true, maximumAge: 30000, timeout: 60000 });
        } else if (!shouldTrack && watchId !== null) {
            navigator.geolocation.clearWatch(watchId);
            watchId = null;
        }
    };

    function render() {
        updateBadge();

        if (!pack) {
            return;
        }

        renderLocation();

        const match = /^#visit\/(.+)$/.exec(window.location.hash);
        const visit = match ? findVisit(decodeURIComponent(match[1])) : null;

        if (visit) {
            renderVisit(visit);
        } else {
            renderList();
        }

        updateTracking();
    }

    // A redraw put off while the technician typed happens once they leave the field.
    view.addEventListener('focusout', () => setTimeout(() => {
        if (renderPending && !isEditing()) {
            renderSoon();
        }
    }, 0));

    window.addEventListener('hashchange', () => {
        signing = false;
        incompleteFor = null;
        render();
        window.scrollTo(0, 0);
    });
    window.addEventListener('online', () => scheduleSync(0));
    window.addEventListener('offline', updateBadge);

    // The pack on the device shows at once; the server's comes next, when online.
    (async () => {
        try {
            pack = await loadPack();
            pending = (await mine('outbox')).length;
        } catch {
            pack = null;
        }

        render();
        await sync();

        if (!pack) {
            view.replaceChildren(element('div', 'alert alert-warning', texts.loadFailed));
        }

        scheduleSync(syncEveryMs);
    })();
})();
