// The SCORM 2004 run-time API of a package frame (window.API_1484_11, 3rd and 4th Edition). The server puts this script,
// with the learner's data, at the start of every HTML page of a SCORM 2004 package it serves, so a SCO finds
// API_1484_11 on its own window (or on a parent or opener page of the package, the way SCOs search for it). The frame is
// sandboxed on an opaque origin: it has no cookie and cannot reach the site. It answers the SCO at once from its copy of
// the data, with the error codes of SCORM 2004, and tells the player page what the SCO set, commits and terminates only
// through postMessage. The player sends it to the server with the learner's session, and the server checks every value
// again (Scorm2004DataModel); nothing this script accepts is trusted. Sequencing and Navigation are not implemented:
// adl.nav.request is checked and stored, and every adl.nav.request_valid element answers "unknown".
(function (root) {
    'use strict';

    var PROTOCOL = 'crestapps-scorm2004';
    var FRAME_NAME = 'crestapps-package-frame';

    var ERRORS = {
        '0': 'No Error',
        '101': 'General Exception',
        '102': 'General Initialization Failure',
        '103': 'Already Initialized',
        '104': 'Content Instance Terminated',
        '111': 'General Termination Failure',
        '112': 'Termination Before Initialization',
        '113': 'Termination After Termination',
        '122': 'Retrieve Data Before Initialization',
        '123': 'Retrieve Data After Termination',
        '132': 'Store Data Before Initialization',
        '133': 'Store Data After Termination',
        '142': 'Commit Before Initialization',
        '143': 'Commit After Termination',
        '201': 'General Argument Error',
        '301': 'General Get Failure',
        '351': 'General Set Failure',
        '391': 'General Commit Failure',
        '401': 'Undefined Data Model Element',
        '402': 'Unimplemented Data Model Element',
        '403': 'Data Model Element Value Not Initialized',
        '404': 'Data Model Element Is Read Only',
        '405': 'Data Model Element Is Write Only',
        '406': 'Data Model Element Type Mismatch',
        '407': 'Data Model Element Value Out Of Range',
        '408': 'Data Model Dependency Not Established',
    };

    var COMPLETION = ['completed', 'incomplete', 'not attempted', 'unknown'];
    var SUCCESS = ['passed', 'failed', 'unknown'];
    var EXITS = ['time-out', 'suspend', 'logout', 'normal', ''];
    var TYPES = ['true-false', 'choice', 'fill-in', 'long-fill-in', 'matching', 'performance', 'sequencing', 'likert', 'numeric', 'other'];
    var SINGLE_RESPONSE_TYPES = ['true-false', 'likert', 'numeric', 'other'];
    var RESULTS = ['correct', 'incorrect', 'unanticipated', 'neutral'];
    var CAPTIONING = ['-1', '0', '1'];
    var NAV_REQUESTS = ['continue', 'previous', 'exit', 'exitAll', 'abandon', 'abandonAll', 'suspendAll', '_none_'];
    var SCORE_CHILDREN = 'scaled,raw,min,max';
    var COMMENT_CHILDREN = 'comment,location,timestamp';

    var OK = '0';
    var TYPE_MISMATCH = '406';
    var OUT_OF_RANGE = '407';

    function isReal(value) {
        return typeof value === 'string' && /^-?([0-9]+(\.[0-9]*)?|\.[0-9]+)$/.test(value);
    }

    function real(min, max) {
        return function (value) {
            if (!isReal(value)) {
                return TYPE_MISMATCH;
            }

            var number = parseFloat(value);

            return (min !== null && number < min) || (max !== null && number > max) ? OUT_OF_RANGE : OK;
        };
    }

    function vocabulary(list) {
        return function (value) { return list.indexOf(value) >= 0 ? OK : TYPE_MISMATCH; };
    }

    function characterString(max) {
        return function (value) { return value.length <= max ? OK : TYPE_MISMATCH; };
    }

    function isLanguage(value) {
        return value.length <= 250 && /^(?:[A-Za-z]{2,3}|[iIxX])(?:-[A-Za-z0-9]{1,8})*$/.test(value);
    }

    function isLocalizedString(value, max) {
        var text = value;

        if (value.indexOf('{lang=') === 0) {
            var end = value.indexOf('}');

            if (end < 0 || !isLanguage(value.slice(6, end))) {
                return false;
            }

            text = value.slice(end + 1);
        }

        return text.length <= max;
    }

    function localizedString(max) {
        return function (value) { return isLocalizedString(value, max) ? OK : TYPE_MISMATCH; };
    }

    function isIdentifier(value, max) {
        if (typeof value !== 'string' || value.length === 0 || value.length > max || /\s/.test(value)) {
            return false;
        }

        return value.toLowerCase().indexOf('urn:') !== 0 || /^urn:[A-Za-z0-9][A-Za-z0-9-]{1,31}:\S+$/i.test(value);
    }

    function identifier(value) {
        return isIdentifier(value, 4000) ? OK : TYPE_MISMATCH;
    }

    function daysInMonth(year, month) {
        return new Date(Date.UTC(year, month, 0)).getUTCDate();
    }

    function isTimestamp(value) {
        var match = /^([0-9]{4})(?:-([0-9]{2})(?:-([0-9]{2})(?:T([0-9]{2})(?::([0-9]{2})(?::([0-9]{2})(?:\.[0-9]+)?)?)?(?:Z|[+-]([0-9]{2})(?::?([0-9]{2}))?)?)?)?)?$/.exec(value);

        if (!match) {
            return false;
        }

        function part(index, fallback) {
            return match[index] === undefined ? fallback : parseInt(match[index], 10);
        }

        var year = part(1, 1970);
        var month = part(2, 1);
        var day = part(3, 1);

        if (year < 1970 || year > 2038 || month < 1 || month > 12 || day < 1 || day > daysInMonth(year, month)) {
            return false;
        }

        return part(4, 0) <= 23 && part(5, 0) <= 59 && part(6, 0) <= 59 && part(7, 0) <= 23 && part(8, 0) <= 59;
    }

    function timestamp(value) {
        return isTimestamp(value) ? OK : TYPE_MISMATCH;
    }

    function isDuration(value) {
        if (typeof value !== 'string' || value.length === 0 || value.length > 64) {
            return false;
        }

        var match = /^P(?:([0-9]{1,10})Y)?(?:([0-9]{1,10})M)?(?:([0-9]{1,10})D)?(T(?:([0-9]{1,10})H)?(?:([0-9]{1,10})M)?(?:([0-9]{1,10}(?:\.[0-9]{1,10})?)S)?)?$/.exec(value);

        if (!match) {
            return false;
        }

        var hasDate = match[1] !== undefined || match[2] !== undefined || match[3] !== undefined;
        var hasTime = match[5] !== undefined || match[6] !== undefined || match[7] !== undefined;

        return (hasDate || hasTime) && (match[4] === undefined || hasTime);
    }

    function duration(value) {
        return isDuration(value) ? OK : TYPE_MISMATCH;
    }

    // The formats of learner responses and correct response patterns per interaction type, as the server's
    // Scorm2004Responses checks them.
    function stripFlags(value, caseMatters, orderMatters) {
        var seen = {};
        var rest = value;

        for (; ;) {
            var name = null;

            if (caseMatters && rest.indexOf('{case_matters=') === 0) {
                name = 'case_matters';
            } else if (orderMatters && rest.indexOf('{order_matters=') === 0) {
                name = 'order_matters';
            }

            if (name === null) {
                return rest;
            }

            var end = rest.indexOf('}');

            if (end < 0 || seen[name]) {
                return null;
            }

            var flag = rest.slice(name.length + 2, end);

            if (flag !== 'true' && flag !== 'false') {
                return null;
            }

            seen[name] = true;
            rest = rest.slice(end + 1);
        }
    }

    function isShortIdentifier(value) {
        return isIdentifier(value, 250);
    }

    function isNumericRange(value) {
        var parts = value.split('[:]');

        if (parts.length === 1) {
            return isReal(value);
        }

        if (parts.length !== 2 || (parts[0] === '' && parts[1] === '')) {
            return false;
        }

        if ((parts[0] !== '' && !isReal(parts[0])) || (parts[1] !== '' && !isReal(parts[1]))) {
            return false;
        }

        return parts[0] === '' || parts[1] === '' || parseFloat(parts[0]) <= parseFloat(parts[1]);
    }

    function isResponse(type, value, pattern) {
        var rest;

        switch (type) {
            case 'true-false':
                return value === 'true' || value === 'false';
            case 'choice': {
                if (value === '') {
                    return true;
                }

                var ids = value.split('[,]');
                var unique = {};

                for (var i = 0; i < ids.length; i++) {
                    if (!isShortIdentifier(ids[i]) || Object.prototype.hasOwnProperty.call(unique, ids[i])) {
                        return false;
                    }

                    unique[ids[i]] = true;
                }

                return true;
            }
            case 'fill-in':
                rest = pattern ? stripFlags(value, true, true) : value;

                return rest !== null && rest.split('[,]').every(function (text) { return isLocalizedString(text, 250); });
            case 'long-fill-in':
                rest = pattern ? stripFlags(value, true, false) : value;

                return rest !== null && isLocalizedString(rest, 4000);
            case 'likert':
                return isShortIdentifier(value);
            case 'matching':
                return value.split('[,]').every(function (record) {
                    var pair = record.split('[.]');

                    return pair.length === 2 && isShortIdentifier(pair[0]) && isShortIdentifier(pair[1]);
                });
            case 'performance':
                rest = pattern ? stripFlags(value, false, true) : value;

                return rest !== null && rest.split('[,]').every(function (record) {
                    var pair = record.split('[.]');

                    return pair.length === 2 &&
                        (pair[0] !== '' || pair[1] !== '') &&
                        (pair[0] === '' || isShortIdentifier(pair[0])) &&
                        pair[1].length <= 250;
                });
            case 'sequencing':
                return value !== '' && value.split('[,]').every(isShortIdentifier);
            case 'numeric':
                return pattern ? isNumericRange(value) : isReal(value);
            case 'other':
                return value.length <= 4000;
            default:
                return false;
        }
    }

    // The status evaluation rules (RTE 4.2.4.1 and 4.2.22.1).
    function completionStatus(cmi) {
        if (!isReal(cmi.CompletionThreshold)) {
            return cmi.CompletionStatus || 'unknown';
        }

        if (!isReal(cmi.ProgressMeasure)) {
            return 'unknown';
        }

        return parseFloat(cmi.ProgressMeasure) >= parseFloat(cmi.CompletionThreshold) ? 'completed' : 'incomplete';
    }

    function successStatus(cmi) {
        if (!isReal(cmi.ScaledPassingScore)) {
            return cmi.SuccessStatus || 'unknown';
        }

        if (!isReal(cmi.ScoreScaled)) {
            return 'unknown';
        }

        return parseFloat(cmi.ScoreScaled) >= parseFloat(cmi.ScaledPassingScore) ? 'passed' : 'failed';
    }

    function suspendDataLength(cmi) {
        return cmi.Edition >= 4 ? 64000 : 4000;
    }

    function at(list, index, create) {
        if (index === list.length) {
            list.push(create());
        }

        return list[index];
    }

    function setAt(list, index, value) {
        if (index === list.length) {
            list.push(value);
        } else {
            list[index] = value;
        }
    }

    function newComment() {
        return { Comment: null, Location: null, Timestamp: null };
    }

    function newObjective() {
        return { Id: null, ScoreScaled: null, ScoreRaw: null, ScoreMin: null, ScoreMax: null, SuccessStatus: 'unknown', CompletionStatus: 'unknown', ProgressMeasure: null, Description: null };
    }

    function newInteraction() {
        return { Id: null, Type: null, ObjectiveIds: [], Timestamp: null, CorrectResponses: [], Weighting: null, LearnerResponse: null, Result: null, Latency: null, Description: null };
    }

    function check(test) {
        return function (cmi, i, value) { return test(value); };
    }

    // The same table as the server's Scorm2004DataModel: access k (keyword), r, w or rw; get returns null for a value
    // never set (403); check returns the error code of a value.
    var ELEMENTS = {};

    function keyword(pattern, get) { ELEMENTS[pattern] = { access: 'k', get: get }; }
    function readOnly(pattern, get) { ELEMENTS[pattern] = { access: 'r', get: get }; }
    function readWrite(pattern, get, set, test) { ELEMENTS[pattern] = { access: 'rw', get: get, set: set, check: test }; }
    function writeOnly(pattern, set, test) { ELEMENTS[pattern] = { access: 'w', set: set, check: test }; }

    function field(pattern, name, test) {
        readWrite(pattern, function (cmi) { return cmi[name]; }, function (cmi, i, value) { cmi[name] = value; }, check(test));
    }

    function readOnlyField(pattern, name) {
        readOnly(pattern, function (cmi) { return cmi[name]; });
    }

    function listField(pattern, list, create, name, test) {
        readWrite(pattern,
            function (cmi, i) { return cmi[list][i[0]][name]; },
            function (cmi, i, value) { at(cmi[list], i[0], create)[name] = value; },
            typeof test === 'function' && test.length === 3 ? test : check(test));
    }

    function count(list) {
        return function (cmi) { return String(cmi[list].length); };
    }

    keyword('cmi._version', function () { return '1.0'; });

    keyword('cmi.comments_from_learner._children', function () { return COMMENT_CHILDREN; });
    keyword('cmi.comments_from_learner._count', count('CommentsFromLearner'));
    listField('cmi.comments_from_learner.n.comment', 'CommentsFromLearner', newComment, 'Comment', localizedString(4000));
    listField('cmi.comments_from_learner.n.location', 'CommentsFromLearner', newComment, 'Location', characterString(250));
    listField('cmi.comments_from_learner.n.timestamp', 'CommentsFromLearner', newComment, 'Timestamp', timestamp);

    keyword('cmi.comments_from_lms._children', function () { return COMMENT_CHILDREN; });
    keyword('cmi.comments_from_lms._count', count('CommentsFromLms'));
    readOnly('cmi.comments_from_lms.n.comment', function (cmi, i) { return cmi.CommentsFromLms[i[0]].Comment; });
    readOnly('cmi.comments_from_lms.n.location', function (cmi, i) { return cmi.CommentsFromLms[i[0]].Location; });
    readOnly('cmi.comments_from_lms.n.timestamp', function (cmi, i) { return cmi.CommentsFromLms[i[0]].Timestamp; });

    readWrite('cmi.completion_status', completionStatus, function (cmi, i, value) { cmi.CompletionStatus = value; }, check(vocabulary(COMPLETION)));
    readOnlyField('cmi.completion_threshold', 'CompletionThreshold');
    readOnlyField('cmi.credit', 'Credit');
    readOnlyField('cmi.entry', 'Entry');
    writeOnly('cmi.exit', function (cmi, i, value) { cmi.Exit = value; }, check(vocabulary(EXITS)));

    keyword('cmi.interactions._children', function () { return 'id,type,objectives,timestamp,correct_responses,weighting,learner_response,result,latency,description'; });
    keyword('cmi.interactions._count', count('Interactions'));
    listField('cmi.interactions.n.id', 'Interactions', newInteraction, 'Id', identifier);
    listField('cmi.interactions.n.type', 'Interactions', newInteraction, 'Type', vocabulary(TYPES));
    keyword('cmi.interactions.n.objectives._count', function (cmi, i) { return String(cmi.Interactions[i[0]].ObjectiveIds.length); });
    readWrite('cmi.interactions.n.objectives.n.id',
        function (cmi, i) { return cmi.Interactions[i[0]].ObjectiveIds[i[1]]; },
        function (cmi, i, value) { setAt(cmi.Interactions[i[0]].ObjectiveIds, i[1], value); },
        function (cmi, i, value) {
            if (identifier(value) !== OK) {
                return TYPE_MISMATCH;
            }

            var ids = cmi.Interactions[i[0]].ObjectiveIds;

            for (var index = 0; index < ids.length; index++) {
                if (index !== i[1] && ids[index] === value) {
                    return '351';
                }
            }

            return OK;
        });
    listField('cmi.interactions.n.timestamp', 'Interactions', newInteraction, 'Timestamp', timestamp);
    keyword('cmi.interactions.n.correct_responses._count', function (cmi, i) { return String(cmi.Interactions[i[0]].CorrectResponses.length); });
    readWrite('cmi.interactions.n.correct_responses.n.pattern',
        function (cmi, i) { return cmi.Interactions[i[0]].CorrectResponses[i[1]]; },
        function (cmi, i, value) { setAt(cmi.Interactions[i[0]].CorrectResponses, i[1], value); },
        function (cmi, i, value) {
            var type = cmi.Interactions[i[0]].Type;

            if (type === null || type === undefined) {
                return '408';
            }

            if (i[1] >= 1 && SINGLE_RESPONSE_TYPES.indexOf(type) >= 0) {
                return '351';
            }

            return isResponse(type, value, true) ? OK : TYPE_MISMATCH;
        });
    listField('cmi.interactions.n.weighting', 'Interactions', newInteraction, 'Weighting', real(null, null));
    listField('cmi.interactions.n.learner_response', 'Interactions', newInteraction, 'LearnerResponse', function (cmi, i, value) {
        var type = cmi.Interactions[i[0]].Type;

        if (type === null || type === undefined) {
            return '408';
        }

        return isResponse(type, value, false) ? OK : TYPE_MISMATCH;
    });
    listField('cmi.interactions.n.result', 'Interactions', newInteraction, 'Result', function (value) { return RESULTS.indexOf(value) >= 0 ? OK : real(null, null)(value); });
    listField('cmi.interactions.n.latency', 'Interactions', newInteraction, 'Latency', duration);
    listField('cmi.interactions.n.description', 'Interactions', newInteraction, 'Description', localizedString(250));

    readOnlyField('cmi.launch_data', 'LaunchData');
    readOnlyField('cmi.learner_id', 'LearnerId');
    readOnlyField('cmi.learner_name', 'LearnerName');

    keyword('cmi.learner_preference._children', function () { return 'audio_level,language,delivery_speed,audio_captioning'; });
    field('cmi.learner_preference.audio_level', 'AudioLevel', real(0, null));
    field('cmi.learner_preference.language', 'Language', function (value) { return value === '' || isLanguage(value) ? OK : TYPE_MISMATCH; });
    field('cmi.learner_preference.delivery_speed', 'DeliverySpeed', real(0, null));
    field('cmi.learner_preference.audio_captioning', 'AudioCaptioning', vocabulary(CAPTIONING));

    field('cmi.location', 'Location', characterString(1000));
    readOnlyField('cmi.max_time_allowed', 'MaxTimeAllowed');
    readOnlyField('cmi.mode', 'Mode');

    keyword('cmi.objectives._children', function () { return 'id,score,success_status,completion_status,progress_measure,description'; });
    keyword('cmi.objectives._count', count('Objectives'));
    listField('cmi.objectives.n.id', 'Objectives', newObjective, 'Id', function (cmi, i, value) {
        if (identifier(value) !== OK) {
            return TYPE_MISMATCH;
        }

        for (var index = 0; index < cmi.Objectives.length; index++) {
            if (index !== i[0] && cmi.Objectives[index].Id === value) {
                return '351';
            }
        }

        var current = i[0] < cmi.Objectives.length ? cmi.Objectives[i[0]].Id : null;

        return current !== null && current !== undefined && current !== value ? '351' : OK;
    });
    keyword('cmi.objectives.n.score._children', function () { return SCORE_CHILDREN; });
    listField('cmi.objectives.n.score.scaled', 'Objectives', newObjective, 'ScoreScaled', real(-1, 1));
    listField('cmi.objectives.n.score.raw', 'Objectives', newObjective, 'ScoreRaw', real(null, null));
    listField('cmi.objectives.n.score.min', 'Objectives', newObjective, 'ScoreMin', real(null, null));
    listField('cmi.objectives.n.score.max', 'Objectives', newObjective, 'ScoreMax', real(null, null));
    listField('cmi.objectives.n.success_status', 'Objectives', newObjective, 'SuccessStatus', vocabulary(SUCCESS));
    listField('cmi.objectives.n.completion_status', 'Objectives', newObjective, 'CompletionStatus', vocabulary(COMPLETION));
    listField('cmi.objectives.n.progress_measure', 'Objectives', newObjective, 'ProgressMeasure', real(0, 1));
    listField('cmi.objectives.n.description', 'Objectives', newObjective, 'Description', localizedString(250));

    field('cmi.progress_measure', 'ProgressMeasure', real(0, 1));
    readOnlyField('cmi.scaled_passing_score', 'ScaledPassingScore');

    keyword('cmi.score._children', function () { return SCORE_CHILDREN; });
    field('cmi.score.scaled', 'ScoreScaled', real(-1, 1));
    field('cmi.score.raw', 'ScoreRaw', real(null, null));
    field('cmi.score.min', 'ScoreMin', real(null, null));
    field('cmi.score.max', 'ScoreMax', real(null, null));

    writeOnly('cmi.session_time', function (cmi, i, value) { cmi.SessionTime = value; }, check(duration));
    readWrite('cmi.success_status', successStatus, function (cmi, i, value) { cmi.SuccessStatus = value; }, check(vocabulary(SUCCESS)));
    readWrite('cmi.suspend_data', function (cmi) { return cmi.SuspendData; }, function (cmi, i, value) { cmi.SuspendData = value; }, function (cmi, i, value) {
        return value.length <= suspendDataLength(cmi) ? OK : TYPE_MISMATCH;
    });
    readOnlyField('cmi.time_limit_action', 'TimeLimitAction');
    readOnlyField('cmi.total_time', 'TotalTime');

    readWrite('adl.nav.request', function (cmi) { return cmi.NavRequest; }, function (cmi, i, value) { cmi.NavRequest = value; }, function (cmi, i, value) {
        if (NAV_REQUESTS.indexOf(value) >= 0) {
            return OK;
        }

        var match = /^\{target=([^}\s]+)\}(choice|jump)$/.exec(value);

        return match && (match[2] === 'choice' || cmi.Edition >= 4) ? OK : TYPE_MISMATCH;
    });

    // Shared data stores (adl.data) are not implemented: a package has none.
    keyword('adl.data._children', function () { return 'id,store'; });
    keyword('adl.data._count', function () { return '0'; });
    readOnly('adl.data.n.id', function () { return null; });
    readWrite('adl.data.n.store', function () { return null; }, function () { }, check(characterString(64000)));

    // Every element and every category above one, for the 301 and 404 keyword errors.
    var KNOWN = {};

    Object.keys(ELEMENTS).forEach(function (pattern) {
        var segments = pattern.split('.');

        for (var length = 2; length <= segments.length; length++) {
            var prefix = segments.slice(0, length).join('.');

            if (!/\._(children|count|version)$/.test(prefix)) {
                KNOWN[prefix] = true;
            }
        }
    });

    function keywordParentIsKnown(pattern) {
        var match = /\._(children|count|version)$/.exec(pattern);

        return !!match && KNOWN[pattern.slice(0, match.index)] === true;
    }

    function isRequestValidElement(cmi, rest) {
        if (rest === 'continue' || rest === 'previous') {
            return true;
        }

        var match = /^(choice|jump)\.\{target=[^}\s]+\}$/.exec(rest);

        return !!match && (match[1] === 'choice' || cmi.Edition >= 4);
    }

    function parse(element) {
        if (element.length > 1000) {
            return null;
        }

        var segments = element.split('.');
        var indexes = [];

        for (var i = 0; i < segments.length; i++) {
            if (segments[i].length === 0) {
                return null;
            }

            if (/^[0-9]+$/.test(segments[i])) {
                if (segments[i].length > 5) {
                    return null;
                }

                indexes.push(parseInt(segments[i], 10));
                segments[i] = 'n';
            }
        }

        return { pattern: segments.join('.'), indexes: indexes };
    }

    function topCount(cmi, pattern) {
        if (pattern.indexOf('cmi.objectives.n.') === 0) {
            return cmi.Objectives.length;
        }

        if (pattern.indexOf('cmi.interactions.n.') === 0) {
            return cmi.Interactions.length;
        }

        if (pattern.indexOf('cmi.comments_from_learner.n.') === 0) {
            return cmi.CommentsFromLearner.length;
        }

        if (pattern.indexOf('cmi.comments_from_lms.n.') === 0) {
            return cmi.CommentsFromLms.length;
        }

        if (pattern.indexOf('adl.data.n.') === 0) {
            return 0;
        }

        return null;
    }

    function subList(interaction, pattern) {
        return pattern.indexOf('cmi.interactions.n.objectives.') === 0 ? interaction.ObjectiveIds : interaction.CorrectResponses;
    }

    function indexesExist(cmi, pattern, indexes) {
        if (indexes.length === 0) {
            return true;
        }

        var size = topCount(cmi, pattern);

        if (size === null || indexes[0] >= size) {
            return false;
        }

        return indexes.length === 1 || indexes[1] < subList(cmi.Interactions[indexes[0]], pattern).length;
    }

    function checkWriteIndexes(cmi, pattern, indexes) {
        if (indexes.length === 0) {
            return OK;
        }

        var size = topCount(cmi, pattern);

        if (size === null || indexes[0] > size) {
            return '351';
        }

        if (indexes[0] === size) {
            if (pattern.indexOf('adl.data.') === 0) {
                return '351';
            }

            var addsById = pattern === 'cmi.objectives.n.id' || pattern === 'cmi.interactions.n.id';

            if (!addsById && pattern.indexOf('cmi.comments_from_learner.') !== 0) {
                return '408';
            }
        }

        if (indexes.length === 1) {
            return OK;
        }

        return indexes[1] > subList(cmi.Interactions[indexes[0]], pattern).length ? '351' : OK;
    }

    function getValue(cmi, element) {
        if (typeof element !== 'string' || element.length === 0) {
            return { value: '', error: '301' };
        }

        if (element.indexOf('adl.nav.request_valid.') === 0) {
            var rest = element.slice('adl.nav.request_valid.'.length);

            if (isRequestValidElement(cmi, rest)) {
                return { value: 'unknown', error: OK };
            }

            return { value: '', error: rest.indexOf('choice') === 0 || rest.indexOf('jump') === 0 ? '301' : '401' };
        }

        var parsed = parse(element);

        if (!parsed) {
            return { value: '', error: '401' };
        }

        var definition = ELEMENTS[parsed.pattern];

        if (!definition) {
            return { value: '', error: keywordParentIsKnown(parsed.pattern) ? '301' : '401' };
        }

        if (definition.access === 'w') {
            return { value: '', error: '405' };
        }

        if (!indexesExist(cmi, parsed.pattern, parsed.indexes)) {
            return { value: '', error: '301' };
        }

        var value = definition.get(cmi, parsed.indexes);

        return value === null || value === undefined ? { value: '', error: '403' } : { value: String(value), error: OK };
    }

    function setValue(cmi, element, value) {
        if (typeof element !== 'string' || element.length === 0) {
            return '351';
        }

        if (element.indexOf('adl.nav.request_valid.') === 0) {
            return isRequestValidElement(cmi, element.slice('adl.nav.request_valid.'.length)) ? '404' : '401';
        }

        var parsed = parse(element);

        if (!parsed) {
            return '401';
        }

        var definition = ELEMENTS[parsed.pattern];

        if (!definition) {
            return keywordParentIsKnown(parsed.pattern) ? '404' : '401';
        }

        if (definition.access === 'k' || definition.access === 'r') {
            return '404';
        }

        var indexError = checkWriteIndexes(cmi, parsed.pattern, parsed.indexes);

        if (indexError !== OK) {
            return indexError;
        }

        if (typeof value !== 'string') {
            return TYPE_MISMATCH;
        }

        var error = definition.check(cmi, parsed.indexes, value);

        if (error !== OK) {
            return error;
        }

        definition.set(cmi, parsed.indexes, value);

        return OK;
    }

    function normalize(cmi) {
        var data = cmi || {};

        data.Edition = data.Edition || 4;
        data.Objectives = data.Objectives || [];
        data.Interactions = data.Interactions || [];
        data.CommentsFromLearner = data.CommentsFromLearner || [];
        data.CommentsFromLms = data.CommentsFromLms || [];
        data.Interactions.forEach(function (item) {
            item.ObjectiveIds = item.ObjectiveIds || [];
            item.CorrectResponses = item.CorrectResponses || [];
        });

        return data;
    }

    // The eight methods of the SCORM 2004 API over a copy of the data; send(message) tells the player.
    function createApi(cmi, send) {
        var data = normalize(cmi);
        var state = 'new';
        var lastError = OK;
        var notify = typeof send === 'function' ? send : function () { };

        function fail(code) {
            lastError = code;

            return 'false';
        }

        function succeed() {
            lastError = OK;

            return 'true';
        }

        function isEmpty(parameter) {
            return parameter === undefined || parameter === null || parameter === '';
        }

        return {
            Initialize: function (parameter) {
                if (!isEmpty(parameter)) {
                    return fail('201');
                }

                if (state === 'running') {
                    return fail('103');
                }

                if (state === 'terminated') {
                    return fail('104');
                }

                state = 'running';
                notify({ type: 'initialize' });

                return succeed();
            },
            Terminate: function (parameter) {
                if (!isEmpty(parameter)) {
                    return fail('201');
                }

                if (state === 'new') {
                    return fail('112');
                }

                if (state === 'terminated') {
                    return fail('113');
                }

                state = 'terminated';
                notify({ type: 'finish' });

                return succeed();
            },
            GetValue: function (element) {
                if (state !== 'running') {
                    fail(state === 'new' ? '122' : '123');

                    return '';
                }

                var read = getValue(data, element === undefined || element === null ? '' : String(element));

                lastError = read.error;

                return read.value;
            },
            SetValue: function (element, value) {
                if (state !== 'running') {
                    return fail(state === 'new' ? '132' : '133');
                }

                var name = element === undefined || element === null ? '' : String(element);
                var text = value === undefined || value === null ? value : String(value);
                var error = setValue(data, name, text);

                if (error !== OK) {
                    return fail(error);
                }

                notify({ type: 'set', element: name, value: text });

                return succeed();
            },
            Commit: function (parameter) {
                if (!isEmpty(parameter)) {
                    return fail('201');
                }

                if (state !== 'running') {
                    return fail(state === 'new' ? '142' : '143');
                }

                notify({ type: 'commit' });

                return succeed();
            },
            GetLastError: function () {
                return lastError;
            },
            GetErrorString: function (code) {
                return ERRORS[String(code)] || '';
            },
            GetDiagnostic: function (code) {
                return ERRORS[isEmpty(code) ? lastError : String(code)] || '';
            },
        };
    }

    root.CrestAppsScorm2004 = {
        createApi: createApi,
        getValue: getValue,
        setValue: setValue,
        isDuration: isDuration,
        isTimestamp: isTimestamp,
        PROTOCOL: PROTOCOL,
        FRAME_NAME: FRAME_NAME,
    };

    var launch = root.CrestAppsScorm2004Launch;

    if (!launch || typeof root.postMessage !== 'function') {
        return;
    }

    // The page the frame tells: the player for the package frame itself (by the name the player gives its frame), else
    // the package page above this one (a frame of a frameset, or a window the package opened), which passes it on.
    function target() {
        if (root.parent && root.parent !== root) {
            return root.parent;
        }

        return root.opener || null;
    }

    function post(message) {
        var page = target();

        if (!page) {
            return;
        }

        message.protocol = PROTOCOL;
        message.launchId = launch.launchId;

        try {
            page.postMessage(message, root.name === FRAME_NAME && launch.playerOrigin ? launch.playerOrigin : '*');
        } catch (error) {
            // A closed page; nothing to tell.
        }
    }

    // Messages of the package's own frames and windows are passed up, as they came.
    root.addEventListener('message', function (event) {
        var message = event.data;

        if (!message || message.protocol !== PROTOCOL || message.launchId !== launch.launchId || !event.source || event.source === root) {
            return;
        }

        var child = false;

        try {
            child = event.source.parent === root || event.source.opener === root;
        } catch (error) {
            child = false;
        }

        if (child) {
            post({ type: message.type, element: message.element, value: message.value });
        }
    });

    root.API_1484_11 = createApi(launch.cmi, post);
})(typeof window !== 'undefined' ? window : globalThis);
