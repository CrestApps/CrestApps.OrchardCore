/*
 * The live dashboard's agent board filters: the queue and campaign an agent is signed in to, their status, and their
 * name. A large contact center has too many agents to scan, so the supervisor narrows the board to the ones they are
 * looking for. The board is filtered in the browser from the state the dashboard already polls, so a filter answers at
 * once and costs the server nothing.
 *
 * Concatenated ahead of the dashboard by the module asset pipeline. It attaches to a shared namespace rather than
 * exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var contactCenter = root.CrestAppsContactCenter = root.CrestAppsContactCenter || {};

    // The status groups, in the order the filter offers them. They group presence the way the queue tiles count it, so a
    // filter and a tile agree on who is busy or not ready; onCall is anybody on a call, whatever their presence says.
    var STATUS_FILTERS = ['available', 'busy', 'onCall', 'notReady', 'offline'];

    var BUSY_STATUSES = { Reserved: true, Busy: true, WrapUp: true };

    function agentStatusGroup(status) {
        if (!status || status === 'Offline') {
            return 'offline';
        }

        if (status === 'Available') {
            return 'available';
        }

        return BUSY_STATUSES[status] ? 'busy' : 'notReady';
    }

    // An agent is on a call when they handle a Contact Center interaction or a phone call of their own (a number dialed
    // from the keypad, an extension call).
    function isOnCall(agent) {
        return !!(agent.activeInteractionId || agent.phoneCall || agent.activeInteractions > 0);
    }

    // Lower case, without accents, so "catia" finds "Cátia".
    function fold(text) {
        var value = String(text || '').toLowerCase();

        return typeof value.normalize === 'function'
            ? value.normalize('NFD').replace(/[̀-ͯ]/g, '')
            : value;
    }

    function contains(list, id) {
        return Array.isArray(list) && list.indexOf(id) >= 0;
    }

    // The agents that pass every filter that is on, in the order the server sent them.
    function filterSupervisorAgents(agents, filters) {
        var chosen = filters || {};
        var search = fold(chosen.search).trim();

        return (agents || []).filter(function (agent) {
            if (!agent) {
                return false;
            }

            if (chosen.queueId && !contains(agent.queueIds, chosen.queueId)) {
                return false;
            }

            if (chosen.campaignId && !contains(agent.campaignIds, chosen.campaignId)) {
                return false;
            }

            if (chosen.status === 'onCall') {
                if (!isOnCall(agent)) {
                    return false;
                }
            } else if (chosen.status && agentStatusGroup(agent.presenceStatus) !== chosen.status) {
                return false;
            }

            return !search || fold(agent.displayName || agent.userId).indexOf(search) >= 0;
        });
    }

    function hasId(list, id) {
        return (list || []).some(function (item) {
            return item && item.id === id;
        });
    }

    // The filters to apply now: a queue or campaign the board no longer has, or a status it does not know -- remembered
    // from an earlier visit -- would hide every agent, so it is dropped.
    function normalizeAgentBoardFilters(filters, state) {
        var chosen = filters || {};
        var current = state || {};

        return {
            queueId: typeof chosen.queueId === 'string' && hasId(current.queues, chosen.queueId) ? chosen.queueId : '',
            campaignId: typeof chosen.campaignId === 'string' && hasId(current.campaigns, chosen.campaignId) ? chosen.campaignId : '',
            status: STATUS_FILTERS.indexOf(chosen.status) >= 0 ? chosen.status : '',
            search: typeof chosen.search === 'string' ? chosen.search : ''
        };
    }

    function agentBoardFiltersActive(filters) {
        var chosen = filters || {};

        return !!(chosen.queueId || chosen.campaignId || chosen.status || String(chosen.search || '').trim());
    }

    contactCenter.AGENT_STATUS_FILTERS = STATUS_FILTERS;
    contactCenter.agentStatusGroup = agentStatusGroup;
    contactCenter.filterSupervisorAgents = filterSupervisorAgents;
    contactCenter.normalizeAgentBoardFilters = normalizeAgentBoardFilters;
    contactCenter.agentBoardFiltersActive = agentBoardFiltersActive;
}(typeof globalThis !== 'undefined' ? globalThis : window));
