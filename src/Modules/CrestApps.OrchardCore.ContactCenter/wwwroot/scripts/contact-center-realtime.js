/*
 * Contact Center real-time client helper.
 *
 * A thin convenience wrapper around the Microsoft SignalR client for connecting to the Contact Center
 * hub (`ContactCenterHub`). It manages the connection, sends periodic heartbeats so the server does not
 * consider the agent session stale, retrieves the reconnect snapshot, and dispatches the strongly-typed
 * server callbacks (`PresenceChanged`, `OfferReceived`, `OfferRevoked`, `QueueStatsChanged`).
 *
 * Usage:
 *   const client = window.contactCenterRealTime.connect({
 *       hubUrl: '/path/to/hub',
 *       onSnapshot: (snapshot) => { ... },
 *       onPresenceChanged: (n) => { ... },
 *       onOfferReceived: (n) => { ... },
 *       onOfferRevoked: (n) => { ... },
 *       onQueueStatsChanged: (n) => { ... }
 *   });
 */
(function (window) {
    'use strict';

    var DEFAULT_HEARTBEAT_INTERVAL_MS = 30000;

    function noop() { }

    function connect(options) {
        options = options || {};

        if (!window.signalR) {
            throw new Error('The SignalR client library is not loaded. Require the "signalr" resource first.');
        }

        if (!options.hubUrl) {
            throw new Error('A "hubUrl" option is required to connect to the Contact Center hub.');
        }

        var connection = new window.signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('PresenceChanged', options.onPresenceChanged || noop);
        connection.on('OfferReceived', options.onOfferReceived || noop);
        connection.on('OfferRevoked', options.onOfferRevoked || noop);
        connection.on('QueueStatsChanged', options.onQueueStatsChanged || noop);

        var heartbeatIntervalMs = options.heartbeatIntervalMs || DEFAULT_HEARTBEAT_INTERVAL_MS;
        var heartbeatTimer = null;

        function startHeartbeat() {
            stopHeartbeat();
            heartbeatTimer = window.setInterval(function () {
                connection.invoke('Heartbeat').catch(function () { });
            }, heartbeatIntervalMs);
        }

        function stopHeartbeat() {
            if (heartbeatTimer) {
                window.clearInterval(heartbeatTimer);
                heartbeatTimer = null;
            }
        }

        function loadSnapshot() {
            return connection.invoke('GetSnapshot').then(function (snapshot) {
                if (typeof options.onSnapshot === 'function') {
                    options.onSnapshot(snapshot);
                }

                return snapshot;
            });
        }

        connection.onreconnected(function () {
            startHeartbeat();
            loadSnapshot().catch(function () { });
        });

        connection.onclose(function () {
            stopHeartbeat();
        });

        var started = connection.start().then(function () {
            startHeartbeat();
            return loadSnapshot().catch(function () { });
        }).catch(function (error) {
            if (typeof options.onError === 'function') {
                options.onError(error);
            }

            throw error;
        });

        return {
            connection: connection,
            started: started,
            getSnapshot: loadSnapshot,
            watchQueue: function (queueId) {
                return connection.invoke('WatchQueue', queueId);
            },
            unwatchQueue: function (queueId) {
                return connection.invoke('UnwatchQueue', queueId);
            },
            stop: function () {
                stopHeartbeat();

                return connection.stop();
            }
        };
    }

    window.contactCenterRealTime = {
        connect: connect
    };
})(window);
