/*
 * Transfers a server-side transfer service carries out, rather than the telephony provider.
 *
 * A Contact Center call cannot be transferred by handing the provider a number: an agent or a queue is not somewhere a
 * provider can dial, and the Contact Center has to route the call, move it off the transferring agent and record the
 * transfer. When the tenant publishes transfer endpoints (the Contact Center does, and the soft phone's configuration
 * carries their addresses), a call that belongs to an interaction is transferred through them: the panel lists the
 * agents with their presence, the queues with who is waiting, and the approved outside numbers, and a warm transfer
 * becomes a consult the agent completes or cancels. Every other call keeps the provider's own transfer.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function label(strings, key, fallback) {
        return strings && typeof strings[key] === 'string' && strings[key] ? strings[key] : fallback;
    }

    function format(template, value) {
        return String(template).replace('{0}', value);
    }

    function presenceLabel(presence, strings) {
        var key = 'presence' + String(presence || '');

        return label(strings, key, String(presence || ''));
    }

    // The service's directory as the transfer panel lists it. Each entry's destination is unique across kinds, and
    // targetType/targetId are what the transfer request names.
    function serviceDirectoryEntries(directory, strings) {
        if (!directory) {
            return [];
        }

        var entries = [];
        var agentsGroup = label(strings, 'transferGroupAgents', 'Agents');
        var queuesGroup = label(strings, 'transferGroupQueues', 'Queues');
        var externalGroup = label(strings, 'transferGroupExternal', 'Outside numbers');

        (directory.agents || []).forEach(function (agent) {
            if (!agent || !agent.id) {
                return;
            }

            entries.push({
                id: String(agent.id),
                name: String(agent.name || agent.id),
                destination: 'agent:' + agent.id,
                detail: agent.extension ? format(label(strings, 'transferExtension', 'Ext {0}'), agent.extension) : '',
                extension: agent.extension ? String(agent.extension) : '',
                kind: 'agent',
                targetType: 'agent',
                targetId: String(agent.id),
                group: agentsGroup,
                presence: String(agent.presence || ''),
                status: presenceLabel(agent.presence, strings),
                disabled: agent.available !== true
            });
        });

        (directory.queues || []).forEach(function (queue) {
            if (!queue || !queue.id) {
                return;
            }

            entries.push({
                id: String(queue.id),
                name: String(queue.name || queue.id),
                destination: 'queue:' + queue.id,
                detail: format(label(strings, 'transferWaiting', '{0} waiting'), Number(queue.waiting) || 0),
                kind: 'queue',
                targetType: 'queue',
                targetId: String(queue.id),
                group: queuesGroup,
                status: '',
                disabled: false
            });
        });

        (directory.externalDestinations || []).forEach(function (destination) {
            if (!destination || !destination.id) {
                return;
            }

            entries.push({
                id: String(destination.id),
                name: String(destination.name || destination.number || destination.id),
                destination: 'external:' + destination.id,
                detail: String(destination.number || ''),
                kind: 'external',
                targetType: 'external',
                targetId: String(destination.id),
                group: externalGroup,
                status: '',
                disabled: false
            });
        });

        return entries;
    }

    // Blind always; warm only when the call's provider can hold the caller while the agent consults.
    function serviceModes(directory) {
        return directory && directory.supportsConsult ? ['blind', 'warm'] : ['blind'];
    }

    // What the service should transfer the call to.
    //   selected   - the entry the agent picked, if any; it wins in either dial mode.
    //   query      - what the agent typed.
    //   dialMode   - 'number' (the default) or 'extension', as the panel's Number / Extension toggle is set.
    //   number     - in number mode, what the country-flag input read from the query: { value, valid }.
    //   mode       - 'blind' or 'warm'.
    //   directory  - the service's directory (for whether outside numbers may be typed).
    //   ownNumbers - the tenant's own numbers, which are never a destination.
    // Returns { targetType, targetId, label, refused }, refused being '' or one of
    // 'empty' | 'unavailable' | 'warm-queue' | 'external-not-allowed' | 'invalid-number' | 'invalid-extension' |
    // 'own-number'. An extension is sent as targetType 'extension': the server resolves it to the agent it rings.
    function resolveServiceTarget(options) {
        options = options || {};

        var selected = options.selected;
        var refuse = function (reason) {
            return { targetType: '', targetId: '', label: '', refused: reason };
        };

        if (selected && selected.targetType) {
            if (selected.disabled) {
                return refuse('unavailable');
            }

            if (options.mode === 'warm' && selected.targetType === 'queue') {
                return refuse('warm-queue');
            }

            return { targetType: selected.targetType, targetId: selected.targetId, label: selected.name, refused: '' };
        }

        var text = options.query == null ? '' : String(options.query).trim();

        if (options.dialMode === 'extension') {
            if (!text) {
                return refuse('empty');
            }

            var extension = softPhone.readExtension(text);

            return extension
                ? { targetType: 'extension', targetId: extension, label: text, refused: '' }
                : refuse('invalid-extension');
        }

        if (!text || !softPhone.isNumberLike(text)) {
            return refuse('empty');
        }

        var directory = options.directory || {};

        if (!directory.canTransferExternally || !directory.allowExternalNumbers) {
            return refuse('external-not-allowed');
        }

        var reading = options.number || null;
        var number = reading ? (reading.valid ? String(reading.value || '') : '') : softPhone.toInternationalNumber(text);

        if ((options.ownNumbers || []).some(function (own) { return softPhone.isSameLine(text, own) || softPhone.isSameLine(number, own); })) {
            return refuse('own-number');
        }

        if (!number) {
            return refuse('invalid-number');
        }

        return { targetType: 'external', targetId: number, label: text, refused: '' };
    }

    // The client for the transfer endpoints.
    //   options.urls              - { targetsUrl, transferUrl, consultUrl, consultCompleteUrl, consultCancelUrl }.
    //   options.antiForgeryToken  - sent on every command.
    //   options.fetch             - the fetch implementation (the browser's by default).
    function createTransferService(options) {
        options = options || {};

        var urls = options.urls || {};
        var fetchImpl = options.fetch || (typeof root.fetch === 'function' ? root.fetch.bind(root) : null);

        function interactionOf(call) {
            return call && call.metadata && call.metadata.interactionId ? String(call.metadata.interactionId) : '';
        }

        function applies(call) {
            return !!(fetchImpl && urls.targetsUrl && urls.transferUrl && interactionOf(call));
        }

        function read(response) {
            return Promise.resolve(response.json ? response.json() : null).catch(function () { return null; }).then(function (body) {
                if (!response.ok) {
                    // The endpoints answer a refusal with a problem body rather than a redirect, so its reason is
                    // what the agent is shown.
                    return { succeeded: false, error: (body && (body.detail || body.error || body.title)) || 'The request was refused.' };
                }

                return body || {};
            });
        }

        function send(method, url, body) {
            var headers = { 'Accept': 'application/json' };
            var init = { method: method, credentials: 'same-origin', headers: headers };

            if (body) {
                headers['Content-Type'] = 'application/json';
                init.body = JSON.stringify(body);
            }

            if (method !== 'GET' && options.antiForgeryToken) {
                headers.RequestVerificationToken = options.antiForgeryToken;
            }

            return fetchImpl(url, init).then(read).catch(function (error) {
                return { succeeded: false, error: error && error.message ? error.message : String(error) };
            });
        }

        function query(url, parameters) {
            var pairs = Object.keys(parameters).filter(function (key) { return parameters[key]; }).map(function (key) {
                return encodeURIComponent(key) + '=' + encodeURIComponent(parameters[key]);
            });

            return url + (pairs.length ? (url.indexOf('?') === -1 ? '?' : '&') + pairs.join('&') : '');
        }

        function loadDirectory(call) {
            return send('GET', query(urls.targetsUrl, { interactionId: interactionOf(call) })).then(function (body) {
                if (body && body.succeeded === false) {
                    throw new Error(body.error);
                }

                return body;
            });
        }

        function transfer(call, target) {
            return send('POST', urls.transferUrl, { interactionId: interactionOf(call), targetType: target.targetType, targetId: target.targetId });
        }

        function startConsult(call, target) {
            return send('POST', urls.consultUrl, { interactionId: interactionOf(call), targetType: target.targetType, targetId: target.targetId });
        }

        function getConsult(call, consultId) {
            return send('GET', query(urls.consultUrl, { interactionId: interactionOf(call), consultId: consultId }));
        }

        function completeConsult(call, consultId) {
            return send('POST', urls.consultCompleteUrl, { interactionId: interactionOf(call), consultId: consultId });
        }

        function cancelConsult(call, consultId) {
            return send('POST', urls.consultCancelUrl, { interactionId: interactionOf(call), consultId: consultId });
        }

        return {
            applies: applies,
            loadDirectory: loadDirectory,
            transfer: transfer,
            startConsult: startConsult,
            getConsult: getConsult,
            completeConsult: completeConsult,
            cancelConsult: cancelConsult
        };
    }

    softPhone.serviceDirectoryEntries = serviceDirectoryEntries;
    softPhone.serviceModes = serviceModes;
    softPhone.resolveServiceTarget = resolveServiceTarget;
    softPhone.createTransferService = createTransferService;
}(typeof globalThis !== 'undefined' ? globalThis : window));
