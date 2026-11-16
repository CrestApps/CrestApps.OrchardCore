// The SCORM 1.2 run-time API of a package frame. The server puts this script, with the learner's data, at the start of
// every HTML page of a package it serves, so a SCO finds window.API on its own window (or on a parent or opener page of
// the package, the way SCOs search for it). The frame is sandboxed on an opaque origin: it has no cookie and cannot reach
// the site. It answers the SCO at once from its copy of the data, with the error codes of SCORM 1.2, and tells the player
// page what the SCO set, commits and finishes only through postMessage. The player sends it to the server with the
// learner's session, and the server checks every value again (Scorm12DataModel); nothing this script accepts is trusted.
(function (root) {
    'use strict';

    var PROTOCOL = 'crestapps-scorm12';
    var FRAME_NAME = 'crestapps-package-frame';

    var ERRORS = {
        '0': 'No error',
        '101': 'General exception',
        '201': 'Invalid argument error',
        '202': 'Element cannot have children',
        '203': 'Element not an array - cannot have count',
        '301': 'Not initialized',
        '401': 'Not implemented error',
        '402': 'Invalid set value, element is a keyword',
        '403': 'Element is read only',
        '404': 'Element is write only',
        '405': 'Incorrect Data Type',
    };

    var LESSON_STATUSES = ['passed', 'completed', 'failed', 'incomplete', 'browsed'];
    var OBJECTIVE_STATUSES = ['passed', 'completed', 'failed', 'incomplete', 'browsed', 'not attempted'];
    var EXITS = ['time-out', 'suspend', 'logout', ''];
    var TYPES = ['true-false', 'choice', 'fill-in', 'matching', 'performance', 'sequencing', 'likert', 'numeric'];
    var RESULTS = ['correct', 'wrong', 'unanticipated', 'neutral'];
    var SCORE_CHILDREN = 'raw,min,max';

    function isIn(list) {
        return function (value) { return list.indexOf(value) >= 0; };
    }

    function isString(max) {
        return function (value) { return value.length <= max; };
    }

    function isDecimal(value) {
        return /^-?([0-9]+(\.[0-9]*)?|\.[0-9]+)$/.test(value);
    }

    function isScore(value) {
        if (value === '') {
            return true;
        }

        if (!isDecimal(value)) {
            return false;
        }

        var number = parseFloat(value);

        return number >= 0 && number <= 100;
    }

    function isInteger(min, max) {
        return function (value) {
            if (!/^-?[0-9]+$/.test(value)) {
                return false;
            }

            var number = parseInt(value, 10);

            return number >= min && number <= max;
        };
    }

    function isIdentifier(value) {
        return /^[!-~]{1,255}$/.test(value);
    }

    function isTimespan(value) {
        var match = /^([0-9]{2,4}):([0-9]{2}):([0-9]{2})(\.[0-9]{1,2})?$/.exec(value);

        return !!match && parseInt(match[2], 10) < 60 && parseInt(match[3], 10) < 60;
    }

    function isTime(value) {
        var match = /^([0-9]{2}):([0-9]{2}):([0-9]{2})(\.[0-9]{1,2})?$/.exec(value);

        return !!match && parseInt(match[1], 10) < 24 && parseInt(match[2], 10) < 60 && parseInt(match[3], 10) < 60;
    }

    function objective(cmi, index) {
        if (index === cmi.Objectives.length) {
            cmi.Objectives.push({ Id: '', ScoreRaw: '', ScoreMin: '', ScoreMax: '', Status: '' });
        }

        return cmi.Objectives[index];
    }

    function interaction(cmi, index) {
        if (index === cmi.Interactions.length) {
            cmi.Interactions.push({ Id: '', ObjectiveIds: [], Time: '', Type: '', CorrectResponses: [], Weighting: '', StudentResponse: '', Result: '', Latency: '' });
        }

        return cmi.Interactions[index];
    }

    function setAt(list, index, value) {
        if (index === list.length) {
            list.push(value);
        } else {
            list[index] = value;
        }
    }

    // The same table as the server's Scorm12DataModel: access k (keyword), r, w or rw.
    var ELEMENTS = {};

    function keyword(pattern, get) { ELEMENTS[pattern] = { access: 'k', get: get }; }
    function readOnly(pattern, name) { ELEMENTS[pattern] = { access: 'r', get: function (cmi) { return cmi[name]; } }; }
    function field(pattern, name, valid) {
        ELEMENTS[pattern] = {
            access: 'rw',
            get: function (cmi) { return cmi[name]; },
            set: function (cmi, i, value) { cmi[name] = value; },
            valid: valid,
        };
    }
    function writeOnly(pattern, set, valid) { ELEMENTS[pattern] = { access: 'w', set: set, valid: valid }; }
    function objectiveField(pattern, name, valid) {
        ELEMENTS[pattern] = {
            access: 'rw',
            get: function (cmi, i) { return cmi.Objectives[i[0]][name]; },
            set: function (cmi, i, value) { objective(cmi, i[0])[name] = value; },
            valid: valid,
        };
    }
    function interactionField(pattern, name, valid) {
        writeOnly(pattern, function (cmi, i, value) { interaction(cmi, i[0])[name] = value; }, valid);
    }

    keyword('cmi._version', function () { return '3.4'; });
    keyword('cmi._children', function () { return 'core,suspend_data,launch_data,comments,objectives,student_data,student_preference,interactions'; });
    keyword('cmi.core._children', function () { return 'student_id,student_name,lesson_location,credit,lesson_status,entry,score,total_time,lesson_mode,exit,session_time'; });
    readOnly('cmi.core.student_id', 'StudentId');
    readOnly('cmi.core.student_name', 'StudentName');
    field('cmi.core.lesson_location', 'LessonLocation', isString(255));
    readOnly('cmi.core.credit', 'Credit');
    field('cmi.core.lesson_status', 'LessonStatus', isIn(LESSON_STATUSES));
    readOnly('cmi.core.entry', 'Entry');
    keyword('cmi.core.score._children', function () { return SCORE_CHILDREN; });
    field('cmi.core.score.raw', 'ScoreRaw', isScore);
    field('cmi.core.score.min', 'ScoreMin', isScore);
    field('cmi.core.score.max', 'ScoreMax', isScore);
    readOnly('cmi.core.total_time', 'TotalTime');
    readOnly('cmi.core.lesson_mode', 'LessonMode');
    writeOnly('cmi.core.exit', function (cmi, i, value) { cmi.Exit = value; }, isIn(EXITS));
    writeOnly('cmi.core.session_time', function (cmi, i, value) { cmi.SessionTime = value; }, isTimespan);
    field('cmi.suspend_data', 'SuspendData', isString(4096));
    readOnly('cmi.launch_data', 'LaunchData');
    field('cmi.comments', 'Comments', isString(4096));
    readOnly('cmi.comments_from_lms', 'CommentsFromLms');
    keyword('cmi.objectives._children', function () { return 'id,score,status'; });
    keyword('cmi.objectives._count', function (cmi) { return String(cmi.Objectives.length); });
    objectiveField('cmi.objectives.n.id', 'Id', isIdentifier);
    keyword('cmi.objectives.n.score._children', function () { return SCORE_CHILDREN; });
    objectiveField('cmi.objectives.n.score.raw', 'ScoreRaw', isScore);
    objectiveField('cmi.objectives.n.score.min', 'ScoreMin', isScore);
    objectiveField('cmi.objectives.n.score.max', 'ScoreMax', isScore);
    objectiveField('cmi.objectives.n.status', 'Status', isIn(OBJECTIVE_STATUSES));
    keyword('cmi.student_data._children', function () { return 'mastery_score,max_time_allowed,time_limit_action'; });
    readOnly('cmi.student_data.mastery_score', 'MasteryScore');
    readOnly('cmi.student_data.max_time_allowed', 'MaxTimeAllowed');
    readOnly('cmi.student_data.time_limit_action', 'TimeLimitAction');
    keyword('cmi.student_preference._children', function () { return 'audio,language,speed,text'; });
    field('cmi.student_preference.audio', 'Audio', isInteger(-1, 100));
    field('cmi.student_preference.language', 'Language', isString(255));
    field('cmi.student_preference.speed', 'Speed', isInteger(-100, 100));
    field('cmi.student_preference.text', 'Text', isInteger(-1, 1));
    keyword('cmi.interactions._children', function () { return 'id,objectives,time,type,correct_responses,weighting,student_response,result,latency'; });
    keyword('cmi.interactions._count', function (cmi) { return String(cmi.Interactions.length); });
    interactionField('cmi.interactions.n.id', 'Id', isIdentifier);
    keyword('cmi.interactions.n.objectives._count', function (cmi, i) { return String(i[0] < cmi.Interactions.length ? cmi.Interactions[i[0]].ObjectiveIds.length : 0); });
    writeOnly('cmi.interactions.n.objectives.n.id', function (cmi, i, value) { setAt(interaction(cmi, i[0]).ObjectiveIds, i[1], value); }, isIdentifier);
    interactionField('cmi.interactions.n.time', 'Time', isTime);
    interactionField('cmi.interactions.n.type', 'Type', isIn(TYPES));
    keyword('cmi.interactions.n.correct_responses._count', function (cmi, i) { return String(i[0] < cmi.Interactions.length ? cmi.Interactions[i[0]].CorrectResponses.length : 0); });
    writeOnly('cmi.interactions.n.correct_responses.n.pattern', function (cmi, i, value) { setAt(interaction(cmi, i[0]).CorrectResponses, i[1], value); }, isString(255));
    interactionField('cmi.interactions.n.weighting', 'Weighting', isDecimal);
    interactionField('cmi.interactions.n.student_response', 'StudentResponse', isString(255));
    interactionField('cmi.interactions.n.result', 'Result', function (value) { return RESULTS.indexOf(value) >= 0 || isDecimal(value); });
    interactionField('cmi.interactions.n.latency', 'Latency', isTimespan);

    // Every element and every category above one, for the 202 and 203 errors.
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

    function parse(element) {
        if (typeof element !== 'string' || element.length === 0 || element.length > 255) {
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

    function keywordError(pattern, childrenError, countError) {
        var children = /\._children$/.exec(pattern);

        if (children && KNOWN[pattern.slice(0, children.index)]) {
            return childrenError;
        }

        var count = /\._count$/.exec(pattern);

        if (count && KNOWN[pattern.slice(0, count.index)]) {
            return countError;
        }

        return '201';
    }

    function within(index, count, allowNext) {
        return index < count || (allowNext && index === count);
    }

    function indexesExist(cmi, pattern, indexes, allowNext) {
        if (indexes.length === 0) {
            return true;
        }

        if (pattern.indexOf('cmi.objectives.n.') === 0) {
            return within(indexes[0], cmi.Objectives.length, allowNext);
        }

        if (pattern.indexOf('cmi.interactions.n.') !== 0 || !within(indexes[0], cmi.Interactions.length, allowNext)) {
            return false;
        }

        if (indexes.length === 1) {
            return true;
        }

        var item = indexes[0] < cmi.Interactions.length ? cmi.Interactions[indexes[0]] : null;
        var count = pattern.indexOf('cmi.interactions.n.objectives.') === 0
            ? (item ? item.ObjectiveIds.length : 0)
            : (item ? item.CorrectResponses.length : 0);

        return within(indexes[1], count, allowNext);
    }

    function getValue(cmi, element) {
        var parsed = parse(element);

        if (!parsed) {
            return { value: '', error: '201' };
        }

        var definition = ELEMENTS[parsed.pattern];

        if (!definition) {
            return { value: '', error: keywordError(parsed.pattern, '202', '203') };
        }

        if (definition.access === 'w') {
            return { value: '', error: '404' };
        }

        if (!indexesExist(cmi, parsed.pattern, parsed.indexes, false)) {
            return { value: '', error: '201' };
        }

        var value = definition.get(cmi, parsed.indexes);

        return { value: value === undefined || value === null ? '' : String(value), error: '0' };
    }

    function setValue(cmi, element, value) {
        var parsed = parse(element);

        if (!parsed) {
            return '201';
        }

        var definition = ELEMENTS[parsed.pattern];

        if (!definition) {
            return keywordError(parsed.pattern, '402', '402');
        }

        if (definition.access === 'k') {
            return '402';
        }

        if (definition.access === 'r') {
            return '403';
        }

        if (!indexesExist(cmi, parsed.pattern, parsed.indexes, true)) {
            return '201';
        }

        if (typeof value !== 'string' || !definition.valid(value)) {
            return '405';
        }

        definition.set(cmi, parsed.indexes, value);

        return '0';
    }

    function normalize(cmi) {
        var data = cmi || {};

        data.Objectives = data.Objectives || [];
        data.Interactions = data.Interactions || [];
        data.Interactions.forEach(function (item) {
            item.ObjectiveIds = item.ObjectiveIds || [];
            item.CorrectResponses = item.CorrectResponses || [];
        });

        return data;
    }

    // The eight functions of the SCORM 1.2 API over a copy of the data; send(message) tells the player.
    function createApi(cmi, send) {
        var data = normalize(cmi);
        var state = 'new';
        var lastError = '0';
        var notify = typeof send === 'function' ? send : function () { };

        function fail(code) {
            lastError = code;

            return 'false';
        }

        function succeed() {
            lastError = '0';

            return 'true';
        }

        function isEmpty(parameter) {
            return parameter === undefined || parameter === null || parameter === '';
        }

        return {
            LMSInitialize: function (parameter) {
                if (!isEmpty(parameter)) {
                    return fail('201');
                }

                if (state !== 'new') {
                    return fail('101');
                }

                state = 'running';
                notify({ type: 'initialize' });

                return succeed();
            },
            LMSFinish: function (parameter) {
                if (!isEmpty(parameter)) {
                    return fail('201');
                }

                if (state !== 'running') {
                    return fail('301');
                }

                state = 'finished';
                notify({ type: 'finish' });

                return succeed();
            },
            LMSGetValue: function (element) {
                if (state !== 'running') {
                    fail('301');

                    return '';
                }

                var read = getValue(data, element === undefined || element === null ? '' : String(element));

                lastError = read.error;

                return read.value;
            },
            LMSSetValue: function (element, value) {
                if (state !== 'running') {
                    return fail('301');
                }

                var name = element === undefined || element === null ? '' : String(element);
                var text = value === undefined || value === null ? value : String(value);
                var error = setValue(data, name, text);

                if (error !== '0') {
                    return fail(error);
                }

                notify({ type: 'set', element: name, value: text });

                return succeed();
            },
            LMSCommit: function (parameter) {
                if (!isEmpty(parameter)) {
                    return fail('201');
                }

                if (state !== 'running') {
                    return fail('301');
                }

                notify({ type: 'commit' });

                return succeed();
            },
            LMSGetLastError: function () {
                return lastError;
            },
            LMSGetErrorString: function (code) {
                return ERRORS[String(code)] || '';
            },
            LMSGetDiagnostic: function (code) {
                return ERRORS[isEmpty(code) ? lastError : String(code)] || '';
            },
        };
    }

    root.CrestAppsScorm12 = { createApi: createApi, getValue: getValue, setValue: setValue, PROTOCOL: PROTOCOL, FRAME_NAME: FRAME_NAME };

    var launch = root.CrestAppsScorm12Launch;

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
        var data = event.data;

        if (!data || data.protocol !== PROTOCOL || data.launchId !== launch.launchId || !event.source || event.source === root) {
            return;
        }

        var child = false;

        try {
            child = event.source.parent === root || event.source.opener === root;
        } catch (error) {
            child = false;
        }

        if (child) {
            post({ type: data.type, element: data.element, value: data.value });
        }
    });

    root.API = createApi(launch.cmi, post);
})(typeof window !== 'undefined' ? window : globalThis);
