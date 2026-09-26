/*
 * One workspace-state fetch at a time, however many real-time events ask for one.
 *
 * The agent workspace and the docked agent bar re-read the agent's whole state on every hub event they hear. A single
 * change arrives as several events -- the offer, the presence change it causes, the stats for each of the agent's
 * queues -- so one change fetched the state 3 to 6 times within a third of a second. A refresh asked for while one is
 * in flight now folds into it, plus exactly one trailing fetch: the events that arrived mid-flight may describe a
 * change the response in flight predates, and the trailing fetch is what picks it up.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundles and under the unit tests.
 */
(function (root) {
    'use strict';

    var contactCenter = root.CrestAppsContactCenter = root.CrestAppsContactCenter || {};

    // Wraps `run` (which returns a promise) so concurrent calls share work. Every call returns a promise that settles
    // once a fetch started at or after that call has finished.
    function coalesceRefresh(run) {
        var inFlight = null;
        var trailing = null;

        function start() {
            try {
                inFlight = Promise.resolve(run());
            } catch (error) {
                inFlight = Promise.reject(error);
            }

            var settled = function () {
                inFlight = null;
            };

            inFlight.then(settled, settled);

            return inFlight;
        }

        return function refresh() {
            if (!inFlight) {
                return start();
            }

            if (!trailing) {
                var after = function () {
                    trailing = null;

                    return start();
                };

                trailing = inFlight.then(after, after);
            }

            return trailing;
        };
    }

    contactCenter.coalesceRefresh = coalesceRefresh;
}(typeof globalThis !== 'undefined' ? globalThis : window));
