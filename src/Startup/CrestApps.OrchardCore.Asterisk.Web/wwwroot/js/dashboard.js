(function (window, document) {
    'use strict';

    function parseConfig(root) {
        try {
            return JSON.parse(root.getAttribute('data-config') || '{}');
        } catch (error) {
            return {};
        }
    }

    function escapeHtml(value) {
        var node = document.createElement('div');
        node.textContent = value == null ? '' : String(value);

        return node.innerHTML;
    }

    function parseUtc(value) {
        if (!value) {
            return null;
        }

        var time = Date.parse(value);

        return isNaN(time) ? null : time;
    }

    function formatDuration(totalSeconds) {
        if (!isFinite(totalSeconds) || totalSeconds <= 0) {
            return '0s';
        }

        var hours = Math.floor(totalSeconds / 3600);
        var minutes = Math.floor((totalSeconds % 3600) / 60);
        var seconds = Math.floor(totalSeconds % 60);

        if (hours > 0) {
            return hours + 'h ' + minutes + 'm';
        }

        if (minutes > 0) {
            return minutes + 'm ' + seconds + 's';
        }

        return seconds + 's';
    }

    function buildNotification(message, tone) {
        return {
            message: message,
            tone: tone || 'info',
            createdAt: new Date().toISOString()
        };
    }

    function renderStateBadges(call) {
        var badges = [];

        if (call.isOnHold) {
            badges.push('<span class="badge text-bg-warning">On hold</span>');
        }

        if (call.isMuted) {
            badges.push('<span class="badge text-bg-secondary">Muted</span>');
        }

        if (call.bridgeType) {
            badges.push('<span class="badge text-bg-info">' + escapeHtml(call.bridgeType) + '</span>');
        }

        if (!badges.length) {
            return '';
        }

        return '<div class="d-flex flex-wrap gap-1 mt-1">' + badges.join('') + '</div>';
    }

    function init(root) {
        var config = parseConfig(root);
        var notifications = [];
        var current = config.initialSnapshot || null;
        var fallbackPollingTimer = null;

        var refs = {
            statusBadge: root.querySelector('[data-dashboard-status]'),
            updated: root.querySelector('[data-dashboard-updated]'),
            summary: root.querySelector('[data-dashboard-summary]'),
            notifications: root.querySelector('[data-dashboard-notifications]'),
            calls: root.querySelector('[data-dashboard-calls]'),
            callCount: root.querySelector('[data-dashboard-call-count]'),
            bridges: root.querySelector('[data-dashboard-bridges]'),
            bridgeCount: root.querySelector('[data-dashboard-bridge-count]'),
            infoJson: root.querySelector('[data-dashboard-info-json]'),
            channelsJson: root.querySelector('[data-dashboard-channels-json]'),
            bridgesJson: root.querySelector('[data-dashboard-bridges-json]')
        };

        function notificationClass(tone) {
            return tone === 'danger'
                ? 'danger'
                : tone === 'warning'
                    ? 'warning'
                    : tone === 'success'
                        ? 'success'
                        : 'secondary';
        }

        function pushNotification(message, tone) {
            notifications.unshift(buildNotification(message, tone));
            notifications = notifications.slice(0, 6);
            renderNotifications();
        }

        function renderNotifications() {
            if (!refs.notifications) {
                return;
            }

            if (!notifications.length) {
                refs.notifications.innerHTML = '<div class="text-body-secondary small">Waiting for live events...</div>';

                return;
            }

            refs.notifications.innerHTML = notifications.map(function (item) {
                return '<div class="alert alert-' + notificationClass(item.tone) + ' py-2 mb-2 small">' +
                    '<div class="fw-semibold">' + escapeHtml(item.message) + '</div>' +
                    '<div class="text-body-secondary">' + escapeHtml(new Date(item.createdAt).toLocaleTimeString()) + '</div>' +
                '</div>';
            }).join('');
        }

        function metricCard(label, value, detail, tone) {
            return '<div class="col-sm-6 col-xl-3">' +
                '<div class="card shadow-sm border-' + escapeHtml(tone || 'light') + '">' +
                    '<div class="card-body">' +
                        '<div class="text-body-secondary small text-uppercase">' + escapeHtml(label) + '</div>' +
                        '<div class="display-6 fw-semibold">' + escapeHtml(value) + '</div>' +
                        '<div class="small text-body-secondary">' + escapeHtml(detail || '') + '</div>' +
                    '</div>' +
                '</div>' +
            '</div>';
        }

        function renderSummary(snapshot) {
            if (!refs.summary || !snapshot) {
                return;
            }

            refs.summary.innerHTML = [
                metricCard('Active calls', snapshot.activeCallCount, snapshot.reachable ? 'Grouped logical calls from ARI channel legs' : 'Waiting for Asterisk', snapshot.activeCallCount > 0 ? 'success' : 'light'),
                metricCard('Channel legs', snapshot.channelCount, snapshot.connectedChannelCount + ' connected / ' + snapshot.ringingChannelCount + ' ringing', snapshot.channelCount > 0 ? 'primary' : 'light'),
                metricCard('Bridges', snapshot.bridgeCount, 'Asterisk bridge inventory', snapshot.bridgeCount > 0 ? 'info' : 'light'),
                metricCard('Oldest live channel', formatDuration(snapshot.oldestChannelSeconds), snapshot.lastUpdatedUtc ? 'Updated from SignalR push with periodic reconciliation' : 'No snapshot yet', snapshot.oldestChannelSeconds > 300 ? 'warning' : 'light')
            ].join('');
        }

        function renderStatus(snapshot) {
            if (refs.statusBadge) {
                refs.statusBadge.className = 'badge ' + (snapshot.reachable ? 'bg-success' : 'bg-secondary');
                refs.statusBadge.textContent = snapshot.reachable ? 'Connected' : 'Unavailable';
            }

            if (refs.updated) {
                refs.updated.textContent = snapshot.lastUpdatedUtc
                    ? new Date(snapshot.lastUpdatedUtc).toLocaleTimeString()
                    : 'Not yet refreshed';
            }
        }

        function renderCalls(snapshot) {
            if (!refs.calls) {
                return;
            }

            var calls = snapshot.calls || [];

            if (refs.callCount) {
                refs.callCount.textContent = snapshot.activeCallCount + ' total / ' + snapshot.channelCount + ' channel legs';
            }

            if (!calls.length) {
                refs.calls.innerHTML = '<tr><td colspan="9" class="text-body-secondary">No active calls.</td></tr>';

                return;
            }

            refs.calls.innerHTML = calls.map(function (call) {
                return '<tr>' +
                    '<td><div class="fw-semibold">' + escapeHtml(call.callerName || call.callerNumber || call.key || call.primaryChannelId) + '</div><div class="small text-body-secondary">' + escapeHtml(call.primaryChannelId || '') + '</div></td>' +
                    '<td>' + escapeHtml(call.direction || 'Unknown') + '</td>' +
                    '<td><div>' + escapeHtml(call.state || 'Unknown') + '</div>' + renderStateBadges(call) + '</td>' +
                    '<td>' + escapeHtml(call.connectedNumber || '-') + '</td>' +
                    '<td>' + escapeHtml(call.application || '-') + '</td>' +
                    '<td>' + escapeHtml(call.channelCount || 1) + '</td>' +
                    '<td>' + escapeHtml(call.partyCount || 1) + '</td>' +
                    '<td>' + escapeHtml(formatDuration(call.durationSeconds)) + '</td>' +
                    '<td><button type="button" class="btn btn-sm btn-outline-danger" data-dashboard-hangup data-channel-id="' + escapeHtml(call.primaryChannelId || '') + '">Disconnect</button></td>' +
                '</tr>';
            }).join('');
        }

        function renderBridges(snapshot) {
            if (!refs.bridges) {
                return;
            }

            var bridges = snapshot.bridges || [];

            if (refs.bridgeCount) {
                refs.bridgeCount.textContent = snapshot.bridgeCount + ' total';
            }

            if (!bridges.length) {
                refs.bridges.innerHTML = '<tr><td colspan="4" class="text-body-secondary">No active bridges.</td></tr>';

                return;
            }

            refs.bridges.innerHTML = bridges.map(function (bridge) {
                return '<tr>' +
                    '<td><div class="fw-semibold">' + escapeHtml(bridge.name || bridge.id) + '</div><div class="small text-body-secondary">' + escapeHtml(bridge.id || '') + '</div></td>' +
                    '<td>' + escapeHtml(bridge.bridgeType || '-') + '</td>' +
                    '<td>' + escapeHtml(bridge.channelCount) + '</td>' +
                    '<td>' + escapeHtml(formatDuration(bridge.durationSeconds)) + '</td>' +
                '</tr>';
            }).join('');
        }

        function renderJson(snapshot) {
            if (refs.infoJson) {
                refs.infoJson.textContent = snapshot.infoJson || '{}';
            }

            if (refs.channelsJson) {
                refs.channelsJson.textContent = snapshot.channelsJson || '[]';
            }

            if (refs.bridgesJson) {
                refs.bridgesJson.textContent = snapshot.bridgesJson || '[]';
            }
        }

        function render(snapshot) {
            if (!snapshot) {
                return;
            }

            renderStatus(snapshot);
            renderSummary(snapshot);
            renderCalls(snapshot);
            renderBridges(snapshot);
            renderJson(snapshot);
        }

        function describeChange(previousSnapshot, nextSnapshot) {
            if (!previousSnapshot) {
                pushNotification('Live dashboard connected to the Asterisk event stream.', 'success');
                return;
            }

            if (previousSnapshot.reachable && !nextSnapshot.reachable) {
                pushNotification(nextSnapshot.errorMessage || 'Asterisk became unavailable.', 'danger');
            } else if (!previousSnapshot.reachable && nextSnapshot.reachable) {
                pushNotification('Asterisk connectivity was restored.', 'success');
            }

            if (previousSnapshot.activeCallCount !== nextSnapshot.activeCallCount) {
                pushNotification('Active calls changed from ' + previousSnapshot.activeCallCount + ' to ' + nextSnapshot.activeCallCount + '.', nextSnapshot.activeCallCount > previousSnapshot.activeCallCount ? 'info' : 'warning');
            }

            if (previousSnapshot.ringingChannelCount !== nextSnapshot.ringingChannelCount && nextSnapshot.ringingChannelCount > 0) {
                pushNotification(nextSnapshot.ringingChannelCount + ' channel(s) are ringing right now.', 'info');
            }
        }

        function applySnapshot(snapshot) {
            var previous = current;
            current = snapshot;
            describeChange(previous, snapshot);
            render(snapshot);
        }

        function fetchSnapshot() {
            if (!config.snapshotUrl) {
                return Promise.resolve();
            }

            return fetch(config.snapshotUrl, {
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json' }
            })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (snapshot) {
                    if (snapshot) {
                        applySnapshot(snapshot);
                    }
                })
                .catch(function () { });
        }

        function startFallbackPolling() {
            if (fallbackPollingTimer) {
                return;
            }

            fetchSnapshot();
            fallbackPollingTimer = window.setInterval(
                fetchSnapshot,
                Math.max((config.reconciliationSeconds || 15) * 1000, 5000));
        }

        function stopFallbackPolling() {
            if (!fallbackPollingTimer) {
                return;
            }

            window.clearInterval(fallbackPollingTimer);
            fallbackPollingTimer = null;
        }

        function disconnectChannel(channelId) {
            if (!channelId) {
                return Promise.resolve();
            }

            return fetch('/api/asterisk/channels/' + encodeURIComponent(channelId), {
                method: 'DELETE',
                credentials: 'same-origin'
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('The channel could not be disconnected.');
                    }

                    pushNotification('Disconnected channel ' + channelId + '.', 'warning');
                    return fetchSnapshot();
                })
                .catch(function (error) {
                    pushNotification(error && error.message ? error.message : 'The channel could not be disconnected.', 'danger');
                });
        }

        root.addEventListener('click', function (event) {
            var button = event.target.closest('[data-dashboard-hangup]');

            if (!button) {
                return;
            }

            event.preventDefault();
            disconnectChannel(button.getAttribute('data-channel-id'));
        });

        if (window.signalR && config.hubUrl) {
            var connection = new window.signalR.HubConnectionBuilder()
                .withUrl(config.hubUrl)
                .withAutomaticReconnect()
                .build();

            connection.on('dashboardSnapshot', function (snapshot) {
                applySnapshot(snapshot);
            });

            connection.onreconnecting(function () {
                pushNotification('SignalR disconnected; reconciliation polling is active until it reconnects.', 'warning');
                startFallbackPolling();
            });

            connection.onreconnected(function () {
                stopFallbackPolling();
                pushNotification('SignalR reconnected to the Asterisk event stream.', 'success');
                fetchSnapshot();
            });

            connection.onclose(function () {
                pushNotification('SignalR closed; reconciliation polling remains active.', 'warning');
                startFallbackPolling();
            });

            connection.start()
                .then(function () {
                    stopFallbackPolling();
                    return fetchSnapshot();
                })
                .catch(function () {
                    pushNotification('SignalR could not connect; falling back to reconciliation polling.', 'warning');
                    startFallbackPolling();
                });
        } else {
            pushNotification('SignalR is unavailable; showing reconciliation polling only.', 'warning');
            startFallbackPolling();
        }

        if (current) {
            render(current);
            renderNotifications();
        }

    }

    function boot() {
        var root = document.querySelector('[data-asterisk-dashboard]');

        if (root) {
            init(root);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(window, document);
