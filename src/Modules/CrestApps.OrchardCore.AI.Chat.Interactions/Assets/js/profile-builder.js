/*
 * AI Profile Builder feedback panel.
 *
 * Drives the panel beside the role-play chat on a builder session page. The chat itself is the stock chat
 * interaction client (window.chatInteractionManager); this script reaches its Vue instance through the config
 * element it was mounted from, and:
 *
 * - handles the Feedback / Looks good buttons the builder's message template adds under each reply,
 * - posts feedback, then writes the rewritten settings into the settings panel so its autosave keeps them,
 * - for "feedback & regenerate", removes the rejected reply and the message it answered from the chat and sends
 *   that message again, so the new instructions answer it,
 * - shows the feedback ledger, the revision history and the replay results, and saves the result as a profile,
 * - runs simulated conversations round by round: each round is one request, and when the coach rewrote the
 *   instructions after a round, the next round tests them, until the run is finished or the author stops it.
 */
(function (window, document) {
    'use strict';

    var FEEDBACK_KIND = {
        Feedback: 0,
        FeedbackAndRegenerate: 1,
        Approved: 2,
    };

    var panel = document.getElementById('profile-builder-panel');

    if (!panel) {
        return;
    }

    var config = parseConfig(panel.getAttribute('data-profile-builder-config'));
    var strings = config.strings || {};
    var state = null;
    var selectedIndex = null;
    var busy = false;
    var refreshTimer = null;
    var simulationFormFilled = false;
    var stopSimulation = false;

    function parseConfig(raw) {
        try {
            return JSON.parse(raw || '{}');
        } catch (error) {
            console.error('Profile Builder: the panel configuration could not be read.', error);

            return {};
        }
    }

    function byId(id) {
        return document.getElementById(id);
    }

    function format(template, value) {
        return String(template || '').replace('{0}', value);
    }

    function getChatApp() {
        var element = document.querySelector('[data-chat-interaction-config]');

        return element && element.__chatInteractionApp ? element.__chatInteractionApp : null;
    }

    function waitForChatApp() {
        return new Promise(function (resolve) {
            var attempts = 0;
            var timer = window.setInterval(function () {
                var app = getChatApp();

                attempts++;

                if (app || attempts > 150) {
                    window.clearInterval(timer);
                    resolve(app);
                }
            }, 100);
        });
    }

    async function request(url, method, body) {
        var response = await fetch(url, {
            method: method || 'GET',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'RequestVerificationToken': config.antiforgeryToken || '',
            },
            body: body === undefined ? undefined : JSON.stringify(body),
        });

        if (!response.ok) {
            throw new Error('Request failed with status ' + response.status + '.');
        }

        return await response.json();
    }

    function setStatus(id, text, tone) {
        var element = byId(id);

        if (!element) {
            return;
        }

        element.textContent = text || '';
        element.className = 'small mb-3' + (tone ? ' text-' + tone : '');
    }

    function truncate(text, length) {
        text = String(text || '');

        return text.length > length ? text.substring(0, length) + '...' : text;
    }

    function create(tag, className, text) {
        var element = document.createElement(tag);

        if (className) {
            element.className = className;
        }

        if (text !== undefined && text !== null) {
            element.textContent = text;
        }

        return element;
    }

    function formatDate(value) {
        if (!value) {
            return '';
        }

        var date = new Date(value);

        return isNaN(date.getTime()) ? '' : date.toLocaleString();
    }

    // ----- Chat messages -----

    function assistantEntries(app) {
        var entries = [];

        (app.messages || []).forEach(function (message, index) {
            if (message && message.role === 'assistant') {
                entries.push({ index: index, message: message });
            }
        });

        return entries;
    }

    function isLatestReply(app, index) {
        var entries = assistantEntries(app);

        return entries.length > 0 && entries[entries.length - 1].index === index;
    }

    function isVoiceActive(app) {
        return !!(app && (app.isConversationMode || (app.realtimeController && app.realtimeController.isActive && app.realtimeController.isActive())));
    }

    // Pairs each reply shown with the stored reply in the same position, so feedback can name the stored prompt and
    // each reply shows the badge of the feedback last given on it.
    function syncMessages() {
        var app = getChatApp();

        if (!app || !state) {
            return;
        }

        var storedReplies = (state.conversation || []).filter(function (prompt) {
            return prompt.role === 'assistant';
        });

        var latestFeedback = {};

        (state.feedback || []).forEach(function (entry) {
            latestFeedback[entry.promptId] = entry;
        });

        assistantEntries(app).forEach(function (entry, ordinal) {
            var stored = storedReplies[ordinal];
            var promptId = stored ? stored.id : null;
            var feedback = promptId ? latestFeedback[promptId] : null;

            entry.message.builderPromptId = promptId;

            if (!feedback) {
                entry.message.builderBadge = null;
                entry.message.builderBadgeClass = null;
            } else if (feedback.kind === 'Approved') {
                entry.message.builderBadge = strings.approvedBadge;
                entry.message.builderBadgeClass = 'text-bg-success';
            } else {
                entry.message.builderBadge = strings.feedbackBadge;
                entry.message.builderBadgeClass = 'text-bg-warning';
            }
        });
    }

    function selectReply(index) {
        var app = getChatApp();
        var message = app && app.messages ? app.messages[index] : null;

        if (!message || message.role !== 'assistant') {
            return;
        }

        selectedIndex = index;

        var preview = byId('pb-selected-reply');
        preview.textContent = truncate(message.content, 1200);
        preview.classList.remove('text-muted');

        document.querySelectorAll('.profile-builder-selected-message').forEach(function (element) {
            element.classList.remove('profile-builder-selected-message');
        });

        var item = document.querySelector('.ai-chat-message-item[data-builder-message-index="' + index + '"]');

        if (item) {
            item.classList.add('profile-builder-selected-message');
        }

        updateButtons();

        var tab = byId('pb-tab-feedback');

        if (tab && window.bootstrap && window.bootstrap.Tab) {
            window.bootstrap.Tab.getOrCreateInstance(tab).show();
        }

        byId('pb-comment').focus();
    }

    function clearSelection() {
        selectedIndex = null;

        var preview = byId('pb-selected-reply');
        preview.textContent = strings.selectReply;
        preview.classList.add('text-muted');

        document.querySelectorAll('.profile-builder-selected-message').forEach(function (element) {
            element.classList.remove('profile-builder-selected-message');
        });

        updateButtons();
    }

    function updateButtons() {
        var app = getChatApp();
        var hasSelection = selectedIndex !== null;
        var canRegenerate = hasSelection && app && isLatestReply(app, selectedIndex) && !isVoiceActive(app);

        byId('pb-send-feedback').disabled = busy || !hasSelection;
        var regenerateButton = byId('pb-send-regenerate');

        if (!regenerateButton.hasAttribute('data-default-title')) {
            regenerateButton.setAttribute('data-default-title', regenerateButton.title);
        }

        regenerateButton.disabled = busy || !canRegenerate;
        regenerateButton.title = hasSelection && !canRegenerate
            ? strings.regenerateOnlyLatest
            : regenerateButton.getAttribute('data-default-title');
        byId('pb-replay').disabled = busy;
        byId('pb-sim-run').disabled = busy;
    }

    // ----- Settings panel -----

    function setSettingValue(name, value) {
        var input = document.querySelector('.setting-input[data-setting="' + name + '"]');

        if (input) {
            input.value = value === null || value === undefined ? '' : value;
        }
    }

    // Writes the settings into the panel without raising change events: the server already saved them, and the
    // panel's next autosave, whatever triggers it, sends these values rather than the ones they replaced.
    function applySettings(settings) {
        if (!settings) {
            return;
        }

        setSettingValue('systemMessage', settings.systemMessage);
        setSettingValue('temperature', settings.temperature);
        setSettingValue('topP', settings.topP);
        setSettingValue('frequencyPenalty', settings.frequencyPenalty);
        setSettingValue('presencePenalty', settings.presencePenalty);
        setSettingValue('maxTokens', settings.maxTokens);
        setSettingValue('pastMessagesCount', settings.pastMessagesCount);

        var initialPrompt = byId('pb-initial-prompt');

        if (initialPrompt && document.activeElement !== initialPrompt) {
            initialPrompt.value = settings.initialPrompt || '';
        }
    }

    async function flushSettings(app) {
        if (app && typeof app.flushPendingSave === 'function') {
            try {
                await app.flushPendingSave();
            } catch (error) {
                console.warn('Profile Builder: the pending settings save failed.', error);
            }
        }
    }

    // ----- Rendering -----

    function renderFeedback() {
        var list = byId('pb-feedback-list');
        list.replaceChildren();

        var entries = (state && state.feedback) || [];

        if (entries.length === 0) {
            list.appendChild(create('div', 'text-muted', strings.noFeedback));

            return;
        }

        entries.slice().reverse().forEach(function (entry) {
            var tone = entry.kind === 'Approved' ? 'is-approved' : entry.wasRegenerated ? 'is-rejected' : 'is-feedback';
            var item = create('div', 'profile-builder-item ' + tone);
            var header = create('div', 'd-flex justify-content-between gap-2');
            var label = strings.kinds && strings.kinds[entry.kind] ? strings.kinds[entry.kind] : entry.kind;

            header.appendChild(create('span', 'fw-semibold', label));

            if (entry.revisionNumber) {
                header.appendChild(create('span', 'text-muted', format(strings.revision, entry.revisionNumber)));
            }

            item.appendChild(header);

            if (entry.comment) {
                item.appendChild(create('div', 'mb-1', entry.comment));
            }

            if (entry.customerMessage) {
                item.appendChild(create('div', 'text-muted profile-builder-quote', '> ' + truncate(entry.customerMessage, 300)));
            }

            item.appendChild(create('div', 'text-muted profile-builder-quote fst-italic', truncate(entry.assistantReply, 300)));
            list.appendChild(item);
        });
    }

    function renderRevisions() {
        var list = byId('pb-revision-list');
        list.replaceChildren();

        var revisions = (state && state.revisions) || [];

        if (revisions.length === 0) {
            list.appendChild(create('div', 'text-muted', strings.noRevisions));

            return;
        }

        var currentNumber = revisions[revisions.length - 1].number;

        revisions.slice().reverse().forEach(function (revision) {
            var item = create('div', 'profile-builder-item');
            var header = create('div', 'd-flex justify-content-between align-items-center gap-2');
            var title = create('span', 'fw-semibold', format(strings.revision, revision.number));
            var source = strings.sources && strings.sources[revision.source] ? strings.sources[revision.source] : revision.source;

            title.appendChild(create('span', 'badge text-bg-light ms-2', source));

            if (revision.number === currentNumber) {
                title.appendChild(create('span', 'badge text-bg-primary ms-1', strings.current));
            }

            header.appendChild(title);

            if (revision.number !== currentNumber) {
                var restore = create('button', 'btn btn-sm btn-outline-secondary py-0', strings.sources ? strings.sources.Restore : 'Restore');
                restore.type = 'button';
                restore.setAttribute('data-builder-restore', revision.number);
                header.appendChild(restore);
            }

            item.appendChild(header);

            if (revision.summary) {
                item.appendChild(create('div', '', revision.summary));
            }

            item.appendChild(create('div', 'text-muted', formatDate(revision.createdUtc)));

            var details = create('details', 'mt-1');
            details.appendChild(create('summary', 'text-muted', 'System message'));
            details.appendChild(create('pre', 'small mb-0 text-wrap', (revision.settings && revision.settings.systemMessage) || ''));
            item.appendChild(details);

            list.appendChild(item);
        });
    }

    function renderReplay() {
        var list = byId('pb-replay-results');
        list.replaceChildren();

        var replay = state && state.lastReplay;

        if (!replay) {
            list.appendChild(create('div', 'text-muted', strings.noReplay));

            return;
        }

        var counts = { Pass: 0, Fail: 0, Unknown: 0 };

        (replay.cases || []).forEach(function (replayCase) {
            counts[replayCase.verdict] = (counts[replayCase.verdict] || 0) + 1;
        });

        list.appendChild(create('div', 'fw-semibold',
            format(strings.revision, replay.revisionNumber) + ': ' +
            counts.Pass + ' ' + strings.pass + ', ' +
            counts.Fail + ' ' + strings.fail + ', ' +
            counts.Unknown + ' ' + strings.unknown));

        (replay.cases || []).forEach(function (replayCase) {
            var tone = replayCase.verdict === 'Pass' ? 'is-approved' : replayCase.verdict === 'Fail' ? 'is-rejected' : 'is-feedback';
            var item = create('div', 'profile-builder-item ' + tone);
            var verdict = replayCase.verdict === 'Pass' ? strings.pass : replayCase.verdict === 'Fail' ? strings.fail : strings.unknown;

            item.appendChild(create('div', 'fw-semibold', verdict));
            item.appendChild(create('div', 'text-muted profile-builder-quote', '> ' + truncate(replayCase.customerMessage, 300)));

            if (replayCase.reason) {
                item.appendChild(create('div', '', replayCase.reason));
            }

            if (replayCase.reply) {
                var details = create('details', 'mt-1');
                details.appendChild(create('summary', 'text-muted', 'Reply'));
                details.appendChild(create('div', 'profile-builder-quote', replayCase.reply));
                item.appendChild(details);
            }

            list.appendChild(item);
        });
    }

    function fillSimulationForm() {
        var settings = state && state.simulationSettings;

        if (!settings || simulationFormFilled) {
            return;
        }

        simulationFormFilled = true;
        byId('pb-sim-scenarios').value = (settings.scenarios || []).join('\n\n');
        byId('pb-sim-criteria').value = (settings.criteria || []).join('\n');
        byId('pb-sim-customer-deployment').value = settings.customerDeploymentName || '';
        byId('pb-sim-judge-deployment').value = settings.judgeDeploymentName || '';
        byId('pb-sim-turns').value = settings.maxTurns;
        byId('pb-sim-rounds').value = settings.rounds;
        byId('pb-sim-passing-score').value = settings.passingScore;
        byId('pb-sim-improve').checked = settings.improve !== false;
    }

    function readSimulationForm() {
        var number = function (id, fallback) {
            var value = parseInt(byId(id).value, 10);

            return isNaN(value) ? fallback : value;
        };

        return {
            scenarios: byId('pb-sim-scenarios').value.split(/\n\s*\n/).map(function (text) { return text.trim(); }).filter(Boolean),
            criteria: byId('pb-sim-criteria').value.split('\n').map(function (text) { return text.trim(); }).filter(Boolean),
            customerDeploymentName: byId('pb-sim-customer-deployment').value || null,
            judgeDeploymentName: byId('pb-sim-judge-deployment').value || null,
            maxTurns: number('pb-sim-turns', 8),
            rounds: number('pb-sim-rounds', 3),
            passingScore: number('pb-sim-passing-score', 70),
            improve: byId('pb-sim-improve').checked,
        };
    }

    function renderSimulations() {
        var list = byId('pb-sim-results');
        list.replaceChildren();

        var simulations = (state && state.simulations) || [];

        if (simulations.length === 0) {
            list.appendChild(create('div', 'text-muted', strings.noSimulations));

            return;
        }

        // Newest round first, so the latest results are on top.
        simulations.slice().reverse().forEach(function (simulation) {
            var tone = simulation.error ? 'is-feedback' : simulation.passed ? 'is-approved' : 'is-rejected';
            var item = create('div', 'profile-builder-item ' + tone);
            var header = create('div', 'd-flex justify-content-between align-items-center gap-2');
            var title = create('span', 'fw-semibold', format(strings.round, simulation.round));

            title.appendChild(create('span', 'badge text-bg-light ms-2', format(strings.revision, simulation.revisionNumber)));
            header.appendChild(title);

            if (!simulation.error) {
                header.appendChild(create('span', 'badge ' + (simulation.passed ? 'text-bg-success' : 'text-bg-danger'),
                    (simulation.passed ? strings.pass : strings.fail) + ' · ' + format(strings.score, simulation.score)));
            }

            item.appendChild(header);
            item.appendChild(create('div', 'text-muted profile-builder-quote', truncate(simulation.scenario, 300)));

            if (simulation.error) {
                item.appendChild(create('div', 'text-danger', simulation.error));
            }

            if (simulation.summary) {
                item.appendChild(create('div', '', simulation.summary));
            }

            if (simulation.problems && simulation.problems.length > 0) {
                var problems = create('ul', 'mb-1 ps-3');

                simulation.problems.forEach(function (problem) {
                    problems.appendChild(create('li', '', problem));
                });

                item.appendChild(problems);
            }

            if (simulation.transcript && simulation.transcript.length > 0) {
                var details = create('details', 'mt-1');
                details.appendChild(create('summary', 'text-muted', strings.transcript));

                simulation.transcript.forEach(function (turn) {
                    var line = create('div', 'profile-builder-quote mb-1');
                    var assistant = String(turn.role || '').toLowerCase() === 'assistant';

                    line.appendChild(create('span', 'fw-semibold', (assistant ? strings.assistant : strings.customer) + ': '));
                    line.appendChild(document.createTextNode(turn.text || ''));
                    details.appendChild(line);
                });

                item.appendChild(details);
            }

            list.appendChild(item);
        });
    }

    function setSimulationStatus(text, tone) {
        setStatus('pb-sim-status', text, tone);
    }

    async function runSimulation() {
        if (busy) {
            return;
        }

        var form = readSimulationForm();

        if (form.scenarios.length === 0) {
            setSimulationStatus(strings.scenarioRequired, 'danger');

            return;
        }

        busy = true;
        stopSimulation = false;
        updateButtons();
        byId('pb-sim-stop').classList.remove('d-none');

        var runId = null;
        var round = 1;

        try {
            await flushSettings(getChatApp());

            var saved = await request(config.urls.simulationSettings, 'POST', form);

            if (!saved.succeeded) {
                setSimulationStatus(saved.error || strings.error, 'danger');

                return;
            }

            state = saved.state;

            while (true) {
                setSimulationStatus(format(strings.simulating, round), 'muted');

                var url = config.urls.simulate + (runId ? (config.urls.simulate.indexOf('?') >= 0 ? '&' : '?') + 'runId=' + encodeURIComponent(runId) : '');
                var result = await request(url, 'POST', {});

                if (!result.succeeded) {
                    setSimulationStatus(result.error || strings.error, 'danger');

                    break;
                }

                state = result.state;
                runId = result.runId;

                // The coach saved new instructions on the server; the settings panel shows them so its autosave
                // keeps them rather than writing the old ones back.
                applySettings(state.settings);
                render();

                if (result.allPassed) {
                    setSimulationStatus(format(strings.simulationPassed, result.round), 'success');

                    break;
                }

                if (result.finished) {
                    setSimulationStatus(format(strings.simulationFinished, result.round), 'warning');

                    break;
                }

                if (stopSimulation) {
                    setSimulationStatus(format(strings.simulationStopped, result.round), 'muted');

                    break;
                }

                setSimulationStatus(format(format(strings.simulationImproved, result.round).replace('{1}', '{0}'), result.revisionNumber), 'muted');
                round = result.round + 1;
            }
        } catch (error) {
            console.error('Profile Builder: the simulation failed.', error);
            setSimulationStatus(strings.error, 'danger');
        } finally {
            busy = false;
            byId('pb-sim-stop').classList.add('d-none');
            updateButtons();
        }
    }

    function render() {
        fillSimulationForm();
        renderFeedback();
        renderRevisions();
        renderReplay();
        renderSimulations();
        syncMessages();
        updateButtons();
    }

    async function refreshState() {
        try {
            state = await request(config.urls.state);
            render();
        } catch (error) {
            console.warn('Profile Builder: the session state could not be loaded.', error);
        }
    }

    function scheduleRefresh() {
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(function () {
            var app = getChatApp();

            // Wait for the reply to finish; the stored conversation only has it once it has.
            if (app && (app.messages || []).some(function (message) { return message && message.isStreaming; })) {
                scheduleRefresh();

                return;
            }

            refreshState();
        }, 600);
    }

    // ----- Actions -----

    async function submitFeedback(kind) {
        var app = getChatApp();

        if (!app || busy) {
            return;
        }

        var message = selectedIndex !== null ? app.messages[selectedIndex] : null;

        if (!message) {
            setStatus('pb-status', strings.selectReply, 'danger');

            return;
        }

        var comment = byId('pb-comment').value.trim();

        if (kind !== FEEDBACK_KIND.Approved && !comment) {
            setStatus('pb-status', strings.commentRequired, 'danger');

            return;
        }

        var ordinal = assistantEntries(app).findIndex(function (entry) {
            return entry.index === selectedIndex;
        });

        busy = true;
        updateButtons();
        setStatus('pb-status', kind === FEEDBACK_KIND.Approved ? '' : strings.working, 'muted');

        try {
            await flushSettings(app);

            var result = await request(config.urls.feedback, 'POST', {
                promptId: message.builderPromptId || message.id || null,
                replyText: message.content || '',
                assistantIndex: ordinal,
                kind: kind,
                comment: comment,
            });

            if (!result.succeeded) {
                setStatus('pb-status', result.error || strings.error, 'danger');

                return;
            }

            state = result.state;
            applySettings(state.settings);

            var regenerateIndex = selectedIndex;

            byId('pb-comment').value = '';
            clearSelection();
            render();

            if (result.regenerateMessage) {
                setStatus('pb-status', strings.regenerating + (result.revisionSummary ? ' ' + result.revisionSummary : ''), 'success');
                regenerate(app, regenerateIndex, result.regenerateMessage);
            } else if (kind === FEEDBACK_KIND.Approved) {
                setStatus('pb-status', strings.approved, 'success');
            } else {
                setStatus('pb-status', strings.updated + (result.revisionSummary ? ' ' + result.revisionSummary : ''), 'success');
            }

            if (kind !== FEEDBACK_KIND.Approved && isVoiceActive(app)) {
                setStatus('pb-status', strings.voiceNotice, 'warning');
            }
        } catch (error) {
            console.error('Profile Builder: the feedback could not be sent.', error);
            setStatus('pb-status', strings.error, 'danger');
        } finally {
            busy = false;
            updateButtons();
        }
    }

    // The server has already deleted the rejected reply and the message it answered; take them off the screen
    // and send that message again through the chat client, so it streams like any other turn.
    function regenerate(app, replyIndex, customerMessage) {
        var removeFrom = replyIndex;

        if (replyIndex > 0 && app.messages[replyIndex - 1] && app.messages[replyIndex - 1].role === 'user') {
            removeFrom = replyIndex - 1;
        }

        app.messages.splice(removeFrom, replyIndex - removeFrom + 1);
        sendThroughChat(app, customerMessage);
    }

    function sendThroughChat(app, text) {
        app.prompt = text;

        if (app.inputElement) {
            app.inputElement.value = text;
        }

        app.sendMessage();
    }

    async function restoreRevision(number) {
        if (busy || !window.confirm(format(strings.restoreConfirm, number))) {
            return;
        }

        busy = true;
        updateButtons();

        try {
            await flushSettings(getChatApp());

            var result = await request(config.urls.restore + (config.urls.restore.indexOf('?') >= 0 ? '&' : '?') + 'number=' + encodeURIComponent(number), 'POST', {});

            if (!result.succeeded) {
                window.alert(result.error || strings.error);

                return;
            }

            state = result.state;
            applySettings(state.settings);
            render();
            setStatus('pb-status', strings.restored, 'success');
        } catch (error) {
            console.error('Profile Builder: the revision could not be restored.', error);
            window.alert(strings.error);
        } finally {
            busy = false;
            updateButtons();
        }
    }

    async function runReplay() {
        if (busy) {
            return;
        }

        busy = true;
        updateButtons();

        var list = byId('pb-replay-results');
        list.replaceChildren(create('div', 'text-muted', strings.replaying));

        try {
            await flushSettings(getChatApp());

            var result = await request(config.urls.replay, 'POST', {});
            state = result.state;
            render();
        } catch (error) {
            console.error('Profile Builder: the replay failed.', error);
            list.replaceChildren(create('div', 'text-danger', strings.error));
        } finally {
            busy = false;
            updateButtons();
        }
    }

    async function saveSessionSettings() {
        try {
            var result = await request(config.urls.settings, 'POST', {
                initialPrompt: byId('pb-initial-prompt').value,
                coachDeploymentName: byId('pb-coach-deployment').value,
            });

            state = result.state;
            render();
            setStatus('pb-finish-status', strings.saved, 'success');

            return true;
        } catch (error) {
            console.error('Profile Builder: the session settings could not be saved.', error);
            setStatus('pb-finish-status', strings.error, 'danger');

            return false;
        }
    }

    async function sendOpening() {
        var app = getChatApp();
        var text = byId('pb-initial-prompt').value.trim();

        if (!app || !text) {
            return;
        }

        if (await saveSessionSettings()) {
            sendThroughChat(app, text);
        }
    }

    async function saveProfile(updateExisting) {
        var payload = { updateExisting: updateExisting };

        if (!updateExisting) {
            payload.name = byId('pb-profile-name').value;
            payload.displayText = byId('pb-profile-display-text').value;
        }

        try {
            await flushSettings(getChatApp());

            var result = await request(config.urls.saveProfile, 'POST', payload);

            if (!result.succeeded) {
                setStatus('pb-finish-status', result.error || strings.error, 'danger');

                return;
            }

            window.location.href = result.redirectUrl;
        } catch (error) {
            console.error('Profile Builder: the AI profile could not be saved.', error);
            setStatus('pb-finish-status', strings.error, 'danger');
        }
    }

    async function copySystemMessage() {
        var input = document.querySelector('.setting-input[data-setting="systemMessage"]');
        var text = input ? input.value : (state && state.settings ? state.settings.systemMessage : '');

        try {
            await navigator.clipboard.writeText(text || '');
            setStatus('pb-finish-status', strings.copied, 'success');
        } catch (error) {
            setStatus('pb-finish-status', strings.error, 'danger');
        }
    }

    // A technical name follows the title until the author types one of their own.
    function toTechnicalName(title) {
        return String(title || '')
            .split(/[^A-Za-z0-9]+/)
            .filter(Boolean)
            .map(function (word) { return word.charAt(0).toUpperCase() + word.slice(1); })
            .join('');
    }

    function wireEvents() {
        document.addEventListener('click', function (event) {
            var action = event.target.closest('.profile-builder-action');

            if (action) {
                event.preventDefault();

                var index = parseInt(action.getAttribute('data-builder-message-index'), 10);

                if (action.getAttribute('data-builder-action') === 'approve') {
                    selectReply(index);
                    submitFeedback(FEEDBACK_KIND.Approved);
                } else {
                    selectReply(index);
                }

                return;
            }

            var restore = event.target.closest('[data-builder-restore]');

            if (restore) {
                restoreRevision(parseInt(restore.getAttribute('data-builder-restore'), 10));
            }
        });

        byId('pb-send-feedback').addEventListener('click', function () {
            submitFeedback(FEEDBACK_KIND.Feedback);
        });

        byId('pb-send-regenerate').addEventListener('click', function () {
            submitFeedback(FEEDBACK_KIND.FeedbackAndRegenerate);
        });

        byId('pb-replay').addEventListener('click', runReplay);
        byId('pb-sim-run').addEventListener('click', runSimulation);
        byId('pb-sim-stop').addEventListener('click', function () {
            stopSimulation = true;
        });
        byId('pb-save-settings').addEventListener('click', saveSessionSettings);
        byId('pb-send-opening').addEventListener('click', sendOpening);
        byId('pb-copy-system-message').addEventListener('click', copySystemMessage);
        byId('pb-create-profile').addEventListener('click', function () {
            saveProfile(false);
        });

        var update = byId('pb-update-profile');

        if (update) {
            update.addEventListener('click', function () {
                saveProfile(true);
            });
        }

        var titleInput = byId('pb-profile-display-text');
        var nameInput = byId('pb-profile-name');
        var nameEdited = false;

        nameInput.addEventListener('input', function () {
            nameEdited = nameInput.value.length > 0;
        });

        titleInput.addEventListener('input', function () {
            if (!nameEdited) {
                nameInput.value = toTechnicalName(titleInput.value);
            }
        });

        nameInput.value = toTechnicalName(titleInput.value);
    }

    async function start() {
        wireEvents();
        clearSelection();

        var app = await waitForChatApp();

        if (app && typeof app.$watch === 'function') {
            // A new turn, or a reply finishing, changes the count of messages or of messages still streaming.
            app.$watch(function () {
                var messages = app.messages || [];
                var streaming = messages.filter(function (message) { return message && message.isStreaming; }).length;

                return messages.length + ':' + streaming;
            }, scheduleRefresh);
        }

        await refreshState();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})(window, document);
