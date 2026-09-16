var SIP = (() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __export = (target, all) => {
    for (var name2 in all)
      __defProp(target, name2, { get: all[name2], enumerable: true });
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

  // entry.js
  var entry_exports = {};
  __export(entry_exports, {
    Ack: () => Ack,
    Bye: () => Bye,
    Cancel: () => Cancel,
    ContentTypeUnsupportedError: () => ContentTypeUnsupportedError,
    Core: () => core_exports,
    EmitterImpl: () => EmitterImpl,
    Grammar: () => Grammar,
    Info: () => Info,
    Invitation: () => Invitation,
    Inviter: () => Inviter,
    Message: () => Message,
    Messager: () => Messager,
    NameAddrHeader: () => NameAddrHeader,
    Notification: () => Notification,
    Parameters: () => Parameters,
    Publisher: () => Publisher,
    PublisherState: () => PublisherState,
    Referral: () => Referral,
    Registerer: () => Registerer,
    RegistererState: () => RegistererState,
    RequestPendingError: () => RequestPendingError,
    SIPExtension: () => SIPExtension,
    Session: () => Session,
    SessionDescriptionHandlerError: () => SessionDescriptionHandlerError,
    SessionState: () => SessionState2,
    SessionTerminatedError: () => SessionTerminatedError,
    StateTransitionError: () => StateTransitionError,
    Subscriber: () => Subscriber,
    Subscription: () => Subscription,
    SubscriptionState: () => SubscriptionState2,
    TransportState: () => TransportState,
    URI: () => URI,
    UserAgent: () => UserAgent,
    UserAgentRegisteredOptionTags: () => UserAgentRegisteredOptionTags,
    UserAgentState: () => UserAgentState,
    Web: () => web_exports,
    equivalentURI: () => equivalentURI,
    name: () => name,
    version: () => version
  });

  // node_modules/sip.js/lib/version.js
  var LIBRARY_VERSION = "0.21.1";

  // node_modules/sip.js/lib/core/exceptions/exception.js
  var Exception = class extends Error {
    constructor(message) {
      super(message);
      Object.setPrototypeOf(this, new.target.prototype);
    }
  };

  // node_modules/sip.js/lib/api/exceptions/content-type-unsupported.js
  var ContentTypeUnsupportedError = class extends Exception {
    constructor(message) {
      super(message ? message : "Unsupported content type.");
    }
  };

  // node_modules/sip.js/lib/api/exceptions/request-pending.js
  var RequestPendingError = class extends Exception {
    /** @internal */
    constructor(message) {
      super(message ? message : "Request pending.");
    }
  };

  // node_modules/sip.js/lib/api/exceptions/session-description-handler.js
  var SessionDescriptionHandlerError = class extends Exception {
    constructor(message) {
      super(message ? message : "Unspecified session description handler error.");
    }
  };

  // node_modules/sip.js/lib/api/exceptions/session-terminated.js
  var SessionTerminatedError = class extends Exception {
    constructor() {
      super("The session has terminated.");
    }
  };

  // node_modules/sip.js/lib/api/exceptions/state-transition.js
  var StateTransitionError = class extends Exception {
    constructor(message) {
      super(message ? message : "An error occurred during state transition.");
    }
  };

  // node_modules/sip.js/lib/api/ack.js
  var Ack = class {
    /** @internal */
    constructor(incomingAckRequest) {
      this.incomingAckRequest = incomingAckRequest;
    }
    /** Incoming ACK request message. */
    get request() {
      return this.incomingAckRequest.message;
    }
  };

  // node_modules/sip.js/lib/api/bye.js
  var Bye = class {
    /** @internal */
    constructor(incomingByeRequest) {
      this.incomingByeRequest = incomingByeRequest;
    }
    /** Incoming BYE request message. */
    get request() {
      return this.incomingByeRequest.message;
    }
    /** Accept the request. */
    accept(options) {
      this.incomingByeRequest.accept(options);
      return Promise.resolve();
    }
    /** Reject the request. */
    reject(options) {
      this.incomingByeRequest.reject(options);
      return Promise.resolve();
    }
  };

  // node_modules/sip.js/lib/api/cancel.js
  var Cancel = class {
    /** @internal */
    constructor(incomingCancelRequest) {
      this.incomingCancelRequest = incomingCancelRequest;
    }
    /** Incoming CANCEL request message. */
    get request() {
      return this.incomingCancelRequest;
    }
  };

  // node_modules/sip.js/lib/api/emitter.js
  var EmitterImpl = class {
    constructor() {
      this.listeners = new Array();
    }
    /**
     * Sets up a function that will be called whenever the target changes.
     * @param listener - Callback function.
     * @param options - An options object that specifies characteristics about the listener.
     *                  If once true, indicates that the listener should be invoked at most once after being added.
     *                  If once true, the listener would be automatically removed when invoked.
     */
    addListener(listener, options) {
      const onceWrapper = (data) => {
        this.removeListener(onceWrapper);
        listener(data);
      };
      (options === null || options === void 0 ? void 0 : options.once) === true ? this.listeners.push(onceWrapper) : this.listeners.push(listener);
    }
    /**
     * Emit change.
     * @param data - Data to emit.
     */
    emit(data) {
      this.listeners.slice().forEach((listener) => listener(data));
    }
    /**
     * Removes all listeners previously registered with addListener.
     */
    removeAllListeners() {
      this.listeners = [];
    }
    /**
     * Removes a listener previously registered with addListener.
     * @param listener - Callback function.
     */
    removeListener(listener) {
      this.listeners = this.listeners.filter((l) => l !== listener);
    }
    /**
     * Registers a listener.
     * @param listener - Callback function.
     * @deprecated Use addListener.
     */
    on(listener) {
      return this.addListener(listener);
    }
    /**
     * Unregisters a listener.
     * @param listener - Callback function.
     * @deprecated Use removeListener.
     */
    off(listener) {
      return this.removeListener(listener);
    }
    /**
     * Registers a listener then unregisters the listener after one event emission.
     * @param listener - Callback function.
     * @deprecated Use addListener.
     */
    once(listener) {
      return this.addListener(listener, { once: true });
    }
  };

  // node_modules/sip.js/lib/api/info.js
  var Info = class {
    /** @internal */
    constructor(incomingInfoRequest) {
      this.incomingInfoRequest = incomingInfoRequest;
    }
    /** Incoming MESSAGE request message. */
    get request() {
      return this.incomingInfoRequest.message;
    }
    /** Accept the request. */
    accept(options) {
      this.incomingInfoRequest.accept(options);
      return Promise.resolve();
    }
    /** Reject the request. */
    reject(options) {
      this.incomingInfoRequest.reject(options);
      return Promise.resolve();
    }
  };

  // node_modules/sip.js/lib/grammar/parameters.js
  var Parameters = class {
    constructor(parameters) {
      this.parameters = {};
      for (const param in parameters) {
        if (parameters.hasOwnProperty(param)) {
          this.setParam(param, parameters[param]);
        }
      }
    }
    setParam(key, value) {
      if (key) {
        this.parameters[key.toLowerCase()] = typeof value === "undefined" || value === null ? null : value.toString();
      }
    }
    getParam(key) {
      if (key) {
        return this.parameters[key.toLowerCase()];
      }
    }
    hasParam(key) {
      return !!(key && this.parameters[key.toLowerCase()] !== void 0);
    }
    deleteParam(key) {
      key = key.toLowerCase();
      if (this.hasParam(key)) {
        const value = this.parameters[key];
        delete this.parameters[key];
        return value;
      }
    }
    clearParams() {
      this.parameters = {};
    }
  };

  // node_modules/sip.js/lib/grammar/name-addr-header.js
  var NameAddrHeader = class _NameAddrHeader extends Parameters {
    /**
     * Constructor
     * @param uri -
     * @param displayName -
     * @param parameters -
     */
    constructor(uri, displayName, parameters) {
      super(parameters);
      this.uri = uri;
      this._displayName = displayName;
    }
    get friendlyName() {
      return this.displayName || this.uri.aor;
    }
    get displayName() {
      return this._displayName;
    }
    set displayName(value) {
      this._displayName = value;
    }
    clone() {
      return new _NameAddrHeader(this.uri.clone(), this._displayName, JSON.parse(JSON.stringify(this.parameters)));
    }
    toString() {
      let body = this.displayName || this.displayName === "0" ? '"' + this.displayName + '" ' : "";
      body += "<" + this.uri.toString() + ">";
      for (const parameter in this.parameters) {
        if (this.parameters.hasOwnProperty(parameter)) {
          body += ";" + parameter;
          if (this.parameters[parameter] !== null) {
            body += "=" + this.parameters[parameter];
          }
        }
      }
      return body;
    }
  };

  // node_modules/sip.js/lib/grammar/uri.js
  var URI = class _URI extends Parameters {
    /**
     * Constructor
     * @param scheme -
     * @param user -
     * @param host -
     * @param port -
     * @param parameters -
     * @param headers -
     */
    constructor(scheme = "sip", user, host, port, parameters, headers) {
      super(parameters || {});
      this.headers = {};
      if (!host) {
        throw new TypeError('missing or invalid "host" parameter');
      }
      for (const header in headers) {
        if (headers.hasOwnProperty(header)) {
          this.setHeader(header, headers[header]);
        }
      }
      this.raw = {
        scheme,
        user,
        host,
        port
      };
      this.normal = {
        scheme: scheme.toLowerCase(),
        user,
        host: host.toLowerCase(),
        port
      };
    }
    get scheme() {
      return this.normal.scheme;
    }
    set scheme(value) {
      this.raw.scheme = value;
      this.normal.scheme = value.toLowerCase();
    }
    get user() {
      return this.normal.user;
    }
    set user(value) {
      this.normal.user = this.raw.user = value;
    }
    get host() {
      return this.normal.host;
    }
    set host(value) {
      this.raw.host = value;
      this.normal.host = value.toLowerCase();
    }
    get aor() {
      return this.normal.user + "@" + this.normal.host;
    }
    get port() {
      return this.normal.port;
    }
    set port(value) {
      this.normal.port = this.raw.port = value === 0 ? value : value;
    }
    setHeader(name2, value) {
      this.headers[this.headerize(name2)] = value instanceof Array ? value : [value];
    }
    getHeader(name2) {
      if (name2) {
        return this.headers[this.headerize(name2)];
      }
    }
    hasHeader(name2) {
      return !!name2 && !!this.headers.hasOwnProperty(this.headerize(name2));
    }
    deleteHeader(header) {
      header = this.headerize(header);
      if (this.headers.hasOwnProperty(header)) {
        const value = this.headers[header];
        delete this.headers[header];
        return value;
      }
    }
    clearHeaders() {
      this.headers = {};
    }
    clone() {
      return new _URI(this._raw.scheme, this._raw.user || "", this._raw.host, this._raw.port, JSON.parse(JSON.stringify(this.parameters)), JSON.parse(JSON.stringify(this.headers)));
    }
    toRaw() {
      return this._toString(this._raw);
    }
    toString() {
      return this._toString(this._normal);
    }
    get _normal() {
      return this.normal;
    }
    get _raw() {
      return this.raw;
    }
    _toString(uri) {
      let uriString = uri.scheme + ":";
      if (!uri.scheme.toLowerCase().match("^sips?$")) {
        uriString += "//";
      }
      if (uri.user) {
        uriString += this.escapeUser(uri.user) + "@";
      }
      uriString += uri.host;
      if (uri.port || uri.port === 0) {
        uriString += ":" + uri.port;
      }
      for (const parameter in this.parameters) {
        if (this.parameters.hasOwnProperty(parameter)) {
          uriString += ";" + parameter;
          if (this.parameters[parameter] !== null) {
            uriString += "=" + this.parameters[parameter];
          }
        }
      }
      const headers = [];
      for (const header in this.headers) {
        if (this.headers.hasOwnProperty(header)) {
          for (const idx in this.headers[header]) {
            if (this.headers[header].hasOwnProperty(idx)) {
              headers.push(header + "=" + this.headers[header][idx]);
            }
          }
        }
      }
      if (headers.length > 0) {
        uriString += "?" + headers.join("&");
      }
      return uriString;
    }
    /*
     * Hex-escape a SIP URI user.
     * @private
     * @param {String} user
     */
    escapeUser(user) {
      let decodedUser;
      try {
        decodedUser = decodeURIComponent(user);
      } catch (error) {
        throw error;
      }
      return encodeURIComponent(decodedUser).replace(/%3A/ig, ":").replace(/%2B/ig, "+").replace(/%3F/ig, "?").replace(/%2F/ig, "/");
    }
    headerize(str) {
      const exceptions = {
        "Call-Id": "Call-ID",
        "Cseq": "CSeq",
        "Min-Se": "Min-SE",
        "Rack": "RAck",
        "Rseq": "RSeq",
        "Www-Authenticate": "WWW-Authenticate"
      };
      const name2 = str.toLowerCase().replace(/_/g, "-").split("-");
      const parts = name2.length;
      let hname = "";
      for (let part = 0; part < parts; part++) {
        if (part !== 0) {
          hname += "-";
        }
        hname += name2[part].charAt(0).toUpperCase() + name2[part].substring(1);
      }
      if (exceptions[hname]) {
        hname = exceptions[hname];
      }
      return hname;
    }
  };
  function equivalentURI(a, b) {
    if (a.scheme !== b.scheme) {
      return false;
    }
    if (a.user !== b.user || a.host !== b.host || a.port !== b.port) {
      return false;
    }
    function compareParameters(a2, b2) {
      const parameterKeysA = Object.keys(a2.parameters);
      const parameterKeysB = Object.keys(b2.parameters);
      const intersection = parameterKeysA.filter((x) => parameterKeysB.includes(x));
      if (!intersection.every((key) => a2.parameters[key] === b2.parameters[key])) {
        return false;
      }
      if (!["user", "ttl", "method", "transport"].every((key) => a2.hasParam(key) && b2.hasParam(key) || !a2.hasParam(key) && !b2.hasParam(key))) {
        return false;
      }
      if (!["maddr"].every((key) => a2.hasParam(key) && b2.hasParam(key) || !a2.hasParam(key) && !b2.hasParam(key))) {
        return false;
      }
      return true;
    }
    if (!compareParameters(a, b)) {
      return false;
    }
    const headerKeysA = Object.keys(a.headers);
    const headerKeysB = Object.keys(b.headers);
    if (headerKeysA.length !== 0 || headerKeysB.length !== 0) {
      if (headerKeysA.length !== headerKeysB.length) {
        return false;
      }
      const intersection = headerKeysA.filter((x) => headerKeysB.includes(x));
      if (intersection.length !== headerKeysB.length) {
        return false;
      }
      if (!intersection.every((key) => a.headers[key].length && b.headers[key].length && a.headers[key][0] === b.headers[key][0])) {
        return false;
      }
    }
    return true;
  }

  // node_modules/sip.js/lib/grammar/pegjs/dist/grammar.js
  function peg$padEnd(str, targetLength, padString) {
    padString = padString || " ";
    if (str.length > targetLength) {
      return str;
    }
    targetLength -= str.length;
    padString += padString.repeat(targetLength);
    return str + padString.slice(0, targetLength);
  }
  var SyntaxError = class _SyntaxError extends Error {
    constructor(message, expected, found, location) {
      super();
      this.message = message;
      this.expected = expected;
      this.found = found;
      this.location = location;
      this.name = "SyntaxError";
      if (typeof Object.setPrototypeOf === "function") {
        Object.setPrototypeOf(this, _SyntaxError.prototype);
      } else {
        this.__proto__ = _SyntaxError.prototype;
      }
      if (typeof Error.captureStackTrace === "function") {
        Error.captureStackTrace(this, _SyntaxError);
      }
    }
    static buildMessage(expected, found) {
      function hex(ch) {
        return ch.charCodeAt(0).toString(16).toUpperCase();
      }
      function literalEscape(s) {
        return s.replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\0/g, "\\0").replace(/\t/g, "\\t").replace(/\n/g, "\\n").replace(/\r/g, "\\r").replace(/[\x00-\x0F]/g, (ch) => "\\x0" + hex(ch)).replace(/[\x10-\x1F\x7F-\x9F]/g, (ch) => "\\x" + hex(ch));
      }
      function classEscape(s) {
        return s.replace(/\\/g, "\\\\").replace(/\]/g, "\\]").replace(/\^/g, "\\^").replace(/-/g, "\\-").replace(/\0/g, "\\0").replace(/\t/g, "\\t").replace(/\n/g, "\\n").replace(/\r/g, "\\r").replace(/[\x00-\x0F]/g, (ch) => "\\x0" + hex(ch)).replace(/[\x10-\x1F\x7F-\x9F]/g, (ch) => "\\x" + hex(ch));
      }
      function describeExpectation(expectation) {
        switch (expectation.type) {
          case "literal":
            return '"' + literalEscape(expectation.text) + '"';
          case "class":
            const escapedParts = expectation.parts.map((part) => {
              return Array.isArray(part) ? classEscape(part[0]) + "-" + classEscape(part[1]) : classEscape(part);
            });
            return "[" + (expectation.inverted ? "^" : "") + escapedParts + "]";
          case "any":
            return "any character";
          case "end":
            return "end of input";
          case "other":
            return expectation.description;
        }
      }
      function describeExpected(expected1) {
        const descriptions = expected1.map(describeExpectation);
        let i;
        let j;
        descriptions.sort();
        if (descriptions.length > 0) {
          for (i = 1, j = 1; i < descriptions.length; i++) {
            if (descriptions[i - 1] !== descriptions[i]) {
              descriptions[j] = descriptions[i];
              j++;
            }
          }
          descriptions.length = j;
        }
        switch (descriptions.length) {
          case 1:
            return descriptions[0];
          case 2:
            return descriptions[0] + " or " + descriptions[1];
          default:
            return descriptions.slice(0, -1).join(", ") + ", or " + descriptions[descriptions.length - 1];
        }
      }
      function describeFound(found1) {
        return found1 ? '"' + literalEscape(found1) + '"' : "end of input";
      }
      return "Expected " + describeExpected(expected) + " but " + describeFound(found) + " found.";
    }
    format(sources) {
      let str = "Error: " + this.message;
      if (this.location) {
        let src = null;
        let k;
        for (k = 0; k < sources.length; k++) {
          if (sources[k].source === this.location.source) {
            src = sources[k].text.split(/\r\n|\n|\r/g);
            break;
          }
        }
        let s = this.location.start;
        let loc = this.location.source + ":" + s.line + ":" + s.column;
        if (src) {
          let e = this.location.end;
          let filler = peg$padEnd("", s.line.toString().length, " ");
          let line = src[s.line - 1];
          let last = s.line === e.line ? e.column : line.length + 1;
          str += "\n --> " + loc + "\n" + filler + " |\n" + s.line + " | " + line + "\n" + filler + " | " + peg$padEnd("", s.column - 1, " ") + peg$padEnd("", last - s.column, "^");
        } else {
          str += "\n at " + loc;
        }
      }
      return str;
    }
  };
  function peg$parse(input, options) {
    options = options !== void 0 ? options : {};
    const peg$FAILED = {};
    const peg$source = options.grammarSource;
    const peg$startRuleIndices = { Contact: 119, Name_Addr_Header: 156, Record_Route: 176, Request_Response: 81, SIP_URI: 45, Subscription_State: 186, Supported: 191, Require: 182, Via: 194, absoluteURI: 84, Call_ID: 118, Content_Disposition: 130, Content_Length: 135, Content_Type: 136, CSeq: 146, displayName: 122, Event: 149, From: 151, host: 52, Max_Forwards: 154, Min_SE: 213, Proxy_Authenticate: 157, quoted_string: 40, Refer_To: 178, Replaces: 179, Session_Expires: 210, stun_URI: 217, To: 192, turn_URI: 223, uuid: 226, WWW_Authenticate: 209, challenge: 158, sipfrag: 230, Referred_By: 231 };
    let peg$startRuleIndex = 119;
    const peg$consts = [
      "\r\n",
      peg$literalExpectation("\r\n", false),
      /^[0-9]/,
      peg$classExpectation([["0", "9"]], false, false),
      /^[a-zA-Z]/,
      peg$classExpectation([["a", "z"], ["A", "Z"]], false, false),
      /^[0-9a-fA-F]/,
      peg$classExpectation([["0", "9"], ["a", "f"], ["A", "F"]], false, false),
      /^[\0-\xFF]/,
      peg$classExpectation([["\0", "\xFF"]], false, false),
      /^["]/,
      peg$classExpectation(['"'], false, false),
      " ",
      peg$literalExpectation(" ", false),
      "	",
      peg$literalExpectation("	", false),
      /^[a-zA-Z0-9]/,
      peg$classExpectation([["a", "z"], ["A", "Z"], ["0", "9"]], false, false),
      ";",
      peg$literalExpectation(";", false),
      "/",
      peg$literalExpectation("/", false),
      "?",
      peg$literalExpectation("?", false),
      ":",
      peg$literalExpectation(":", false),
      "@",
      peg$literalExpectation("@", false),
      "&",
      peg$literalExpectation("&", false),
      "=",
      peg$literalExpectation("=", false),
      "+",
      peg$literalExpectation("+", false),
      "$",
      peg$literalExpectation("$", false),
      ",",
      peg$literalExpectation(",", false),
      "-",
      peg$literalExpectation("-", false),
      "_",
      peg$literalExpectation("_", false),
      ".",
      peg$literalExpectation(".", false),
      "!",
      peg$literalExpectation("!", false),
      "~",
      peg$literalExpectation("~", false),
      "*",
      peg$literalExpectation("*", false),
      "'",
      peg$literalExpectation("'", false),
      "(",
      peg$literalExpectation("(", false),
      ")",
      peg$literalExpectation(")", false),
      "%",
      peg$literalExpectation("%", false),
      function() {
        return " ";
      },
      function() {
        return ":";
      },
      /^[!-~]/,
      peg$classExpectation([["!", "~"]], false, false),
      /^[\x80-\uFFFF]/,
      peg$classExpectation([["\x80", "\uFFFF"]], false, false),
      /^[\x80-\xBF]/,
      peg$classExpectation([["\x80", "\xBF"]], false, false),
      /^[a-f]/,
      peg$classExpectation([["a", "f"]], false, false),
      "`",
      peg$literalExpectation("`", false),
      "<",
      peg$literalExpectation("<", false),
      ">",
      peg$literalExpectation(">", false),
      "\\",
      peg$literalExpectation("\\", false),
      "[",
      peg$literalExpectation("[", false),
      "]",
      peg$literalExpectation("]", false),
      "{",
      peg$literalExpectation("{", false),
      "}",
      peg$literalExpectation("}", false),
      function() {
        return "*";
      },
      function() {
        return "/";
      },
      function() {
        return "=";
      },
      function() {
        return "(";
      },
      function() {
        return ")";
      },
      function() {
        return ">";
      },
      function() {
        return "<";
      },
      function() {
        return ",";
      },
      function() {
        return ";";
      },
      function() {
        return ":";
      },
      function() {
        return '"';
      },
      /^[!-']/,
      peg$classExpectation([["!", "'"]], false, false),
      /^[*-[]/,
      peg$classExpectation([["*", "["]], false, false),
      /^[\]-~]/,
      peg$classExpectation([["]", "~"]], false, false),
      function(contents) {
        return contents;
      },
      /^[#-[]/,
      peg$classExpectation([["#", "["]], false, false),
      /^[\0-\t]/,
      peg$classExpectation([["\0", "	"]], false, false),
      /^[\v-\f]/,
      peg$classExpectation([["\v", "\f"]], false, false),
      /^[\x0E-\x7F]/,
      peg$classExpectation([["", "\x7F"]], false, false),
      function() {
        options = options || { data: {} };
        options.data.uri = new URI(options.data.scheme, options.data.user, options.data.host, options.data.port);
        delete options.data.scheme;
        delete options.data.user;
        delete options.data.host;
        delete options.data.host_type;
        delete options.data.port;
      },
      function() {
        options = options || { data: {} };
        options.data.uri = new URI(options.data.scheme, options.data.user, options.data.host, options.data.port, options.data.uri_params, options.data.uri_headers);
        delete options.data.scheme;
        delete options.data.user;
        delete options.data.host;
        delete options.data.host_type;
        delete options.data.port;
        delete options.data.uri_params;
        if (options.startRule === "SIP_URI") {
          options.data = options.data.uri;
        }
      },
      "sips",
      peg$literalExpectation("sips", true),
      "sip",
      peg$literalExpectation("sip", true),
      function(uri_scheme) {
        options = options || { data: {} };
        options.data.scheme = uri_scheme;
      },
      function() {
        options = options || { data: {} };
        options.data.user = decodeURIComponent(text().slice(0, -1));
      },
      function() {
        options = options || { data: {} };
        options.data.password = text();
      },
      function() {
        options = options || { data: {} };
        options.data.host = text();
        return options.data.host;
      },
      function() {
        options = options || { data: {} };
        options.data.host_type = "domain";
        return text();
      },
      /^[a-zA-Z0-9_\-]/,
      peg$classExpectation([["a", "z"], ["A", "Z"], ["0", "9"], "_", "-"], false, false),
      /^[a-zA-Z0-9\-]/,
      peg$classExpectation([["a", "z"], ["A", "Z"], ["0", "9"], "-"], false, false),
      function() {
        options = options || { data: {} };
        options.data.host_type = "IPv6";
        return text();
      },
      "::",
      peg$literalExpectation("::", false),
      function() {
        options = options || { data: {} };
        options.data.host_type = "IPv6";
        return text();
      },
      function() {
        options = options || { data: {} };
        options.data.host_type = "IPv4";
        return text();
      },
      "25",
      peg$literalExpectation("25", false),
      /^[0-5]/,
      peg$classExpectation([["0", "5"]], false, false),
      "2",
      peg$literalExpectation("2", false),
      /^[0-4]/,
      peg$classExpectation([["0", "4"]], false, false),
      "1",
      peg$literalExpectation("1", false),
      /^[1-9]/,
      peg$classExpectation([["1", "9"]], false, false),
      function(port) {
        options = options || { data: {} };
        port = parseInt(port.join(""));
        options.data.port = port;
        return port;
      },
      "transport=",
      peg$literalExpectation("transport=", true),
      "udp",
      peg$literalExpectation("udp", true),
      "tcp",
      peg$literalExpectation("tcp", true),
      "sctp",
      peg$literalExpectation("sctp", true),
      "tls",
      peg$literalExpectation("tls", true),
      function(transport) {
        options = options || { data: {} };
        if (!options.data.uri_params)
          options.data.uri_params = {};
        options.data.uri_params["transport"] = transport.toLowerCase();
      },
      "user=",
      peg$literalExpectation("user=", true),
      "phone",
      peg$literalExpectation("phone", true),
      "ip",
      peg$literalExpectation("ip", true),
      function(user) {
        options = options || { data: {} };
        if (!options.data.uri_params)
          options.data.uri_params = {};
        options.data.uri_params["user"] = user.toLowerCase();
      },
      "method=",
      peg$literalExpectation("method=", true),
      function(method) {
        options = options || { data: {} };
        if (!options.data.uri_params)
          options.data.uri_params = {};
        options.data.uri_params["method"] = method;
      },
      "ttl=",
      peg$literalExpectation("ttl=", true),
      function(ttl) {
        options = options || { data: {} };
        if (!options.data.params)
          options.data.params = {};
        options.data.params["ttl"] = ttl;
      },
      "maddr=",
      peg$literalExpectation("maddr=", true),
      function(maddr) {
        options = options || { data: {} };
        if (!options.data.uri_params)
          options.data.uri_params = {};
        options.data.uri_params["maddr"] = maddr;
      },
      "lr",
      peg$literalExpectation("lr", true),
      function() {
        options = options || { data: {} };
        if (!options.data.uri_params)
          options.data.uri_params = {};
        options.data.uri_params["lr"] = void 0;
      },
      function(param, value) {
        options = options || { data: {} };
        if (!options.data.uri_params)
          options.data.uri_params = {};
        if (value === null) {
          value = void 0;
        } else {
          value = value[1];
        }
        options.data.uri_params[param.toLowerCase()] = value;
      },
      function(hname, hvalue) {
        hname = hname.join("").toLowerCase();
        hvalue = hvalue.join("");
        options = options || { data: {} };
        if (!options.data.uri_headers)
          options.data.uri_headers = {};
        if (!options.data.uri_headers[hname]) {
          options.data.uri_headers[hname] = [hvalue];
        } else {
          options.data.uri_headers[hname].push(hvalue);
        }
      },
      function() {
        options = options || { data: {} };
        if (options.startRule === "Refer_To") {
          options.data.uri = new URI(options.data.scheme, options.data.user, options.data.host, options.data.port, options.data.uri_params, options.data.uri_headers);
          delete options.data.scheme;
          delete options.data.user;
          delete options.data.host;
          delete options.data.host_type;
          delete options.data.port;
          delete options.data.uri_params;
        }
      },
      "//",
      peg$literalExpectation("//", false),
      function() {
        options = options || { data: {} };
        options.data.scheme = text();
      },
      peg$literalExpectation("SIP", true),
      function() {
        options = options || { data: {} };
        options.data.sip_version = text();
      },
      "INVITE",
      peg$literalExpectation("INVITE", false),
      "ACK",
      peg$literalExpectation("ACK", false),
      "VXACH",
      peg$literalExpectation("VXACH", false),
      "OPTIONS",
      peg$literalExpectation("OPTIONS", false),
      "BYE",
      peg$literalExpectation("BYE", false),
      "CANCEL",
      peg$literalExpectation("CANCEL", false),
      "REGISTER",
      peg$literalExpectation("REGISTER", false),
      "SUBSCRIBE",
      peg$literalExpectation("SUBSCRIBE", false),
      "NOTIFY",
      peg$literalExpectation("NOTIFY", false),
      "REFER",
      peg$literalExpectation("REFER", false),
      "PUBLISH",
      peg$literalExpectation("PUBLISH", false),
      function() {
        options = options || { data: {} };
        options.data.method = text();
        return options.data.method;
      },
      function(status_code) {
        options = options || { data: {} };
        options.data.status_code = parseInt(status_code.join(""));
      },
      function() {
        options = options || { data: {} };
        options.data.reason_phrase = text();
      },
      function() {
        options = options || { data: {} };
        options.data = text();
      },
      function() {
        var idx, length;
        options = options || { data: {} };
        length = options.data.multi_header.length;
        for (idx = 0; idx < length; idx++) {
          if (options.data.multi_header[idx].parsed === null) {
            options.data = null;
            break;
          }
        }
        if (options.data !== null) {
          options.data = options.data.multi_header;
        } else {
          options.data = -1;
        }
      },
      function() {
        var header;
        options = options || { data: {} };
        if (!options.data.multi_header)
          options.data.multi_header = [];
        try {
          header = new NameAddrHeader(options.data.uri, options.data.displayName, options.data.params);
          delete options.data.uri;
          delete options.data.displayName;
          delete options.data.params;
        } catch (e) {
          header = null;
        }
        options.data.multi_header.push({
          "position": peg$currPos,
          "offset": location().start.offset,
          "parsed": header
        });
      },
      function(displayName) {
        displayName = text().trim();
        if (displayName[0] === '"') {
          displayName = displayName.substring(1, displayName.length - 1);
        }
        options = options || { data: {} };
        options.data.displayName = displayName;
      },
      "q",
      peg$literalExpectation("q", true),
      function(q) {
        options = options || { data: {} };
        if (!options.data.params)
          options.data.params = {};
        options.data.params["q"] = q;
      },
      "expires",
      peg$literalExpectation("expires", true),
      function(expires) {
        options = options || { data: {} };
        if (!options.data.params)
          options.data.params = {};
        options.data.params["expires"] = expires;
      },
      function(delta_seconds) {
        return parseInt(delta_seconds.join(""));
      },
      "0",
      peg$literalExpectation("0", false),
      function() {
        return parseFloat(text());
      },
      function(param, value) {
        options = options || { data: {} };
        if (!options.data.params)
          options.data.params = {};
        if (value === null) {
          value = void 0;
        } else {
          value = value[1];
        }
        options.data.params[param.toLowerCase()] = value;
      },
      "render",
      peg$literalExpectation("render", true),
      "session",
      peg$literalExpectation("session", true),
      "icon",
      peg$literalExpectation("icon", true),
      "alert",
      peg$literalExpectation("alert", true),
      function() {
        options = options || { data: {} };
        if (options.startRule === "Content_Disposition") {
          options.data.type = text().toLowerCase();
        }
      },
      "handling",
      peg$literalExpectation("handling", true),
      "optional",
      peg$literalExpectation("optional", true),
      "required",
      peg$literalExpectation("required", true),
      function(length) {
        options = options || { data: {} };
        options.data = parseInt(length.join(""));
      },
      function() {
        options = options || { data: {} };
        options.data = text();
      },
      "text",
      peg$literalExpectation("text", true),
      "image",
      peg$literalExpectation("image", true),
      "audio",
      peg$literalExpectation("audio", true),
      "video",
      peg$literalExpectation("video", true),
      "application",
      peg$literalExpectation("application", true),
      "message",
      peg$literalExpectation("message", true),
      "multipart",
      peg$literalExpectation("multipart", true),
      "x-",
      peg$literalExpectation("x-", true),
      function(cseq_value) {
        options = options || { data: {} };
        options.data.value = parseInt(cseq_value.join(""));
      },
      function(expires) {
        options = options || { data: {} };
        options.data = expires;
      },
      function(event_type) {
        options = options || { data: {} };
        options.data.event = event_type.toLowerCase();
      },
      function() {
        options = options || { data: {} };
        var tag = options.data.tag;
        options.data = new NameAddrHeader(options.data.uri, options.data.displayName, options.data.params);
        if (tag) {
          options.data.setParam("tag", tag);
        }
      },
      "tag",
      peg$literalExpectation("tag", true),
      function(tag) {
        options = options || { data: {} };
        options.data.tag = tag;
      },
      function(forwards) {
        options = options || { data: {} };
        options.data = parseInt(forwards.join(""));
      },
      function(min_expires) {
        options = options || { data: {} };
        options.data = min_expires;
      },
      function() {
        options = options || { data: {} };
        options.data = new NameAddrHeader(options.data.uri, options.data.displayName, options.data.params);
      },
      "digest",
      peg$literalExpectation("Digest", true),
      "realm",
      peg$literalExpectation("realm", true),
      function(realm) {
        options = options || { data: {} };
        options.data.realm = realm;
      },
      "domain",
      peg$literalExpectation("domain", true),
      "nonce",
      peg$literalExpectation("nonce", true),
      function(nonce) {
        options = options || { data: {} };
        options.data.nonce = nonce;
      },
      "opaque",
      peg$literalExpectation("opaque", true),
      function(opaque) {
        options = options || { data: {} };
        options.data.opaque = opaque;
      },
      "stale",
      peg$literalExpectation("stale", true),
      "true",
      peg$literalExpectation("true", true),
      function() {
        options = options || { data: {} };
        options.data.stale = true;
      },
      "false",
      peg$literalExpectation("false", true),
      function() {
        options = options || { data: {} };
        options.data.stale = false;
      },
      "algorithm",
      peg$literalExpectation("algorithm", true),
      "md5",
      peg$literalExpectation("MD5", true),
      "md5-sess",
      peg$literalExpectation("MD5-sess", true),
      function(algorithm) {
        options = options || { data: {} };
        options.data.algorithm = algorithm.toUpperCase();
      },
      "qop",
      peg$literalExpectation("qop", true),
      "auth-int",
      peg$literalExpectation("auth-int", true),
      "auth",
      peg$literalExpectation("auth", true),
      function(qop_value) {
        options = options || { data: {} };
        options.data.qop || (options.data.qop = []);
        options.data.qop.push(qop_value.toLowerCase());
      },
      function(rack_value) {
        options = options || { data: {} };
        options.data.value = parseInt(rack_value.join(""));
      },
      function() {
        var idx, length;
        options = options || { data: {} };
        length = options.data.multi_header.length;
        for (idx = 0; idx < length; idx++) {
          if (options.data.multi_header[idx].parsed === null) {
            options.data = null;
            break;
          }
        }
        if (options.data !== null) {
          options.data = options.data.multi_header;
        } else {
          options.data = -1;
        }
      },
      function() {
        var header;
        options = options || { data: {} };
        if (!options.data.multi_header)
          options.data.multi_header = [];
        try {
          header = new NameAddrHeader(options.data.uri, options.data.displayName, options.data.params);
          delete options.data.uri;
          delete options.data.displayName;
          delete options.data.params;
        } catch (e) {
          header = null;
        }
        options.data.multi_header.push({
          "position": peg$currPos,
          "offset": location().start.offset,
          "parsed": header
        });
      },
      function() {
        options = options || { data: {} };
        options.data = new NameAddrHeader(options.data.uri, options.data.displayName, options.data.params);
      },
      function() {
        options = options || { data: {} };
        if (!(options.data.replaces_from_tag && options.data.replaces_to_tag)) {
          options.data = -1;
        }
      },
      function() {
        options = options || { data: {} };
        options.data = {
          call_id: options.data
        };
      },
      "from-tag",
      peg$literalExpectation("from-tag", true),
      function(from_tag) {
        options = options || { data: {} };
        options.data.replaces_from_tag = from_tag;
      },
      "to-tag",
      peg$literalExpectation("to-tag", true),
      function(to_tag) {
        options = options || { data: {} };
        options.data.replaces_to_tag = to_tag;
      },
      "early-only",
      peg$literalExpectation("early-only", true),
      function() {
        options = options || { data: {} };
        options.data.early_only = true;
      },
      function(head, r) {
        return r;
      },
      function(head, tail) {
        return list(head, tail);
      },
      function(value) {
        options = options || { data: {} };
        if (options.startRule === "Require") {
          options.data = value || [];
        }
      },
      function(rseq_value) {
        options = options || { data: {} };
        options.data.value = parseInt(rseq_value.join(""));
      },
      "active",
      peg$literalExpectation("active", true),
      "pending",
      peg$literalExpectation("pending", true),
      "terminated",
      peg$literalExpectation("terminated", true),
      function() {
        options = options || { data: {} };
        options.data.state = text();
      },
      "reason",
      peg$literalExpectation("reason", true),
      function(reason) {
        options = options || { data: {} };
        if (typeof reason !== "undefined")
          options.data.reason = reason;
      },
      function(expires) {
        options = options || { data: {} };
        if (typeof expires !== "undefined")
          options.data.expires = expires;
      },
      "retry_after",
      peg$literalExpectation("retry_after", true),
      function(retry_after) {
        options = options || { data: {} };
        if (typeof retry_after !== "undefined")
          options.data.retry_after = retry_after;
      },
      "deactivated",
      peg$literalExpectation("deactivated", true),
      "probation",
      peg$literalExpectation("probation", true),
      "rejected",
      peg$literalExpectation("rejected", true),
      "timeout",
      peg$literalExpectation("timeout", true),
      "giveup",
      peg$literalExpectation("giveup", true),
      "noresource",
      peg$literalExpectation("noresource", true),
      "invariant",
      peg$literalExpectation("invariant", true),
      function(value) {
        options = options || { data: {} };
        if (options.startRule === "Supported") {
          options.data = value || [];
        }
      },
      function() {
        options = options || { data: {} };
        var tag = options.data.tag;
        options.data = new NameAddrHeader(options.data.uri, options.data.displayName, options.data.params);
        if (tag) {
          options.data.setParam("tag", tag);
        }
      },
      "ttl",
      peg$literalExpectation("ttl", true),
      function(via_ttl_value) {
        options = options || { data: {} };
        options.data.ttl = via_ttl_value;
      },
      "maddr",
      peg$literalExpectation("maddr", true),
      function(via_maddr) {
        options = options || { data: {} };
        options.data.maddr = via_maddr;
      },
      "received",
      peg$literalExpectation("received", true),
      function(via_received) {
        options = options || { data: {} };
        options.data.received = via_received;
      },
      "branch",
      peg$literalExpectation("branch", true),
      function(via_branch) {
        options = options || { data: {} };
        options.data.branch = via_branch;
      },
      "rport",
      peg$literalExpectation("rport", true),
      function(response_port) {
        options = options || { data: {} };
        if (typeof response_port !== "undefined")
          options.data.rport = response_port.join("");
      },
      function(via_protocol) {
        options = options || { data: {} };
        options.data.protocol = via_protocol;
      },
      peg$literalExpectation("UDP", true),
      peg$literalExpectation("TCP", true),
      peg$literalExpectation("TLS", true),
      peg$literalExpectation("SCTP", true),
      function(via_transport) {
        options = options || { data: {} };
        options.data.transport = via_transport;
      },
      function() {
        options = options || { data: {} };
        options.data.host = text();
      },
      function(via_sent_by_port) {
        options = options || { data: {} };
        options.data.port = parseInt(via_sent_by_port.join(""));
      },
      function(ttl) {
        return parseInt(ttl.join(""));
      },
      function(deltaSeconds) {
        options = options || { data: {} };
        if (options.startRule === "Session_Expires") {
          options.data.deltaSeconds = deltaSeconds;
        }
      },
      "refresher",
      peg$literalExpectation("refresher", false),
      "uas",
      peg$literalExpectation("uas", false),
      "uac",
      peg$literalExpectation("uac", false),
      function(endpoint) {
        options = options || { data: {} };
        if (options.startRule === "Session_Expires") {
          options.data.refresher = endpoint;
        }
      },
      function(deltaSeconds) {
        options = options || { data: {} };
        if (options.startRule === "Min_SE") {
          options.data = deltaSeconds;
        }
      },
      "stuns",
      peg$literalExpectation("stuns", true),
      "stun",
      peg$literalExpectation("stun", true),
      function(scheme) {
        options = options || { data: {} };
        options.data.scheme = scheme;
      },
      function(host) {
        options = options || { data: {} };
        options.data.host = host;
      },
      "?transport=",
      peg$literalExpectation("?transport=", false),
      "turns",
      peg$literalExpectation("turns", true),
      "turn",
      peg$literalExpectation("turn", true),
      function(transport) {
        options = options || { data: {} };
        options.data.transport = transport;
      },
      function() {
        options = options || { data: {} };
        options.data = text();
      },
      "Referred-By",
      peg$literalExpectation("Referred-By", false),
      "b",
      peg$literalExpectation("b", false),
      "cid",
      peg$literalExpectation("cid", false)
    ];
    const peg$bytecode = [
      peg$decode('2 ""6 7!'),
      peg$decode('4"""5!7#'),
      peg$decode('4$""5!7%'),
      peg$decode(`4&""5!7'`),
      peg$decode(";'.# &;("),
      peg$decode('4(""5!7)'),
      peg$decode('4*""5!7+'),
      peg$decode('2,""6,7-'),
      peg$decode('2.""6.7/'),
      peg$decode('40""5!71'),
      peg$decode('22""6273.\x89 &24""6475.} &26""6677.q &28""6879.e &2:""6:7;.Y &2<""6<7=.M &2>""6>7?.A &2@""6@7A.5 &2B""6B7C.) &2D""6D7E'),
      peg$decode(";).# &;,"),
      peg$decode('2F""6F7G.} &2H""6H7I.q &2J""6J7K.e &2L""6L7M.Y &2N""6N7O.M &2P""6P7Q.A &2R""6R7S.5 &2T""6T7U.) &2V""6V7W'),
      peg$decode(`%%2X""6X7Y/5#;#/,$;#/#$+#)(#'#("'#&'#/"!&,)`),
      peg$decode(`%%$;$0#*;$&/,#; /#$+")("'#&'#." &"/=#$;$/&#0#*;$&&&#/'$8":Z" )("'#&'#`),
      peg$decode(';.." &"'),
      peg$decode(`%$;'.# &;(0)*;'.# &;(&/?#28""6879/0$;//'$8#:[# )(#'#("'#&'#`),
      peg$decode(`%%$;2/&#0#*;2&&&#/g#$%$;.0#*;.&/,#;2/#$+")("'#&'#0=*%$;.0#*;.&/,#;2/#$+")("'#&'#&/#$+")("'#&'#/"!&,)`),
      peg$decode('4\\""5!7].# &;3'),
      peg$decode('4^""5!7_'),
      peg$decode('4`""5!7a'),
      peg$decode(';!.) &4b""5!7c'),
      peg$decode('%$;).\x95 &2F""6F7G.\x89 &2J""6J7K.} &2L""6L7M.q &2X""6X7Y.e &2P""6P7Q.Y &2H""6H7I.M &2@""6@7A.A &2d""6d7e.5 &2R""6R7S.) &2N""6N7O/\x9E#0\x9B*;).\x95 &2F""6F7G.\x89 &2J""6J7K.} &2L""6L7M.q &2X""6X7Y.e &2P""6P7Q.Y &2H""6H7I.M &2@""6@7A.A &2d""6d7e.5 &2R""6R7S.) &2N""6N7O&&&#/"!&,)'),
      peg$decode('%$;).\x89 &2F""6F7G.} &2L""6L7M.q &2X""6X7Y.e &2P""6P7Q.Y &2H""6H7I.M &2@""6@7A.A &2d""6d7e.5 &2R""6R7S.) &2N""6N7O/\x92#0\x8F*;).\x89 &2F""6F7G.} &2L""6L7M.q &2X""6X7Y.e &2P""6P7Q.Y &2H""6H7I.M &2@""6@7A.A &2d""6d7e.5 &2R""6R7S.) &2N""6N7O&&&#/"!&,)'),
      peg$decode(`2T""6T7U.\xE3 &2V""6V7W.\xD7 &2f""6f7g.\xCB &2h""6h7i.\xBF &2:""6:7;.\xB3 &2D""6D7E.\xA7 &22""6273.\x9B &28""6879.\x8F &2j""6j7k.\x83 &;&.} &24""6475.q &2l""6l7m.e &2n""6n7o.Y &26""6677.M &2>""6>7?.A &2p""6p7q.5 &2r""6r7s.) &;'.# &;(`),
      peg$decode('%$;).\u012B &2F""6F7G.\u011F &2J""6J7K.\u0113 &2L""6L7M.\u0107 &2X""6X7Y.\xFB &2P""6P7Q.\xEF &2H""6H7I.\xE3 &2@""6@7A.\xD7 &2d""6d7e.\xCB &2R""6R7S.\xBF &2N""6N7O.\xB3 &2T""6T7U.\xA7 &2V""6V7W.\x9B &2f""6f7g.\x8F &2h""6h7i.\x83 &28""6879.w &2j""6j7k.k &;&.e &24""6475.Y &2l""6l7m.M &2n""6n7o.A &26""6677.5 &2p""6p7q.) &2r""6r7s/\u0134#0\u0131*;).\u012B &2F""6F7G.\u011F &2J""6J7K.\u0113 &2L""6L7M.\u0107 &2X""6X7Y.\xFB &2P""6P7Q.\xEF &2H""6H7I.\xE3 &2@""6@7A.\xD7 &2d""6d7e.\xCB &2R""6R7S.\xBF &2N""6N7O.\xB3 &2T""6T7U.\xA7 &2V""6V7W.\x9B &2f""6f7g.\x8F &2h""6h7i.\x83 &28""6879.w &2j""6j7k.k &;&.e &24""6475.Y &2l""6l7m.M &2n""6n7o.A &26""6677.5 &2p""6p7q.) &2r""6r7s&&&#/"!&,)'),
      peg$decode(`%;//?#2P""6P7Q/0$;//'$8#:t# )(#'#("'#&'#`),
      peg$decode(`%;//?#24""6475/0$;//'$8#:u# )(#'#("'#&'#`),
      peg$decode(`%;//?#2>""6>7?/0$;//'$8#:v# )(#'#("'#&'#`),
      peg$decode(`%;//?#2T""6T7U/0$;//'$8#:w# )(#'#("'#&'#`),
      peg$decode(`%;//?#2V""6V7W/0$;//'$8#:x# )(#'#("'#&'#`),
      peg$decode(`%2h""6h7i/0#;//'$8":y" )("'#&'#`),
      peg$decode(`%;//6#2f""6f7g/'$8":z" )("'#&'#`),
      peg$decode(`%;//?#2D""6D7E/0$;//'$8#:{# )(#'#("'#&'#`),
      peg$decode(`%;//?#22""6273/0$;//'$8#:|# )(#'#("'#&'#`),
      peg$decode(`%;//?#28""6879/0$;//'$8#:}# )(#'#("'#&'#`),
      peg$decode(`%;//0#;&/'$8":~" )("'#&'#`),
      peg$decode(`%;&/0#;//'$8":~" )("'#&'#`),
      peg$decode(`%;=/T#$;G.) &;K.# &;F0/*;G.) &;K.# &;F&/,$;>/#$+#)(#'#("'#&'#`),
      peg$decode('4\x7F""5!7\x80.A &4\x81""5!7\x82.5 &4\x83""5!7\x84.) &;3.# &;.'),
      peg$decode(`%%;//Q#;&/H$$;J.# &;K0)*;J.# &;K&/,$;&/#$+$)($'#(#'#("'#&'#/"!&,)`),
      peg$decode(`%;//]#;&/T$%$;J.# &;K0)*;J.# &;K&/"!&,)/1$;&/($8$:\x85$!!)($'#(#'#("'#&'#`),
      peg$decode(';..G &2L""6L7M.; &4\x86""5!7\x87./ &4\x83""5!7\x84.# &;3'),
      peg$decode(`%2j""6j7k/J#4\x88""5!7\x89.5 &4\x8A""5!7\x8B.) &4\x8C""5!7\x8D/#$+")("'#&'#`),
      peg$decode(`%;N/M#28""6879/>$;O." &"/0$;S/'$8$:\x8E$ )($'#(#'#("'#&'#`),
      peg$decode(`%;N/d#28""6879/U$;O." &"/G$;S/>$;_/5$;l." &"/'$8&:\x8F& )(&'#(%'#($'#(#'#("'#&'#`),
      peg$decode(`%3\x90""5$7\x91.) &3\x92""5#7\x93/' 8!:\x94!! )`),
      peg$decode(`%;P/]#%28""6879/,#;R/#$+")("'#&'#." &"/6$2:""6:7;/'$8#:\x95# )(#'#("'#&'#`),
      peg$decode("$;+.) &;-.# &;Q/2#0/*;+.) &;-.# &;Q&&&#"),
      peg$decode('2<""6<7=.q &2>""6>7?.e &2@""6@7A.Y &2B""6B7C.M &2D""6D7E.A &22""6273.5 &26""6677.) &24""6475'),
      peg$decode('%$;+._ &;-.Y &2<""6<7=.M &2>""6>7?.A &2@""6@7A.5 &2B""6B7C.) &2D""6D7E0e*;+._ &;-.Y &2<""6<7=.M &2>""6>7?.A &2@""6@7A.5 &2B""6B7C.) &2D""6D7E&/& 8!:\x96! )'),
      peg$decode(`%;T/J#%28""6879/,#;^/#$+")("'#&'#." &"/#$+")("'#&'#`),
      peg$decode("%;U.) &;\\.# &;X/& 8!:\x97! )"),
      peg$decode(`%$%;V/2#2J""6J7K/#$+")("'#&'#0<*%;V/2#2J""6J7K/#$+")("'#&'#&/D#;W/;$2J""6J7K." &"/'$8#:\x98# )(#'#("'#&'#`),
      peg$decode('$4\x99""5!7\x9A/,#0)*4\x99""5!7\x9A&&&#'),
      peg$decode(`%4$""5!7%/?#$4\x9B""5!7\x9C0)*4\x9B""5!7\x9C&/#$+")("'#&'#`),
      peg$decode(`%2l""6l7m/?#;Y/6$2n""6n7o/'$8#:\x9D# )(#'#("'#&'#`),
      peg$decode(`%%;Z/\xB3#28""6879/\xA4$;Z/\x9B$28""6879/\x8C$;Z/\x83$28""6879/t$;Z/k$28""6879/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+-)(-'#(,'#(+'#(*'#()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u0790 &%2\x9E""6\x9E7\x9F/\xA4#;Z/\x9B$28""6879/\x8C$;Z/\x83$28""6879/t$;Z/k$28""6879/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+,)(,'#(+'#(*'#()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u06F9 &%2\x9E""6\x9E7\x9F/\x8C#;Z/\x83$28""6879/t$;Z/k$28""6879/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+*)(*'#()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u067A &%2\x9E""6\x9E7\x9F/t#;Z/k$28""6879/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+()(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u0613 &%2\x9E""6\x9E7\x9F/\\#;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+&)(&'#(%'#($'#(#'#("'#&'#.\u05C4 &%2\x9E""6\x9E7\x9F/D#;Z/;$28""6879/,$;[/#$+$)($'#(#'#("'#&'#.\u058D &%2\x9E""6\x9E7\x9F/,#;[/#$+")("'#&'#.\u056E &%2\x9E""6\x9E7\x9F/,#;Z/#$+")("'#&'#.\u054F &%;Z/\x9B#2\x9E""6\x9E7\x9F/\x8C$;Z/\x83$28""6879/t$;Z/k$28""6879/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$++)(+'#(*'#()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u04C7 &%;Z/\xAA#%28""6879/,#;Z/#$+")("'#&'#." &"/\x83$2\x9E""6\x9E7\x9F/t$;Z/k$28""6879/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+*)(*'#()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u0430 &%;Z/\xB9#%28""6879/,#;Z/#$+")("'#&'#." &"/\x92$%28""6879/,#;Z/#$+")("'#&'#." &"/k$2\x9E""6\x9E7\x9F/\\$;Z/S$28""6879/D$;Z/;$28""6879/,$;[/#$+))()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u038A &%;Z/\xC8#%28""6879/,#;Z/#$+")("'#&'#." &"/\xA1$%28""6879/,#;Z/#$+")("'#&'#." &"/z$%28""6879/,#;Z/#$+")("'#&'#." &"/S$2\x9E""6\x9E7\x9F/D$;Z/;$28""6879/,$;[/#$+()(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u02D5 &%;Z/\xD7#%28""6879/,#;Z/#$+")("'#&'#." &"/\xB0$%28""6879/,#;Z/#$+")("'#&'#." &"/\x89$%28""6879/,#;Z/#$+")("'#&'#." &"/b$%28""6879/,#;Z/#$+")("'#&'#." &"/;$2\x9E""6\x9E7\x9F/,$;[/#$+')(''#(&'#(%'#($'#(#'#("'#&'#.\u0211 &%;Z/\xFE#%28""6879/,#;Z/#$+")("'#&'#." &"/\xD7$%28""6879/,#;Z/#$+")("'#&'#." &"/\xB0$%28""6879/,#;Z/#$+")("'#&'#." &"/\x89$%28""6879/,#;Z/#$+")("'#&'#." &"/b$%28""6879/,#;Z/#$+")("'#&'#." &"/;$2\x9E""6\x9E7\x9F/,$;Z/#$+()(('#(''#(&'#(%'#($'#(#'#("'#&'#.\u0126 &%;Z/\u011C#%28""6879/,#;Z/#$+")("'#&'#." &"/\xF5$%28""6879/,#;Z/#$+")("'#&'#." &"/\xCE$%28""6879/,#;Z/#$+")("'#&'#." &"/\xA7$%28""6879/,#;Z/#$+")("'#&'#." &"/\x80$%28""6879/,#;Z/#$+")("'#&'#." &"/Y$%28""6879/,#;Z/#$+")("'#&'#." &"/2$2\x9E""6\x9E7\x9F/#$+()(('#(''#(&'#(%'#($'#(#'#("'#&'#/& 8!:\xA0! )`),
      peg$decode(`%;#/M#;#." &"/?$;#." &"/1$;#." &"/#$+$)($'#(#'#("'#&'#`),
      peg$decode(`%;Z/;#28""6879/,$;Z/#$+#)(#'#("'#&'#.# &;\\`),
      peg$decode(`%;]/o#2J""6J7K/\`$;]/W$2J""6J7K/H$;]/?$2J""6J7K/0$;]/'$8':\xA1' )(''#(&'#(%'#($'#(#'#("'#&'#`),
      peg$decode(`%2\xA2""6\xA27\xA3/2#4\xA4""5!7\xA5/#$+")("'#&'#.\x98 &%2\xA6""6\xA67\xA7/;#4\xA8""5!7\xA9/,$;!/#$+#)(#'#("'#&'#.j &%2\xAA""6\xAA7\xAB/5#;!/,$;!/#$+#)(#'#("'#&'#.B &%4\xAC""5!7\xAD/,#;!/#$+")("'#&'#.# &;!`),
      peg$decode(`%%;!." &"/[#;!." &"/M$;!." &"/?$;!." &"/1$;!." &"/#$+%)(%'#($'#(#'#("'#&'#/' 8!:\xAE!! )`),
      peg$decode(`$%22""6273/,#;\`/#$+")("'#&'#0<*%22""6273/,#;\`/#$+")("'#&'#&`),
      peg$decode(";a.A &;b.; &;c.5 &;d./ &;e.) &;f.# &;g"),
      peg$decode(`%3\xAF""5*7\xB0/a#3\xB1""5#7\xB2.G &3\xB3""5#7\xB4.; &3\xB5""5$7\xB6./ &3\xB7""5#7\xB8.# &;6/($8":\xB9"! )("'#&'#`),
      peg$decode(`%3\xBA""5%7\xBB/I#3\xBC""5%7\xBD./ &3\xBE""5"7\xBF.# &;6/($8":\xC0"! )("'#&'#`),
      peg$decode(`%3\xC1""5'7\xC2/1#;\x90/($8":\xC3"! )("'#&'#`),
      peg$decode(`%3\xC4""5$7\xC5/1#;\xF0/($8":\xC6"! )("'#&'#`),
      peg$decode(`%3\xC7""5&7\xC8/1#;T/($8":\xC9"! )("'#&'#`),
      peg$decode(`%3\xCA""5"7\xCB/N#%2>""6>7?/,#;6/#$+")("'#&'#." &"/'$8":\xCC" )("'#&'#`),
      peg$decode(`%;h/P#%2>""6>7?/,#;i/#$+")("'#&'#." &"/)$8":\xCD""! )("'#&'#`),
      peg$decode('%$;j/&#0#*;j&&&#/"!&,)'),
      peg$decode('%$;j/&#0#*;j&&&#/"!&,)'),
      peg$decode(";k.) &;+.# &;-"),
      peg$decode('2l""6l7m.e &2n""6n7o.Y &24""6475.M &28""6879.A &2<""6<7=.5 &2@""6@7A.) &2B""6B7C'),
      peg$decode(`%26""6677/n#;m/e$$%2<""6<7=/,#;m/#$+")("'#&'#0<*%2<""6<7=/,#;m/#$+")("'#&'#&/#$+#)(#'#("'#&'#`),
      peg$decode(`%;n/A#2>""6>7?/2$;o/)$8#:\xCE#"" )(#'#("'#&'#`),
      peg$decode("$;p.) &;+.# &;-/2#0/*;p.) &;+.# &;-&&&#"),
      peg$decode("$;p.) &;+.# &;-0/*;p.) &;+.# &;-&"),
      peg$decode('2l""6l7m.e &2n""6n7o.Y &24""6475.M &26""6677.A &28""6879.5 &2@""6@7A.) &2B""6B7C'),
      peg$decode(";\x91.# &;r"),
      peg$decode(`%;\x90/G#;'/>$;s/5$;'/,$;\x84/#$+%)(%'#($'#(#'#("'#&'#`),
      peg$decode(";M.# &;t"),
      peg$decode(`%;\x7F/E#28""6879/6$;u.# &;x/'$8#:\xCF# )(#'#("'#&'#`),
      peg$decode(`%;v.# &;w/J#%26""6677/,#;\x83/#$+")("'#&'#." &"/#$+")("'#&'#`),
      peg$decode(`%2\xD0""6\xD07\xD1/:#;\x80/1$;w." &"/#$+#)(#'#("'#&'#`),
      peg$decode(`%24""6475/,#;{/#$+")("'#&'#`),
      peg$decode(`%;z/3#$;y0#*;y&/#$+")("'#&'#`),
      peg$decode(";*.) &;+.# &;-"),
      peg$decode(';+.\x8F &;-.\x89 &22""6273.} &26""6677.q &28""6879.e &2:""6:7;.Y &2<""6<7=.M &2>""6>7?.A &2@""6@7A.5 &2B""6B7C.) &2D""6D7E'),
      peg$decode(`%;|/e#$%24""6475/,#;|/#$+")("'#&'#0<*%24""6475/,#;|/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%$;~0#*;~&/e#$%22""6273/,#;}/#$+")("'#&'#0<*%22""6273/,#;}/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode("$;~0#*;~&"),
      peg$decode(';+.w &;-.q &28""6879.e &2:""6:7;.Y &2<""6<7=.M &2>""6>7?.A &2@""6@7A.5 &2B""6B7C.) &2D""6D7E'),
      peg$decode(`%%;"/\x87#$;".G &;!.A &2@""6@7A.5 &2F""6F7G.) &2J""6J7K0M*;".G &;!.A &2@""6@7A.5 &2F""6F7G.) &2J""6J7K&/#$+")("'#&'#/& 8!:\xD2! )`),
      peg$decode(";\x81.# &;\x82"),
      peg$decode(`%%;O/2#2:""6:7;/#$+")("'#&'#." &"/,#;S/#$+")("'#&'#." &"`),
      peg$decode('$;+.\x83 &;-.} &2B""6B7C.q &2D""6D7E.e &22""6273.Y &28""6879.M &2:""6:7;.A &2<""6<7=.5 &2>""6>7?.) &2@""6@7A/\x8C#0\x89*;+.\x83 &;-.} &2B""6B7C.q &2D""6D7E.e &22""6273.Y &28""6879.M &2:""6:7;.A &2<""6<7=.5 &2>""6>7?.) &2@""6@7A&&&#'),
      peg$decode("$;y0#*;y&"),
      peg$decode(`%3\x92""5#7\xD3/q#24""6475/b$$;!/&#0#*;!&&&#/L$2J""6J7K/=$$;!/&#0#*;!&&&#/'$8%:\xD4% )(%'#($'#(#'#("'#&'#`),
      peg$decode('2\xD5""6\xD57\xD6'),
      peg$decode('2\xD7""6\xD77\xD8'),
      peg$decode('2\xD9""6\xD97\xDA'),
      peg$decode('2\xDB""6\xDB7\xDC'),
      peg$decode('2\xDD""6\xDD7\xDE'),
      peg$decode('2\xDF""6\xDF7\xE0'),
      peg$decode('2\xE1""6\xE17\xE2'),
      peg$decode('2\xE3""6\xE37\xE4'),
      peg$decode('2\xE5""6\xE57\xE6'),
      peg$decode('2\xE7""6\xE77\xE8'),
      peg$decode('2\xE9""6\xE97\xEA'),
      peg$decode("%;\x85.Y &;\x86.S &;\x88.M &;\x89.G &;\x8A.A &;\x8B.; &;\x8C.5 &;\x8F./ &;\x8D.) &;\x8E.# &;6/& 8!:\xEB! )"),
      peg$decode(`%;\x84/G#;'/>$;\x92/5$;'/,$;\x94/#$+%)(%'#($'#(#'#("'#&'#`),
      peg$decode("%;\x93/' 8!:\xEC!! )"),
      peg$decode(`%;!/5#;!/,$;!/#$+#)(#'#("'#&'#`),
      peg$decode("%$;*.A &;+.; &;-.5 &;3./ &;4.) &;'.# &;(0G*;*.A &;+.; &;-.5 &;3./ &;4.) &;'.# &;(&/& 8!:\xED! )"),
      peg$decode(`%;\xB6/Y#$%;A/,#;\xB6/#$+")("'#&'#06*%;A/,#;\xB6/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%;9/N#%2:""6:7;/,#;9/#$+")("'#&'#." &"/'$8":\xEE" )("'#&'#`),
      peg$decode(`%;:.c &%;\x98/Y#$%;A/,#;\x98/#$+")("'#&'#06*%;A/,#;\x98/#$+")("'#&'#&/#$+")("'#&'#/& 8!:\xEF! )`),
      peg$decode(`%;L.# &;\x99/]#$%;B/,#;\x9B/#$+")("'#&'#06*%;B/,#;\x9B/#$+")("'#&'#&/'$8":\xF0" )("'#&'#`),
      peg$decode(`%;\x9A." &"/>#;@/5$;M/,$;?/#$+$)($'#(#'#("'#&'#`),
      peg$decode(`%%;6/Y#$%;./,#;6/#$+")("'#&'#06*%;./,#;6/#$+")("'#&'#&/#$+")("'#&'#.# &;H/' 8!:\xF1!! )`),
      peg$decode(";\x9C.) &;\x9D.# &;\xA0"),
      peg$decode(`%3\xF2""5!7\xF3/:#;</1$;\x9F/($8#:\xF4#! )(#'#("'#&'#`),
      peg$decode(`%3\xF5""5'7\xF6/:#;</1$;\x9E/($8#:\xF7#! )(#'#("'#&'#`),
      peg$decode("%$;!/&#0#*;!&&&#/' 8!:\xF8!! )"),
      peg$decode(`%2\xF9""6\xF97\xFA/o#%2J""6J7K/M#;!." &"/?$;!." &"/1$;!." &"/#$+$)($'#(#'#("'#&'#." &"/'$8":\xFB" )("'#&'#`),
      peg$decode(`%;6/J#%;</,#;\xA1/#$+")("'#&'#." &"/)$8":\xFC""! )("'#&'#`),
      peg$decode(";6.) &;T.# &;H"),
      peg$decode(`%;\xA3/Y#$%;B/,#;\xA4/#$+")("'#&'#06*%;B/,#;\xA4/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%3\xFD""5&7\xFE.G &3\xFF""5'7\u0100.; &3\u0101""5$7\u0102./ &3\u0103""5%7\u0104.# &;6/& 8!:\u0105! )`),
      peg$decode(";\xA5.# &;\xA0"),
      peg$decode(`%3\u0106""5(7\u0107/M#;</D$3\u0108""5(7\u0109./ &3\u010A""5(7\u010B.# &;6/#$+#)(#'#("'#&'#`),
      peg$decode(`%;6/Y#$%;A/,#;6/#$+")("'#&'#06*%;A/,#;6/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode("%$;!/&#0#*;!&&&#/' 8!:\u010C!! )"),
      peg$decode("%;\xA9/& 8!:\u010D! )"),
      peg$decode(`%;\xAA/k#;;/b$;\xAF/Y$$%;B/,#;\xB0/#$+")("'#&'#06*%;B/,#;\xB0/#$+")("'#&'#&/#$+$)($'#(#'#("'#&'#`),
      peg$decode(";\xAB.# &;\xAC"),
      peg$decode('3\u010E""5$7\u010F.S &3\u0110""5%7\u0111.G &3\u0112""5%7\u0113.; &3\u0114""5%7\u0115./ &3\u0116""5+7\u0117.# &;\xAD'),
      peg$decode(`3\u0118""5'7\u0119./ &3\u011A""5)7\u011B.# &;\xAD`),
      peg$decode(";6.# &;\xAE"),
      peg$decode(`%3\u011C""5"7\u011D/,#;6/#$+")("'#&'#`),
      peg$decode(";\xAD.# &;6"),
      peg$decode(`%;6/5#;</,$;\xB1/#$+#)(#'#("'#&'#`),
      peg$decode(";6.# &;H"),
      peg$decode(`%;\xB3/5#;./,$;\x90/#$+#)(#'#("'#&'#`),
      peg$decode("%$;!/&#0#*;!&&&#/' 8!:\u011E!! )"),
      peg$decode("%;\x9E/' 8!:\u011F!! )"),
      peg$decode(`%;\xB6/^#$%;B/,#;\xA0/#$+")("'#&'#06*%;B/,#;\xA0/#$+")("'#&'#&/($8":\u0120"!!)("'#&'#`),
      peg$decode(`%%;7/e#$%2J""6J7K/,#;7/#$+")("'#&'#0<*%2J""6J7K/,#;7/#$+")("'#&'#&/#$+")("'#&'#/"!&,)`),
      peg$decode(`%;L.# &;\x99/]#$%;B/,#;\xB8/#$+")("'#&'#06*%;B/,#;\xB8/#$+")("'#&'#&/'$8":\u0121" )("'#&'#`),
      peg$decode(";\xB9.# &;\xA0"),
      peg$decode(`%3\u0122""5#7\u0123/:#;</1$;6/($8#:\u0124#! )(#'#("'#&'#`),
      peg$decode("%$;!/&#0#*;!&&&#/' 8!:\u0125!! )"),
      peg$decode("%;\x9E/' 8!:\u0126!! )"),
      peg$decode(`%$;\x9A0#*;\x9A&/x#;@/o$;M/f$;?/]$$%;B/,#;\xA0/#$+")("'#&'#06*%;B/,#;\xA0/#$+")("'#&'#&/'$8%:\u0127% )(%'#($'#(#'#("'#&'#`),
      peg$decode(";\xBE"),
      peg$decode(`%3\u0128""5&7\u0129/k#;./b$;\xC1/Y$$%;A/,#;\xC1/#$+")("'#&'#06*%;A/,#;\xC1/#$+")("'#&'#&/#$+$)($'#(#'#("'#&'#.# &;\xBF`),
      peg$decode(`%;6/k#;./b$;\xC0/Y$$%;A/,#;\xC0/#$+")("'#&'#06*%;A/,#;\xC0/#$+")("'#&'#&/#$+$)($'#(#'#("'#&'#`),
      peg$decode(`%;6/;#;</2$;6.# &;H/#$+#)(#'#("'#&'#`),
      peg$decode(";\xC2.G &;\xC4.A &;\xC6.; &;\xC8.5 &;\xC9./ &;\xCA.) &;\xCB.# &;\xC0"),
      peg$decode(`%3\u012A""5%7\u012B/5#;</,$;\xC3/#$+#)(#'#("'#&'#`),
      peg$decode("%;I/' 8!:\u012C!! )"),
      peg$decode(`%3\u012D""5&7\u012E/\x97#;</\x8E$;D/\x85$;\xC5/|$$%$;'/&#0#*;'&&&#/,#;\xC5/#$+")("'#&'#0C*%$;'/&#0#*;'&&&#/,#;\xC5/#$+")("'#&'#&/,$;E/#$+&)(&'#(%'#($'#(#'#("'#&'#`),
      peg$decode(";t.# &;w"),
      peg$decode(`%3\u012F""5%7\u0130/5#;</,$;\xC7/#$+#)(#'#("'#&'#`),
      peg$decode("%;I/' 8!:\u0131!! )"),
      peg$decode(`%3\u0132""5&7\u0133/:#;</1$;I/($8#:\u0134#! )(#'#("'#&'#`),
      peg$decode(`%3\u0135""5%7\u0136/]#;</T$%3\u0137""5$7\u0138/& 8!:\u0139! ).4 &%3\u013A""5%7\u013B/& 8!:\u013C! )/#$+#)(#'#("'#&'#`),
      peg$decode(`%3\u013D""5)7\u013E/R#;</I$3\u013F""5#7\u0140./ &3\u0141""5(7\u0142.# &;6/($8#:\u0143#! )(#'#("'#&'#`),
      peg$decode(`%3\u0144""5#7\u0145/\x93#;</\x8A$;D/\x81$%;\xCC/e#$%2D""6D7E/,#;\xCC/#$+")("'#&'#0<*%2D""6D7E/,#;\xCC/#$+")("'#&'#&/#$+")("'#&'#/,$;E/#$+%)(%'#($'#(#'#("'#&'#`),
      peg$decode(`%3\u0146""5(7\u0147./ &3\u0148""5$7\u0149.# &;6/' 8!:\u014A!! )`),
      peg$decode(`%;6/Y#$%;A/,#;6/#$+")("'#&'#06*%;A/,#;6/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%;\xCF/G#;./>$;\xCF/5$;./,$;\x90/#$+%)(%'#($'#(#'#("'#&'#`),
      peg$decode("%$;!/&#0#*;!&&&#/' 8!:\u014B!! )"),
      peg$decode(`%;\xD1/]#$%;A/,#;\xD1/#$+")("'#&'#06*%;A/,#;\xD1/#$+")("'#&'#&/'$8":\u014C" )("'#&'#`),
      peg$decode(`%;\x99/]#$%;B/,#;\xA0/#$+")("'#&'#06*%;B/,#;\xA0/#$+")("'#&'#&/'$8":\u014D" )("'#&'#`),
      peg$decode(`%;L.O &;\x99.I &%;@." &"/:#;t/1$;?." &"/#$+#)(#'#("'#&'#/]#$%;B/,#;\xA0/#$+")("'#&'#06*%;B/,#;\xA0/#$+")("'#&'#&/'$8":\u014E" )("'#&'#`),
      peg$decode(`%;\xD4/]#$%;B/,#;\xD5/#$+")("'#&'#06*%;B/,#;\xD5/#$+")("'#&'#&/'$8":\u014F" )("'#&'#`),
      peg$decode("%;\x96/& 8!:\u0150! )"),
      peg$decode(`%3\u0151""5(7\u0152/:#;</1$;6/($8#:\u0153#! )(#'#("'#&'#.g &%3\u0154""5&7\u0155/:#;</1$;6/($8#:\u0156#! )(#'#("'#&'#.: &%3\u0157""5*7\u0158/& 8!:\u0159! ).# &;\xA0`),
      peg$decode(`%%;6/k#$%;A/2#;6/)$8":\u015A""$ )("'#&'#0<*%;A/2#;6/)$8":\u015A""$ )("'#&'#&/)$8":\u015B""! )("'#&'#." &"/' 8!:\u015C!! )`),
      peg$decode(`%;\xD8/Y#$%;A/,#;\xD8/#$+")("'#&'#06*%;A/,#;\xD8/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%;\x99/Y#$%;B/,#;\xA0/#$+")("'#&'#06*%;B/,#;\xA0/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode("%$;!/&#0#*;!&&&#/' 8!:\u015D!! )"),
      peg$decode(`%;\xDB/Y#$%;B/,#;\xDC/#$+")("'#&'#06*%;B/,#;\xDC/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%3\u015E""5&7\u015F.; &3\u0160""5'7\u0161./ &3\u0162""5*7\u0163.# &;6/& 8!:\u0164! )`),
      peg$decode(`%3\u0165""5&7\u0166/:#;</1$;\xDD/($8#:\u0167#! )(#'#("'#&'#.} &%3\xF5""5'7\xF6/:#;</1$;\x9E/($8#:\u0168#! )(#'#("'#&'#.P &%3\u0169""5+7\u016A/:#;</1$;\x9E/($8#:\u016B#! )(#'#("'#&'#.# &;\xA0`),
      peg$decode(`3\u016C""5+7\u016D.k &3\u016E""5)7\u016F._ &3\u0170""5(7\u0171.S &3\u0172""5'7\u0173.G &3\u0174""5&7\u0175.; &3\u0176""5*7\u0177./ &3\u0178""5)7\u0179.# &;6`),
      peg$decode(';1." &"'),
      peg$decode(`%%;6/k#$%;A/2#;6/)$8":\u015A""$ )("'#&'#0<*%;A/2#;6/)$8":\u015A""$ )("'#&'#&/)$8":\u015B""! )("'#&'#." &"/' 8!:\u017A!! )`),
      peg$decode(`%;L.# &;\x99/]#$%;B/,#;\xE1/#$+")("'#&'#06*%;B/,#;\xE1/#$+")("'#&'#&/'$8":\u017B" )("'#&'#`),
      peg$decode(";\xB9.# &;\xA0"),
      peg$decode(`%;\xE3/Y#$%;A/,#;\xE3/#$+")("'#&'#06*%;A/,#;\xE3/#$+")("'#&'#&/#$+")("'#&'#`),
      peg$decode(`%;\xEA/k#;./b$;\xED/Y$$%;B/,#;\xE4/#$+")("'#&'#06*%;B/,#;\xE4/#$+")("'#&'#&/#$+$)($'#(#'#("'#&'#`),
      peg$decode(";\xE5.; &;\xE6.5 &;\xE7./ &;\xE8.) &;\xE9.# &;\xA0"),
      peg$decode(`%3\u017C""5#7\u017D/:#;</1$;\xF0/($8#:\u017E#! )(#'#("'#&'#`),
      peg$decode(`%3\u017F""5%7\u0180/:#;</1$;T/($8#:\u0181#! )(#'#("'#&'#`),
      peg$decode(`%3\u0182""5(7\u0183/F#;</=$;\\.) &;Y.# &;X/($8#:\u0184#! )(#'#("'#&'#`),
      peg$decode(`%3\u0185""5&7\u0186/:#;</1$;6/($8#:\u0187#! )(#'#("'#&'#`),
      peg$decode(`%3\u0188""5%7\u0189/A#;</8$$;!0#*;!&/($8#:\u018A#! )(#'#("'#&'#`),
      peg$decode(`%;\xEB/G#;;/>$;6/5$;;/,$;\xEC/#$+%)(%'#($'#(#'#("'#&'#`),
      peg$decode(`%3\x92""5#7\xD3.# &;6/' 8!:\u018B!! )`),
      peg$decode(`%3\xB1""5#7\u018C.G &3\xB3""5#7\u018D.; &3\xB7""5#7\u018E./ &3\xB5""5$7\u018F.# &;6/' 8!:\u0190!! )`),
      peg$decode(`%;\xEE/D#%;C/,#;\xEF/#$+")("'#&'#." &"/#$+")("'#&'#`),
      peg$decode("%;U.) &;\\.# &;X/& 8!:\u0191! )"),
      peg$decode(`%%;!." &"/[#;!." &"/M$;!." &"/?$;!." &"/1$;!." &"/#$+%)(%'#($'#(#'#("'#&'#/' 8!:\u0192!! )`),
      peg$decode(`%%;!/?#;!." &"/1$;!." &"/#$+#)(#'#("'#&'#/' 8!:\u0193!! )`),
      peg$decode(";\xBE"),
      peg$decode(`%;\x9E/^#$%;B/,#;\xF3/#$+")("'#&'#06*%;B/,#;\xF3/#$+")("'#&'#&/($8":\u0194"!!)("'#&'#`),
      peg$decode(";\xF4.# &;\xA0"),
      peg$decode(`%2\u0195""6\u01957\u0196/L#;</C$2\u0197""6\u01977\u0198.) &2\u0199""6\u01997\u019A/($8#:\u019B#! )(#'#("'#&'#`),
      peg$decode(`%;\x9E/^#$%;B/,#;\xA0/#$+")("'#&'#06*%;B/,#;\xA0/#$+")("'#&'#&/($8":\u019C"!!)("'#&'#`),
      peg$decode(`%;6/5#;0/,$;\xF7/#$+#)(#'#("'#&'#`),
      peg$decode("$;2.) &;4.# &;.0/*;2.) &;4.# &;.&"),
      peg$decode("$;%0#*;%&"),
      peg$decode(`%;\xFA/;#28""6879/,$;\xFB/#$+#)(#'#("'#&'#`),
      peg$decode(`%3\u019D""5%7\u019E.) &3\u019F""5$7\u01A0/' 8!:\u01A1!! )`),
      peg$decode(`%;\xFC/J#%28""6879/,#;^/#$+")("'#&'#." &"/#$+")("'#&'#`),
      peg$decode("%;\\.) &;X.# &;\x82/' 8!:\u01A2!! )"),
      peg$decode(';".S &;!.M &2F""6F7G.A &2J""6J7K.5 &2H""6H7I.) &2N""6N7O'),
      peg$decode('2L""6L7M.\x95 &2B""6B7C.\x89 &2<""6<7=.} &2R""6R7S.q &2T""6T7U.e &2V""6V7W.Y &2P""6P7Q.M &2@""6@7A.A &2D""6D7E.5 &22""6273.) &2>""6>7?'),
      peg$decode(`%;\u0100/b#28""6879/S$;\xFB/J$%2\u01A3""6\u01A37\u01A4/,#;\xEC/#$+")("'#&'#." &"/#$+$)($'#(#'#("'#&'#`),
      peg$decode(`%3\u01A5""5%7\u01A6.) &3\u01A7""5$7\u01A8/' 8!:\u01A1!! )`),
      peg$decode(`%3\xB1""5#7\xB2.6 &3\xB3""5#7\xB4.* &$;+0#*;+&/' 8!:\u01A9!! )`),
      peg$decode(`%;\u0104/\x87#2F""6F7G/x$;\u0103/o$2F""6F7G/\`$;\u0103/W$2F""6F7G/H$;\u0103/?$2F""6F7G/0$;\u0105/'$8):\u01AA) )()'#(('#(''#(&'#(%'#($'#(#'#("'#&'#`),
      peg$decode(`%;#/>#;#/5$;#/,$;#/#$+$)($'#(#'#("'#&'#`),
      peg$decode(`%;\u0103/,#;\u0103/#$+")("'#&'#`),
      peg$decode(`%;\u0103/5#;\u0103/,$;\u0103/#$+#)(#'#("'#&'#`),
      peg$decode(`%;q/T#$;m0#*;m&/D$%; /,#;\xF8/#$+")("'#&'#." &"/#$+#)(#'#("'#&'#`),
      peg$decode(`%2\u01AB""6\u01AB7\u01AC.) &2\u01AD""6\u01AD7\u01AE/w#;0/n$;\u0108/e$$%;B/2#;\u0109.# &;\xA0/#$+")("'#&'#0<*%;B/2#;\u0109.# &;\xA0/#$+")("'#&'#&/#$+$)($'#(#'#("'#&'#`),
      peg$decode(";\x99.# &;L"),
      peg$decode(`%2\u01AF""6\u01AF7\u01B0/5#;</,$;\u010A/#$+#)(#'#("'#&'#`),
      peg$decode(`%;D/S#;,/J$2:""6:7;/;$;,.# &;T/,$;E/#$+%)(%'#($'#(#'#("'#&'#`)
    ];
    let peg$currPos = 0;
    let peg$savedPos = 0;
    const peg$posDetailsCache = [{ line: 1, column: 1 }];
    let peg$maxFailPos = 0;
    let peg$maxFailExpected = [];
    let peg$silentFails = 0;
    let peg$result;
    if (options.startRule !== void 0) {
      if (!(options.startRule in peg$startRuleIndices)) {
        throw new Error(`Can't start parsing from rule "` + options.startRule + '".');
      }
      peg$startRuleIndex = peg$startRuleIndices[options.startRule];
    }
    function text() {
      return input.substring(peg$savedPos, peg$currPos);
    }
    function location() {
      return peg$computeLocation(peg$savedPos, peg$currPos);
    }
    function expected(description, location1) {
      location1 = location1 !== void 0 ? location1 : peg$computeLocation(peg$savedPos, peg$currPos);
      throw peg$buildStructuredError([peg$otherExpectation(description)], input.substring(peg$savedPos, peg$currPos), location1);
    }
    function error(message, location1) {
      location1 = location1 !== void 0 ? location1 : peg$computeLocation(peg$savedPos, peg$currPos);
      throw peg$buildSimpleError(message, location1);
    }
    function peg$literalExpectation(text1, ignoreCase) {
      return { type: "literal", text: text1, ignoreCase };
    }
    function peg$classExpectation(parts, inverted, ignoreCase) {
      return { type: "class", parts, inverted, ignoreCase };
    }
    function peg$anyExpectation() {
      return { type: "any" };
    }
    function peg$endExpectation() {
      return { type: "end" };
    }
    function peg$otherExpectation(description) {
      return { type: "other", description };
    }
    function peg$computePosDetails(pos) {
      let details = peg$posDetailsCache[pos];
      let p;
      if (details) {
        return details;
      } else {
        p = pos - 1;
        while (!peg$posDetailsCache[p]) {
          p--;
        }
        details = peg$posDetailsCache[p];
        details = {
          line: details.line,
          column: details.column
        };
        while (p < pos) {
          if (input.charCodeAt(p) === 10) {
            details.line++;
            details.column = 1;
          } else {
            details.column++;
          }
          p++;
        }
        peg$posDetailsCache[pos] = details;
        return details;
      }
    }
    function peg$computeLocation(startPos, endPos) {
      const startPosDetails = peg$computePosDetails(startPos);
      const endPosDetails = peg$computePosDetails(endPos);
      return {
        source: peg$source,
        start: {
          offset: startPos,
          line: startPosDetails.line,
          column: startPosDetails.column
        },
        end: {
          offset: endPos,
          line: endPosDetails.line,
          column: endPosDetails.column
        }
      };
    }
    function peg$fail(expected1) {
      if (peg$currPos < peg$maxFailPos) {
        return;
      }
      if (peg$currPos > peg$maxFailPos) {
        peg$maxFailPos = peg$currPos;
        peg$maxFailExpected = [];
      }
      peg$maxFailExpected.push(expected1);
    }
    function peg$buildSimpleError(message, location1) {
      return new SyntaxError(message, [], "", location1);
    }
    function peg$buildStructuredError(expected1, found, location1) {
      return new SyntaxError(SyntaxError.buildMessage(expected1, found), expected1, found, location1);
    }
    function peg$decode(s) {
      return s.split("").map((ch) => ch.charCodeAt(0) - 32);
    }
    function peg$parseRule(index) {
      const bc = peg$bytecode[index];
      let ip = 0;
      const ips = [];
      let end = bc.length;
      const ends = [];
      const stack = [];
      let params;
      while (true) {
        while (ip < end) {
          switch (bc[ip]) {
            case 0:
              stack.push(peg$consts[bc[ip + 1]]);
              ip += 2;
              break;
            case 1:
              stack.push(void 0);
              ip++;
              break;
            case 2:
              stack.push(null);
              ip++;
              break;
            case 3:
              stack.push(peg$FAILED);
              ip++;
              break;
            case 4:
              stack.push([]);
              ip++;
              break;
            case 5:
              stack.push(peg$currPos);
              ip++;
              break;
            case 6:
              stack.pop();
              ip++;
              break;
            case 7:
              peg$currPos = stack.pop();
              ip++;
              break;
            case 8:
              stack.length -= bc[ip + 1];
              ip += 2;
              break;
            case 9:
              stack.splice(-2, 1);
              ip++;
              break;
            case 10:
              stack[stack.length - 2].push(stack.pop());
              ip++;
              break;
            case 11:
              stack.push(stack.splice(stack.length - bc[ip + 1], bc[ip + 1]));
              ip += 2;
              break;
            case 12:
              stack.push(input.substring(stack.pop(), peg$currPos));
              ip++;
              break;
            case 13:
              ends.push(end);
              ips.push(ip + 3 + bc[ip + 1] + bc[ip + 2]);
              if (stack[stack.length - 1]) {
                end = ip + 3 + bc[ip + 1];
                ip += 3;
              } else {
                end = ip + 3 + bc[ip + 1] + bc[ip + 2];
                ip += 3 + bc[ip + 1];
              }
              break;
            case 14:
              ends.push(end);
              ips.push(ip + 3 + bc[ip + 1] + bc[ip + 2]);
              if (stack[stack.length - 1] === peg$FAILED) {
                end = ip + 3 + bc[ip + 1];
                ip += 3;
              } else {
                end = ip + 3 + bc[ip + 1] + bc[ip + 2];
                ip += 3 + bc[ip + 1];
              }
              break;
            case 15:
              ends.push(end);
              ips.push(ip + 3 + bc[ip + 1] + bc[ip + 2]);
              if (stack[stack.length - 1] !== peg$FAILED) {
                end = ip + 3 + bc[ip + 1];
                ip += 3;
              } else {
                end = ip + 3 + bc[ip + 1] + bc[ip + 2];
                ip += 3 + bc[ip + 1];
              }
              break;
            case 16:
              if (stack[stack.length - 1] !== peg$FAILED) {
                ends.push(end);
                ips.push(ip);
                end = ip + 2 + bc[ip + 1];
                ip += 2;
              } else {
                ip += 2 + bc[ip + 1];
              }
              break;
            case 17:
              ends.push(end);
              ips.push(ip + 3 + bc[ip + 1] + bc[ip + 2]);
              if (input.length > peg$currPos) {
                end = ip + 3 + bc[ip + 1];
                ip += 3;
              } else {
                end = ip + 3 + bc[ip + 1] + bc[ip + 2];
                ip += 3 + bc[ip + 1];
              }
              break;
            case 18:
              ends.push(end);
              ips.push(ip + 4 + bc[ip + 2] + bc[ip + 3]);
              if (input.substr(peg$currPos, peg$consts[bc[ip + 1]].length) === peg$consts[bc[ip + 1]]) {
                end = ip + 4 + bc[ip + 2];
                ip += 4;
              } else {
                end = ip + 4 + bc[ip + 2] + bc[ip + 3];
                ip += 4 + bc[ip + 2];
              }
              break;
            case 19:
              ends.push(end);
              ips.push(ip + 4 + bc[ip + 2] + bc[ip + 3]);
              if (input.substr(peg$currPos, peg$consts[bc[ip + 1]].length).toLowerCase() === peg$consts[bc[ip + 1]]) {
                end = ip + 4 + bc[ip + 2];
                ip += 4;
              } else {
                end = ip + 4 + bc[ip + 2] + bc[ip + 3];
                ip += 4 + bc[ip + 2];
              }
              break;
            case 20:
              ends.push(end);
              ips.push(ip + 4 + bc[ip + 2] + bc[ip + 3]);
              if (peg$consts[bc[ip + 1]].test(input.charAt(peg$currPos))) {
                end = ip + 4 + bc[ip + 2];
                ip += 4;
              } else {
                end = ip + 4 + bc[ip + 2] + bc[ip + 3];
                ip += 4 + bc[ip + 2];
              }
              break;
            case 21:
              stack.push(input.substr(peg$currPos, bc[ip + 1]));
              peg$currPos += bc[ip + 1];
              ip += 2;
              break;
            case 22:
              stack.push(peg$consts[bc[ip + 1]]);
              peg$currPos += peg$consts[bc[ip + 1]].length;
              ip += 2;
              break;
            case 23:
              stack.push(peg$FAILED);
              if (peg$silentFails === 0) {
                peg$fail(peg$consts[bc[ip + 1]]);
              }
              ip += 2;
              break;
            case 24:
              peg$savedPos = stack[stack.length - 1 - bc[ip + 1]];
              ip += 2;
              break;
            case 25:
              peg$savedPos = peg$currPos;
              ip++;
              break;
            case 26:
              params = bc.slice(ip + 4, ip + 4 + bc[ip + 3]).map(function(p) {
                return stack[stack.length - 1 - p];
              });
              stack.splice(stack.length - bc[ip + 2], bc[ip + 2], peg$consts[bc[ip + 1]].apply(null, params));
              ip += 4 + bc[ip + 3];
              break;
            case 27:
              stack.push(peg$parseRule(bc[ip + 1]));
              ip += 2;
              break;
            case 28:
              peg$silentFails++;
              ip++;
              break;
            case 29:
              peg$silentFails--;
              ip++;
              break;
            default:
              throw new Error("Invalid opcode: " + bc[ip] + ".");
          }
        }
        if (ends.length > 0) {
          end = ends.pop();
          ip = ips.pop();
        } else {
          break;
        }
      }
      return stack[0];
    }
    options.data = {};
    function list(head, tail) {
      return [head].concat(tail);
    }
    peg$result = peg$parseRule(peg$startRuleIndex);
    if (peg$result !== peg$FAILED && peg$currPos === input.length) {
      return peg$result;
    } else {
      if (peg$result !== peg$FAILED && peg$currPos < input.length) {
        peg$fail(peg$endExpectation());
      }
      throw peg$buildStructuredError(peg$maxFailExpected, peg$maxFailPos < input.length ? input.charAt(peg$maxFailPos) : null, peg$maxFailPos < input.length ? peg$computeLocation(peg$maxFailPos, peg$maxFailPos + 1) : peg$computeLocation(peg$maxFailPos, peg$maxFailPos));
    }
  }
  var parse = peg$parse;

  // node_modules/sip.js/lib/grammar/grammar.js
  var Grammar;
  (function(Grammar2) {
    function parse2(input, startRule) {
      const options = { startRule };
      try {
        parse(input, options);
      } catch (e) {
        options.data = -1;
      }
      return options.data;
    }
    Grammar2.parse = parse2;
    function nameAddrHeaderParse(nameAddrHeader) {
      const parsedNameAddrHeader = Grammar2.parse(nameAddrHeader, "Name_Addr_Header");
      return parsedNameAddrHeader !== -1 ? parsedNameAddrHeader : void 0;
    }
    Grammar2.nameAddrHeaderParse = nameAddrHeaderParse;
    function URIParse(uri) {
      const parsedUri = Grammar2.parse(uri, "SIP_URI");
      return parsedUri !== -1 ? parsedUri : void 0;
    }
    Grammar2.URIParse = URIParse;
  })(Grammar = Grammar || (Grammar = {}));

  // node_modules/sip.js/lib/core/messages/utils.js
  var REASON_PHRASE = {
    100: "Trying",
    180: "Ringing",
    181: "Call Is Being Forwarded",
    182: "Queued",
    183: "Session Progress",
    199: "Early Dialog Terminated",
    200: "OK",
    202: "Accepted",
    204: "No Notification",
    300: "Multiple Choices",
    301: "Moved Permanently",
    302: "Moved Temporarily",
    305: "Use Proxy",
    380: "Alternative Service",
    400: "Bad Request",
    401: "Unauthorized",
    402: "Payment Required",
    403: "Forbidden",
    404: "Not Found",
    405: "Method Not Allowed",
    406: "Not Acceptable",
    407: "Proxy Authentication Required",
    408: "Request Timeout",
    410: "Gone",
    412: "Conditional Request Failed",
    413: "Request Entity Too Large",
    414: "Request-URI Too Long",
    415: "Unsupported Media Type",
    416: "Unsupported URI Scheme",
    417: "Unknown Resource-Priority",
    420: "Bad Extension",
    421: "Extension Required",
    422: "Session Interval Too Small",
    423: "Interval Too Brief",
    428: "Use Identity Header",
    429: "Provide Referrer Identity",
    430: "Flow Failed",
    433: "Anonymity Disallowed",
    436: "Bad Identity-Info",
    437: "Unsupported Certificate",
    438: "Invalid Identity Header",
    439: "First Hop Lacks Outbound Support",
    440: "Max-Breadth Exceeded",
    469: "Bad Info Package",
    470: "Consent Needed",
    478: "Unresolvable Destination",
    480: "Temporarily Unavailable",
    481: "Call/Transaction Does Not Exist",
    482: "Loop Detected",
    483: "Too Many Hops",
    484: "Address Incomplete",
    485: "Ambiguous",
    486: "Busy Here",
    487: "Request Terminated",
    488: "Not Acceptable Here",
    489: "Bad Event",
    491: "Request Pending",
    493: "Undecipherable",
    494: "Security Agreement Required",
    500: "Internal Server Error",
    501: "Not Implemented",
    502: "Bad Gateway",
    503: "Service Unavailable",
    504: "Server Time-out",
    505: "Version Not Supported",
    513: "Message Too Large",
    580: "Precondition Failure",
    600: "Busy Everywhere",
    603: "Decline",
    604: "Does Not Exist Anywhere",
    606: "Not Acceptable"
  };
  function createRandomToken(size, base = 32) {
    let token = "";
    for (let i = 0; i < size; i++) {
      const r = Math.floor(Math.random() * base);
      token += r.toString(base);
    }
    return token;
  }
  function getReasonPhrase(code) {
    return REASON_PHRASE[code] || "";
  }
  function newTag() {
    return createRandomToken(10);
  }
  function headerize(str) {
    const exceptions = {
      "Call-Id": "Call-ID",
      Cseq: "CSeq",
      "Min-Se": "Min-SE",
      Rack: "RAck",
      Rseq: "RSeq",
      "Www-Authenticate": "WWW-Authenticate"
    };
    const name2 = str.toLowerCase().replace(/_/g, "-").split("-");
    const parts = name2.length;
    let hname = "";
    for (let part = 0; part < parts; part++) {
      if (part !== 0) {
        hname += "-";
      }
      hname += name2[part].charAt(0).toUpperCase() + name2[part].substring(1);
    }
    if (exceptions[hname]) {
      hname = exceptions[hname];
    }
    return hname;
  }
  function utf8Length(str) {
    return encodeURIComponent(str).replace(/%[A-F\d]{2}/g, "U").length;
  }

  // node_modules/sip.js/lib/core/messages/incoming-message.js
  var IncomingMessage = class {
    constructor() {
      this.headers = {};
    }
    /**
     * Insert a header of the given name and value into the last position of the
     * header array.
     * @param name - header name
     * @param value - header value
     */
    addHeader(name2, value) {
      const header = { raw: value };
      name2 = headerize(name2);
      if (this.headers[name2]) {
        this.headers[name2].push(header);
      } else {
        this.headers[name2] = [header];
      }
    }
    /**
     * Get the value of the given header name at the given position.
     * @param name - header name
     * @returns Returns the specified header, undefined if header doesn't exist.
     */
    getHeader(name2) {
      const header = this.headers[headerize(name2)];
      if (header) {
        if (header[0]) {
          return header[0].raw;
        }
      } else {
        return;
      }
    }
    /**
     * Get the header/s of the given name.
     * @param name - header name
     * @returns Array - with all the headers of the specified name.
     */
    getHeaders(name2) {
      const header = this.headers[headerize(name2)];
      const result = [];
      if (!header) {
        return [];
      }
      for (const headerPart of header) {
        result.push(headerPart.raw);
      }
      return result;
    }
    /**
     * Verify the existence of the given header.
     * @param name - header name
     * @returns true if header with given name exists, false otherwise
     */
    hasHeader(name2) {
      return !!this.headers[headerize(name2)];
    }
    /**
     * Parse the given header on the given index.
     * @param name - header name
     * @param idx - header index
     * @returns Parsed header object, undefined if the
     *   header is not present or in case of a parsing error.
     */
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    parseHeader(name2, idx = 0) {
      name2 = headerize(name2);
      if (!this.headers[name2]) {
        return;
      } else if (idx >= this.headers[name2].length) {
        return;
      }
      const header = this.headers[name2][idx];
      const value = header.raw;
      if (header.parsed) {
        return header.parsed;
      }
      const parsed = Grammar.parse(value, name2.replace(/-/g, "_"));
      if (parsed === -1) {
        this.headers[name2].splice(idx, 1);
        return;
      } else {
        header.parsed = parsed;
        return parsed;
      }
    }
    /**
     * Message Header attribute selector. Alias of parseHeader.
     * @param name - header name
     * @param idx - header index
     * @returns Parsed header object, undefined if the
     *   header is not present or in case of a parsing error.
     *
     * @example
     * message.s('via',3).port
     */
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    s(name2, idx = 0) {
      return this.parseHeader(name2, idx);
    }
    /**
     * Replace the value of the given header by the value.
     * @param name - header name
     * @param value - header value
     */
    setHeader(name2, value) {
      this.headers[headerize(name2)] = [{ raw: value }];
    }
    toString() {
      return this.data;
    }
  };

  // node_modules/sip.js/lib/core/messages/incoming-request-message.js
  var IncomingRequestMessage = class extends IncomingMessage {
    constructor() {
      super();
    }
  };

  // node_modules/sip.js/lib/core/messages/incoming-response-message.js
  var IncomingResponseMessage = class extends IncomingMessage {
    constructor() {
      super();
    }
  };

  // node_modules/sip.js/lib/core/messages/outgoing-request-message.js
  var OutgoingRequestMessage = class _OutgoingRequestMessage {
    constructor(method, ruri, fromURI, toURI, options, extraHeaders, body) {
      this.headers = {};
      this.extraHeaders = [];
      this.options = _OutgoingRequestMessage.getDefaultOptions();
      if (options) {
        this.options = Object.assign(Object.assign({}, this.options), options);
        if (this.options.optionTags && this.options.optionTags.length) {
          this.options.optionTags = this.options.optionTags.slice();
        }
        if (this.options.routeSet && this.options.routeSet.length) {
          this.options.routeSet = this.options.routeSet.slice();
        }
      }
      if (extraHeaders && extraHeaders.length) {
        this.extraHeaders = extraHeaders.slice();
      }
      if (body) {
        this.body = {
          body: body.content,
          contentType: body.contentType
        };
      }
      this.method = method;
      this.ruri = ruri.clone();
      this.fromURI = fromURI.clone();
      this.fromTag = this.options.fromTag ? this.options.fromTag : newTag();
      this.from = _OutgoingRequestMessage.makeNameAddrHeader(this.fromURI, this.options.fromDisplayName, this.fromTag);
      this.toURI = toURI.clone();
      this.toTag = this.options.toTag;
      this.to = _OutgoingRequestMessage.makeNameAddrHeader(this.toURI, this.options.toDisplayName, this.toTag);
      this.callId = this.options.callId ? this.options.callId : this.options.callIdPrefix + createRandomToken(15);
      this.cseq = this.options.cseq;
      this.setHeader("route", this.options.routeSet);
      this.setHeader("via", "");
      this.setHeader("to", this.to.toString());
      this.setHeader("from", this.from.toString());
      this.setHeader("cseq", this.cseq + " " + this.method);
      this.setHeader("call-id", this.callId);
      this.setHeader("max-forwards", "70");
    }
    /** Get a copy of the default options. */
    static getDefaultOptions() {
      return {
        callId: "",
        callIdPrefix: "",
        cseq: 1,
        toDisplayName: "",
        toTag: "",
        fromDisplayName: "",
        fromTag: "",
        forceRport: false,
        hackViaTcp: false,
        optionTags: ["outbound"],
        routeSet: [],
        userAgentString: "sip.js",
        viaHost: ""
      };
    }
    static makeNameAddrHeader(uri, displayName, tag) {
      const parameters = {};
      if (tag) {
        parameters.tag = tag;
      }
      return new NameAddrHeader(uri, displayName, parameters);
    }
    /**
     * Get the value of the given header name at the given position.
     * @param name - header name
     * @returns Returns the specified header, undefined if header doesn't exist.
     */
    getHeader(name2) {
      const header = this.headers[headerize(name2)];
      if (header) {
        if (header[0]) {
          return header[0];
        }
      } else {
        const regexp = new RegExp("^\\s*" + name2 + "\\s*:", "i");
        for (const exHeader of this.extraHeaders) {
          if (regexp.test(exHeader)) {
            return exHeader.substring(exHeader.indexOf(":") + 1).trim();
          }
        }
      }
      return;
    }
    /**
     * Get the header/s of the given name.
     * @param name - header name
     * @returns Array with all the headers of the specified name.
     */
    getHeaders(name2) {
      const result = [];
      const headerArray = this.headers[headerize(name2)];
      if (headerArray) {
        for (const headerPart of headerArray) {
          result.push(headerPart);
        }
      } else {
        const regexp = new RegExp("^\\s*" + name2 + "\\s*:", "i");
        for (const exHeader of this.extraHeaders) {
          if (regexp.test(exHeader)) {
            result.push(exHeader.substring(exHeader.indexOf(":") + 1).trim());
          }
        }
      }
      return result;
    }
    /**
     * Verify the existence of the given header.
     * @param name - header name
     * @returns true if header with given name exists, false otherwise
     */
    hasHeader(name2) {
      if (this.headers[headerize(name2)]) {
        return true;
      } else {
        const regexp = new RegExp("^\\s*" + name2 + "\\s*:", "i");
        for (const extraHeader of this.extraHeaders) {
          if (regexp.test(extraHeader)) {
            return true;
          }
        }
      }
      return false;
    }
    /**
     * Replace the the given header by the given value.
     * @param name - header name
     * @param value - header value
     */
    setHeader(name2, value) {
      this.headers[headerize(name2)] = value instanceof Array ? value : [value];
    }
    /**
     * The Via header field indicates the transport used for the transaction
     * and identifies the location where the response is to be sent.  A Via
     * header field value is added only after the transport that will be
     * used to reach the next hop has been selected (which may involve the
     * usage of the procedures in [4]).
     *
     * When the UAC creates a request, it MUST insert a Via into that
     * request.  The protocol name and protocol version in the header field
     * MUST be SIP and 2.0, respectively.  The Via header field value MUST
     * contain a branch parameter.  This parameter is used to identify the
     * transaction created by that request.  This parameter is used by both
     * the client and the server.
     * https://tools.ietf.org/html/rfc3261#section-8.1.1.7
     * @param branchParameter - The branch parameter.
     * @param transport - The sent protocol transport.
     */
    setViaHeader(branch, transport) {
      if (this.options.hackViaTcp) {
        transport = "TCP";
      }
      let via = "SIP/2.0/" + transport;
      via += " " + this.options.viaHost + ";branch=" + branch;
      if (this.options.forceRport) {
        via += ";rport";
      }
      this.setHeader("via", via);
      this.branch = branch;
    }
    toString() {
      let msg = "";
      msg += this.method + " " + this.ruri.toRaw() + " SIP/2.0\r\n";
      for (const header in this.headers) {
        if (this.headers[header]) {
          for (const headerPart of this.headers[header]) {
            msg += header + ": " + headerPart + "\r\n";
          }
        }
      }
      for (const header of this.extraHeaders) {
        msg += header.trim() + "\r\n";
      }
      msg += "Supported: " + this.options.optionTags.join(", ") + "\r\n";
      msg += "User-Agent: " + this.options.userAgentString + "\r\n";
      if (this.body) {
        if (typeof this.body === "string") {
          msg += "Content-Length: " + utf8Length(this.body) + "\r\n\r\n";
          msg += this.body;
        } else {
          if (this.body.body && this.body.contentType) {
            msg += "Content-Type: " + this.body.contentType + "\r\n";
            msg += "Content-Length: " + utf8Length(this.body.body) + "\r\n\r\n";
            msg += this.body.body;
          } else {
            msg += "Content-Length: 0\r\n\r\n";
          }
        }
      } else {
        msg += "Content-Length: 0\r\n\r\n";
      }
      return msg;
    }
  };

  // node_modules/sip.js/lib/core/messages/body.js
  function contentTypeToContentDisposition(contentType) {
    if (contentType === "application/sdp") {
      return "session";
    } else {
      return "render";
    }
  }
  function fromBodyLegacy(bodyLegacy) {
    const content = typeof bodyLegacy === "string" ? bodyLegacy : bodyLegacy.body;
    const contentType = typeof bodyLegacy === "string" ? "application/sdp" : bodyLegacy.contentType;
    const contentDisposition = contentTypeToContentDisposition(contentType);
    const body = { contentDisposition, contentType, content };
    return body;
  }
  function isBody(body) {
    return body && typeof body.content === "string" && typeof body.contentType === "string" && body.contentDisposition === void 0 ? true : typeof body.contentDisposition === "string";
  }
  function getBody(message) {
    let contentDisposition;
    let contentType;
    let content;
    if (message instanceof IncomingRequestMessage) {
      if (message.body) {
        const parse2 = message.parseHeader("Content-Disposition");
        contentDisposition = parse2 ? parse2.type : void 0;
        contentType = message.parseHeader("Content-Type");
        content = message.body;
      }
    }
    if (message instanceof IncomingResponseMessage) {
      if (message.body) {
        const parse2 = message.parseHeader("Content-Disposition");
        contentDisposition = parse2 ? parse2.type : void 0;
        contentType = message.parseHeader("Content-Type");
        content = message.body;
      }
    }
    if (message instanceof OutgoingRequestMessage) {
      if (message.body) {
        contentDisposition = message.getHeader("Content-Disposition");
        contentType = message.getHeader("Content-Type");
        if (typeof message.body === "string") {
          if (!contentType) {
            throw new Error("Header content type header does not equal body content type.");
          }
          content = message.body;
        } else {
          if (contentType && contentType !== message.body.contentType) {
            throw new Error("Header content type header does not equal body content type.");
          }
          contentType = message.body.contentType;
          content = message.body.body;
        }
      }
    }
    if (isBody(message)) {
      contentDisposition = message.contentDisposition;
      contentType = message.contentType;
      content = message.content;
    }
    if (!content) {
      return void 0;
    }
    if (contentType && !contentDisposition) {
      contentDisposition = contentTypeToContentDisposition(contentType);
    }
    if (!contentDisposition) {
      throw new Error("Content disposition undefined.");
    }
    if (!contentType) {
      throw new Error("Content type undefined.");
    }
    return {
      contentDisposition,
      contentType,
      content
    };
  }

  // node_modules/sip.js/lib/core/session/session.js
  var SessionState;
  (function(SessionState3) {
    SessionState3["Initial"] = "Initial";
    SessionState3["Early"] = "Early";
    SessionState3["AckWait"] = "AckWait";
    SessionState3["Confirmed"] = "Confirmed";
    SessionState3["Terminated"] = "Terminated";
  })(SessionState = SessionState || (SessionState = {}));
  var SignalingState;
  (function(SignalingState2) {
    SignalingState2["Initial"] = "Initial";
    SignalingState2["HaveLocalOffer"] = "HaveLocalOffer";
    SignalingState2["HaveRemoteOffer"] = "HaveRemoteOffer";
    SignalingState2["Stable"] = "Stable";
    SignalingState2["Closed"] = "Closed";
  })(SignalingState = SignalingState || (SignalingState = {}));

  // node_modules/sip.js/lib/core/timers.js
  var T1 = 500;
  var T2 = 4e3;
  var T4 = 5e3;
  var Timers = {
    T1,
    T2,
    T4,
    TIMER_B: 64 * T1,
    TIMER_D: 0 * T1,
    TIMER_F: 64 * T1,
    TIMER_H: 64 * T1,
    TIMER_I: 0 * T4,
    TIMER_J: 0 * T1,
    TIMER_K: 0 * T4,
    TIMER_L: 64 * T1,
    TIMER_M: 64 * T1,
    TIMER_N: 64 * T1,
    PROVISIONAL_RESPONSE_INTERVAL: 6e4
    // See RFC 3261 Section 13.3.1.1
  };

  // node_modules/sip.js/lib/core/exceptions/transaction-state-error.js
  var TransactionStateError = class extends Exception {
    constructor(message) {
      super(message ? message : "Transaction state error.");
    }
  };

  // node_modules/sip.js/lib/core/messages/methods/constants.js
  var C;
  (function(C2) {
    C2.ACK = "ACK";
    C2.BYE = "BYE";
    C2.CANCEL = "CANCEL";
    C2.INFO = "INFO";
    C2.INVITE = "INVITE";
    C2.MESSAGE = "MESSAGE";
    C2.NOTIFY = "NOTIFY";
    C2.OPTIONS = "OPTIONS";
    C2.REGISTER = "REGISTER";
    C2.UPDATE = "UPDATE";
    C2.SUBSCRIBE = "SUBSCRIBE";
    C2.PUBLISH = "PUBLISH";
    C2.REFER = "REFER";
    C2.PRACK = "PRACK";
  })(C = C || (C = {}));

  // node_modules/sip.js/lib/core/user-agent-core/allowed-methods.js
  var AllowedMethods = [
    C.ACK,
    C.BYE,
    C.CANCEL,
    C.INFO,
    C.INVITE,
    C.MESSAGE,
    C.NOTIFY,
    C.OPTIONS,
    C.PRACK,
    C.REFER,
    C.REGISTER,
    C.SUBSCRIBE
  ];

  // node_modules/sip.js/lib/api/message.js
  var Message = class {
    /** @internal */
    constructor(incomingMessageRequest) {
      this.incomingMessageRequest = incomingMessageRequest;
    }
    /** Incoming MESSAGE request message. */
    get request() {
      return this.incomingMessageRequest.message;
    }
    /** Accept the request. */
    accept(options) {
      this.incomingMessageRequest.accept(options);
      return Promise.resolve();
    }
    /** Reject the request. */
    reject(options) {
      this.incomingMessageRequest.reject(options);
      return Promise.resolve();
    }
  };

  // node_modules/sip.js/lib/api/notification.js
  var Notification = class {
    /** @internal */
    constructor(incomingNotifyRequest) {
      this.incomingNotifyRequest = incomingNotifyRequest;
    }
    /** Incoming NOTIFY request message. */
    get request() {
      return this.incomingNotifyRequest.message;
    }
    /** Accept the request. */
    accept(options) {
      this.incomingNotifyRequest.accept(options);
      return Promise.resolve();
    }
    /** Reject the request. */
    reject(options) {
      this.incomingNotifyRequest.reject(options);
      return Promise.resolve();
    }
  };

  // node_modules/sip.js/lib/api/referral.js
  var Referral = class {
    /** @internal */
    constructor(incomingReferRequest, session) {
      this.incomingReferRequest = incomingReferRequest;
      this.session = session;
    }
    get referTo() {
      const referTo = this.incomingReferRequest.message.parseHeader("refer-to");
      if (!(referTo instanceof NameAddrHeader)) {
        throw new Error("Failed to parse Refer-To header.");
      }
      return referTo;
    }
    get referredBy() {
      return this.incomingReferRequest.message.getHeader("referred-by");
    }
    get replaces() {
      const value = this.referTo.uri.getHeader("replaces");
      if (value instanceof Array) {
        return value[0];
      }
      return value;
    }
    /** Incoming REFER request message. */
    get request() {
      return this.incomingReferRequest.message;
    }
    /** Accept the request. */
    accept(options = { statusCode: 202 }) {
      this.incomingReferRequest.accept(options);
      return Promise.resolve();
    }
    /** Reject the request. */
    reject(options) {
      this.incomingReferRequest.reject(options);
      return Promise.resolve();
    }
    /**
     * Creates an inviter which may be used to send an out of dialog INVITE request.
     *
     * @remarks
     * This a helper method to create an Inviter which will execute the referral
     * of the `Session` which was referred. The appropriate headers are set and
     * the referred `Session` is linked to the new `Session`. Note that only a
     * single instance of the `Inviter` will be created and returned (if called
     * more than once a reference to the same `Inviter` will be returned every time).
     *
     * @param options - Options bucket.
     * @param modifiers - Session description handler modifiers.
     */
    makeInviter(options) {
      if (this.inviter) {
        return this.inviter;
      }
      const targetURI = this.referTo.uri.clone();
      targetURI.clearHeaders();
      options = options || {};
      const extraHeaders = (options.extraHeaders || []).slice();
      const replaces = this.replaces;
      if (replaces) {
        extraHeaders.push("Replaces: " + decodeURIComponent(replaces));
      }
      const referredBy = this.referredBy;
      if (referredBy) {
        extraHeaders.push("Referred-By: " + referredBy);
      }
      options.extraHeaders = extraHeaders;
      this.inviter = this.session.userAgent._makeInviter(targetURI, options);
      this.inviter._referred = this.session;
      this.session._referral = this.inviter;
      return this.inviter;
    }
  };

  // node_modules/sip.js/lib/api/session-state.js
  var SessionState2;
  (function(SessionState3) {
    SessionState3["Initial"] = "Initial";
    SessionState3["Establishing"] = "Establishing";
    SessionState3["Established"] = "Established";
    SessionState3["Terminating"] = "Terminating";
    SessionState3["Terminated"] = "Terminated";
  })(SessionState2 = SessionState2 || (SessionState2 = {}));

  // node_modules/sip.js/lib/api/session.js
  var Session = class _Session {
    /**
     * Constructor.
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @internal
     */
    constructor(userAgent, options = {}) {
      this.pendingReinvite = false;
      this.pendingReinviteAck = false;
      this._state = SessionState2.Initial;
      this.delegate = options.delegate;
      this._stateEventEmitter = new EmitterImpl();
      this._userAgent = userAgent;
    }
    /**
     * Destructor.
     */
    dispose() {
      this.logger.log(`Session ${this.id} in state ${this._state} is being disposed`);
      delete this.userAgent._sessions[this.id];
      if (this._sessionDescriptionHandler) {
        this._sessionDescriptionHandler.close();
      }
      switch (this.state) {
        case SessionState2.Initial:
          break;
        // the Inviter/Invitation sub class dispose method handles this case
        case SessionState2.Establishing:
          break;
        // the Inviter/Invitation sub class dispose method handles this case
        case SessionState2.Established:
          return new Promise((resolve) => {
            this._bye({
              // wait for the response to the BYE before resolving
              onAccept: () => resolve(),
              onRedirect: () => resolve(),
              onReject: () => resolve()
            });
          });
        case SessionState2.Terminating:
          break;
        // nothing to be done
        case SessionState2.Terminated:
          break;
        // nothing to be done
        default:
          throw new Error("Unknown state.");
      }
      return Promise.resolve();
    }
    /**
     * The asserted identity of the remote user.
     */
    get assertedIdentity() {
      return this._assertedIdentity;
    }
    /**
     * The confirmed session dialog.
     */
    get dialog() {
      return this._dialog;
    }
    /**
     * A unique identifier for this session.
     */
    get id() {
      return this._id;
    }
    /**
     * The session being replace by this one.
     */
    get replacee() {
      return this._replacee;
    }
    /**
     * Session description handler.
     * @remarks
     * If `this` is an instance of `Invitation`,
     * `sessionDescriptionHandler` will be defined when the session state changes to "established".
     * If `this` is an instance of `Inviter` and an offer was sent in the INVITE,
     * `sessionDescriptionHandler` will be defined when the session state changes to "establishing".
     * If `this` is an instance of `Inviter` and an offer was not sent in the INVITE,
     * `sessionDescriptionHandler` will be defined when the session state changes to "established".
     * Otherwise `undefined`.
     */
    get sessionDescriptionHandler() {
      return this._sessionDescriptionHandler;
    }
    /**
     * Session description handler factory.
     */
    get sessionDescriptionHandlerFactory() {
      return this.userAgent.configuration.sessionDescriptionHandlerFactory;
    }
    /**
     * SDH modifiers for the initial INVITE transaction.
     * @remarks
     * Used in all cases when handling the initial INVITE transaction as either UAC or UAS.
     * May be set directly at anytime.
     * May optionally be set via constructor option.
     * May optionally be set via options passed to Inviter.invite() or Invitation.accept().
     */
    get sessionDescriptionHandlerModifiers() {
      return this._sessionDescriptionHandlerModifiers || [];
    }
    set sessionDescriptionHandlerModifiers(modifiers) {
      this._sessionDescriptionHandlerModifiers = modifiers.slice();
    }
    /**
     * SDH options for the initial INVITE transaction.
     * @remarks
     * Used in all cases when handling the initial INVITE transaction as either UAC or UAS.
     * May be set directly at anytime.
     * May optionally be set via constructor option.
     * May optionally be set via options passed to Inviter.invite() or Invitation.accept().
     */
    get sessionDescriptionHandlerOptions() {
      return this._sessionDescriptionHandlerOptions || {};
    }
    set sessionDescriptionHandlerOptions(options) {
      this._sessionDescriptionHandlerOptions = Object.assign({}, options);
    }
    /**
     * SDH modifiers for re-INVITE transactions.
     * @remarks
     * Used in all cases when handling a re-INVITE transaction as either UAC or UAS.
     * May be set directly at anytime.
     * May optionally be set via constructor option.
     * May optionally be set via options passed to Session.invite().
     */
    get sessionDescriptionHandlerModifiersReInvite() {
      return this._sessionDescriptionHandlerModifiersReInvite || [];
    }
    set sessionDescriptionHandlerModifiersReInvite(modifiers) {
      this._sessionDescriptionHandlerModifiersReInvite = modifiers.slice();
    }
    /**
     * SDH options for re-INVITE transactions.
     * @remarks
     * Used in all cases when handling a re-INVITE transaction as either UAC or UAS.
     * May be set directly at anytime.
     * May optionally be set via constructor option.
     * May optionally be set via options passed to Session.invite().
     */
    get sessionDescriptionHandlerOptionsReInvite() {
      return this._sessionDescriptionHandlerOptionsReInvite || {};
    }
    set sessionDescriptionHandlerOptionsReInvite(options) {
      this._sessionDescriptionHandlerOptionsReInvite = Object.assign({}, options);
    }
    /**
     * Session state.
     */
    get state() {
      return this._state;
    }
    /**
     * Session state change emitter.
     */
    get stateChange() {
      return this._stateEventEmitter;
    }
    /**
     * The user agent.
     */
    get userAgent() {
      return this._userAgent;
    }
    /**
     * End the {@link Session}. Sends a BYE.
     * @param options - Options bucket. See {@link SessionByeOptions} for details.
     */
    bye(options = {}) {
      let message = "Session.bye() may only be called if established session.";
      switch (this.state) {
        case SessionState2.Initial:
          if (typeof this.cancel === "function") {
            message += " However Inviter.invite() has not yet been called.";
            message += " Perhaps you should have called Inviter.cancel()?";
          } else if (typeof this.reject === "function") {
            message += " However Invitation.accept() has not yet been called.";
            message += " Perhaps you should have called Invitation.reject()?";
          }
          break;
        case SessionState2.Establishing:
          if (typeof this.cancel === "function") {
            message += " However a dialog does not yet exist.";
            message += " Perhaps you should have called Inviter.cancel()?";
          } else if (typeof this.reject === "function") {
            message += " However Invitation.accept() has not yet been called (or not yet resolved).";
            message += " Perhaps you should have called Invitation.reject()?";
          }
          break;
        case SessionState2.Established: {
          const requestDelegate = options.requestDelegate;
          const requestOptions = this.copyRequestOptions(options.requestOptions);
          return this._bye(requestDelegate, requestOptions);
        }
        case SessionState2.Terminating:
          message += " However this session is already terminating.";
          if (typeof this.cancel === "function") {
            message += " Perhaps you have already called Inviter.cancel()?";
          } else if (typeof this.reject === "function") {
            message += " Perhaps you have already called Session.bye()?";
          }
          break;
        case SessionState2.Terminated:
          message += " However this session is already terminated.";
          break;
        default:
          throw new Error("Unknown state");
      }
      this.logger.error(message);
      return Promise.reject(new Error(`Invalid session state ${this.state}`));
    }
    /**
     * Share {@link Info} with peer. Sends an INFO.
     * @param options - Options bucket. See {@link SessionInfoOptions} for details.
     */
    info(options = {}) {
      if (this.state !== SessionState2.Established) {
        const message = "Session.info() may only be called if established session.";
        this.logger.error(message);
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      const requestDelegate = options.requestDelegate;
      const requestOptions = this.copyRequestOptions(options.requestOptions);
      return this._info(requestDelegate, requestOptions);
    }
    /**
     * Renegotiate the session. Sends a re-INVITE.
     * @param options - Options bucket. See {@link SessionInviteOptions} for details.
     */
    invite(options = {}) {
      this.logger.log("Session.invite");
      if (this.state !== SessionState2.Established) {
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      if (this.pendingReinvite) {
        return Promise.reject(new RequestPendingError("Reinvite in progress. Please wait until complete, then try again."));
      }
      this.pendingReinvite = true;
      if (options.sessionDescriptionHandlerModifiers) {
        this.sessionDescriptionHandlerModifiersReInvite = options.sessionDescriptionHandlerModifiers;
      }
      if (options.sessionDescriptionHandlerOptions) {
        this.sessionDescriptionHandlerOptionsReInvite = options.sessionDescriptionHandlerOptions;
      }
      const delegate = {
        onAccept: (response) => {
          const body = getBody(response.message);
          if (!body) {
            this.logger.error("Received 2xx response to re-INVITE without a session description");
            this.ackAndBye(response, 400, "Missing session description");
            this.stateTransition(SessionState2.Terminated);
            this.pendingReinvite = false;
            return;
          }
          if (options.withoutSdp) {
            const answerOptions = {
              sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptionsReInvite,
              sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiersReInvite
            };
            this.setOfferAndGetAnswer(body, answerOptions).then((answerBody) => {
              response.ack({ body: answerBody });
            }).catch((error) => {
              this.logger.error("Failed to handle offer in 2xx response to re-INVITE");
              this.logger.error(error.message);
              if (this.state === SessionState2.Terminated) {
                response.ack();
              } else {
                this.ackAndBye(response, 488, "Bad Media Description");
                this.stateTransition(SessionState2.Terminated);
              }
            }).then(() => {
              this.pendingReinvite = false;
              if (options.requestDelegate && options.requestDelegate.onAccept) {
                options.requestDelegate.onAccept(response);
              }
            });
          } else {
            const answerOptions = {
              sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptionsReInvite,
              sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiersReInvite
            };
            this.setAnswer(body, answerOptions).then(() => {
              response.ack();
            }).catch((error) => {
              this.logger.error("Failed to handle answer in 2xx response to re-INVITE");
              this.logger.error(error.message);
              if (this.state !== SessionState2.Terminated) {
                this.ackAndBye(response, 488, "Bad Media Description");
                this.stateTransition(SessionState2.Terminated);
              } else {
                response.ack();
              }
            }).then(() => {
              this.pendingReinvite = false;
              if (options.requestDelegate && options.requestDelegate.onAccept) {
                options.requestDelegate.onAccept(response);
              }
            });
          }
        },
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        onProgress: (response) => {
          return;
        },
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        onRedirect: (response) => {
          return;
        },
        onReject: (response) => {
          this.logger.warn("Received a non-2xx response to re-INVITE");
          this.pendingReinvite = false;
          if (options.withoutSdp) {
            if (options.requestDelegate && options.requestDelegate.onReject) {
              options.requestDelegate.onReject(response);
            }
          } else {
            this.rollbackOffer().catch((error) => {
              this.logger.error("Failed to rollback offer on non-2xx response to re-INVITE");
              this.logger.error(error.message);
              if (this.state !== SessionState2.Terminated) {
                if (!this.dialog) {
                  throw new Error("Dialog undefined.");
                }
                const extraHeaders = [];
                extraHeaders.push("Reason: " + this.getReasonHeaderValue(500, "Internal Server Error"));
                this.dialog.bye(void 0, { extraHeaders });
                this.stateTransition(SessionState2.Terminated);
              }
            }).then(() => {
              if (options.requestDelegate && options.requestDelegate.onReject) {
                options.requestDelegate.onReject(response);
              }
            });
          }
        },
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        onTrying: (response) => {
          return;
        }
      };
      const requestOptions = options.requestOptions || {};
      requestOptions.extraHeaders = (requestOptions.extraHeaders || []).slice();
      requestOptions.extraHeaders.push("Allow: " + AllowedMethods.toString());
      requestOptions.extraHeaders.push("Contact: " + this._contact);
      if (options.withoutSdp) {
        if (!this.dialog) {
          this.pendingReinvite = false;
          throw new Error("Dialog undefined.");
        }
        return Promise.resolve(this.dialog.invite(delegate, requestOptions));
      }
      const offerOptions = {
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptionsReInvite,
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiersReInvite
      };
      return this.getOffer(offerOptions).then((offerBody) => {
        if (!this.dialog) {
          this.pendingReinvite = false;
          throw new Error("Dialog undefined.");
        }
        requestOptions.body = offerBody;
        return this.dialog.invite(delegate, requestOptions);
      }).catch((error) => {
        this.logger.error(error.message);
        this.logger.error("Failed to send re-INVITE");
        this.pendingReinvite = false;
        throw error;
      });
    }
    /**
     * Deliver a {@link Message}. Sends a MESSAGE.
     * @param options - Options bucket. See {@link SessionMessageOptions} for details.
     */
    message(options = {}) {
      if (this.state !== SessionState2.Established) {
        const message = "Session.message() may only be called if established session.";
        this.logger.error(message);
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      const requestDelegate = options.requestDelegate;
      const requestOptions = this.copyRequestOptions(options.requestOptions);
      return this._message(requestDelegate, requestOptions);
    }
    /**
     * Proffer a {@link Referral}. Send a REFER.
     * @param referTo - The referral target. If a `Session`, a REFER w/Replaces is sent.
     * @param options - Options bucket. See {@link SessionReferOptions} for details.
     */
    refer(referTo, options = {}) {
      if (this.state !== SessionState2.Established) {
        const message = "Session.refer() may only be called if established session.";
        this.logger.error(message);
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      if (referTo instanceof _Session && !referTo.dialog) {
        const message = "Session.refer() may only be called with session which is established. You are perhaps attempting to attended transfer to a target for which there is not dialog yet established. Perhaps you are attempting a 'semi-attended' tansfer? Regardless, this is not supported. The recommended approached is to check to see if the target Session is in the Established state before calling refer(); if the state is not Established you may proceed by falling back using a URI as the target (blind transfer).";
        this.logger.error(message);
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      const requestDelegate = options.requestDelegate;
      const requestOptions = this.copyRequestOptions(options.requestOptions);
      requestOptions.extraHeaders = requestOptions.extraHeaders ? requestOptions.extraHeaders.concat(this.referExtraHeaders(this.referToString(referTo))) : this.referExtraHeaders(this.referToString(referTo));
      return this._refer(options.onNotify, requestDelegate, requestOptions);
    }
    /**
     * Send BYE.
     * @param delegate - Request delegate.
     * @param options - Request options bucket.
     * @internal
     */
    _bye(delegate, options) {
      if (!this.dialog) {
        return Promise.reject(new Error("Session dialog undefined."));
      }
      const dialog = this.dialog;
      switch (dialog.sessionState) {
        case SessionState.Initial:
          throw new Error(`Invalid dialog state ${dialog.sessionState}`);
        case SessionState.Early:
          throw new Error(`Invalid dialog state ${dialog.sessionState}`);
        case SessionState.AckWait: {
          this.stateTransition(SessionState2.Terminating);
          return new Promise((resolve) => {
            dialog.delegate = {
              // When ACK shows up, say BYE.
              onAck: () => {
                const request = dialog.bye(delegate, options);
                this.stateTransition(SessionState2.Terminated);
                resolve(request);
                return Promise.resolve();
              },
              // Or the server transaction times out before the ACK arrives.
              onAckTimeout: () => {
                const request = dialog.bye(delegate, options);
                this.stateTransition(SessionState2.Terminated);
                resolve(request);
              }
            };
          });
        }
        case SessionState.Confirmed: {
          const request = dialog.bye(delegate, options);
          this.stateTransition(SessionState2.Terminated);
          return Promise.resolve(request);
        }
        case SessionState.Terminated:
          throw new Error(`Invalid dialog state ${dialog.sessionState}`);
        default:
          throw new Error("Unrecognized state.");
      }
    }
    /**
     * Send INFO.
     * @param delegate - Request delegate.
     * @param options - Request options bucket.
     * @internal
     */
    _info(delegate, options) {
      if (!this.dialog) {
        return Promise.reject(new Error("Session dialog undefined."));
      }
      return Promise.resolve(this.dialog.info(delegate, options));
    }
    /**
     * Send MESSAGE.
     * @param delegate - Request delegate.
     * @param options - Request options bucket.
     * @internal
     */
    _message(delegate, options) {
      if (!this.dialog) {
        return Promise.reject(new Error("Session dialog undefined."));
      }
      return Promise.resolve(this.dialog.message(delegate, options));
    }
    /**
     * Send REFER.
     * @param onNotify - Notification callback.
     * @param delegate - Request delegate.
     * @param options - Request options bucket.
     * @internal
     */
    _refer(onNotify, delegate, options) {
      if (!this.dialog) {
        return Promise.reject(new Error("Session dialog undefined."));
      }
      this.onNotify = onNotify;
      return Promise.resolve(this.dialog.refer(delegate, options));
    }
    /**
     * Send ACK and then BYE. There are unrecoverable errors which can occur
     * while handling dialog forming and in-dialog INVITE responses and when
     * they occur we ACK the response and send a BYE.
     * Note that the BYE is sent in the dialog associated with the response
     * which is not necessarily `this.dialog`. And, accordingly, the
     * session state is not transitioned to terminated and session is not closed.
     * @param inviteResponse - The response causing the error.
     * @param statusCode - Status code for he reason phrase.
     * @param reasonPhrase - Reason phrase for the BYE.
     * @internal
     */
    ackAndBye(response, statusCode, reasonPhrase) {
      response.ack();
      const extraHeaders = [];
      if (statusCode) {
        extraHeaders.push("Reason: " + this.getReasonHeaderValue(statusCode, reasonPhrase));
      }
      response.session.bye(void 0, { extraHeaders });
    }
    /**
     * Handle in dialog ACK request.
     * @internal
     */
    onAckRequest(request) {
      this.logger.log("Session.onAckRequest");
      if (this.state !== SessionState2.Established && this.state !== SessionState2.Terminating) {
        this.logger.error(`ACK received while in state ${this.state}, dropping request`);
        return Promise.resolve();
      }
      const dialog = this.dialog;
      if (!dialog) {
        throw new Error("Dialog undefined.");
      }
      const answerOptions = {
        sessionDescriptionHandlerOptions: this.pendingReinviteAck ? this.sessionDescriptionHandlerOptionsReInvite : this.sessionDescriptionHandlerOptions,
        sessionDescriptionHandlerModifiers: this.pendingReinviteAck ? this._sessionDescriptionHandlerModifiersReInvite : this._sessionDescriptionHandlerModifiers
      };
      if (this.delegate && this.delegate.onAck) {
        const ack = new Ack(request);
        this.delegate.onAck(ack);
      }
      this.pendingReinviteAck = false;
      switch (dialog.signalingState) {
        case SignalingState.Initial: {
          this.logger.error(`Invalid signaling state ${dialog.signalingState}.`);
          const extraHeaders = ["Reason: " + this.getReasonHeaderValue(488, "Bad Media Description")];
          dialog.bye(void 0, { extraHeaders });
          this.stateTransition(SessionState2.Terminated);
          return Promise.resolve();
        }
        case SignalingState.Stable: {
          const body = getBody(request.message);
          if (!body) {
            return Promise.resolve();
          }
          if (body.contentDisposition === "render") {
            this._renderbody = body.content;
            this._rendertype = body.contentType;
            return Promise.resolve();
          }
          if (body.contentDisposition !== "session") {
            return Promise.resolve();
          }
          return this.setAnswer(body, answerOptions).catch((error) => {
            this.logger.error(error.message);
            const extraHeaders = ["Reason: " + this.getReasonHeaderValue(488, "Bad Media Description")];
            dialog.bye(void 0, { extraHeaders });
            this.stateTransition(SessionState2.Terminated);
          });
        }
        case SignalingState.HaveLocalOffer: {
          this.logger.error(`Invalid signaling state ${dialog.signalingState}.`);
          const extraHeaders = ["Reason: " + this.getReasonHeaderValue(488, "Bad Media Description")];
          dialog.bye(void 0, { extraHeaders });
          this.stateTransition(SessionState2.Terminated);
          return Promise.resolve();
        }
        case SignalingState.HaveRemoteOffer: {
          this.logger.error(`Invalid signaling state ${dialog.signalingState}.`);
          const extraHeaders = ["Reason: " + this.getReasonHeaderValue(488, "Bad Media Description")];
          dialog.bye(void 0, { extraHeaders });
          this.stateTransition(SessionState2.Terminated);
          return Promise.resolve();
        }
        case SignalingState.Closed:
          throw new Error(`Invalid signaling state ${dialog.signalingState}.`);
        default:
          throw new Error(`Invalid signaling state ${dialog.signalingState}.`);
      }
    }
    /**
     * Handle in dialog BYE request.
     * @internal
     */
    onByeRequest(request) {
      this.logger.log("Session.onByeRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`BYE received while in state ${this.state}, dropping request`);
        return;
      }
      if (this.delegate && this.delegate.onBye) {
        const bye = new Bye(request);
        this.delegate.onBye(bye);
      } else {
        request.accept();
      }
      this.stateTransition(SessionState2.Terminated);
    }
    /**
     * Handle in dialog INFO request.
     * @internal
     */
    onInfoRequest(request) {
      this.logger.log("Session.onInfoRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`INFO received while in state ${this.state}, dropping request`);
        return;
      }
      if (this.delegate && this.delegate.onInfo) {
        const info = new Info(request);
        this.delegate.onInfo(info);
      } else {
        request.accept();
      }
    }
    /**
     * Handle in dialog INVITE request.
     * @internal
     */
    onInviteRequest(request) {
      this.logger.log("Session.onInviteRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`INVITE received while in state ${this.state}, dropping request`);
        return;
      }
      this.pendingReinviteAck = true;
      const extraHeaders = ["Contact: " + this._contact];
      if (request.message.hasHeader("P-Asserted-Identity")) {
        const header = request.message.getHeader("P-Asserted-Identity");
        if (!header) {
          throw new Error("Header undefined.");
        }
        this._assertedIdentity = Grammar.nameAddrHeaderParse(header);
      }
      const options = {
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptionsReInvite,
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiersReInvite
      };
      this.generateResponseOfferAnswerInDialog(options).then((body) => {
        const outgoingResponse = request.accept({ statusCode: 200, extraHeaders, body });
        if (this.delegate && this.delegate.onInvite) {
          this.delegate.onInvite(request.message, outgoingResponse.message, 200);
        }
      }).catch((error) => {
        this.logger.error(error.message);
        this.logger.error("Failed to handle to re-INVITE request");
        if (!this.dialog) {
          throw new Error("Dialog undefined.");
        }
        this.logger.error(this.dialog.signalingState);
        if (this.dialog.signalingState === SignalingState.Stable) {
          const outgoingResponse = request.reject({ statusCode: 488 });
          if (this.delegate && this.delegate.onInvite) {
            this.delegate.onInvite(request.message, outgoingResponse.message, 488);
          }
          return;
        }
        this.rollbackOffer().then(() => {
          const outgoingResponse = request.reject({ statusCode: 488 });
          if (this.delegate && this.delegate.onInvite) {
            this.delegate.onInvite(request.message, outgoingResponse.message, 488);
          }
        }).catch((errorRollback) => {
          this.logger.error(errorRollback.message);
          this.logger.error("Failed to rollback offer on re-INVITE request");
          const outgoingResponse = request.reject({ statusCode: 488 });
          if (this.state !== SessionState2.Terminated) {
            if (!this.dialog) {
              throw new Error("Dialog undefined.");
            }
            const extraHeadersBye = [];
            extraHeadersBye.push("Reason: " + this.getReasonHeaderValue(500, "Internal Server Error"));
            this.dialog.bye(void 0, { extraHeaders: extraHeadersBye });
            this.stateTransition(SessionState2.Terminated);
          }
          if (this.delegate && this.delegate.onInvite) {
            this.delegate.onInvite(request.message, outgoingResponse.message, 488);
          }
        });
      });
    }
    /**
     * Handle in dialog MESSAGE request.
     * @internal
     */
    onMessageRequest(request) {
      this.logger.log("Session.onMessageRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`MESSAGE received while in state ${this.state}, dropping request`);
        return;
      }
      if (this.delegate && this.delegate.onMessage) {
        const message = new Message(request);
        this.delegate.onMessage(message);
      } else {
        request.accept();
      }
    }
    /**
     * Handle in dialog NOTIFY request.
     * @internal
     */
    onNotifyRequest(request) {
      this.logger.log("Session.onNotifyRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`NOTIFY received while in state ${this.state}, dropping request`);
        return;
      }
      if (this.onNotify) {
        const notification = new Notification(request);
        this.onNotify(notification);
        return;
      }
      if (this.delegate && this.delegate.onNotify) {
        const notification = new Notification(request);
        this.delegate.onNotify(notification);
      } else {
        request.accept();
      }
    }
    /**
     * Handle in dialog PRACK request.
     * @internal
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    onPrackRequest(request) {
      this.logger.log("Session.onPrackRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`PRACK received while in state ${this.state}, dropping request`);
        return;
      }
      throw new Error("Unimplemented.");
    }
    /**
     * Handle in dialog REFER request.
     * @internal
     */
    onReferRequest(request) {
      this.logger.log("Session.onReferRequest");
      if (this.state !== SessionState2.Established) {
        this.logger.error(`REFER received while in state ${this.state}, dropping request`);
        return;
      }
      if (!request.message.hasHeader("refer-to")) {
        this.logger.warn("Invalid REFER packet. A refer-to header is required. Rejecting.");
        request.reject();
        return;
      }
      const referral = new Referral(request, this);
      if (this.delegate && this.delegate.onRefer) {
        this.delegate.onRefer(referral);
      } else {
        this.logger.log("No delegate available to handle REFER, automatically accepting and following.");
        referral.accept().then(() => referral.makeInviter(this._referralInviterOptions).invite()).catch((error) => {
          this.logger.error(error.message);
        });
      }
    }
    /**
     * Generate an offer or answer for a response to an INVITE request.
     * If a remote offer was provided in the request, set the remote
     * description and get a local answer. If a remote offer was not
     * provided, generates a local offer.
     * @internal
     */
    generateResponseOfferAnswer(request, options) {
      if (this.dialog) {
        return this.generateResponseOfferAnswerInDialog(options);
      }
      const body = getBody(request.message);
      if (!body || body.contentDisposition !== "session") {
        return this.getOffer(options);
      } else {
        return this.setOfferAndGetAnswer(body, options);
      }
    }
    /**
     * Generate an offer or answer for a response to an INVITE request
     * when a dialog (early or otherwise) has already been established.
     * This method may NOT be called if a dialog has yet to be established.
     * @internal
     */
    generateResponseOfferAnswerInDialog(options) {
      if (!this.dialog) {
        throw new Error("Dialog undefined.");
      }
      switch (this.dialog.signalingState) {
        case SignalingState.Initial:
          return this.getOffer(options);
        case SignalingState.HaveLocalOffer:
          return Promise.resolve(void 0);
        case SignalingState.HaveRemoteOffer:
          if (!this.dialog.offer) {
            throw new Error(`Session offer undefined in signaling state ${this.dialog.signalingState}.`);
          }
          return this.setOfferAndGetAnswer(this.dialog.offer, options);
        case SignalingState.Stable:
          if (this.state !== SessionState2.Established) {
            return Promise.resolve(void 0);
          }
          return this.getOffer(options);
        case SignalingState.Closed:
          throw new Error(`Invalid signaling state ${this.dialog.signalingState}.`);
        default:
          throw new Error(`Invalid signaling state ${this.dialog.signalingState}.`);
      }
    }
    /**
     * Get local offer.
     * @internal
     */
    getOffer(options) {
      const sdh = this.setupSessionDescriptionHandler();
      const sdhOptions = options.sessionDescriptionHandlerOptions;
      const sdhModifiers = options.sessionDescriptionHandlerModifiers;
      try {
        return sdh.getDescription(sdhOptions, sdhModifiers).then((bodyAndContentType) => fromBodyLegacy(bodyAndContentType)).catch((error) => {
          this.logger.error("Session.getOffer: SDH getDescription rejected...");
          const e = error instanceof Error ? error : new Error("Session.getOffer unknown error.");
          this.logger.error(e.message);
          throw e;
        });
      } catch (error) {
        this.logger.error("Session.getOffer: SDH getDescription threw...");
        const e = error instanceof Error ? error : new Error(error);
        this.logger.error(e.message);
        return Promise.reject(e);
      }
    }
    /**
     * Rollback local/remote offer.
     * @internal
     */
    rollbackOffer() {
      const sdh = this.setupSessionDescriptionHandler();
      if (sdh.rollbackDescription === void 0) {
        return Promise.resolve();
      }
      try {
        return sdh.rollbackDescription().catch((error) => {
          this.logger.error("Session.rollbackOffer: SDH rollbackDescription rejected...");
          const e = error instanceof Error ? error : new Error("Session.rollbackOffer unknown error.");
          this.logger.error(e.message);
          throw e;
        });
      } catch (error) {
        this.logger.error("Session.rollbackOffer: SDH rollbackDescription threw...");
        const e = error instanceof Error ? error : new Error(error);
        this.logger.error(e.message);
        return Promise.reject(e);
      }
    }
    /**
     * Set remote answer.
     * @internal
     */
    setAnswer(answer, options) {
      const sdh = this.setupSessionDescriptionHandler();
      const sdhOptions = options.sessionDescriptionHandlerOptions;
      const sdhModifiers = options.sessionDescriptionHandlerModifiers;
      try {
        if (!sdh.hasDescription(answer.contentType)) {
          return Promise.reject(new ContentTypeUnsupportedError());
        }
      } catch (error) {
        this.logger.error("Session.setAnswer: SDH hasDescription threw...");
        const e = error instanceof Error ? error : new Error(error);
        this.logger.error(e.message);
        return Promise.reject(e);
      }
      try {
        return sdh.setDescription(answer.content, sdhOptions, sdhModifiers).catch((error) => {
          this.logger.error("Session.setAnswer: SDH setDescription rejected...");
          const e = error instanceof Error ? error : new Error("Session.setAnswer unknown error.");
          this.logger.error(e.message);
          throw e;
        });
      } catch (error) {
        this.logger.error("Session.setAnswer: SDH setDescription threw...");
        const e = error instanceof Error ? error : new Error(error);
        this.logger.error(e.message);
        return Promise.reject(e);
      }
    }
    /**
     * Set remote offer and get local answer.
     * @internal
     */
    setOfferAndGetAnswer(offer, options) {
      const sdh = this.setupSessionDescriptionHandler();
      const sdhOptions = options.sessionDescriptionHandlerOptions;
      const sdhModifiers = options.sessionDescriptionHandlerModifiers;
      try {
        if (!sdh.hasDescription(offer.contentType)) {
          return Promise.reject(new ContentTypeUnsupportedError());
        }
      } catch (error) {
        this.logger.error("Session.setOfferAndGetAnswer: SDH hasDescription threw...");
        const e = error instanceof Error ? error : new Error(error);
        this.logger.error(e.message);
        return Promise.reject(e);
      }
      try {
        return sdh.setDescription(offer.content, sdhOptions, sdhModifiers).then(() => sdh.getDescription(sdhOptions, sdhModifiers)).then((bodyAndContentType) => fromBodyLegacy(bodyAndContentType)).catch((error) => {
          this.logger.error("Session.setOfferAndGetAnswer: SDH setDescription or getDescription rejected...");
          const e = error instanceof Error ? error : new Error("Session.setOfferAndGetAnswer unknown error.");
          this.logger.error(e.message);
          throw e;
        });
      } catch (error) {
        this.logger.error("Session.setOfferAndGetAnswer: SDH setDescription or getDescription threw...");
        const e = error instanceof Error ? error : new Error(error);
        this.logger.error(e.message);
        return Promise.reject(e);
      }
    }
    /**
     * SDH for confirmed dialog.
     * @internal
     */
    setSessionDescriptionHandler(sdh) {
      if (this._sessionDescriptionHandler) {
        throw new Error("Session description handler defined.");
      }
      this._sessionDescriptionHandler = sdh;
    }
    /**
     * SDH for confirmed dialog.
     * @internal
     */
    setupSessionDescriptionHandler() {
      var _a;
      if (this._sessionDescriptionHandler) {
        return this._sessionDescriptionHandler;
      }
      this._sessionDescriptionHandler = this.sessionDescriptionHandlerFactory(this, this.userAgent.configuration.sessionDescriptionHandlerFactoryOptions);
      if ((_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onSessionDescriptionHandler) {
        this.delegate.onSessionDescriptionHandler(this._sessionDescriptionHandler, false);
      }
      return this._sessionDescriptionHandler;
    }
    /**
     * Transition session state.
     * @internal
     */
    stateTransition(newState) {
      const invalidTransition = () => {
        throw new Error(`Invalid state transition from ${this._state} to ${newState}`);
      };
      switch (this._state) {
        case SessionState2.Initial:
          if (newState !== SessionState2.Establishing && newState !== SessionState2.Established && newState !== SessionState2.Terminating && newState !== SessionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SessionState2.Establishing:
          if (newState !== SessionState2.Established && newState !== SessionState2.Terminating && newState !== SessionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SessionState2.Established:
          if (newState !== SessionState2.Terminating && newState !== SessionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SessionState2.Terminating:
          if (newState !== SessionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SessionState2.Terminated:
          invalidTransition();
          break;
        default:
          throw new Error("Unrecognized state.");
      }
      this._state = newState;
      this.logger.log(`Session ${this.id} transitioned to state ${this._state}`);
      this._stateEventEmitter.emit(this._state);
      if (newState === SessionState2.Terminated) {
        this.dispose();
      }
    }
    copyRequestOptions(requestOptions = {}) {
      const extraHeaders = requestOptions.extraHeaders ? requestOptions.extraHeaders.slice() : void 0;
      const body = requestOptions.body ? {
        contentDisposition: requestOptions.body.contentDisposition || "render",
        contentType: requestOptions.body.contentType || "text/plain",
        content: requestOptions.body.content || ""
      } : void 0;
      return {
        extraHeaders,
        body
      };
    }
    getReasonHeaderValue(code, reason) {
      const cause = code;
      let text = getReasonPhrase(code);
      if (!text && reason) {
        text = reason;
      }
      return "SIP;cause=" + cause + ';text="' + text + '"';
    }
    referExtraHeaders(referTo) {
      const extraHeaders = [];
      extraHeaders.push("Referred-By: <" + this.userAgent.configuration.uri + ">");
      extraHeaders.push("Contact: " + this._contact);
      extraHeaders.push("Allow: " + ["ACK", "CANCEL", "INVITE", "MESSAGE", "BYE", "OPTIONS", "INFO", "NOTIFY", "REFER"].toString());
      extraHeaders.push("Refer-To: " + referTo);
      return extraHeaders;
    }
    referToString(target) {
      let referTo;
      if (target instanceof URI) {
        referTo = target.toString();
      } else {
        if (!target.dialog) {
          throw new Error("Dialog undefined.");
        }
        const displayName = target.remoteIdentity.friendlyName;
        const remoteTarget = target.dialog.remoteTarget.toString();
        const callId = target.dialog.callId;
        const remoteTag = target.dialog.remoteTag;
        const localTag = target.dialog.localTag;
        const replaces = encodeURIComponent(`${callId};to-tag=${remoteTag};from-tag=${localTag}`);
        referTo = `"${displayName}" <${remoteTarget}?Replaces=${replaces}>`;
      }
      return referTo;
    }
  };

  // node_modules/sip.js/lib/api/user-agent-options.js
  var SIPExtension;
  (function(SIPExtension2) {
    SIPExtension2["Required"] = "Required";
    SIPExtension2["Supported"] = "Supported";
    SIPExtension2["Unsupported"] = "Unsupported";
  })(SIPExtension = SIPExtension || (SIPExtension = {}));
  var UserAgentRegisteredOptionTags = {
    "100rel": true,
    "199": true,
    answermode: true,
    "early-session": true,
    eventlist: true,
    explicitsub: true,
    "from-change": true,
    "geolocation-http": true,
    "geolocation-sip": true,
    gin: true,
    gruu: true,
    histinfo: true,
    ice: true,
    join: true,
    "multiple-refer": true,
    norefersub: true,
    nosub: true,
    outbound: true,
    path: true,
    policy: true,
    precondition: true,
    pref: true,
    privacy: true,
    "recipient-list-invite": true,
    "recipient-list-message": true,
    "recipient-list-subscribe": true,
    replaces: true,
    "resource-priority": true,
    "sdp-anat": true,
    "sec-agree": true,
    tdialog: true,
    timer: true,
    uui: true
    // RFC 7433
  };

  // node_modules/sip.js/lib/api/invitation.js
  var Invitation = class extends Session {
    /** @internal */
    constructor(userAgent, incomingInviteRequest) {
      super(userAgent);
      this.incomingInviteRequest = incomingInviteRequest;
      this.disposed = false;
      this.expiresTimer = void 0;
      this.isCanceled = false;
      this.rel100 = "none";
      this.rseq = Math.floor(Math.random() * 1e4);
      this.userNoAnswerTimer = void 0;
      this.waitingForPrack = false;
      this.logger = userAgent.getLogger("sip.Invitation");
      const incomingRequestMessage = this.incomingInviteRequest.message;
      const requireHeader = incomingRequestMessage.getHeader("require");
      if (requireHeader && requireHeader.toLowerCase().includes("100rel")) {
        this.rel100 = "required";
      }
      const supportedHeader = incomingRequestMessage.getHeader("supported");
      if (supportedHeader && supportedHeader.toLowerCase().includes("100rel")) {
        this.rel100 = "supported";
      }
      incomingRequestMessage.toTag = incomingInviteRequest.toTag;
      if (typeof incomingRequestMessage.toTag !== "string") {
        throw new TypeError("toTag should have been a string.");
      }
      this.userNoAnswerTimer = setTimeout(() => {
        incomingInviteRequest.reject({ statusCode: 480 });
        this.stateTransition(SessionState2.Terminated);
      }, this.userAgent.configuration.noAnswerTimeout ? this.userAgent.configuration.noAnswerTimeout * 1e3 : 6e4);
      if (incomingRequestMessage.hasHeader("expires")) {
        const expires = Number(incomingRequestMessage.getHeader("expires") || 0) * 1e3;
        this.expiresTimer = setTimeout(() => {
          if (this.state === SessionState2.Initial) {
            incomingInviteRequest.reject({ statusCode: 487 });
            this.stateTransition(SessionState2.Terminated);
          }
        }, expires);
      }
      const assertedIdentity = this.request.getHeader("P-Asserted-Identity");
      if (assertedIdentity) {
        this._assertedIdentity = Grammar.nameAddrHeaderParse(assertedIdentity);
      }
      this._contact = this.userAgent.contact.toString();
      const contentDisposition = incomingRequestMessage.parseHeader("Content-Disposition");
      if (contentDisposition && contentDisposition.type === "render") {
        this._renderbody = incomingRequestMessage.body;
        this._rendertype = incomingRequestMessage.getHeader("Content-Type");
      }
      this._id = incomingRequestMessage.callId + incomingRequestMessage.fromTag;
      this.userAgent._sessions[this._id] = this;
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this.disposed) {
        return Promise.resolve();
      }
      this.disposed = true;
      if (this.expiresTimer) {
        clearTimeout(this.expiresTimer);
        this.expiresTimer = void 0;
      }
      if (this.userNoAnswerTimer) {
        clearTimeout(this.userNoAnswerTimer);
        this.userNoAnswerTimer = void 0;
      }
      this.prackNeverArrived();
      switch (this.state) {
        case SessionState2.Initial:
          return this.reject().then(() => super.dispose());
        case SessionState2.Establishing:
          return this.reject().then(() => super.dispose());
        case SessionState2.Established:
          return super.dispose();
        case SessionState2.Terminating:
          return super.dispose();
        case SessionState2.Terminated:
          return super.dispose();
        default:
          throw new Error("Unknown state.");
      }
    }
    /**
     * If true, a first provisional response after the 100 Trying
     * will be sent automatically. This is false it the UAC required
     * reliable provisional responses (100rel in Require header) or
     * the user agent configuration has specified to not send an
     * initial response, otherwise it is true. The provisional is sent by
     * calling `progress()` without any options.
     */
    get autoSendAnInitialProvisionalResponse() {
      return this.rel100 !== "required" && this.userAgent.configuration.sendInitialProvisionalResponse;
    }
    /**
     * Initial incoming INVITE request message body.
     */
    get body() {
      return this.incomingInviteRequest.message.body;
    }
    /**
     * The identity of the local user.
     */
    get localIdentity() {
      return this.request.to;
    }
    /**
     * The identity of the remote user.
     */
    get remoteIdentity() {
      return this.request.from;
    }
    /**
     * Initial incoming INVITE request message.
     */
    get request() {
      return this.incomingInviteRequest.message;
    }
    /**
     * Accept the invitation.
     *
     * @remarks
     * Accept the incoming INVITE request to start a Session.
     * Replies to the INVITE request with a 200 Ok response.
     * Resolves once the response sent, otherwise rejects.
     *
     * This method may reject for a variety of reasons including
     * the receipt of a CANCEL request before `accept` is able
     * to construct a response.
     * @param options - Options bucket.
     */
    accept(options = {}) {
      this.logger.log("Invitation.accept");
      if (this.state !== SessionState2.Initial) {
        const error = new Error(`Invalid session state ${this.state}`);
        this.logger.error(error.message);
        return Promise.reject(error);
      }
      if (options.sessionDescriptionHandlerModifiers) {
        this.sessionDescriptionHandlerModifiers = options.sessionDescriptionHandlerModifiers;
      }
      if (options.sessionDescriptionHandlerOptions) {
        this.sessionDescriptionHandlerOptions = options.sessionDescriptionHandlerOptions;
      }
      this.stateTransition(SessionState2.Establishing);
      return this.sendAccept(options).then(({ message, session }) => {
        session.delegate = {
          onAck: (ackRequest) => this.onAckRequest(ackRequest),
          onAckTimeout: () => this.onAckTimeout(),
          onBye: (byeRequest) => this.onByeRequest(byeRequest),
          onInfo: (infoRequest) => this.onInfoRequest(infoRequest),
          onInvite: (inviteRequest) => this.onInviteRequest(inviteRequest),
          onMessage: (messageRequest) => this.onMessageRequest(messageRequest),
          onNotify: (notifyRequest) => this.onNotifyRequest(notifyRequest),
          onPrack: (prackRequest) => this.onPrackRequest(prackRequest),
          onRefer: (referRequest) => this.onReferRequest(referRequest)
        };
        this._dialog = session;
        this.stateTransition(SessionState2.Established);
        if (this._replacee) {
          this._replacee._bye();
        }
      }).catch((error) => this.handleResponseError(error));
    }
    /**
     * Indicate progress processing the invitation.
     *
     * @remarks
     * Report progress to the the caller.
     * Replies to the INVITE request with a 1xx provisional response.
     * Resolves once the response sent, otherwise rejects.
     * @param options - Options bucket.
     */
    progress(options = {}) {
      this.logger.log("Invitation.progress");
      if (this.state !== SessionState2.Initial) {
        const error = new Error(`Invalid session state ${this.state}`);
        this.logger.error(error.message);
        return Promise.reject(error);
      }
      const statusCode = options.statusCode || 180;
      if (statusCode < 100 || statusCode > 199) {
        throw new TypeError("Invalid statusCode: " + statusCode);
      }
      if (options.sessionDescriptionHandlerModifiers) {
        this.sessionDescriptionHandlerModifiers = options.sessionDescriptionHandlerModifiers;
      }
      if (options.sessionDescriptionHandlerOptions) {
        this.sessionDescriptionHandlerOptions = options.sessionDescriptionHandlerOptions;
      }
      if (this.waitingForPrack) {
        this.logger.warn("Unexpected call for progress while waiting for prack, ignoring");
        return Promise.resolve();
      }
      if (options.statusCode === 100) {
        return this.sendProgressTrying().then(() => {
          return;
        }).catch((error) => this.handleResponseError(error));
      }
      if (!(this.rel100 === "required") && !(this.rel100 === "supported" && options.rel100) && !(this.rel100 === "supported" && this.userAgent.configuration.sipExtension100rel === SIPExtension.Required)) {
        return this.sendProgress(options).then(() => {
          return;
        }).catch((error) => this.handleResponseError(error));
      }
      return this.sendProgressReliableWaitForPrack(options).then(() => {
        return;
      }).catch((error) => this.handleResponseError(error));
    }
    /**
     * Reject the invitation.
     *
     * @remarks
     * Replies to the INVITE request with a 4xx, 5xx, or 6xx final response.
     * Resolves once the response sent, otherwise rejects.
     *
     * The expectation is that this method is used to reject an INVITE request.
     * That is indeed the case - a call to `progress` followed by `reject` is
     * a typical way to "decline" an incoming INVITE request. However it may
     * also be called after calling `accept` (but only before it completes)
     * which will reject the call and cause `accept` to reject.
     * @param options - Options bucket.
     */
    reject(options = {}) {
      this.logger.log("Invitation.reject");
      if (this.state !== SessionState2.Initial && this.state !== SessionState2.Establishing) {
        const error = new Error(`Invalid session state ${this.state}`);
        this.logger.error(error.message);
        return Promise.reject(error);
      }
      const statusCode = options.statusCode || 480;
      const reasonPhrase = options.reasonPhrase ? options.reasonPhrase : getReasonPhrase(statusCode);
      const extraHeaders = options.extraHeaders || [];
      if (statusCode < 300 || statusCode > 699) {
        throw new TypeError("Invalid statusCode: " + statusCode);
      }
      const body = options.body ? fromBodyLegacy(options.body) : void 0;
      statusCode < 400 ? this.incomingInviteRequest.redirect([], { statusCode, reasonPhrase, extraHeaders, body }) : this.incomingInviteRequest.reject({ statusCode, reasonPhrase, extraHeaders, body });
      this.stateTransition(SessionState2.Terminated);
      return Promise.resolve();
    }
    /**
     * Handle CANCEL request.
     *
     * @param message - CANCEL message.
     * @internal
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    _onCancel(message) {
      this.logger.log("Invitation._onCancel");
      if (this.state !== SessionState2.Initial && this.state !== SessionState2.Establishing) {
        this.logger.error(`CANCEL received while in state ${this.state}, dropping request`);
        return;
      }
      if (this.delegate && this.delegate.onCancel) {
        const cancel = new Cancel(message);
        this.delegate.onCancel(cancel);
      }
      this.isCanceled = true;
      this.incomingInviteRequest.reject({ statusCode: 487 });
      this.stateTransition(SessionState2.Terminated);
    }
    /**
     * Helper function to handle offer/answer in a PRACK.
     */
    handlePrackOfferAnswer(request) {
      if (!this.dialog) {
        throw new Error("Dialog undefined.");
      }
      const body = getBody(request.message);
      if (!body || body.contentDisposition !== "session") {
        return Promise.resolve(void 0);
      }
      const options = {
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions,
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers
      };
      switch (this.dialog.signalingState) {
        case SignalingState.Initial:
          throw new Error(`Invalid signaling state ${this.dialog.signalingState}.`);
        case SignalingState.Stable:
          return this.setAnswer(body, options).then(() => void 0);
        case SignalingState.HaveLocalOffer:
          throw new Error(`Invalid signaling state ${this.dialog.signalingState}.`);
        case SignalingState.HaveRemoteOffer:
          return this.setOfferAndGetAnswer(body, options);
        case SignalingState.Closed:
          throw new Error(`Invalid signaling state ${this.dialog.signalingState}.`);
        default:
          throw new Error(`Invalid signaling state ${this.dialog.signalingState}.`);
      }
    }
    /**
     * A handler for errors which occur while attempting to send 1xx and 2xx responses.
     * In all cases, an attempt is made to reject the request if it is still outstanding.
     * And while there are a variety of things which can go wrong and we log something here
     * for all errors, there are a handful of common exceptions we pay some extra attention to.
     * @param error - The error which occurred.
     */
    handleResponseError(error) {
      let statusCode = 480;
      if (error instanceof Error) {
        this.logger.error(error.message);
      } else {
        this.logger.error(error);
      }
      if (error instanceof ContentTypeUnsupportedError) {
        this.logger.error("A session description handler occurred while sending response (content type unsupported");
        statusCode = 415;
      } else if (error instanceof SessionDescriptionHandlerError) {
        this.logger.error("A session description handler occurred while sending response");
      } else if (error instanceof SessionTerminatedError) {
        this.logger.error("Session ended before response could be formulated and sent (while waiting for PRACK)");
      } else if (error instanceof TransactionStateError) {
        this.logger.error("Session changed state before response could be formulated and sent");
      }
      if (this.state === SessionState2.Initial || this.state === SessionState2.Establishing) {
        try {
          this.incomingInviteRequest.reject({ statusCode });
          this.stateTransition(SessionState2.Terminated);
        } catch (e) {
          this.logger.error("An error occurred attempting to reject the request while handling another error");
          throw e;
        }
      }
      if (this.isCanceled) {
        this.logger.warn("An error occurred while attempting to formulate and send a response to an incoming INVITE. However a CANCEL was received and processed while doing so which can (and often does) result in errors occurring as the session terminates in the meantime. Said error is being ignored.");
        return;
      }
      throw error;
    }
    /**
     * Callback for when ACK for a 2xx response is never received.
     * @param session - Session the ACK never arrived for.
     */
    onAckTimeout() {
      this.logger.log("Invitation.onAckTimeout");
      if (!this.dialog) {
        throw new Error("Dialog undefined.");
      }
      this.logger.log("No ACK received for an extended period of time, terminating session");
      this.dialog.bye();
      this.stateTransition(SessionState2.Terminated);
    }
    /**
     * A version of `accept` which resolves a session when the 200 Ok response is sent.
     * @param options - Options bucket.
     */
    sendAccept(options = {}) {
      const responseOptions = {
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions,
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers
      };
      const extraHeaders = options.extraHeaders || [];
      if (this.waitingForPrack) {
        return this.waitForArrivalOfPrack().then(() => clearTimeout(this.userNoAnswerTimer)).then(() => this.generateResponseOfferAnswer(this.incomingInviteRequest, responseOptions)).then((body) => this.incomingInviteRequest.accept({ statusCode: 200, body, extraHeaders }));
      }
      clearTimeout(this.userNoAnswerTimer);
      return this.generateResponseOfferAnswer(this.incomingInviteRequest, responseOptions).then((body) => this.incomingInviteRequest.accept({ statusCode: 200, body, extraHeaders }));
    }
    /**
     * A version of `progress` which resolves when the provisional response is sent.
     * @param options - Options bucket.
     */
    sendProgress(options = {}) {
      const statusCode = options.statusCode || 180;
      const reasonPhrase = options.reasonPhrase;
      const extraHeaders = (options.extraHeaders || []).slice();
      const body = options.body ? fromBodyLegacy(options.body) : void 0;
      if (statusCode === 183 && !body) {
        return this.sendProgressWithSDP(options);
      }
      try {
        const progressResponse = this.incomingInviteRequest.progress({ statusCode, reasonPhrase, extraHeaders, body });
        this._dialog = progressResponse.session;
        return Promise.resolve(progressResponse);
      } catch (error) {
        return Promise.reject(error);
      }
    }
    /**
     * A version of `progress` which resolves when the provisional response with sdp is sent.
     * @param options - Options bucket.
     */
    sendProgressWithSDP(options = {}) {
      const responseOptions = {
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions,
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers
      };
      const statusCode = options.statusCode || 183;
      const reasonPhrase = options.reasonPhrase;
      const extraHeaders = (options.extraHeaders || []).slice();
      return this.generateResponseOfferAnswer(this.incomingInviteRequest, responseOptions).then((body) => this.incomingInviteRequest.progress({ statusCode, reasonPhrase, extraHeaders, body })).then((progressResponse) => {
        this._dialog = progressResponse.session;
        return progressResponse;
      });
    }
    /**
     * A version of `progress` which resolves when the reliable provisional response is sent.
     * @param options - Options bucket.
     */
    sendProgressReliable(options = {}) {
      options.extraHeaders = (options.extraHeaders || []).slice();
      options.extraHeaders.push("Require: 100rel");
      options.extraHeaders.push("RSeq: " + Math.floor(Math.random() * 1e4));
      return this.sendProgressWithSDP(options);
    }
    /**
     * A version of `progress` which resolves when the reliable provisional response is acknowledged.
     * @param options - Options bucket.
     */
    sendProgressReliableWaitForPrack(options = {}) {
      const responseOptions = {
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions,
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers
      };
      const statusCode = options.statusCode || 183;
      const reasonPhrase = options.reasonPhrase;
      const extraHeaders = (options.extraHeaders || []).slice();
      extraHeaders.push("Require: 100rel");
      extraHeaders.push("RSeq: " + this.rseq++);
      let body;
      return new Promise((resolve, reject) => {
        this.waitingForPrack = true;
        this.generateResponseOfferAnswer(this.incomingInviteRequest, responseOptions).then((offerAnswer) => {
          body = offerAnswer;
          return this.incomingInviteRequest.progress({ statusCode, reasonPhrase, extraHeaders, body });
        }).then((progressResponse) => {
          this._dialog = progressResponse.session;
          let prackRequest;
          let prackResponse;
          progressResponse.session.delegate = {
            onPrack: (request) => {
              prackRequest = request;
              clearTimeout(prackWaitTimeoutTimer);
              clearTimeout(rel1xxRetransmissionTimer);
              if (!this.waitingForPrack) {
                return;
              }
              this.waitingForPrack = false;
              this.handlePrackOfferAnswer(prackRequest).then((prackResponseBody) => {
                try {
                  prackResponse = prackRequest.accept({ statusCode: 200, body: prackResponseBody });
                  this.prackArrived();
                  resolve({ prackRequest, prackResponse, progressResponse });
                } catch (error) {
                  reject(error);
                }
              }).catch((error) => reject(error));
            }
          };
          const prackWaitTimeout = () => {
            if (!this.waitingForPrack) {
              return;
            }
            this.waitingForPrack = false;
            this.logger.warn("No PRACK received, rejecting INVITE.");
            clearTimeout(rel1xxRetransmissionTimer);
            this.reject({ statusCode: 504 }).then(() => reject(new SessionTerminatedError())).catch((error) => reject(error));
          };
          const prackWaitTimeoutTimer = setTimeout(prackWaitTimeout, Timers.T1 * 64);
          const rel1xxRetransmission = () => {
            try {
              this.incomingInviteRequest.progress({ statusCode, reasonPhrase, extraHeaders, body });
            } catch (error) {
              this.waitingForPrack = false;
              reject(error);
              return;
            }
            rel1xxRetransmissionTimer = setTimeout(rel1xxRetransmission, timeout *= 2);
          };
          let timeout = Timers.T1;
          let rel1xxRetransmissionTimer = setTimeout(rel1xxRetransmission, timeout);
        }).catch((error) => {
          this.waitingForPrack = false;
          reject(error);
        });
      });
    }
    /**
     * A version of `progress` which resolves when a 100 Trying provisional response is sent.
     */
    sendProgressTrying() {
      try {
        const progressResponse = this.incomingInviteRequest.trying();
        return Promise.resolve(progressResponse);
      } catch (error) {
        return Promise.reject(error);
      }
    }
    /**
     * When attempting to accept the INVITE, an invitation waits
     * for any outstanding PRACK to arrive before sending the 200 Ok.
     * It will be waiting on this Promise to resolve which lets it know
     * the PRACK has arrived and it may proceed to send the 200 Ok.
     */
    waitForArrivalOfPrack() {
      if (this.waitingForPrackPromise) {
        throw new Error("Already waiting for PRACK");
      }
      this.waitingForPrackPromise = new Promise((resolve, reject) => {
        this.waitingForPrackResolve = resolve;
        this.waitingForPrackReject = reject;
      });
      return this.waitingForPrackPromise;
    }
    /**
     * Here we are resolving the promise which in turn will cause
     * the accept to proceed (it may still fail for other reasons, but...).
     */
    prackArrived() {
      if (this.waitingForPrackResolve) {
        this.waitingForPrackResolve();
      }
      this.waitingForPrackPromise = void 0;
      this.waitingForPrackResolve = void 0;
      this.waitingForPrackReject = void 0;
    }
    /**
     * Here we are rejecting the promise which in turn will cause
     * the accept to fail and the session to transition to "terminated".
     */
    prackNeverArrived() {
      if (this.waitingForPrackReject) {
        this.waitingForPrackReject(new SessionTerminatedError());
      }
      this.waitingForPrackPromise = void 0;
      this.waitingForPrackResolve = void 0;
      this.waitingForPrackReject = void 0;
    }
  };

  // node_modules/sip.js/lib/api/inviter.js
  var Inviter = class extends Session {
    /**
     * Constructs a new instance of the `Inviter` class.
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @param targetURI - Request URI identifying the target of the message.
     * @param options - Options bucket. See {@link InviterOptions} for details.
     */
    constructor(userAgent, targetURI, options = {}) {
      super(userAgent, options);
      this.disposed = false;
      this.earlyMedia = false;
      this.earlyMediaSessionDescriptionHandlers = /* @__PURE__ */ new Map();
      this.isCanceled = false;
      this.inviteWithoutSdp = false;
      this.logger = userAgent.getLogger("sip.Inviter");
      this.earlyMedia = options.earlyMedia !== void 0 ? options.earlyMedia : this.earlyMedia;
      this.fromTag = newTag();
      this.inviteWithoutSdp = options.inviteWithoutSdp !== void 0 ? options.inviteWithoutSdp : this.inviteWithoutSdp;
      const inviterOptions = Object.assign({}, options);
      inviterOptions.params = Object.assign({}, options.params);
      const anonymous = options.anonymous || false;
      const contact = userAgent.contact.toString({
        anonymous,
        // Do not add ;ob in initial forming dialog requests if the
        // registration over the current connection got a GRUU URI.
        outbound: anonymous ? !userAgent.contact.tempGruu : !userAgent.contact.pubGruu
      });
      if (anonymous && userAgent.configuration.uri) {
        inviterOptions.params.fromDisplayName = "Anonymous";
        inviterOptions.params.fromUri = "sip:anonymous@anonymous.invalid";
      }
      let fromURI = userAgent.userAgentCore.configuration.aor;
      if (inviterOptions.params.fromUri) {
        fromURI = typeof inviterOptions.params.fromUri === "string" ? Grammar.URIParse(inviterOptions.params.fromUri) : inviterOptions.params.fromUri;
      }
      if (!fromURI) {
        throw new TypeError("Invalid from URI: " + inviterOptions.params.fromUri);
      }
      let toURI = targetURI;
      if (inviterOptions.params.toUri) {
        toURI = typeof inviterOptions.params.toUri === "string" ? Grammar.URIParse(inviterOptions.params.toUri) : inviterOptions.params.toUri;
      }
      if (!toURI) {
        throw new TypeError("Invalid to URI: " + inviterOptions.params.toUri);
      }
      const messageOptions = Object.assign({}, inviterOptions.params);
      messageOptions.fromTag = this.fromTag;
      const extraHeaders = (inviterOptions.extraHeaders || []).slice();
      if (anonymous && userAgent.configuration.uri) {
        extraHeaders.push("P-Preferred-Identity: " + userAgent.configuration.uri.toString());
        extraHeaders.push("Privacy: id");
      }
      extraHeaders.push("Contact: " + contact);
      extraHeaders.push("Allow: " + ["ACK", "CANCEL", "INVITE", "MESSAGE", "BYE", "OPTIONS", "INFO", "NOTIFY", "REFER"].toString());
      if (userAgent.configuration.sipExtension100rel === SIPExtension.Required) {
        extraHeaders.push("Require: 100rel");
      }
      if (userAgent.configuration.sipExtensionReplaces === SIPExtension.Required) {
        extraHeaders.push("Require: replaces");
      }
      inviterOptions.extraHeaders = extraHeaders;
      const body = void 0;
      this.outgoingRequestMessage = userAgent.userAgentCore.makeOutgoingRequestMessage(C.INVITE, targetURI, fromURI, toURI, messageOptions, extraHeaders, body);
      this._contact = contact;
      this._referralInviterOptions = inviterOptions;
      this._renderbody = options.renderbody;
      this._rendertype = options.rendertype;
      if (options.sessionDescriptionHandlerModifiers) {
        this.sessionDescriptionHandlerModifiers = options.sessionDescriptionHandlerModifiers;
      }
      if (options.sessionDescriptionHandlerOptions) {
        this.sessionDescriptionHandlerOptions = options.sessionDescriptionHandlerOptions;
      }
      if (options.sessionDescriptionHandlerModifiersReInvite) {
        this.sessionDescriptionHandlerModifiersReInvite = options.sessionDescriptionHandlerModifiersReInvite;
      }
      if (options.sessionDescriptionHandlerOptionsReInvite) {
        this.sessionDescriptionHandlerOptionsReInvite = options.sessionDescriptionHandlerOptionsReInvite;
      }
      this._id = this.outgoingRequestMessage.callId + this.fromTag;
      this.userAgent._sessions[this._id] = this;
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this.disposed) {
        return Promise.resolve();
      }
      this.disposed = true;
      this.disposeEarlyMedia();
      switch (this.state) {
        case SessionState2.Initial:
          return this.cancel().then(() => super.dispose());
        case SessionState2.Establishing:
          return this.cancel().then(() => super.dispose());
        case SessionState2.Established:
          return super.dispose();
        case SessionState2.Terminating:
          return super.dispose();
        case SessionState2.Terminated:
          return super.dispose();
        default:
          throw new Error("Unknown state.");
      }
    }
    /**
     * Initial outgoing INVITE request message body.
     */
    get body() {
      return this.outgoingRequestMessage.body;
    }
    /**
     * The identity of the local user.
     */
    get localIdentity() {
      return this.outgoingRequestMessage.from;
    }
    /**
     * The identity of the remote user.
     */
    get remoteIdentity() {
      return this.outgoingRequestMessage.to;
    }
    /**
     * Initial outgoing INVITE request message.
     */
    get request() {
      return this.outgoingRequestMessage;
    }
    /**
     * Cancels the INVITE request.
     *
     * @remarks
     * Sends a CANCEL request.
     * Resolves once the response sent, otherwise rejects.
     *
     * After sending a CANCEL request the expectation is that a 487 final response
     * will be received for the INVITE. However a 200 final response to the INVITE
     * may nonetheless arrive (it's a race between the CANCEL reaching the UAS before
     * the UAS sends a 200) in which case an ACK & BYE will be sent. The net effect
     * is that this method will terminate the session regardless of the race.
     * @param options - Options bucket.
     */
    cancel(options = {}) {
      this.logger.log("Inviter.cancel");
      if (this.state !== SessionState2.Initial && this.state !== SessionState2.Establishing) {
        const error = new Error(`Invalid session state ${this.state}`);
        this.logger.error(error.message);
        return Promise.reject(error);
      }
      this.isCanceled = true;
      this.stateTransition(SessionState2.Terminating);
      function getCancelReason(code, reason) {
        if (code && code < 200 || code > 699) {
          throw new TypeError("Invalid statusCode: " + code);
        } else if (code) {
          const cause = code;
          const text = getReasonPhrase(code) || reason;
          return "SIP;cause=" + cause + ';text="' + text + '"';
        }
      }
      if (this.outgoingInviteRequest) {
        let cancelReason;
        if (options.statusCode && options.reasonPhrase) {
          cancelReason = getCancelReason(options.statusCode, options.reasonPhrase);
        }
        this.outgoingInviteRequest.cancel(cancelReason, options);
      } else {
        this.logger.warn("Canceled session before INVITE was sent");
        this.stateTransition(SessionState2.Terminated);
      }
      return Promise.resolve();
    }
    /**
     * Sends the INVITE request.
     *
     * @remarks
     * TLDR...
     *  1) Only one offer/answer exchange permitted during initial INVITE.
     *  2) No "early media" if the initial offer is in an INVITE (default behavior).
     *  3) If "early media" and the initial offer is in an INVITE, no INVITE forking.
     *
     * 1) Only one offer/answer exchange permitted during initial INVITE.
     *
     * Our implementation replaces the following bullet point...
     *
     * o  After having sent or received an answer to the first offer, the
     *    UAC MAY generate subsequent offers in requests based on rules
     *    specified for that method, but only if it has received answers
     *    to any previous offers, and has not sent any offers to which it
     *    hasn't gotten an answer.
     * https://tools.ietf.org/html/rfc3261#section-13.2.1
     *
     * ...with...
     *
     * o  After having sent or received an answer to the first offer, the
     *    UAC MUST NOT generate subsequent offers in requests based on rules
     *    specified for that method.
     *
     * ...which in combination with this bullet point...
     *
     * o  Once the UAS has sent or received an answer to the initial
     *    offer, it MUST NOT generate subsequent offers in any responses
     *    to the initial INVITE.  This means that a UAS based on this
     *    specification alone can never generate subsequent offers until
     *    completion of the initial transaction.
     * https://tools.ietf.org/html/rfc3261#section-13.2.1
     *
     * ...ensures that EXACTLY ONE offer/answer exchange will occur
     * during an initial out of dialog INVITE request made by our UAC.
     *
     *
     * 2) No "early media" if the initial offer is in an INVITE (default behavior).
     *
     * While our implementation adheres to the following bullet point...
     *
     * o  If the initial offer is in an INVITE, the answer MUST be in a
     *    reliable non-failure message from UAS back to UAC which is
     *    correlated to that INVITE.  For this specification, that is
     *    only the final 2xx response to that INVITE.  That same exact
     *    answer MAY also be placed in any provisional responses sent
     *    prior to the answer.  The UAC MUST treat the first session
     *    description it receives as the answer, and MUST ignore any
     *    session descriptions in subsequent responses to the initial
     *    INVITE.
     * https://tools.ietf.org/html/rfc3261#section-13.2.1
     *
     * We have made the following implementation decision with regard to early media...
     *
     * o  If the initial offer is in the INVITE, the answer from the
     *    UAS back to the UAC will establish a media session only
     *    only after the final 2xx response to that INVITE is received.
     *
     * The reason for this decision is rooted in a restriction currently
     * inherent in WebRTC. Specifically, while a SIP INVITE request with an
     * initial offer may fork resulting in more than one provisional answer,
     * there is currently no easy/good way to to "fork" an offer generated
     * by a peer connection. In particular, a WebRTC offer currently may only
     * be matched with one answer and we have no good way to know which
     * "provisional answer" is going to be the "final answer". So we have
     * decided to punt and not create any "early media" sessions in this case.
     *
     * The upshot is that if you want "early media", you must not put the
     * initial offer in the INVITE. Instead, force the UAS to provide the
     * initial offer by sending an INVITE without an offer. In the WebRTC
     * case this allows us to create a unique peer connection with a unique
     * answer for every provisional offer with "early media" on all of them.
     *
     *
     * 3) If "early media" and the initial offer is in an INVITE, no INVITE forking.
     *
     * The default behavior may be altered and "early media" utilized if the
     * initial offer is in the an INVITE by setting the `earlyMedia` options.
     * However in that case the INVITE request MUST NOT fork. This allows for
     * "early media" in environments where the forking behavior of the SIP
     * servers being utilized is configured to disallow forking.
     */
    invite(options = {}) {
      this.logger.log("Inviter.invite");
      if (this.state !== SessionState2.Initial) {
        return super.invite(options);
      }
      if (options.sessionDescriptionHandlerModifiers) {
        this.sessionDescriptionHandlerModifiers = options.sessionDescriptionHandlerModifiers;
      }
      if (options.sessionDescriptionHandlerOptions) {
        this.sessionDescriptionHandlerOptions = options.sessionDescriptionHandlerOptions;
      }
      if (options.withoutSdp || this.inviteWithoutSdp) {
        if (this._renderbody && this._rendertype) {
          this.outgoingRequestMessage.body = { contentType: this._rendertype, body: this._renderbody };
        }
        this.stateTransition(SessionState2.Establishing);
        return Promise.resolve(this.sendInvite(options));
      }
      const offerOptions = {
        sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers,
        sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions
      };
      return this.getOffer(offerOptions).then((body) => {
        this.outgoingRequestMessage.body = { body: body.content, contentType: body.contentType };
        this.stateTransition(SessionState2.Establishing);
        return this.sendInvite(options);
      }).catch((error) => {
        this.logger.log(error.message);
        if (this.state !== SessionState2.Terminated) {
          this.stateTransition(SessionState2.Terminated);
        }
        throw error;
      });
    }
    /**
     * 13.2.1 Creating the Initial INVITE
     *
     * Since the initial INVITE represents a request outside of a dialog,
     * its construction follows the procedures of Section 8.1.1.  Additional
     * processing is required for the specific case of INVITE.
     *
     * An Allow header field (Section 20.5) SHOULD be present in the INVITE.
     * It indicates what methods can be invoked within a dialog, on the UA
     * sending the INVITE, for the duration of the dialog.  For example, a
     * UA capable of receiving INFO requests within a dialog [34] SHOULD
     * include an Allow header field listing the INFO method.
     *
     * A Supported header field (Section 20.37) SHOULD be present in the
     * INVITE.  It enumerates all the extensions understood by the UAC.
     *
     * An Accept (Section 20.1) header field MAY be present in the INVITE.
     * It indicates which Content-Types are acceptable to the UA, in both
     * the response received by it, and in any subsequent requests sent to
     * it within dialogs established by the INVITE.  The Accept header field
     * is especially useful for indicating support of various session
     * description formats.
     *
     * The UAC MAY add an Expires header field (Section 20.19) to limit the
     * validity of the invitation.  If the time indicated in the Expires
     * header field is reached and no final answer for the INVITE has been
     * received, the UAC core SHOULD generate a CANCEL request for the
     * INVITE, as per Section 9.
     *
     * A UAC MAY also find it useful to add, among others, Subject (Section
     * 20.36), Organization (Section 20.25) and User-Agent (Section 20.41)
     * header fields.  They all contain information related to the INVITE.
     *
     * The UAC MAY choose to add a message body to the INVITE.  Section
     * 8.1.1.10 deals with how to construct the header fields -- Content-
     * Type among others -- needed to describe the message body.
     *
     * https://tools.ietf.org/html/rfc3261#section-13.2.1
     */
    sendInvite(options = {}) {
      this.outgoingInviteRequest = this.userAgent.userAgentCore.invite(this.outgoingRequestMessage, {
        onAccept: (inviteResponse) => {
          if (this.dialog) {
            this.logger.log("Additional confirmed dialog, sending ACK and BYE");
            this.ackAndBye(inviteResponse);
            return;
          }
          if (this.isCanceled) {
            this.logger.log("Canceled session accepted, sending ACK and BYE");
            this.ackAndBye(inviteResponse);
            this.stateTransition(SessionState2.Terminated);
            return;
          }
          this.notifyReferer(inviteResponse);
          this.onAccept(inviteResponse).then(() => {
            this.disposeEarlyMedia();
          }).catch(() => {
            this.disposeEarlyMedia();
          }).then(() => {
            if (options.requestDelegate && options.requestDelegate.onAccept) {
              options.requestDelegate.onAccept(inviteResponse);
            }
          });
        },
        onProgress: (inviteResponse) => {
          if (this.isCanceled) {
            return;
          }
          this.notifyReferer(inviteResponse);
          this.onProgress(inviteResponse).catch(() => {
            this.disposeEarlyMedia();
          }).then(() => {
            if (options.requestDelegate && options.requestDelegate.onProgress) {
              options.requestDelegate.onProgress(inviteResponse);
            }
          });
        },
        onRedirect: (inviteResponse) => {
          this.notifyReferer(inviteResponse);
          this.onRedirect(inviteResponse);
          if (options.requestDelegate && options.requestDelegate.onRedirect) {
            options.requestDelegate.onRedirect(inviteResponse);
          }
        },
        onReject: (inviteResponse) => {
          this.notifyReferer(inviteResponse);
          this.onReject(inviteResponse);
          if (options.requestDelegate && options.requestDelegate.onReject) {
            options.requestDelegate.onReject(inviteResponse);
          }
        },
        onTrying: (inviteResponse) => {
          this.notifyReferer(inviteResponse);
          this.onTrying(inviteResponse);
          if (options.requestDelegate && options.requestDelegate.onTrying) {
            options.requestDelegate.onTrying(inviteResponse);
          }
        }
      });
      return this.outgoingInviteRequest;
    }
    disposeEarlyMedia() {
      this.earlyMediaSessionDescriptionHandlers.forEach((sessionDescriptionHandler) => {
        sessionDescriptionHandler.close();
      });
      this.earlyMediaSessionDescriptionHandlers.clear();
    }
    notifyReferer(response) {
      if (!this._referred) {
        return;
      }
      if (!(this._referred instanceof Session)) {
        throw new Error("Referred session not instance of session");
      }
      if (!this._referred.dialog) {
        return;
      }
      if (!response.message.statusCode) {
        throw new Error("Status code undefined.");
      }
      if (!response.message.reasonPhrase) {
        throw new Error("Reason phrase undefined.");
      }
      const statusCode = response.message.statusCode;
      const reasonPhrase = response.message.reasonPhrase;
      const body = `SIP/2.0 ${statusCode} ${reasonPhrase}`.trim();
      const outgoingNotifyRequest = this._referred.dialog.notify(void 0, {
        extraHeaders: ["Event: refer", "Subscription-State: terminated"],
        body: {
          contentDisposition: "render",
          contentType: "message/sipfrag",
          content: body
        }
      });
      outgoingNotifyRequest.delegate = {
        onReject: () => {
          this._referred = void 0;
        }
      };
    }
    /**
     * Handle final response to initial INVITE.
     * @param inviteResponse - 2xx response.
     */
    onAccept(inviteResponse) {
      this.logger.log("Inviter.onAccept");
      if (this.state !== SessionState2.Establishing) {
        this.logger.error(`Accept received while in state ${this.state}, dropping response`);
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      const response = inviteResponse.message;
      const session = inviteResponse.session;
      if (response.hasHeader("P-Asserted-Identity")) {
        this._assertedIdentity = Grammar.nameAddrHeaderParse(response.getHeader("P-Asserted-Identity"));
      }
      session.delegate = {
        onAck: (ackRequest) => this.onAckRequest(ackRequest),
        onBye: (byeRequest) => this.onByeRequest(byeRequest),
        onInfo: (infoRequest) => this.onInfoRequest(infoRequest),
        onInvite: (inviteRequest) => this.onInviteRequest(inviteRequest),
        onMessage: (messageRequest) => this.onMessageRequest(messageRequest),
        onNotify: (notifyRequest) => this.onNotifyRequest(notifyRequest),
        onPrack: (prackRequest) => this.onPrackRequest(prackRequest),
        onRefer: (referRequest) => this.onReferRequest(referRequest)
      };
      this._dialog = session;
      switch (session.signalingState) {
        case SignalingState.Initial:
          this.logger.error("Received 2xx response to INVITE without a session description");
          this.ackAndBye(inviteResponse, 400, "Missing session description");
          this.stateTransition(SessionState2.Terminated);
          return Promise.reject(new Error("Bad Media Description"));
        case SignalingState.HaveLocalOffer:
          this.logger.error("Received 2xx response to INVITE without a session description");
          this.ackAndBye(inviteResponse, 400, "Missing session description");
          this.stateTransition(SessionState2.Terminated);
          return Promise.reject(new Error("Bad Media Description"));
        case SignalingState.HaveRemoteOffer: {
          if (!this._dialog.offer) {
            throw new Error(`Session offer undefined in signaling state ${this._dialog.signalingState}.`);
          }
          const options = {
            sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers,
            sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions
          };
          return this.setOfferAndGetAnswer(this._dialog.offer, options).then((body) => {
            inviteResponse.ack({ body });
            this.stateTransition(SessionState2.Established);
          }).catch((error) => {
            this.ackAndBye(inviteResponse, 488, "Invalid session description");
            this.stateTransition(SessionState2.Terminated);
            throw error;
          });
        }
        case SignalingState.Stable: {
          if (this.earlyMediaSessionDescriptionHandlers.size > 0) {
            const sdh = this.earlyMediaSessionDescriptionHandlers.get(session.id);
            if (!sdh) {
              throw new Error("Session description handler undefined.");
            }
            this.setSessionDescriptionHandler(sdh);
            this.earlyMediaSessionDescriptionHandlers.delete(session.id);
            inviteResponse.ack();
            this.stateTransition(SessionState2.Established);
            return Promise.resolve();
          }
          if (this.earlyMediaDialog) {
            if (this.earlyMediaDialog !== session) {
              if (this.earlyMedia) {
                const message = "You have set the 'earlyMedia' option to 'true' which requires that your INVITE requests do not fork and yet this INVITE request did in fact fork. Consequentially and not surprisingly the end point which accepted the INVITE (confirmed dialog) does not match the end point with which early media has been setup (early dialog) and thus this session is unable to proceed. In accordance with the SIP specifications, the SIP servers your end point is connected to determine if an INVITE forks and the forking behavior of those servers cannot be controlled by this library. If you wish to use early media with this library you must configure those servers accordingly. Alternatively you may set the 'earlyMedia' to 'false' which will allow this library to function with any INVITE requests which do fork.";
                this.logger.error(message);
              }
              const error = new Error("Early media dialog does not equal confirmed dialog, terminating session");
              this.logger.error(error.message);
              this.ackAndBye(inviteResponse, 488, "Not Acceptable Here");
              this.stateTransition(SessionState2.Terminated);
              return Promise.reject(error);
            }
            inviteResponse.ack();
            this.stateTransition(SessionState2.Established);
            return Promise.resolve();
          }
          const answer = session.answer;
          if (!answer) {
            throw new Error("Answer is undefined.");
          }
          const options = {
            sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers,
            sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions
          };
          return this.setAnswer(answer, options).then(() => {
            let ackOptions;
            if (this._renderbody && this._rendertype) {
              ackOptions = {
                body: { contentDisposition: "render", contentType: this._rendertype, content: this._renderbody }
              };
            }
            inviteResponse.ack(ackOptions);
            this.stateTransition(SessionState2.Established);
          }).catch((error) => {
            this.logger.error(error.message);
            this.ackAndBye(inviteResponse, 488, "Not Acceptable Here");
            this.stateTransition(SessionState2.Terminated);
            throw error;
          });
        }
        case SignalingState.Closed:
          return Promise.reject(new Error("Terminated."));
        default:
          throw new Error("Unknown session signaling state.");
      }
    }
    /**
     * Handle provisional response to initial INVITE.
     * @param inviteResponse - 1xx response.
     */
    onProgress(inviteResponse) {
      var _a;
      this.logger.log("Inviter.onProgress");
      if (this.state !== SessionState2.Establishing) {
        this.logger.error(`Progress received while in state ${this.state}, dropping response`);
        return Promise.reject(new Error(`Invalid session state ${this.state}`));
      }
      if (!this.outgoingInviteRequest) {
        throw new Error("Outgoing INVITE request undefined.");
      }
      const response = inviteResponse.message;
      const session = inviteResponse.session;
      if (response.hasHeader("P-Asserted-Identity")) {
        this._assertedIdentity = Grammar.nameAddrHeaderParse(response.getHeader("P-Asserted-Identity"));
      }
      const requireHeader = response.getHeader("require");
      const rseqHeader = response.getHeader("rseq");
      const rseq = requireHeader && requireHeader.includes("100rel") && rseqHeader ? Number(rseqHeader) : void 0;
      const responseReliable = !!rseq;
      const extraHeaders = [];
      if (responseReliable) {
        extraHeaders.push("RAck: " + response.getHeader("rseq") + " " + response.getHeader("cseq"));
      }
      switch (session.signalingState) {
        case SignalingState.Initial:
          if (responseReliable) {
            this.logger.warn("First reliable provisional response received MUST contain an offer when INVITE does not contain an offer.");
            inviteResponse.prack({ extraHeaders });
          }
          return Promise.resolve();
        case SignalingState.HaveLocalOffer:
          if (responseReliable) {
            inviteResponse.prack({ extraHeaders });
          }
          return Promise.resolve();
        case SignalingState.HaveRemoteOffer:
          if (!responseReliable) {
            this.logger.warn("Non-reliable provisional response MUST NOT contain an initial offer, discarding response.");
            return Promise.resolve();
          }
          {
            const sdh = this.sessionDescriptionHandlerFactory(this, this.userAgent.configuration.sessionDescriptionHandlerFactoryOptions || {});
            if ((_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onSessionDescriptionHandler) {
              this.delegate.onSessionDescriptionHandler(sdh, true);
            }
            this.earlyMediaSessionDescriptionHandlers.set(session.id, sdh);
            return sdh.setDescription(response.body, this.sessionDescriptionHandlerOptions, this.sessionDescriptionHandlerModifiers).then(() => sdh.getDescription(this.sessionDescriptionHandlerOptions, this.sessionDescriptionHandlerModifiers)).then((description) => {
              const body = {
                contentDisposition: "session",
                contentType: description.contentType,
                content: description.body
              };
              inviteResponse.prack({ extraHeaders, body });
            }).catch((error) => {
              this.stateTransition(SessionState2.Terminated);
              throw error;
            });
          }
        case SignalingState.Stable:
          if (responseReliable) {
            inviteResponse.prack({ extraHeaders });
          }
          if (this.earlyMedia && !this.earlyMediaDialog) {
            this.earlyMediaDialog = session;
            const answer = session.answer;
            if (!answer) {
              throw new Error("Answer is undefined.");
            }
            const options = {
              sessionDescriptionHandlerModifiers: this.sessionDescriptionHandlerModifiers,
              sessionDescriptionHandlerOptions: this.sessionDescriptionHandlerOptions
            };
            return this.setAnswer(answer, options).catch((error) => {
              this.stateTransition(SessionState2.Terminated);
              throw error;
            });
          }
          return Promise.resolve();
        case SignalingState.Closed:
          return Promise.reject(new Error("Terminated."));
        default:
          throw new Error("Unknown session signaling state.");
      }
    }
    /**
     * Handle final response to initial INVITE.
     * @param inviteResponse - 3xx response.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    onRedirect(inviteResponse) {
      this.logger.log("Inviter.onRedirect");
      if (this.state !== SessionState2.Establishing && this.state !== SessionState2.Terminating) {
        this.logger.error(`Redirect received while in state ${this.state}, dropping response`);
        return;
      }
      this.stateTransition(SessionState2.Terminated);
    }
    /**
     * Handle final response to initial INVITE.
     * @param inviteResponse - 4xx, 5xx, or 6xx response.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    onReject(inviteResponse) {
      this.logger.log("Inviter.onReject");
      if (this.state !== SessionState2.Establishing && this.state !== SessionState2.Terminating) {
        this.logger.error(`Reject received while in state ${this.state}, dropping response`);
        return;
      }
      this.stateTransition(SessionState2.Terminated);
    }
    /**
     * Handle final response to initial INVITE.
     * @param inviteResponse - 100 response.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    onTrying(inviteResponse) {
      this.logger.log("Inviter.onTrying");
      if (this.state !== SessionState2.Establishing) {
        this.logger.error(`Trying received while in state ${this.state}, dropping response`);
        return;
      }
    }
  };

  // node_modules/sip.js/lib/api/messager.js
  var Messager = class {
    /**
     * Constructs a new instance of the `Messager` class.
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @param targetURI - Request URI identifying the target of the message.
     * @param content - Content for the body of the message.
     * @param contentType - Content type of the body of the message.
     * @param options - Options bucket. See {@link MessagerOptions} for details.
     */
    constructor(userAgent, targetURI, content, contentType = "text/plain", options = {}) {
      this.logger = userAgent.getLogger("sip.Messager");
      options.params = options.params || {};
      let fromURI = userAgent.userAgentCore.configuration.aor;
      if (options.params.fromUri) {
        fromURI = typeof options.params.fromUri === "string" ? Grammar.URIParse(options.params.fromUri) : options.params.fromUri;
      }
      if (!fromURI) {
        throw new TypeError("Invalid from URI: " + options.params.fromUri);
      }
      let toURI = targetURI;
      if (options.params.toUri) {
        toURI = typeof options.params.toUri === "string" ? Grammar.URIParse(options.params.toUri) : options.params.toUri;
      }
      if (!toURI) {
        throw new TypeError("Invalid to URI: " + options.params.toUri);
      }
      const params = options.params ? Object.assign({}, options.params) : {};
      const extraHeaders = (options.extraHeaders || []).slice();
      const contentDisposition = "render";
      const body = {
        contentDisposition,
        contentType,
        content
      };
      this.request = userAgent.userAgentCore.makeOutgoingRequestMessage(C.MESSAGE, targetURI, fromURI, toURI, params, extraHeaders, body);
      this.userAgent = userAgent;
    }
    /**
     * Send the message.
     */
    message(options = {}) {
      this.userAgent.userAgentCore.request(this.request, options.requestDelegate);
      return Promise.resolve();
    }
  };

  // node_modules/sip.js/lib/api/publisher-state.js
  var PublisherState;
  (function(PublisherState2) {
    PublisherState2["Initial"] = "Initial";
    PublisherState2["Published"] = "Published";
    PublisherState2["Unpublished"] = "Unpublished";
    PublisherState2["Terminated"] = "Terminated";
  })(PublisherState = PublisherState || (PublisherState = {}));

  // node_modules/sip.js/lib/api/publisher.js
  var Publisher = class {
    /**
     * Constructs a new instance of the `Publisher` class.
     *
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @param targetURI - Request URI identifying the target of the message.
     * @param eventType - The event type identifying the published document.
     * @param options - Options bucket. See {@link PublisherOptions} for details.
     */
    constructor(userAgent, targetURI, eventType, options = {}) {
      this.disposed = false;
      this._state = PublisherState.Initial;
      this._stateEventEmitter = new EmitterImpl();
      this.userAgent = userAgent;
      options.extraHeaders = (options.extraHeaders || []).slice();
      options.contentType = options.contentType || "text/plain";
      if (typeof options.expires !== "number" || options.expires % 1 !== 0) {
        options.expires = 3600;
      } else {
        options.expires = Number(options.expires);
      }
      if (typeof options.unpublishOnClose !== "boolean") {
        options.unpublishOnClose = true;
      }
      this.target = targetURI;
      this.event = eventType;
      this.options = options;
      this.pubRequestExpires = options.expires;
      this.logger = userAgent.getLogger("sip.Publisher");
      const params = options.params || {};
      const fromURI = params.fromUri ? params.fromUri : userAgent.userAgentCore.configuration.aor;
      const toURI = params.toUri ? params.toUri : targetURI;
      let body;
      if (options.body && options.contentType) {
        const contentDisposition = "render";
        const contentType = options.contentType;
        const content = options.body;
        body = {
          contentDisposition,
          contentType,
          content
        };
      }
      const extraHeaders = (options.extraHeaders || []).slice();
      this.request = userAgent.userAgentCore.makeOutgoingRequestMessage(C.PUBLISH, targetURI, fromURI, toURI, params, extraHeaders, body);
      this.id = this.target.toString() + ":" + this.event;
      this.userAgent._publishers[this.id] = this;
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this.disposed) {
        return Promise.resolve();
      }
      this.disposed = true;
      this.logger.log(`Publisher ${this.id} in state ${this.state} is being disposed`);
      delete this.userAgent._publishers[this.id];
      if (this.options.unpublishOnClose && this.state === PublisherState.Published) {
        return this.unpublish();
      }
      if (this.publishRefreshTimer) {
        clearTimeout(this.publishRefreshTimer);
        this.publishRefreshTimer = void 0;
      }
      this.pubRequestBody = void 0;
      this.pubRequestExpires = 0;
      this.pubRequestEtag = void 0;
      return Promise.resolve();
    }
    /** The publication state. */
    get state() {
      return this._state;
    }
    /** Emits when the publisher state changes. */
    get stateChange() {
      return this._stateEventEmitter;
    }
    /**
     * Publish.
     * @param content - Body to publish
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    publish(content, options = {}) {
      if (this.publishRefreshTimer) {
        clearTimeout(this.publishRefreshTimer);
        this.publishRefreshTimer = void 0;
      }
      this.options.body = content;
      this.pubRequestBody = this.options.body;
      if (this.pubRequestExpires === 0) {
        if (this.options.expires === void 0) {
          throw new Error("Expires undefined.");
        }
        this.pubRequestExpires = this.options.expires;
        this.pubRequestEtag = void 0;
      }
      this.sendPublishRequest();
      return Promise.resolve();
    }
    /**
     * Unpublish.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    unpublish(options = {}) {
      if (this.publishRefreshTimer) {
        clearTimeout(this.publishRefreshTimer);
        this.publishRefreshTimer = void 0;
      }
      this.pubRequestBody = void 0;
      this.pubRequestExpires = 0;
      if (this.pubRequestEtag !== void 0) {
        this.sendPublishRequest();
      }
      return Promise.resolve();
    }
    /** @internal */
    receiveResponse(response) {
      const statusCode = response.statusCode || 0;
      switch (true) {
        case /^1[0-9]{2}$/.test(statusCode.toString()):
          break;
        case /^2[0-9]{2}$/.test(statusCode.toString()):
          if (response.hasHeader("SIP-ETag")) {
            this.pubRequestEtag = response.getHeader("SIP-ETag");
          } else {
            this.logger.warn("SIP-ETag header missing in a 200-class response to PUBLISH");
          }
          if (response.hasHeader("Expires")) {
            const expires = Number(response.getHeader("Expires"));
            if (typeof expires === "number" && expires >= 0 && expires <= this.pubRequestExpires) {
              this.pubRequestExpires = expires;
            } else {
              this.logger.warn("Bad Expires header in a 200-class response to PUBLISH");
            }
          } else {
            this.logger.warn("Expires header missing in a 200-class response to PUBLISH");
          }
          if (this.pubRequestExpires !== 0) {
            this.publishRefreshTimer = setTimeout(() => this.refreshRequest(), this.pubRequestExpires * 900);
            if (this._state !== PublisherState.Published) {
              this.stateTransition(PublisherState.Published);
            }
          } else {
            this.stateTransition(PublisherState.Unpublished);
          }
          break;
        case /^412$/.test(statusCode.toString()):
          if (this.pubRequestEtag !== void 0 && this.pubRequestExpires !== 0) {
            this.logger.warn("412 response to PUBLISH, recovering");
            this.pubRequestEtag = void 0;
            if (this.options.body === void 0) {
              throw new Error("Body undefined.");
            }
            this.publish(this.options.body);
          } else {
            this.logger.warn("412 response to PUBLISH, recovery failed");
            this.pubRequestExpires = 0;
            this.stateTransition(PublisherState.Unpublished);
            this.stateTransition(PublisherState.Terminated);
          }
          break;
        case /^423$/.test(statusCode.toString()):
          if (this.pubRequestExpires !== 0 && response.hasHeader("Min-Expires")) {
            const minExpires = Number(response.getHeader("Min-Expires"));
            if (typeof minExpires === "number" || minExpires > this.pubRequestExpires) {
              this.logger.warn("423 code in response to PUBLISH, adjusting the Expires value and trying to recover");
              this.pubRequestExpires = minExpires;
              if (this.options.body === void 0) {
                throw new Error("Body undefined.");
              }
              this.publish(this.options.body);
            } else {
              this.logger.warn("Bad 423 response Min-Expires header received for PUBLISH");
              this.pubRequestExpires = 0;
              this.stateTransition(PublisherState.Unpublished);
              this.stateTransition(PublisherState.Terminated);
            }
          } else {
            this.logger.warn("423 response to PUBLISH, recovery failed");
            this.pubRequestExpires = 0;
            this.stateTransition(PublisherState.Unpublished);
            this.stateTransition(PublisherState.Terminated);
          }
          break;
        default:
          this.pubRequestExpires = 0;
          this.stateTransition(PublisherState.Unpublished);
          this.stateTransition(PublisherState.Terminated);
          break;
      }
      if (this.pubRequestExpires === 0) {
        if (this.publishRefreshTimer) {
          clearTimeout(this.publishRefreshTimer);
          this.publishRefreshTimer = void 0;
        }
        this.pubRequestBody = void 0;
        this.pubRequestEtag = void 0;
      }
    }
    /** @internal */
    send() {
      return this.userAgent.userAgentCore.publish(this.request, {
        onAccept: (response) => this.receiveResponse(response.message),
        onProgress: (response) => this.receiveResponse(response.message),
        onRedirect: (response) => this.receiveResponse(response.message),
        onReject: (response) => this.receiveResponse(response.message),
        onTrying: (response) => this.receiveResponse(response.message)
      });
    }
    refreshRequest() {
      if (this.publishRefreshTimer) {
        clearTimeout(this.publishRefreshTimer);
        this.publishRefreshTimer = void 0;
      }
      this.pubRequestBody = void 0;
      if (this.pubRequestEtag === void 0) {
        throw new Error("Etag undefined");
      }
      if (this.pubRequestExpires === 0) {
        throw new Error("Expires zero");
      }
      this.sendPublishRequest();
    }
    sendPublishRequest() {
      const reqOptions = Object.assign({}, this.options);
      reqOptions.extraHeaders = (this.options.extraHeaders || []).slice();
      reqOptions.extraHeaders.push("Event: " + this.event);
      reqOptions.extraHeaders.push("Expires: " + this.pubRequestExpires);
      if (this.pubRequestEtag !== void 0) {
        reqOptions.extraHeaders.push("SIP-If-Match: " + this.pubRequestEtag);
      }
      const ruri = this.target;
      const params = this.options.params || {};
      let bodyAndContentType;
      if (this.pubRequestBody !== void 0) {
        if (this.options.contentType === void 0) {
          throw new Error("Content type undefined.");
        }
        bodyAndContentType = {
          body: this.pubRequestBody,
          contentType: this.options.contentType
        };
      }
      let body;
      if (bodyAndContentType) {
        body = fromBodyLegacy(bodyAndContentType);
      }
      this.request = this.userAgent.userAgentCore.makeOutgoingRequestMessage(C.PUBLISH, ruri, params.fromUri ? params.fromUri : this.userAgent.userAgentCore.configuration.aor, params.toUri ? params.toUri : this.target, params, reqOptions.extraHeaders, body);
      return this.send();
    }
    /**
     * Transition publication state.
     */
    stateTransition(newState) {
      const invalidTransition = () => {
        throw new Error(`Invalid state transition from ${this._state} to ${newState}`);
      };
      switch (this._state) {
        case PublisherState.Initial:
          if (newState !== PublisherState.Published && newState !== PublisherState.Unpublished && newState !== PublisherState.Terminated) {
            invalidTransition();
          }
          break;
        case PublisherState.Published:
          if (newState !== PublisherState.Unpublished && newState !== PublisherState.Terminated) {
            invalidTransition();
          }
          break;
        case PublisherState.Unpublished:
          if (newState !== PublisherState.Published && newState !== PublisherState.Terminated) {
            invalidTransition();
          }
          break;
        case PublisherState.Terminated:
          invalidTransition();
          break;
        default:
          throw new Error("Unrecognized state.");
      }
      this._state = newState;
      this.logger.log(`Publication transitioned to state ${this._state}`);
      this._stateEventEmitter.emit(this._state);
      if (newState === PublisherState.Terminated) {
        this.dispose();
      }
    }
  };

  // node_modules/sip.js/lib/api/registerer-state.js
  var RegistererState;
  (function(RegistererState2) {
    RegistererState2["Initial"] = "Initial";
    RegistererState2["Registered"] = "Registered";
    RegistererState2["Unregistered"] = "Unregistered";
    RegistererState2["Terminated"] = "Terminated";
  })(RegistererState = RegistererState || (RegistererState = {}));

  // node_modules/sip.js/lib/api/registerer.js
  var Registerer = class _Registerer {
    /**
     * Constructs a new instance of the `Registerer` class.
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @param options - Options bucket. See {@link RegistererOptions} for details.
     */
    constructor(userAgent, options = {}) {
      this.disposed = false;
      this._contacts = [];
      this._retryAfter = void 0;
      this._state = RegistererState.Initial;
      this._waiting = false;
      this._stateEventEmitter = new EmitterImpl();
      this._waitingEventEmitter = new EmitterImpl();
      this.userAgent = userAgent;
      const defaultUserAgentRegistrar = userAgent.configuration.uri.clone();
      defaultUserAgentRegistrar.user = void 0;
      this.options = Object.assign(Object.assign(Object.assign({}, _Registerer.defaultOptions()), { registrar: defaultUserAgentRegistrar }), _Registerer.stripUndefinedProperties(options));
      this.options.extraContactHeaderParams = (this.options.extraContactHeaderParams || []).slice();
      this.options.extraHeaders = (this.options.extraHeaders || []).slice();
      if (!this.options.registrar) {
        throw new Error("Registrar undefined.");
      }
      this.options.registrar = this.options.registrar.clone();
      if (this.options.regId && !this.options.instanceId) {
        this.options.instanceId = this.userAgent.instanceId;
      } else if (!this.options.regId && this.options.instanceId) {
        this.options.regId = 1;
      }
      if (this.options.instanceId && Grammar.parse(this.options.instanceId, "uuid") === -1) {
        throw new Error("Invalid instanceId.");
      }
      if (this.options.regId && this.options.regId < 0) {
        throw new Error("Invalid regId.");
      }
      const registrar = this.options.registrar;
      const fromURI = this.options.params && this.options.params.fromUri || userAgent.userAgentCore.configuration.aor;
      const toURI = this.options.params && this.options.params.toUri || userAgent.configuration.uri;
      const params = this.options.params || {};
      const extraHeaders = (options.extraHeaders || []).slice();
      this.request = userAgent.userAgentCore.makeOutgoingRequestMessage(C.REGISTER, registrar, fromURI, toURI, params, extraHeaders, void 0);
      this.expires = this.options.expires || _Registerer.defaultExpires;
      if (this.expires < 0) {
        throw new Error("Invalid expires.");
      }
      this.refreshFrequency = this.options.refreshFrequency || _Registerer.defaultRefreshFrequency;
      if (this.refreshFrequency < 50 || this.refreshFrequency > 99) {
        throw new Error("Invalid refresh frequency. The value represents a percentage of the expiration time and should be between 50 and 99.");
      }
      this.logger = userAgent.getLogger("sip.Registerer");
      if (this.options.logConfiguration) {
        this.logger.log("Configuration:");
        Object.keys(this.options).forEach((key) => {
          const value = this.options[key];
          switch (key) {
            case "registrar":
              this.logger.log("\xB7 " + key + ": " + value);
              break;
            default:
              this.logger.log("\xB7 " + key + ": " + JSON.stringify(value));
          }
        });
      }
      this.id = this.request.callId + this.request.from.parameters.tag;
      this.userAgent._registerers[this.id] = this;
    }
    /** Default registerer options. */
    static defaultOptions() {
      return {
        expires: _Registerer.defaultExpires,
        extraContactHeaderParams: [],
        extraHeaders: [],
        logConfiguration: true,
        instanceId: "",
        params: {},
        regId: 0,
        registrar: new URI("sip", "anonymous", "anonymous.invalid"),
        refreshFrequency: _Registerer.defaultRefreshFrequency
      };
    }
    /**
     * Strip properties with undefined values from options.
     * This is a work around while waiting for missing vs undefined to be addressed (or not)...
     * https://github.com/Microsoft/TypeScript/issues/13195
     * @param options - Options to reduce
     */
    static stripUndefinedProperties(options) {
      return Object.keys(options).reduce((object, key) => {
        if (options[key] !== void 0) {
          object[key] = options[key];
        }
        return object;
      }, {});
    }
    /** The registered contacts. */
    get contacts() {
      return this._contacts.slice();
    }
    /**
     * The number of seconds to wait before retrying to register.
     * @defaultValue `undefined`
     * @remarks
     * When the server rejects a registration request, if it provides a suggested
     * duration to wait before retrying, that value is available here when and if
     * the state transitions to `Unsubscribed`. It is also available during the
     * callback to `onReject` after a call to `register`. (Note that if the state
     * if already `Unsubscribed`, a rejected request created by `register` will
     * not cause the state to transition to `Unsubscribed`. One way to avoid this
     * case is to dispose of `Registerer` when unregistered and create a new
     * `Registerer` for any attempts to retry registering.)
     * @example
     * ```ts
     * // Checking for retry after on state change
     * registerer.stateChange.addListener((newState) => {
     *   switch (newState) {
     *     case RegistererState.Unregistered:
     *       const retryAfter = registerer.retryAfter;
     *       break;
     *   }
     * });
     *
     * // Checking for retry after on request rejection
     * registerer.register({
     *   requestDelegate: {
     *     onReject: () => {
     *       const retryAfter = registerer.retryAfter;
     *     }
     *   }
     * });
     * ```
     */
    get retryAfter() {
      return this._retryAfter;
    }
    /** The registration state. */
    get state() {
      return this._state;
    }
    /** Emits when the registerer state changes. */
    get stateChange() {
      return this._stateEventEmitter;
    }
    /** Destructor. */
    dispose() {
      if (this.disposed) {
        return Promise.resolve();
      }
      this.disposed = true;
      this.logger.log(`Registerer ${this.id} in state ${this.state} is being disposed`);
      delete this.userAgent._registerers[this.id];
      return new Promise((resolve) => {
        const doClose = () => {
          if (!this.waiting && this._state === RegistererState.Registered) {
            this.stateChange.addListener(() => {
              this.terminated();
              resolve();
            }, { once: true });
            this.unregister();
            return;
          }
          this.terminated();
          resolve();
        };
        if (this.waiting) {
          this.waitingChange.addListener(() => {
            doClose();
          }, { once: true });
        } else {
          doClose();
        }
      });
    }
    /**
     * Sends the REGISTER request.
     * @remarks
     * If successful, sends re-REGISTER requests prior to registration expiration until `unsubscribe()` is called.
     * Rejects with `RequestPendingError` if a REGISTER request is already in progress.
     */
    register(options = {}) {
      if (this.state === RegistererState.Terminated) {
        this.stateError();
        throw new Error("Registerer terminated. Unable to register.");
      }
      if (this.disposed) {
        this.stateError();
        throw new Error("Registerer disposed. Unable to register.");
      }
      if (this.waiting) {
        this.waitingWarning();
        const error = new RequestPendingError("REGISTER request already in progress, waiting for final response");
        return Promise.reject(error);
      }
      if (options.requestOptions) {
        this.options = Object.assign(Object.assign({}, this.options), options.requestOptions);
      }
      const extraHeaders = (this.options.extraHeaders || []).slice();
      extraHeaders.push("Contact: " + this.generateContactHeader(this.expires));
      extraHeaders.push("Allow: " + ["ACK", "CANCEL", "INVITE", "MESSAGE", "BYE", "OPTIONS", "INFO", "NOTIFY", "REFER"].toString());
      this.request.cseq++;
      this.request.setHeader("cseq", this.request.cseq + " REGISTER");
      this.request.extraHeaders = extraHeaders;
      this.waitingToggle(true);
      const outgoingRegisterRequest = this.userAgent.userAgentCore.register(this.request, {
        onAccept: (response) => {
          let expires;
          if (response.message.hasHeader("expires")) {
            expires = Number(response.message.getHeader("expires"));
          }
          this._contacts = response.message.getHeaders("contact");
          let contacts = this._contacts.length;
          if (!contacts) {
            this.logger.error("No Contact header in response to REGISTER, dropping response.");
            this.unregistered();
            return;
          }
          let contact;
          while (contacts--) {
            contact = response.message.parseHeader("contact", contacts);
            if (!contact) {
              throw new Error("Contact undefined");
            }
            if (this.userAgent.contact.pubGruu && equivalentURI(contact.uri, this.userAgent.contact.pubGruu)) {
              expires = Number(contact.getParam("expires"));
              break;
            }
            if (this.userAgent.configuration.contactName === "") {
              if (contact.uri.user === this.userAgent.contact.uri.user) {
                expires = Number(contact.getParam("expires"));
                break;
              }
            } else {
              if (equivalentURI(contact.uri, this.userAgent.contact.uri)) {
                expires = Number(contact.getParam("expires"));
                break;
              }
            }
            contact = void 0;
          }
          if (contact === void 0) {
            this.logger.error("No Contact header pointing to us, dropping response");
            this.unregistered();
            this.waitingToggle(false);
            return;
          }
          if (expires === void 0) {
            this.logger.error("Contact pointing to us is missing expires parameter, dropping response");
            this.unregistered();
            this.waitingToggle(false);
            return;
          }
          if (contact.hasParam("temp-gruu")) {
            const gruu = contact.getParam("temp-gruu");
            if (gruu) {
              this.userAgent.contact.tempGruu = Grammar.URIParse(gruu.replace(/"/g, ""));
            }
          }
          if (contact.hasParam("pub-gruu")) {
            const gruu = contact.getParam("pub-gruu");
            if (gruu) {
              this.userAgent.contact.pubGruu = Grammar.URIParse(gruu.replace(/"/g, ""));
            }
          }
          this.registered(expires);
          if (options.requestDelegate && options.requestDelegate.onAccept) {
            options.requestDelegate.onAccept(response);
          }
          this.waitingToggle(false);
        },
        onProgress: (response) => {
          if (options.requestDelegate && options.requestDelegate.onProgress) {
            options.requestDelegate.onProgress(response);
          }
        },
        onRedirect: (response) => {
          this.logger.error("Redirect received. Not supported.");
          this.unregistered();
          if (options.requestDelegate && options.requestDelegate.onRedirect) {
            options.requestDelegate.onRedirect(response);
          }
          this.waitingToggle(false);
        },
        onReject: (response) => {
          if (response.message.statusCode === 423) {
            if (!response.message.hasHeader("min-expires")) {
              this.logger.error("423 response received for REGISTER without Min-Expires, dropping response");
              this.unregistered();
              this.waitingToggle(false);
              return;
            }
            this.expires = Number(response.message.getHeader("min-expires"));
            this.waitingToggle(false);
            this.register();
            return;
          }
          this.logger.warn(`Failed to register, status code ${response.message.statusCode}`);
          let retryAfterDuration = NaN;
          if (response.message.statusCode === 500 || response.message.statusCode === 503) {
            const header = response.message.getHeader("retry-after");
            if (header) {
              retryAfterDuration = Number.parseInt(header, void 0);
            }
          }
          this._retryAfter = isNaN(retryAfterDuration) ? void 0 : retryAfterDuration;
          this.unregistered();
          if (options.requestDelegate && options.requestDelegate.onReject) {
            options.requestDelegate.onReject(response);
          }
          this._retryAfter = void 0;
          this.waitingToggle(false);
        },
        onTrying: (response) => {
          if (options.requestDelegate && options.requestDelegate.onTrying) {
            options.requestDelegate.onTrying(response);
          }
        }
      });
      return Promise.resolve(outgoingRegisterRequest);
    }
    /**
     * Sends the REGISTER request with expires equal to zero.
     * @remarks
     * Rejects with `RequestPendingError` if a REGISTER request is already in progress.
     */
    unregister(options = {}) {
      if (this.state === RegistererState.Terminated) {
        this.stateError();
        throw new Error("Registerer terminated. Unable to register.");
      }
      if (this.disposed) {
        if (this.state !== RegistererState.Registered) {
          this.stateError();
          throw new Error("Registerer disposed. Unable to register.");
        }
      }
      if (this.waiting) {
        this.waitingWarning();
        const error = new RequestPendingError("REGISTER request already in progress, waiting for final response");
        return Promise.reject(error);
      }
      if (this._state !== RegistererState.Registered && !options.all) {
        this.logger.warn("Not currently registered, but sending an unregister anyway.");
      }
      const extraHeaders = (options.requestOptions && options.requestOptions.extraHeaders || []).slice();
      this.request.extraHeaders = extraHeaders;
      if (options.all) {
        extraHeaders.push("Contact: *");
        extraHeaders.push("Expires: 0");
      } else {
        extraHeaders.push("Contact: " + this.generateContactHeader(0));
      }
      this.request.cseq++;
      this.request.setHeader("cseq", this.request.cseq + " REGISTER");
      if (this.registrationTimer !== void 0) {
        clearTimeout(this.registrationTimer);
        this.registrationTimer = void 0;
      }
      this.waitingToggle(true);
      const outgoingRegisterRequest = this.userAgent.userAgentCore.register(this.request, {
        onAccept: (response) => {
          this._contacts = response.message.getHeaders("contact");
          this.unregistered();
          if (options.requestDelegate && options.requestDelegate.onAccept) {
            options.requestDelegate.onAccept(response);
          }
          this.waitingToggle(false);
        },
        onProgress: (response) => {
          if (options.requestDelegate && options.requestDelegate.onProgress) {
            options.requestDelegate.onProgress(response);
          }
        },
        onRedirect: (response) => {
          this.logger.error("Unregister redirected. Not currently supported.");
          this.unregistered();
          if (options.requestDelegate && options.requestDelegate.onRedirect) {
            options.requestDelegate.onRedirect(response);
          }
          this.waitingToggle(false);
        },
        onReject: (response) => {
          this.logger.error(`Unregister rejected with status code ${response.message.statusCode}`);
          this.unregistered();
          if (options.requestDelegate && options.requestDelegate.onReject) {
            options.requestDelegate.onReject(response);
          }
          this.waitingToggle(false);
        },
        onTrying: (response) => {
          if (options.requestDelegate && options.requestDelegate.onTrying) {
            options.requestDelegate.onTrying(response);
          }
        }
      });
      return Promise.resolve(outgoingRegisterRequest);
    }
    /**
     * Clear registration timers.
     */
    clearTimers() {
      if (this.registrationTimer !== void 0) {
        clearTimeout(this.registrationTimer);
        this.registrationTimer = void 0;
      }
      if (this.registrationExpiredTimer !== void 0) {
        clearTimeout(this.registrationExpiredTimer);
        this.registrationExpiredTimer = void 0;
      }
    }
    /**
     * Generate Contact Header
     */
    generateContactHeader(expires) {
      let contact = this.userAgent.contact.toString({ register: true });
      if (this.options.regId && this.options.instanceId) {
        contact += ";reg-id=" + this.options.regId;
        contact += ';+sip.instance="<urn:uuid:' + this.options.instanceId + '>"';
      }
      if (this.options.extraContactHeaderParams) {
        this.options.extraContactHeaderParams.forEach((header) => {
          contact += ";" + header;
        });
      }
      contact += ";expires=" + expires;
      return contact;
    }
    /**
     * Helper function, called when registered.
     */
    registered(expires) {
      this.clearTimers();
      this.registrationTimer = setTimeout(() => {
        this.registrationTimer = void 0;
        this.register();
      }, this.refreshFrequency / 100 * expires * 1e3);
      this.registrationExpiredTimer = setTimeout(() => {
        this.logger.warn("Registration expired");
        this.unregistered();
      }, expires * 1e3);
      if (this._state !== RegistererState.Registered) {
        this.stateTransition(RegistererState.Registered);
      }
    }
    /**
     * Helper function, called when unregistered.
     */
    unregistered() {
      this.clearTimers();
      if (this._state !== RegistererState.Unregistered) {
        this.stateTransition(RegistererState.Unregistered);
      }
    }
    /**
     * Helper function, called when terminated.
     */
    terminated() {
      this.clearTimers();
      if (this._state !== RegistererState.Terminated) {
        this.stateTransition(RegistererState.Terminated);
      }
    }
    /**
     * Transition registration state.
     */
    stateTransition(newState) {
      const invalidTransition = () => {
        throw new Error(`Invalid state transition from ${this._state} to ${newState}`);
      };
      switch (this._state) {
        case RegistererState.Initial:
          if (newState !== RegistererState.Registered && newState !== RegistererState.Unregistered && newState !== RegistererState.Terminated) {
            invalidTransition();
          }
          break;
        case RegistererState.Registered:
          if (newState !== RegistererState.Unregistered && newState !== RegistererState.Terminated) {
            invalidTransition();
          }
          break;
        case RegistererState.Unregistered:
          if (newState !== RegistererState.Registered && newState !== RegistererState.Terminated) {
            invalidTransition();
          }
          break;
        case RegistererState.Terminated:
          invalidTransition();
          break;
        default:
          throw new Error("Unrecognized state.");
      }
      this._state = newState;
      this.logger.log(`Registration transitioned to state ${this._state}`);
      this._stateEventEmitter.emit(this._state);
      if (newState === RegistererState.Terminated) {
        this.dispose();
      }
    }
    /** True if the registerer is currently waiting for final response to a REGISTER request. */
    get waiting() {
      return this._waiting;
    }
    /** Emits when the registerer waiting state changes. */
    get waitingChange() {
      return this._waitingEventEmitter;
    }
    /**
     * Toggle waiting.
     */
    waitingToggle(waiting) {
      if (this._waiting === waiting) {
        throw new Error(`Invalid waiting transition from ${this._waiting} to ${waiting}`);
      }
      this._waiting = waiting;
      this.logger.log(`Waiting toggled to ${this._waiting}`);
      this._waitingEventEmitter.emit(this._waiting);
    }
    /** Hopefully helpful as the standard behavior has been found to be unexpected. */
    waitingWarning() {
      let message = "An attempt was made to send a REGISTER request while a prior one was still in progress.";
      message += " RFC 3261 requires UAs MUST NOT send a new registration until they have received a final response";
      message += " from the registrar for the previous one or the previous REGISTER request has timed out.";
      message += " Note that if the transport disconnects, you still must wait for the prior request to time out before";
      message += " sending a new REGISTER request or alternatively dispose of the current Registerer and create a new Registerer.";
      this.logger.warn(message);
    }
    /** Hopefully helpful as the standard behavior has been found to be unexpected. */
    stateError() {
      const reason = this.state === RegistererState.Terminated ? "is in 'Terminated' state" : "has been disposed";
      let message = `An attempt was made to send a REGISTER request when the Registerer ${reason}.`;
      message += " The Registerer transitions to 'Terminated' when Registerer.dispose() is called.";
      message += " Perhaps you called UserAgent.stop() which dipsoses of all Registerers?";
      this.logger.error(message);
    }
  };
  Registerer.defaultExpires = 600;
  Registerer.defaultRefreshFrequency = 99;

  // node_modules/sip.js/lib/core/subscription/subscription.js
  var SubscriptionState;
  (function(SubscriptionState3) {
    SubscriptionState3["Initial"] = "Initial";
    SubscriptionState3["NotifyWait"] = "NotifyWait";
    SubscriptionState3["Pending"] = "Pending";
    SubscriptionState3["Active"] = "Active";
    SubscriptionState3["Terminated"] = "Terminated";
  })(SubscriptionState = SubscriptionState || (SubscriptionState = {}));

  // node_modules/sip.js/lib/api/subscription-state.js
  var SubscriptionState2;
  (function(SubscriptionState3) {
    SubscriptionState3["Initial"] = "Initial";
    SubscriptionState3["NotifyWait"] = "NotifyWait";
    SubscriptionState3["Subscribed"] = "Subscribed";
    SubscriptionState3["Terminated"] = "Terminated";
  })(SubscriptionState2 = SubscriptionState2 || (SubscriptionState2 = {}));

  // node_modules/sip.js/lib/api/subscription.js
  var Subscription = class {
    /**
     * Constructor.
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @internal
     */
    constructor(userAgent, options = {}) {
      this._disposed = false;
      this._state = SubscriptionState2.Initial;
      this._logger = userAgent.getLogger("sip.Subscription");
      this._stateEventEmitter = new EmitterImpl();
      this._userAgent = userAgent;
      this.delegate = options.delegate;
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this._disposed) {
        return Promise.resolve();
      }
      this._disposed = true;
      this._stateEventEmitter.removeAllListeners();
      return Promise.resolve();
    }
    /**
     * The subscribed subscription dialog.
     */
    get dialog() {
      return this._dialog;
    }
    /**
     * True if disposed.
     * @internal
     */
    get disposed() {
      return this._disposed;
    }
    /**
     * Subscription state. See {@link SubscriptionState} for details.
     */
    get state() {
      return this._state;
    }
    /**
     * Emits when the subscription `state` property changes.
     */
    get stateChange() {
      return this._stateEventEmitter;
    }
    /** @internal */
    stateTransition(newState) {
      const invalidTransition = () => {
        throw new Error(`Invalid state transition from ${this._state} to ${newState}`);
      };
      switch (this._state) {
        case SubscriptionState2.Initial:
          if (newState !== SubscriptionState2.NotifyWait && newState !== SubscriptionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SubscriptionState2.NotifyWait:
          if (newState !== SubscriptionState2.Subscribed && newState !== SubscriptionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SubscriptionState2.Subscribed:
          if (newState !== SubscriptionState2.Terminated) {
            invalidTransition();
          }
          break;
        case SubscriptionState2.Terminated:
          invalidTransition();
          break;
        default:
          throw new Error("Unrecognized state.");
      }
      if (this._state === newState) {
        return;
      }
      this._state = newState;
      this._logger.log(`Subscription ${this._dialog ? this._dialog.id : void 0} transitioned to ${this._state}`);
      this._stateEventEmitter.emit(this._state);
      if (newState === SubscriptionState2.Terminated) {
        this.dispose();
      }
    }
  };

  // node_modules/sip.js/lib/api/subscriber.js
  var Subscriber = class extends Subscription {
    /**
     * Constructor.
     * @param userAgent - User agent. See {@link UserAgent} for details.
     * @param targetURI - The request URI identifying the subscribed event.
     * @param eventType - The event type identifying the subscribed event.
     * @param options - Options bucket. See {@link SubscriberOptions} for details.
     */
    constructor(userAgent, targetURI, eventType, options = {}) {
      super(userAgent, options);
      this.body = void 0;
      this.logger = userAgent.getLogger("sip.Subscriber");
      if (options.body) {
        this.body = {
          body: options.body,
          contentType: options.contentType ? options.contentType : "application/sdp"
        };
      }
      this.targetURI = targetURI;
      this.event = eventType;
      if (options.expires === void 0) {
        this.expires = 3600;
      } else if (typeof options.expires !== "number") {
        this.logger.warn(`Option "expires" must be a number. Using default of 3600.`);
        this.expires = 3600;
      } else {
        this.expires = options.expires;
      }
      this.extraHeaders = (options.extraHeaders || []).slice();
      this.subscriberRequest = this.initSubscriberRequest();
      this.outgoingRequestMessage = this.subscriberRequest.message;
      this.id = this.outgoingRequestMessage.callId + this.outgoingRequestMessage.from.parameters.tag + this.event;
      this._userAgent._subscriptions[this.id] = this;
    }
    /**
     * Destructor.
     * @internal
     */
    dispose() {
      if (this.disposed) {
        return Promise.resolve();
      }
      this.logger.log(`Subscription ${this.id} in state ${this.state} is being disposed`);
      delete this._userAgent._subscriptions[this.id];
      if (this.retryAfterTimer) {
        clearTimeout(this.retryAfterTimer);
        this.retryAfterTimer = void 0;
      }
      this.subscriberRequest.dispose();
      return super.dispose().then(() => {
        if (this.state !== SubscriptionState2.Subscribed) {
          return;
        }
        if (!this._dialog) {
          throw new Error("Dialog undefined.");
        }
        if (this._dialog.subscriptionState === SubscriptionState.Pending || this._dialog.subscriptionState === SubscriptionState.Active) {
          const dialog = this._dialog;
          return new Promise((resolve, reject) => {
            dialog.delegate = {
              onTerminated: () => resolve()
            };
            dialog.unsubscribe();
          });
        }
      });
    }
    /**
     * Subscribe to event notifications.
     *
     * @remarks
     * Send an initial SUBSCRIBE request if no subscription as been established.
     * Sends a re-SUBSCRIBE request if the subscription is "active".
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    subscribe(options = {}) {
      switch (this.subscriberRequest.state) {
        case SubscriptionState.Initial:
          if (this.state === SubscriptionState2.Initial) {
            this.stateTransition(SubscriptionState2.NotifyWait);
          }
          this.subscriberRequest.subscribe().then((result) => {
            if (result.success) {
              if (result.success.subscription) {
                this._dialog = result.success.subscription;
                this._dialog.delegate = {
                  onNotify: (request) => this.onNotify(request),
                  onRefresh: (request) => this.onRefresh(request),
                  onTerminated: () => {
                    if (this.state !== SubscriptionState2.Terminated) {
                      this.stateTransition(SubscriptionState2.Terminated);
                    }
                  }
                };
              }
              this.onNotify(result.success.request);
            } else if (result.failure) {
              this.unsubscribe();
            }
          });
          break;
        case SubscriptionState.NotifyWait:
          break;
        case SubscriptionState.Pending:
          break;
        case SubscriptionState.Active:
          if (this._dialog) {
            const request = this._dialog.refresh();
            request.delegate = {
              onAccept: (response) => this.onAccepted(response),
              // eslint-disable-next-line @typescript-eslint/no-unused-vars
              onRedirect: (response) => this.unsubscribe(),
              // eslint-disable-next-line @typescript-eslint/no-unused-vars
              onReject: (response) => this.unsubscribe()
            };
          }
          break;
        case SubscriptionState.Terminated:
          break;
        default:
          break;
      }
      return Promise.resolve();
    }
    /**
     * {@inheritDoc Subscription.unsubscribe}
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    unsubscribe(options = {}) {
      if (this.disposed) {
        return Promise.resolve();
      }
      switch (this.subscriberRequest.state) {
        case SubscriptionState.Initial:
          break;
        case SubscriptionState.NotifyWait:
          break;
        case SubscriptionState.Pending:
          if (this._dialog) {
            this._dialog.unsubscribe();
          }
          break;
        case SubscriptionState.Active:
          if (this._dialog) {
            this._dialog.unsubscribe();
          }
          break;
        case SubscriptionState.Terminated:
          break;
        default:
          throw new Error("Unknown state.");
      }
      this.stateTransition(SubscriptionState2.Terminated);
      return Promise.resolve();
    }
    /**
     * Sends a re-SUBSCRIBE request if the subscription is "active".
     * @deprecated Use `subscribe` instead.
     * @internal
     */
    _refresh() {
      if (this.subscriberRequest.state === SubscriptionState.Active) {
        return this.subscribe();
      }
      return Promise.resolve();
    }
    /** @internal */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    onAccepted(response) {
    }
    /** @internal */
    onNotify(request) {
      if (this.disposed) {
        request.accept();
        return;
      }
      if (this.state !== SubscriptionState2.Subscribed) {
        this.stateTransition(SubscriptionState2.Subscribed);
      }
      if (this.delegate && this.delegate.onNotify) {
        const notification = new Notification(request);
        this.delegate.onNotify(notification);
      } else {
        request.accept();
      }
      const subscriptionState = request.message.parseHeader("Subscription-State");
      if (subscriptionState && subscriptionState.state) {
        switch (subscriptionState.state) {
          case "terminated":
            if (subscriptionState.reason) {
              this.logger.log(`Terminated subscription with reason ${subscriptionState.reason}`);
              switch (subscriptionState.reason) {
                case "deactivated":
                case "timeout":
                  this.initSubscriberRequest();
                  this.subscribe();
                  return;
                case "probation":
                case "giveup":
                  this.initSubscriberRequest();
                  if (subscriptionState.params && subscriptionState.params["retry-after"]) {
                    this.retryAfterTimer = setTimeout(() => {
                      this.subscribe();
                    }, subscriptionState.params["retry-after"]);
                  } else {
                    this.subscribe();
                  }
                  return;
                case "rejected":
                case "noresource":
                case "invariant":
                  break;
              }
            }
            this.unsubscribe();
            break;
          default:
            break;
        }
      }
    }
    /** @internal */
    onRefresh(request) {
      request.delegate = {
        onAccept: (response) => this.onAccepted(response)
      };
    }
    initSubscriberRequest() {
      const options = {
        extraHeaders: this.extraHeaders,
        body: this.body ? fromBodyLegacy(this.body) : void 0
      };
      this.subscriberRequest = new SubscriberRequest(this._userAgent.userAgentCore, this.targetURI, this.event, this.expires, options);
      this.subscriberRequest.delegate = {
        onAccept: (response) => this.onAccepted(response)
      };
      return this.subscriberRequest;
    }
  };
  var SubscriberRequest = class {
    constructor(core, target, event, expires, options, delegate) {
      this.core = core;
      this.target = target;
      this.event = event;
      this.expires = expires;
      this.subscribed = false;
      this.logger = core.loggerFactory.getLogger("sip.Subscriber");
      this.delegate = delegate;
      const allowHeader = "Allow: " + AllowedMethods.toString();
      const extraHeaders = (options && options.extraHeaders || []).slice();
      extraHeaders.push(allowHeader);
      extraHeaders.push("Event: " + this.event);
      extraHeaders.push("Expires: " + this.expires);
      extraHeaders.push("Contact: " + this.core.configuration.contact.toString());
      const body = options && options.body;
      this.message = core.makeOutgoingRequestMessage(C.SUBSCRIBE, this.target, this.core.configuration.aor, this.target, {}, extraHeaders, body);
    }
    /** Destructor. */
    dispose() {
      if (this.request) {
        this.request.waitNotifyStop();
        this.request.dispose();
        this.request = void 0;
      }
    }
    /** Subscription state. */
    get state() {
      if (this.subscription) {
        return this.subscription.subscriptionState;
      } else if (this.subscribed) {
        return SubscriptionState.NotifyWait;
      } else {
        return SubscriptionState.Initial;
      }
    }
    /**
     * Establish subscription.
     * @param options Options bucket.
     */
    subscribe() {
      if (this.subscribed) {
        return Promise.reject(new Error("Not in initial state. Did you call subscribe more than once?"));
      }
      this.subscribed = true;
      return new Promise((resolve) => {
        if (!this.message) {
          throw new Error("Message undefined.");
        }
        this.request = this.core.subscribe(this.message, {
          // This SUBSCRIBE request will be confirmed with a final response.
          // 200-class responses indicate that the subscription has been accepted
          // and that a NOTIFY request will be sent immediately.
          // https://tools.ietf.org/html/rfc6665#section-4.1.2.1
          onAccept: (response) => {
            if (this.delegate && this.delegate.onAccept) {
              this.delegate.onAccept(response);
            }
          },
          // Due to the potential for out-of-order messages, packet loss, and
          // forking, the subscriber MUST be prepared to receive NOTIFY requests
          // before the SUBSCRIBE transaction has completed.
          // https://tools.ietf.org/html/rfc6665#section-4.1.2.4
          onNotify: (requestWithSubscription) => {
            this.subscription = requestWithSubscription.subscription;
            if (this.subscription) {
              this.subscription.autoRefresh = true;
            }
            resolve({ success: requestWithSubscription });
          },
          // If this Timer N expires prior to the receipt of a NOTIFY request,
          // the subscriber considers the subscription failed, and cleans up
          // any state associated with the subscription attempt.
          // https://tools.ietf.org/html/rfc6665#section-4.1.2.4
          onNotifyTimeout: () => {
            resolve({ failure: {} });
          },
          // This SUBSCRIBE request will be confirmed with a final response.
          // Non-200-class final responses indicate that no subscription or new
          // dialog usage has been created, and no subsequent NOTIFY request will
          // be sent.
          // https://tools.ietf.org/html/rfc6665#section-4.1.2.1
          onRedirect: (response) => {
            resolve({ failure: { response } });
          },
          // This SUBSCRIBE request will be confirmed with a final response.
          // Non-200-class final responses indicate that no subscription or new
          // dialog usage has been created, and no subsequent NOTIFY request will
          // be sent.
          // https://tools.ietf.org/html/rfc6665#section-4.1.2.1
          onReject: (response) => {
            resolve({ failure: { response } });
          }
        });
      });
    }
  };

  // node_modules/sip.js/lib/api/transport-state.js
  var TransportState;
  (function(TransportState2) {
    TransportState2["Connecting"] = "Connecting";
    TransportState2["Connected"] = "Connected";
    TransportState2["Disconnecting"] = "Disconnecting";
    TransportState2["Disconnected"] = "Disconnected";
  })(TransportState = TransportState || (TransportState = {}));

  // node_modules/sip.js/lib/api/user-agent-state.js
  var UserAgentState;
  (function(UserAgentState2) {
    UserAgentState2["Started"] = "Started";
    UserAgentState2["Stopped"] = "Stopped";
  })(UserAgentState = UserAgentState || (UserAgentState = {}));

  // node_modules/sip.js/lib/core/messages/md5.js
  var Md5 = class _Md5 {
    constructor() {
      this._dataLength = 0;
      this._bufferLength = 0;
      this._state = new Int32Array(4);
      this._buffer = new ArrayBuffer(68);
      this._buffer8 = new Uint8Array(this._buffer, 0, 68);
      this._buffer32 = new Uint32Array(this._buffer, 0, 17);
      this.start();
    }
    static hashStr(str, raw = false) {
      return this.onePassHasher.start().appendStr(str).end(raw);
    }
    static hashAsciiStr(str, raw = false) {
      return this.onePassHasher.start().appendAsciiStr(str).end(raw);
    }
    static _hex(x) {
      const hc = _Md5.hexChars;
      const ho = _Md5.hexOut;
      let n;
      let offset;
      let j;
      let i;
      for (i = 0; i < 4; i += 1) {
        offset = i * 8;
        n = x[i];
        for (j = 0; j < 8; j += 2) {
          ho[offset + 1 + j] = hc.charAt(n & 15);
          n >>>= 4;
          ho[offset + 0 + j] = hc.charAt(n & 15);
          n >>>= 4;
        }
      }
      return ho.join("");
    }
    static _md5cycle(x, k) {
      let a = x[0];
      let b = x[1];
      let c = x[2];
      let d = x[3];
      a += (b & c | ~b & d) + k[0] - 680876936 | 0;
      a = (a << 7 | a >>> 25) + b | 0;
      d += (a & b | ~a & c) + k[1] - 389564586 | 0;
      d = (d << 12 | d >>> 20) + a | 0;
      c += (d & a | ~d & b) + k[2] + 606105819 | 0;
      c = (c << 17 | c >>> 15) + d | 0;
      b += (c & d | ~c & a) + k[3] - 1044525330 | 0;
      b = (b << 22 | b >>> 10) + c | 0;
      a += (b & c | ~b & d) + k[4] - 176418897 | 0;
      a = (a << 7 | a >>> 25) + b | 0;
      d += (a & b | ~a & c) + k[5] + 1200080426 | 0;
      d = (d << 12 | d >>> 20) + a | 0;
      c += (d & a | ~d & b) + k[6] - 1473231341 | 0;
      c = (c << 17 | c >>> 15) + d | 0;
      b += (c & d | ~c & a) + k[7] - 45705983 | 0;
      b = (b << 22 | b >>> 10) + c | 0;
      a += (b & c | ~b & d) + k[8] + 1770035416 | 0;
      a = (a << 7 | a >>> 25) + b | 0;
      d += (a & b | ~a & c) + k[9] - 1958414417 | 0;
      d = (d << 12 | d >>> 20) + a | 0;
      c += (d & a | ~d & b) + k[10] - 42063 | 0;
      c = (c << 17 | c >>> 15) + d | 0;
      b += (c & d | ~c & a) + k[11] - 1990404162 | 0;
      b = (b << 22 | b >>> 10) + c | 0;
      a += (b & c | ~b & d) + k[12] + 1804603682 | 0;
      a = (a << 7 | a >>> 25) + b | 0;
      d += (a & b | ~a & c) + k[13] - 40341101 | 0;
      d = (d << 12 | d >>> 20) + a | 0;
      c += (d & a | ~d & b) + k[14] - 1502002290 | 0;
      c = (c << 17 | c >>> 15) + d | 0;
      b += (c & d | ~c & a) + k[15] + 1236535329 | 0;
      b = (b << 22 | b >>> 10) + c | 0;
      a += (b & d | c & ~d) + k[1] - 165796510 | 0;
      a = (a << 5 | a >>> 27) + b | 0;
      d += (a & c | b & ~c) + k[6] - 1069501632 | 0;
      d = (d << 9 | d >>> 23) + a | 0;
      c += (d & b | a & ~b) + k[11] + 643717713 | 0;
      c = (c << 14 | c >>> 18) + d | 0;
      b += (c & a | d & ~a) + k[0] - 373897302 | 0;
      b = (b << 20 | b >>> 12) + c | 0;
      a += (b & d | c & ~d) + k[5] - 701558691 | 0;
      a = (a << 5 | a >>> 27) + b | 0;
      d += (a & c | b & ~c) + k[10] + 38016083 | 0;
      d = (d << 9 | d >>> 23) + a | 0;
      c += (d & b | a & ~b) + k[15] - 660478335 | 0;
      c = (c << 14 | c >>> 18) + d | 0;
      b += (c & a | d & ~a) + k[4] - 405537848 | 0;
      b = (b << 20 | b >>> 12) + c | 0;
      a += (b & d | c & ~d) + k[9] + 568446438 | 0;
      a = (a << 5 | a >>> 27) + b | 0;
      d += (a & c | b & ~c) + k[14] - 1019803690 | 0;
      d = (d << 9 | d >>> 23) + a | 0;
      c += (d & b | a & ~b) + k[3] - 187363961 | 0;
      c = (c << 14 | c >>> 18) + d | 0;
      b += (c & a | d & ~a) + k[8] + 1163531501 | 0;
      b = (b << 20 | b >>> 12) + c | 0;
      a += (b & d | c & ~d) + k[13] - 1444681467 | 0;
      a = (a << 5 | a >>> 27) + b | 0;
      d += (a & c | b & ~c) + k[2] - 51403784 | 0;
      d = (d << 9 | d >>> 23) + a | 0;
      c += (d & b | a & ~b) + k[7] + 1735328473 | 0;
      c = (c << 14 | c >>> 18) + d | 0;
      b += (c & a | d & ~a) + k[12] - 1926607734 | 0;
      b = (b << 20 | b >>> 12) + c | 0;
      a += (b ^ c ^ d) + k[5] - 378558 | 0;
      a = (a << 4 | a >>> 28) + b | 0;
      d += (a ^ b ^ c) + k[8] - 2022574463 | 0;
      d = (d << 11 | d >>> 21) + a | 0;
      c += (d ^ a ^ b) + k[11] + 1839030562 | 0;
      c = (c << 16 | c >>> 16) + d | 0;
      b += (c ^ d ^ a) + k[14] - 35309556 | 0;
      b = (b << 23 | b >>> 9) + c | 0;
      a += (b ^ c ^ d) + k[1] - 1530992060 | 0;
      a = (a << 4 | a >>> 28) + b | 0;
      d += (a ^ b ^ c) + k[4] + 1272893353 | 0;
      d = (d << 11 | d >>> 21) + a | 0;
      c += (d ^ a ^ b) + k[7] - 155497632 | 0;
      c = (c << 16 | c >>> 16) + d | 0;
      b += (c ^ d ^ a) + k[10] - 1094730640 | 0;
      b = (b << 23 | b >>> 9) + c | 0;
      a += (b ^ c ^ d) + k[13] + 681279174 | 0;
      a = (a << 4 | a >>> 28) + b | 0;
      d += (a ^ b ^ c) + k[0] - 358537222 | 0;
      d = (d << 11 | d >>> 21) + a | 0;
      c += (d ^ a ^ b) + k[3] - 722521979 | 0;
      c = (c << 16 | c >>> 16) + d | 0;
      b += (c ^ d ^ a) + k[6] + 76029189 | 0;
      b = (b << 23 | b >>> 9) + c | 0;
      a += (b ^ c ^ d) + k[9] - 640364487 | 0;
      a = (a << 4 | a >>> 28) + b | 0;
      d += (a ^ b ^ c) + k[12] - 421815835 | 0;
      d = (d << 11 | d >>> 21) + a | 0;
      c += (d ^ a ^ b) + k[15] + 530742520 | 0;
      c = (c << 16 | c >>> 16) + d | 0;
      b += (c ^ d ^ a) + k[2] - 995338651 | 0;
      b = (b << 23 | b >>> 9) + c | 0;
      a += (c ^ (b | ~d)) + k[0] - 198630844 | 0;
      a = (a << 6 | a >>> 26) + b | 0;
      d += (b ^ (a | ~c)) + k[7] + 1126891415 | 0;
      d = (d << 10 | d >>> 22) + a | 0;
      c += (a ^ (d | ~b)) + k[14] - 1416354905 | 0;
      c = (c << 15 | c >>> 17) + d | 0;
      b += (d ^ (c | ~a)) + k[5] - 57434055 | 0;
      b = (b << 21 | b >>> 11) + c | 0;
      a += (c ^ (b | ~d)) + k[12] + 1700485571 | 0;
      a = (a << 6 | a >>> 26) + b | 0;
      d += (b ^ (a | ~c)) + k[3] - 1894986606 | 0;
      d = (d << 10 | d >>> 22) + a | 0;
      c += (a ^ (d | ~b)) + k[10] - 1051523 | 0;
      c = (c << 15 | c >>> 17) + d | 0;
      b += (d ^ (c | ~a)) + k[1] - 2054922799 | 0;
      b = (b << 21 | b >>> 11) + c | 0;
      a += (c ^ (b | ~d)) + k[8] + 1873313359 | 0;
      a = (a << 6 | a >>> 26) + b | 0;
      d += (b ^ (a | ~c)) + k[15] - 30611744 | 0;
      d = (d << 10 | d >>> 22) + a | 0;
      c += (a ^ (d | ~b)) + k[6] - 1560198380 | 0;
      c = (c << 15 | c >>> 17) + d | 0;
      b += (d ^ (c | ~a)) + k[13] + 1309151649 | 0;
      b = (b << 21 | b >>> 11) + c | 0;
      a += (c ^ (b | ~d)) + k[4] - 145523070 | 0;
      a = (a << 6 | a >>> 26) + b | 0;
      d += (b ^ (a | ~c)) + k[11] - 1120210379 | 0;
      d = (d << 10 | d >>> 22) + a | 0;
      c += (a ^ (d | ~b)) + k[2] + 718787259 | 0;
      c = (c << 15 | c >>> 17) + d | 0;
      b += (d ^ (c | ~a)) + k[9] - 343485551 | 0;
      b = (b << 21 | b >>> 11) + c | 0;
      x[0] = a + x[0] | 0;
      x[1] = b + x[1] | 0;
      x[2] = c + x[2] | 0;
      x[3] = d + x[3] | 0;
    }
    start() {
      this._dataLength = 0;
      this._bufferLength = 0;
      this._state.set(_Md5.stateIdentity);
      return this;
    }
    // Char to code point to to array conversion:
    // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/String/charCodeAt
    // #Example.3A_Fixing_charCodeAt_to_handle_non-Basic-Multilingual-Plane_characters_if_their_presence_earlier_in_the_string_is_unknown
    appendStr(str) {
      const buf8 = this._buffer8;
      const buf32 = this._buffer32;
      let bufLen = this._bufferLength;
      let code;
      let i;
      for (i = 0; i < str.length; i += 1) {
        code = str.charCodeAt(i);
        if (code < 128) {
          buf8[bufLen++] = code;
        } else if (code < 2048) {
          buf8[bufLen++] = (code >>> 6) + 192;
          buf8[bufLen++] = code & 63 | 128;
        } else if (code < 55296 || code > 56319) {
          buf8[bufLen++] = (code >>> 12) + 224;
          buf8[bufLen++] = code >>> 6 & 63 | 128;
          buf8[bufLen++] = code & 63 | 128;
        } else {
          code = (code - 55296) * 1024 + (str.charCodeAt(++i) - 56320) + 65536;
          if (code > 1114111) {
            throw new Error("Unicode standard supports code points up to U+10FFFF");
          }
          buf8[bufLen++] = (code >>> 18) + 240;
          buf8[bufLen++] = code >>> 12 & 63 | 128;
          buf8[bufLen++] = code >>> 6 & 63 | 128;
          buf8[bufLen++] = code & 63 | 128;
        }
        if (bufLen >= 64) {
          this._dataLength += 64;
          _Md5._md5cycle(this._state, buf32);
          bufLen -= 64;
          buf32[0] = buf32[16];
        }
      }
      this._bufferLength = bufLen;
      return this;
    }
    appendAsciiStr(str) {
      const buf8 = this._buffer8;
      const buf32 = this._buffer32;
      let bufLen = this._bufferLength;
      let i;
      let j = 0;
      for (; ; ) {
        i = Math.min(str.length - j, 64 - bufLen);
        while (i--) {
          buf8[bufLen++] = str.charCodeAt(j++);
        }
        if (bufLen < 64) {
          break;
        }
        this._dataLength += 64;
        _Md5._md5cycle(this._state, buf32);
        bufLen = 0;
      }
      this._bufferLength = bufLen;
      return this;
    }
    appendByteArray(input) {
      const buf8 = this._buffer8;
      const buf32 = this._buffer32;
      let bufLen = this._bufferLength;
      let i;
      let j = 0;
      for (; ; ) {
        i = Math.min(input.length - j, 64 - bufLen);
        while (i--) {
          buf8[bufLen++] = input[j++];
        }
        if (bufLen < 64) {
          break;
        }
        this._dataLength += 64;
        _Md5._md5cycle(this._state, buf32);
        bufLen = 0;
      }
      this._bufferLength = bufLen;
      return this;
    }
    getState() {
      const self = this;
      const s = self._state;
      return {
        buffer: String.fromCharCode.apply(null, self._buffer8),
        buflen: self._bufferLength,
        length: self._dataLength,
        state: [s[0], s[1], s[2], s[3]]
      };
    }
    setState(state) {
      const buf = state.buffer;
      const x = state.state;
      const s = this._state;
      let i;
      this._dataLength = state.length;
      this._bufferLength = state.buflen;
      s[0] = x[0];
      s[1] = x[1];
      s[2] = x[2];
      s[3] = x[3];
      for (i = 0; i < buf.length; i += 1) {
        this._buffer8[i] = buf.charCodeAt(i);
      }
    }
    end(raw = false) {
      const bufLen = this._bufferLength;
      const buf8 = this._buffer8;
      const buf32 = this._buffer32;
      const i = (bufLen >> 2) + 1;
      let dataBitsLen;
      this._dataLength += bufLen;
      buf8[bufLen] = 128;
      buf8[bufLen + 1] = buf8[bufLen + 2] = buf8[bufLen + 3] = 0;
      buf32.set(_Md5.buffer32Identity.subarray(i), i);
      if (bufLen > 55) {
        _Md5._md5cycle(this._state, buf32);
        buf32.set(_Md5.buffer32Identity);
      }
      dataBitsLen = this._dataLength * 8;
      if (dataBitsLen <= 4294967295) {
        buf32[14] = dataBitsLen;
      } else {
        const matches = dataBitsLen.toString(16).match(/(.*?)(.{0,8})$/);
        if (matches === null) {
          return;
        }
        const lo = parseInt(matches[2], 16);
        const hi = parseInt(matches[1], 16) || 0;
        buf32[14] = lo;
        buf32[15] = hi;
      }
      _Md5._md5cycle(this._state, buf32);
      return raw ? this._state : _Md5._hex(this._state);
    }
  };
  Md5.stateIdentity = new Int32Array([1732584193, -271733879, -1732584194, 271733878]);
  Md5.buffer32Identity = new Int32Array([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
  Md5.hexChars = "0123456789abcdef";
  Md5.hexOut = [];
  Md5.onePassHasher = new Md5();
  if (Md5.hashStr("hello") !== "5d41402abc4b2a76b9719d911017c592") {
    console.error("Md5 self test failed.");
  }

  // node_modules/sip.js/lib/core/messages/digest-authentication.js
  function MD5(s) {
    return Md5.hashStr(s);
  }
  var DigestAuthentication = class {
    /**
     * Constructor.
     * @param loggerFactory - LoggerFactory.
     * @param username - Username.
     * @param password - Password.
     */
    constructor(loggerFactory, ha1, username, password) {
      this.logger = loggerFactory.getLogger("sipjs.digestauthentication");
      this.username = username;
      this.password = password;
      this.ha1 = ha1;
      this.nc = 0;
      this.ncHex = "00000000";
    }
    /**
     * Performs Digest authentication given a SIP request and the challenge
     * received in a response to that request.
     * @param request -
     * @param challenge -
     * @returns true if credentials were successfully generated, false otherwise.
     */
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    authenticate(request, challenge, body) {
      this.algorithm = challenge.algorithm;
      this.realm = challenge.realm;
      this.nonce = challenge.nonce;
      this.opaque = challenge.opaque;
      this.stale = challenge.stale;
      if (this.algorithm) {
        if (this.algorithm !== "MD5") {
          this.logger.warn("challenge with Digest algorithm different than 'MD5', authentication aborted");
          return false;
        }
      } else {
        this.algorithm = "MD5";
      }
      if (!this.realm) {
        this.logger.warn("challenge without Digest realm, authentication aborted");
        return false;
      }
      if (!this.nonce) {
        this.logger.warn("challenge without Digest nonce, authentication aborted");
        return false;
      }
      if (challenge.qop) {
        if (challenge.qop.indexOf("auth") > -1) {
          this.qop = "auth";
        } else if (challenge.qop.indexOf("auth-int") > -1) {
          this.qop = "auth-int";
        } else {
          this.logger.warn("challenge without Digest qop different than 'auth' or 'auth-int', authentication aborted");
          return false;
        }
      } else {
        this.qop = void 0;
      }
      this.method = request.method;
      this.uri = request.ruri;
      this.cnonce = createRandomToken(12);
      this.nc += 1;
      this.updateNcHex();
      if (this.nc === 4294967296) {
        this.nc = 1;
        this.ncHex = "00000001";
      }
      this.calculateResponse(body);
      return true;
    }
    /**
     * Return the Proxy-Authorization or WWW-Authorization header value.
     */
    toString() {
      const authParams = [];
      if (!this.response) {
        throw new Error("response field does not exist, cannot generate Authorization header");
      }
      authParams.push("algorithm=" + this.algorithm);
      authParams.push('username="' + this.username + '"');
      authParams.push('realm="' + this.realm + '"');
      authParams.push('nonce="' + this.nonce + '"');
      authParams.push('uri="' + this.uri + '"');
      authParams.push('response="' + this.response + '"');
      if (this.opaque) {
        authParams.push('opaque="' + this.opaque + '"');
      }
      if (this.qop) {
        authParams.push("qop=" + this.qop);
        authParams.push('cnonce="' + this.cnonce + '"');
        authParams.push("nc=" + this.ncHex);
      }
      return "Digest " + authParams.join(", ");
    }
    /**
     * Generate the 'nc' value as required by Digest in this.ncHex by reading this.nc.
     */
    updateNcHex() {
      const hex = Number(this.nc).toString(16);
      this.ncHex = "00000000".substr(0, 8 - hex.length) + hex;
    }
    /**
     * Generate Digest 'response' value.
     */
    calculateResponse(body) {
      let ha1, ha2;
      ha1 = this.ha1;
      if (ha1 === "" || ha1 === void 0) {
        ha1 = MD5(this.username + ":" + this.realm + ":" + this.password);
      }
      if (this.qop === "auth") {
        ha2 = MD5(this.method + ":" + this.uri);
        this.response = MD5(ha1 + ":" + this.nonce + ":" + this.ncHex + ":" + this.cnonce + ":auth:" + ha2);
      } else if (this.qop === "auth-int") {
        ha2 = MD5(this.method + ":" + this.uri + ":" + MD5(body ? body : ""));
        this.response = MD5(ha1 + ":" + this.nonce + ":" + this.ncHex + ":" + this.cnonce + ":auth-int:" + ha2);
      } else if (this.qop === void 0) {
        ha2 = MD5(this.method + ":" + this.uri);
        this.response = MD5(ha1 + ":" + this.nonce + ":" + ha2);
      }
    }
  };

  // node_modules/sip.js/lib/core/log/levels.js
  var Levels;
  (function(Levels2) {
    Levels2[Levels2["error"] = 0] = "error";
    Levels2[Levels2["warn"] = 1] = "warn";
    Levels2[Levels2["log"] = 2] = "log";
    Levels2[Levels2["debug"] = 3] = "debug";
  })(Levels = Levels || (Levels = {}));

  // node_modules/sip.js/lib/core/log/logger.js
  var Logger = class {
    constructor(logger, category, label) {
      this.logger = logger;
      this.category = category;
      this.label = label;
    }
    error(content) {
      this.genericLog(Levels.error, content);
    }
    warn(content) {
      this.genericLog(Levels.warn, content);
    }
    log(content) {
      this.genericLog(Levels.log, content);
    }
    debug(content) {
      this.genericLog(Levels.debug, content);
    }
    genericLog(level, content) {
      this.logger.genericLog(level, this.category, this.label, content);
    }
    get level() {
      return this.logger.level;
    }
    set level(newLevel) {
      this.logger.level = newLevel;
    }
  };

  // node_modules/sip.js/lib/core/log/logger-factory.js
  var LoggerFactory = class {
    constructor() {
      this.builtinEnabled = true;
      this._level = Levels.log;
      this.loggers = {};
      this.logger = this.getLogger("sip:loggerfactory");
    }
    get level() {
      return this._level;
    }
    set level(newLevel) {
      if (newLevel >= 0 && newLevel <= 3) {
        this._level = newLevel;
      } else if (newLevel > 3) {
        this._level = 3;
      } else if (Levels.hasOwnProperty(newLevel)) {
        this._level = newLevel;
      } else {
        this.logger.error("invalid 'level' parameter value: " + JSON.stringify(newLevel));
      }
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    get connector() {
      return this._connector;
    }
    set connector(value) {
      if (!value) {
        this._connector = void 0;
      } else if (typeof value === "function") {
        this._connector = value;
      } else {
        this.logger.error("invalid 'connector' parameter value: " + JSON.stringify(value));
      }
    }
    getLogger(category, label) {
      if (label && this.level === 3) {
        return new Logger(this, category, label);
      } else if (this.loggers[category]) {
        return this.loggers[category];
      } else {
        const logger = new Logger(this, category);
        this.loggers[category] = logger;
        return logger;
      }
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    genericLog(levelToLog, category, label, content) {
      if (this.level >= levelToLog) {
        if (this.builtinEnabled) {
          this.print(levelToLog, category, label, content);
        }
      }
      if (this.connector) {
        this.connector(Levels[levelToLog], category, label, content);
      }
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    print(levelToLog, category, label, content) {
      if (typeof content === "string") {
        const prefix = [/* @__PURE__ */ new Date(), category];
        if (label) {
          prefix.push(label);
        }
        content = prefix.concat(content).join(" | ");
      }
      switch (levelToLog) {
        case Levels.error:
          console.error(content);
          break;
        case Levels.warn:
          console.warn(content);
          break;
        case Levels.log:
          console.log(content);
          break;
        case Levels.debug:
          console.debug(content);
          break;
        default:
          break;
      }
    }
  };

  // node_modules/sip.js/lib/core/messages/parser.js
  var Parser;
  (function(Parser2) {
    function getHeader(data, headerStart) {
      let start = headerStart;
      let end = 0;
      let partialEnd = 0;
      if (data.substring(start, start + 2).match(/(^\r\n)/)) {
        return -2;
      }
      while (end === 0) {
        partialEnd = data.indexOf("\r\n", start);
        if (partialEnd === -1) {
          return partialEnd;
        }
        if (!data.substring(partialEnd + 2, partialEnd + 4).match(/(^\r\n)/) && data.charAt(partialEnd + 2).match(/(^\s+)/)) {
          start = partialEnd + 2;
        } else {
          end = partialEnd;
        }
      }
      return end;
    }
    Parser2.getHeader = getHeader;
    function parseHeader(message, data, headerStart, headerEnd) {
      const hcolonIndex = data.indexOf(":", headerStart);
      const headerName = data.substring(headerStart, hcolonIndex).trim();
      const headerValue = data.substring(hcolonIndex + 1, headerEnd).trim();
      let parsed;
      switch (headerName.toLowerCase()) {
        case "via":
        case "v":
          message.addHeader("via", headerValue);
          if (message.getHeaders("via").length === 1) {
            parsed = message.parseHeader("Via");
            if (parsed) {
              message.via = parsed;
              message.viaBranch = parsed.branch;
            }
          } else {
            parsed = 0;
          }
          break;
        case "from":
        case "f":
          message.setHeader("from", headerValue);
          parsed = message.parseHeader("from");
          if (parsed) {
            message.from = parsed;
            message.fromTag = parsed.getParam("tag");
          }
          break;
        case "to":
        case "t":
          message.setHeader("to", headerValue);
          parsed = message.parseHeader("to");
          if (parsed) {
            message.to = parsed;
            message.toTag = parsed.getParam("tag");
          }
          break;
        case "record-route":
          parsed = Grammar.parse(headerValue, "Record_Route");
          if (parsed === -1) {
            parsed = void 0;
            break;
          }
          if (!(parsed instanceof Array)) {
            parsed = void 0;
            break;
          }
          parsed.forEach((header) => {
            message.addHeader("record-route", headerValue.substring(header.position, header.offset));
            message.headers["Record-Route"][message.getHeaders("record-route").length - 1].parsed = header.parsed;
          });
          break;
        case "call-id":
        case "i":
          message.setHeader("call-id", headerValue);
          parsed = message.parseHeader("call-id");
          if (parsed) {
            message.callId = headerValue;
          }
          break;
        case "contact":
        case "m":
          parsed = Grammar.parse(headerValue, "Contact");
          if (parsed === -1) {
            parsed = void 0;
            break;
          }
          if (!(parsed instanceof Array)) {
            parsed = void 0;
            break;
          }
          parsed.forEach((header) => {
            message.addHeader("contact", headerValue.substring(header.position, header.offset));
            message.headers.Contact[message.getHeaders("contact").length - 1].parsed = header.parsed;
          });
          break;
        case "content-length":
        case "l":
          message.setHeader("content-length", headerValue);
          parsed = message.parseHeader("content-length");
          break;
        case "content-type":
        case "c":
          message.setHeader("content-type", headerValue);
          parsed = message.parseHeader("content-type");
          break;
        case "cseq":
          message.setHeader("cseq", headerValue);
          parsed = message.parseHeader("cseq");
          if (parsed) {
            message.cseq = parsed.value;
          }
          if (message instanceof IncomingResponseMessage) {
            message.method = parsed.method;
          }
          break;
        case "max-forwards":
          message.setHeader("max-forwards", headerValue);
          parsed = message.parseHeader("max-forwards");
          break;
        case "www-authenticate":
          message.setHeader("www-authenticate", headerValue);
          parsed = message.parseHeader("www-authenticate");
          break;
        case "proxy-authenticate":
          message.setHeader("proxy-authenticate", headerValue);
          parsed = message.parseHeader("proxy-authenticate");
          break;
        case "refer-to":
        case "r":
          message.setHeader("refer-to", headerValue);
          parsed = message.parseHeader("refer-to");
          if (parsed) {
            message.referTo = parsed;
          }
          break;
        default:
          message.addHeader(headerName.toLowerCase(), headerValue);
          parsed = 0;
      }
      if (parsed === void 0) {
        return {
          error: "error parsing header '" + headerName + "'"
        };
      } else {
        return true;
      }
    }
    Parser2.parseHeader = parseHeader;
    function parseMessage(data, logger) {
      let headerStart = 0;
      let headerEnd = data.indexOf("\r\n");
      if (headerEnd === -1) {
        logger.warn("no CRLF found, not a SIP message, discarded");
        return;
      }
      const firstLine = data.substring(0, headerEnd);
      const parsed = Grammar.parse(firstLine, "Request_Response");
      let message;
      if (parsed === -1) {
        logger.warn('error parsing first line of SIP message: "' + firstLine + '"');
        return;
      } else if (!parsed.status_code) {
        message = new IncomingRequestMessage();
        message.method = parsed.method;
        message.ruri = parsed.uri;
      } else {
        message = new IncomingResponseMessage();
        message.statusCode = parsed.status_code;
        message.reasonPhrase = parsed.reason_phrase;
      }
      message.data = data;
      headerStart = headerEnd + 2;
      let bodyStart;
      while (true) {
        headerEnd = getHeader(data, headerStart);
        if (headerEnd === -2) {
          bodyStart = headerStart + 2;
          break;
        } else if (headerEnd === -1) {
          logger.error("malformed message");
          return;
        }
        const parsedHeader = parseHeader(message, data, headerStart, headerEnd);
        if (parsedHeader && parsedHeader !== true) {
          logger.error(parsedHeader.error);
          return;
        }
        headerStart = headerEnd + 2;
      }
      if (message.hasHeader("content-length")) {
        message.body = data.substr(bodyStart, Number(message.getHeader("content-length")));
      } else {
        message.body = data.substring(bodyStart);
      }
      return message;
    }
    Parser2.parseMessage = parseMessage;
  })(Parser = Parser || (Parser = {}));

  // node_modules/sip.js/lib/core/messages/outgoing-response.js
  function constructOutgoingResponse(message, options) {
    const CRLF = "\r\n";
    if (options.statusCode < 100 || options.statusCode > 699) {
      throw new TypeError("Invalid statusCode: " + options.statusCode);
    }
    const reasonPhrase = options.reasonPhrase ? options.reasonPhrase : getReasonPhrase(options.statusCode);
    let response = "SIP/2.0 " + options.statusCode + " " + reasonPhrase + CRLF;
    if (options.statusCode >= 100 && options.statusCode < 200) {
    }
    if (options.statusCode === 100) {
    }
    const fromHeader = "From: " + message.getHeader("From") + CRLF;
    const callIdHeader = "Call-ID: " + message.callId + CRLF;
    const cSeqHeader = "CSeq: " + message.cseq + " " + message.method + CRLF;
    const viaHeaders = message.getHeaders("via").reduce((previous, current) => {
      return previous + "Via: " + current + CRLF;
    }, "");
    let toHeader = "To: " + message.getHeader("to");
    if (options.statusCode > 100 && !message.parseHeader("to").hasParam("tag")) {
      let toTag = options.toTag;
      if (!toTag) {
        toTag = newTag();
      }
      toHeader += ";tag=" + toTag;
    }
    toHeader += CRLF;
    let supportedHeader = "";
    if (options.supported) {
      supportedHeader = "Supported: " + options.supported.join(", ") + CRLF;
    }
    let userAgentHeader = "";
    if (options.userAgent) {
      userAgentHeader = "User-Agent: " + options.userAgent + CRLF;
    }
    let extensionHeaders = "";
    if (options.extraHeaders) {
      extensionHeaders = options.extraHeaders.reduce((previous, current) => {
        return previous + current.trim() + CRLF;
      }, "");
    }
    response += viaHeaders;
    response += fromHeader;
    response += toHeader;
    response += cSeqHeader;
    response += callIdHeader;
    response += supportedHeader;
    response += userAgentHeader;
    response += extensionHeaders;
    if (options.body) {
      response += "Content-Type: " + options.body.contentType + CRLF;
      response += "Content-Length: " + utf8Length(options.body.content) + CRLF + CRLF;
      response += options.body.content;
    } else {
      response += "Content-Length: 0" + CRLF + CRLF;
    }
    return { message: response };
  }

  // node_modules/sip.js/lib/core/exceptions/transport-error.js
  var TransportError = class extends Exception {
    constructor(message) {
      super(message ? message : "Unspecified transport error.");
    }
  };

  // node_modules/sip.js/lib/core/transactions/transaction.js
  var Transaction = class {
    constructor(_transport, _user, _id, _state, loggerCategory) {
      this._transport = _transport;
      this._user = _user;
      this._id = _id;
      this._state = _state;
      this.listeners = new Array();
      this.logger = _user.loggerFactory.getLogger(loggerCategory, _id);
      this.logger.debug(`Constructing ${this.typeToString()} with id ${this.id}.`);
    }
    /**
     * Destructor.
     * Once the transaction is in the "terminated" state, it is destroyed
     * immediately and there is no need to call `dispose`. However, if a
     * transaction needs to be ended prematurely, the transaction user may
     * do so by calling this method (for example, perhaps the UA is shutting down).
     * No state transition will occur upon calling this method, all outstanding
     * transmission timers will be cancelled, and use of the transaction after
     * calling `dispose` is undefined.
     */
    dispose() {
      this.logger.debug(`Destroyed ${this.typeToString()} with id ${this.id}.`);
    }
    /** Transaction id. */
    get id() {
      return this._id;
    }
    /** Transaction kind. Deprecated. */
    get kind() {
      throw new Error("Invalid kind.");
    }
    /** Transaction state. */
    get state() {
      return this._state;
    }
    /** Transaction transport. */
    get transport() {
      return this._transport;
    }
    /**
     * Sets up a function that will be called whenever the transaction state changes.
     * @param listener - Callback function.
     * @param options - An options object that specifies characteristics about the listener.
     *                  If once true, indicates that the listener should be invoked at most once after being added.
     *                  If once true, the listener would be automatically removed when invoked.
     */
    addStateChangeListener(listener, options) {
      const onceWrapper = () => {
        this.removeStateChangeListener(onceWrapper);
        listener();
      };
      (options === null || options === void 0 ? void 0 : options.once) === true ? this.listeners.push(onceWrapper) : this.listeners.push(listener);
    }
    /**
     * This is currently public so tests may spy on it.
     * @internal
     */
    notifyStateChangeListeners() {
      this.listeners.slice().forEach((listener) => listener());
    }
    /**
     * Removes a listener previously registered with addStateListener.
     * @param listener - Callback function.
     */
    removeStateChangeListener(listener) {
      this.listeners = this.listeners.filter((l) => l !== listener);
    }
    logTransportError(error, message) {
      this.logger.error(error.message);
      this.logger.error(`Transport error occurred in ${this.typeToString()} with id ${this.id}.`);
      this.logger.error(message);
    }
    /**
     * Pass message to transport for transmission. If transport fails,
     * the transaction user is notified by callback to onTransportError().
     * @returns
     * Rejects with `TransportError` if transport fails.
     */
    send(message) {
      return this.transport.send(message).catch((error) => {
        if (error instanceof TransportError) {
          this.onTransportError(error);
          throw error;
        }
        let transportError;
        if (error && typeof error.message === "string") {
          transportError = new TransportError(error.message);
        } else {
          transportError = new TransportError();
        }
        this.onTransportError(transportError);
        throw transportError;
      });
    }
    setState(state) {
      this.logger.debug(`State change to "${state}" on ${this.typeToString()} with id ${this.id}.`);
      this._state = state;
      if (this._user.onStateChange) {
        this._user.onStateChange(state);
      }
      this.notifyStateChangeListeners();
    }
    typeToString() {
      return "UnknownType";
    }
  };

  // node_modules/sip.js/lib/core/transactions/server-transaction.js
  var ServerTransaction = class extends Transaction {
    constructor(_request, transport, user, state, loggerCategory) {
      super(transport, user, _request.viaBranch, state, loggerCategory);
      this._request = _request;
      this.user = user;
    }
    /** The incoming request the transaction handling. */
    get request() {
      return this._request;
    }
  };

  // node_modules/sip.js/lib/core/transactions/transaction-state.js
  var TransactionState;
  (function(TransactionState2) {
    TransactionState2["Accepted"] = "Accepted";
    TransactionState2["Calling"] = "Calling";
    TransactionState2["Completed"] = "Completed";
    TransactionState2["Confirmed"] = "Confirmed";
    TransactionState2["Proceeding"] = "Proceeding";
    TransactionState2["Terminated"] = "Terminated";
    TransactionState2["Trying"] = "Trying";
  })(TransactionState = TransactionState || (TransactionState = {}));

  // node_modules/sip.js/lib/core/transactions/invite-server-transaction.js
  var InviteServerTransaction = class extends ServerTransaction {
    /**
     * Constructor.
     * Upon construction, a "100 Trying" reply will be immediately sent.
     * After construction the transaction will be in the "proceeding" state and the transaction
     * `id` will equal the branch parameter set in the Via header of the incoming request.
     * https://tools.ietf.org/html/rfc3261#section-17.2.1
     * @param request - Incoming INVITE request from the transport.
     * @param transport - The transport.
     * @param user - The transaction user.
     */
    constructor(request, transport, user) {
      super(request, transport, user, TransactionState.Proceeding, "sip.transaction.ist");
    }
    /**
     * Destructor.
     */
    dispose() {
      this.stopProgressExtensionTimer();
      if (this.H) {
        clearTimeout(this.H);
        this.H = void 0;
      }
      if (this.I) {
        clearTimeout(this.I);
        this.I = void 0;
      }
      if (this.L) {
        clearTimeout(this.L);
        this.L = void 0;
      }
      super.dispose();
    }
    /** Transaction kind. Deprecated. */
    get kind() {
      return "ist";
    }
    /**
     * Receive requests from transport matching this transaction.
     * @param request - Request matching this transaction.
     */
    receiveRequest(request) {
      switch (this.state) {
        case TransactionState.Proceeding:
          if (request.method === C.INVITE) {
            if (this.lastProvisionalResponse) {
              this.send(this.lastProvisionalResponse).catch((error) => {
                this.logTransportError(error, "Failed to send retransmission of provisional response.");
              });
            }
            return;
          }
          break;
        case TransactionState.Accepted:
          if (request.method === C.INVITE) {
            return;
          }
          break;
        case TransactionState.Completed:
          if (request.method === C.INVITE) {
            if (!this.lastFinalResponse) {
              throw new Error("Last final response undefined.");
            }
            this.send(this.lastFinalResponse).catch((error) => {
              this.logTransportError(error, "Failed to send retransmission of final response.");
            });
            return;
          }
          if (request.method === C.ACK) {
            this.stateTransition(TransactionState.Confirmed);
            return;
          }
          break;
        case TransactionState.Confirmed:
          if (request.method === C.INVITE || request.method === C.ACK) {
            return;
          }
          break;
        case TransactionState.Terminated:
          if (request.method === C.INVITE || request.method === C.ACK) {
            return;
          }
          break;
        default:
          throw new Error(`Invalid state ${this.state}`);
      }
      const message = `INVITE server transaction received unexpected ${request.method} request while in state ${this.state}.`;
      this.logger.warn(message);
      return;
    }
    /**
     * Receive responses from TU for this transaction.
     * @param statusCode - Status code of response.
     * @param response - Response.
     */
    receiveResponse(statusCode, response) {
      if (statusCode < 100 || statusCode > 699) {
        throw new Error(`Invalid status code ${statusCode}`);
      }
      switch (this.state) {
        case TransactionState.Proceeding:
          if (statusCode >= 100 && statusCode <= 199) {
            this.lastProvisionalResponse = response;
            if (statusCode > 100) {
              this.startProgressExtensionTimer();
            }
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send 1xx response.");
            });
            return;
          }
          if (statusCode >= 200 && statusCode <= 299) {
            this.lastFinalResponse = response;
            this.stateTransition(TransactionState.Accepted);
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send 2xx response.");
            });
            return;
          }
          if (statusCode >= 300 && statusCode <= 699) {
            this.lastFinalResponse = response;
            this.stateTransition(TransactionState.Completed);
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send non-2xx final response.");
            });
            return;
          }
          break;
        case TransactionState.Accepted:
          if (statusCode >= 200 && statusCode <= 299) {
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send 2xx response.");
            });
            return;
          }
          break;
        case TransactionState.Completed:
          break;
        case TransactionState.Confirmed:
          break;
        case TransactionState.Terminated:
          break;
        default:
          throw new Error(`Invalid state ${this.state}`);
      }
      const message = `INVITE server transaction received unexpected ${statusCode} response from TU while in state ${this.state}.`;
      this.logger.error(message);
      throw new Error(message);
    }
    /**
     * Retransmit the last 2xx response. This is a noop if not in the "accepted" state.
     */
    retransmitAcceptedResponse() {
      if (this.state === TransactionState.Accepted && this.lastFinalResponse) {
        this.send(this.lastFinalResponse).catch((error) => {
          this.logTransportError(error, "Failed to send 2xx response.");
        });
      }
    }
    /**
     * First, the procedures in [4] are followed, which attempt to deliver the response to a backup.
     * If those should all fail, based on the definition of failure in [4], the server transaction SHOULD
     * inform the TU that a failure has occurred, and MUST remain in the current state.
     * https://tools.ietf.org/html/rfc6026#section-8.8
     */
    onTransportError(error) {
      if (this.user.onTransportError) {
        this.user.onTransportError(error);
      }
    }
    /** For logging. */
    typeToString() {
      return "INVITE server transaction";
    }
    /**
     * Execute a state transition.
     * @param newState - New state.
     */
    stateTransition(newState) {
      const invalidStateTransition = () => {
        throw new Error(`Invalid state transition from ${this.state} to ${newState}`);
      };
      switch (newState) {
        case TransactionState.Proceeding:
          invalidStateTransition();
          break;
        case TransactionState.Accepted:
        case TransactionState.Completed:
          if (this.state !== TransactionState.Proceeding) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Confirmed:
          if (this.state !== TransactionState.Completed) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Terminated:
          if (this.state !== TransactionState.Accepted && this.state !== TransactionState.Completed && this.state !== TransactionState.Confirmed) {
            invalidStateTransition();
          }
          break;
        default:
          invalidStateTransition();
      }
      this.stopProgressExtensionTimer();
      if (newState === TransactionState.Accepted) {
        this.L = setTimeout(() => this.timerL(), Timers.TIMER_L);
      }
      if (newState === TransactionState.Completed) {
        this.H = setTimeout(() => this.timerH(), Timers.TIMER_H);
      }
      if (newState === TransactionState.Confirmed) {
        this.I = setTimeout(() => this.timerI(), Timers.TIMER_I);
      }
      if (newState === TransactionState.Terminated) {
        this.dispose();
      }
      this.setState(newState);
    }
    /**
     * FIXME: UAS Provisional Retransmission Timer. See RFC 3261 Section 13.3.1.1
     * This is in the wrong place. This is not a transaction level thing. It's a UAS level thing.
     */
    startProgressExtensionTimer() {
      if (this.progressExtensionTimer === void 0) {
        this.progressExtensionTimer = setInterval(() => {
          this.logger.debug(`Progress extension timer expired for INVITE server transaction ${this.id}.`);
          if (!this.lastProvisionalResponse) {
            throw new Error("Last provisional response undefined.");
          }
          this.send(this.lastProvisionalResponse).catch((error) => {
            this.logTransportError(error, "Failed to send retransmission of provisional response.");
          });
        }, Timers.PROVISIONAL_RESPONSE_INTERVAL);
      }
    }
    /**
     * FIXME: UAS Provisional Retransmission Timer id. See RFC 3261 Section 13.3.1.1
     * This is in the wrong place. This is not a transaction level thing. It's a UAS level thing.
     */
    stopProgressExtensionTimer() {
      if (this.progressExtensionTimer !== void 0) {
        clearInterval(this.progressExtensionTimer);
        this.progressExtensionTimer = void 0;
      }
    }
    /**
     * While in the "Proceeding" state, if the TU passes a response with status code
     * from 300 to 699 to the server transaction, the response MUST be passed to the
     * transport layer for transmission, and the state machine MUST enter the "Completed" state.
     * For unreliable transports, timer G is set to fire in T1 seconds, and is not set to fire for
     * reliable transports. If timer G fires, the response is passed to the transport layer once
     * more for retransmission, and timer G is set to fire in MIN(2*T1, T2) seconds. From then on,
     * when timer G fires, the response is passed to the transport again for transmission, and
     * timer G is reset with a value that doubles, unless that value exceeds T2, in which case
     * it is reset with the value of T2.
     * https://tools.ietf.org/html/rfc3261#section-17.2.1
     */
    timerG() {
    }
    /**
     * If timer H fires while in the "Completed" state, it implies that the ACK was never received.
     * In this case, the server transaction MUST transition to the "Terminated" state, and MUST
     * indicate to the TU that a transaction failure has occurred.
     * https://tools.ietf.org/html/rfc3261#section-17.2.1
     */
    timerH() {
      this.logger.debug(`Timer H expired for INVITE server transaction ${this.id}.`);
      if (this.state === TransactionState.Completed) {
        this.logger.warn("ACK to negative final response was never received, terminating transaction.");
        this.stateTransition(TransactionState.Terminated);
      }
    }
    /**
     * Once timer I fires, the server MUST transition to the "Terminated" state.
     * https://tools.ietf.org/html/rfc3261#section-17.2.1
     */
    timerI() {
      this.logger.debug(`Timer I expired for INVITE server transaction ${this.id}.`);
      this.stateTransition(TransactionState.Terminated);
    }
    /**
     * When Timer L fires and the state machine is in the "Accepted" state, the machine MUST
     * transition to the "Terminated" state. Once the transaction is in the "Terminated" state,
     * it MUST be destroyed immediately. Timer L reflects the amount of time the server
     * transaction could receive 2xx responses for retransmission from the
     * TU while it is waiting to receive an ACK.
     * https://tools.ietf.org/html/rfc6026#section-7.1
     * https://tools.ietf.org/html/rfc6026#section-8.7
     */
    timerL() {
      this.logger.debug(`Timer L expired for INVITE server transaction ${this.id}.`);
      if (this.state === TransactionState.Accepted) {
        this.stateTransition(TransactionState.Terminated);
      }
    }
  };

  // node_modules/sip.js/lib/core/transactions/client-transaction.js
  var ClientTransaction = class _ClientTransaction extends Transaction {
    constructor(_request, transport, user, state, loggerCategory) {
      super(transport, user, _ClientTransaction.makeId(_request), state, loggerCategory);
      this._request = _request;
      this.user = user;
      _request.setViaHeader(this.id, transport.protocol);
    }
    static makeId(request) {
      if (request.method === "CANCEL") {
        if (!request.branch) {
          throw new Error("Outgoing CANCEL request without a branch.");
        }
        return request.branch;
      } else {
        return "z9hG4bK" + Math.floor(Math.random() * 1e7);
      }
    }
    /** The outgoing request the transaction handling. */
    get request() {
      return this._request;
    }
    /**
     * A 408 to non-INVITE will always arrive too late to be useful ([3]),
     * The client already has full knowledge of the timeout. The only
     * information this message would convey is whether or not the server
     * believed the transaction timed out. However, with the current design
     * of the NIT, a client cannot do anything with this knowledge. Thus,
     * the 408 is simply wasting network resources and contributes to the
     * response bombardment illustrated in [3].
     * https://tools.ietf.org/html/rfc4320#section-4.1
     */
    onRequestTimeout() {
      if (this.user.onRequestTimeout) {
        this.user.onRequestTimeout();
      }
    }
  };

  // node_modules/sip.js/lib/core/transactions/non-invite-client-transaction.js
  var NonInviteClientTransaction = class extends ClientTransaction {
    /**
     * Constructor
     * Upon construction, the outgoing request's Via header is updated by calling `setViaHeader`.
     * Then `toString` is called on the outgoing request and the message is sent via the transport.
     * After construction the transaction will be in the "calling" state and the transaction id
     * will equal the branch parameter set in the Via header of the outgoing request.
     * https://tools.ietf.org/html/rfc3261#section-17.1.2
     * @param request - The outgoing Non-INVITE request.
     * @param transport - The transport.
     * @param user - The transaction user.
     */
    constructor(request, transport, user) {
      super(request, transport, user, TransactionState.Trying, "sip.transaction.nict");
      this.F = setTimeout(() => this.timerF(), Timers.TIMER_F);
      this.send(request.toString()).catch((error) => {
        this.logTransportError(error, "Failed to send initial outgoing request.");
      });
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this.F) {
        clearTimeout(this.F);
        this.F = void 0;
      }
      if (this.K) {
        clearTimeout(this.K);
        this.K = void 0;
      }
      super.dispose();
    }
    /** Transaction kind. Deprecated. */
    get kind() {
      return "nict";
    }
    /**
     * Handler for incoming responses from the transport which match this transaction.
     * @param response - The incoming response.
     */
    receiveResponse(response) {
      const statusCode = response.statusCode;
      if (!statusCode || statusCode < 100 || statusCode > 699) {
        throw new Error(`Invalid status code ${statusCode}`);
      }
      switch (this.state) {
        case TransactionState.Trying:
          if (statusCode >= 100 && statusCode <= 199) {
            this.stateTransition(TransactionState.Proceeding);
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          if (statusCode >= 200 && statusCode <= 699) {
            this.stateTransition(TransactionState.Completed);
            if (statusCode === 408) {
              this.onRequestTimeout();
              return;
            }
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          break;
        case TransactionState.Proceeding:
          if (statusCode >= 100 && statusCode <= 199) {
            if (this.user.receiveResponse) {
              return this.user.receiveResponse(response);
            }
          }
          if (statusCode >= 200 && statusCode <= 699) {
            this.stateTransition(TransactionState.Completed);
            if (statusCode === 408) {
              this.onRequestTimeout();
              return;
            }
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          break;
        case TransactionState.Completed:
          return;
        case TransactionState.Terminated:
          return;
        default:
          throw new Error(`Invalid state ${this.state}`);
      }
      const message = `Non-INVITE client transaction received unexpected ${statusCode} response while in state ${this.state}.`;
      this.logger.warn(message);
      return;
    }
    /**
     * The client transaction SHOULD inform the TU that a transport failure has occurred,
     * and the client transaction SHOULD transition directly to the "Terminated" state.
     * The TU will handle the fail over mechanisms described in [4].
     * https://tools.ietf.org/html/rfc3261#section-17.1.4
     * @param error - Transport error
     */
    onTransportError(error) {
      if (this.user.onTransportError) {
        this.user.onTransportError(error);
      }
      this.stateTransition(TransactionState.Terminated, true);
    }
    /** For logging. */
    typeToString() {
      return "non-INVITE client transaction";
    }
    /**
     * Execute a state transition.
     * @param newState - New state.
     */
    stateTransition(newState, dueToTransportError = false) {
      const invalidStateTransition = () => {
        throw new Error(`Invalid state transition from ${this.state} to ${newState}`);
      };
      switch (newState) {
        case TransactionState.Trying:
          invalidStateTransition();
          break;
        case TransactionState.Proceeding:
          if (this.state !== TransactionState.Trying) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Completed:
          if (this.state !== TransactionState.Trying && this.state !== TransactionState.Proceeding) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Terminated:
          if (this.state !== TransactionState.Trying && this.state !== TransactionState.Proceeding && this.state !== TransactionState.Completed) {
            if (!dueToTransportError) {
              invalidStateTransition();
            }
          }
          break;
        default:
          invalidStateTransition();
      }
      if (newState === TransactionState.Completed) {
        if (this.F) {
          clearTimeout(this.F);
          this.F = void 0;
        }
        this.K = setTimeout(() => this.timerK(), Timers.TIMER_K);
      }
      if (newState === TransactionState.Terminated) {
        this.dispose();
      }
      this.setState(newState);
    }
    /**
     * If Timer F fires while the client transaction is still in the
     * "Trying" state, the client transaction SHOULD inform the TU about the
     * timeout, and then it SHOULD enter the "Terminated" state.
     * If timer F fires while in the "Proceeding" state, the TU MUST be informed of
     * a timeout, and the client transaction MUST transition to the terminated state.
     * https://tools.ietf.org/html/rfc3261#section-17.1.2.2
     */
    timerF() {
      this.logger.debug(`Timer F expired for non-INVITE client transaction ${this.id}.`);
      if (this.state === TransactionState.Trying || this.state === TransactionState.Proceeding) {
        this.onRequestTimeout();
        this.stateTransition(TransactionState.Terminated);
      }
    }
    /**
     * If Timer K fires while in this (COMPLETED) state, the client transaction
     * MUST transition to the "Terminated" state.
     * https://tools.ietf.org/html/rfc3261#section-17.1.2.2
     */
    timerK() {
      if (this.state === TransactionState.Completed) {
        this.stateTransition(TransactionState.Terminated);
      }
    }
  };

  // node_modules/sip.js/lib/core/dialogs/dialog.js
  var Dialog = class {
    /**
     * Dialog constructor.
     * @param core - User agent core.
     * @param dialogState - Initial dialog state.
     */
    constructor(core, dialogState) {
      this.core = core;
      this.dialogState = dialogState;
      this.core.dialogs.set(this.id, this);
    }
    /**
     * When a UAC receives a response that establishes a dialog, it
     * constructs the state of the dialog.  This state MUST be maintained
     * for the duration of the dialog.
     * https://tools.ietf.org/html/rfc3261#section-12.1.2
     * @param outgoingRequestMessage - Outgoing request message for dialog.
     * @param incomingResponseMessage - Incoming response message creating dialog.
     */
    static initialDialogStateForUserAgentClient(outgoingRequestMessage, incomingResponseMessage) {
      const secure = false;
      const routeSet = incomingResponseMessage.getHeaders("record-route").reverse();
      const contact = incomingResponseMessage.parseHeader("contact");
      if (!contact) {
        throw new Error("Contact undefined.");
      }
      if (!(contact instanceof NameAddrHeader)) {
        throw new Error("Contact not instance of NameAddrHeader.");
      }
      const remoteTarget = contact.uri;
      const localSequenceNumber = outgoingRequestMessage.cseq;
      const remoteSequenceNumber = void 0;
      const callId = outgoingRequestMessage.callId;
      const localTag = outgoingRequestMessage.fromTag;
      const remoteTag = incomingResponseMessage.toTag;
      if (!callId) {
        throw new Error("Call id undefined.");
      }
      if (!localTag) {
        throw new Error("From tag undefined.");
      }
      if (!remoteTag) {
        throw new Error("To tag undefined.");
      }
      if (!outgoingRequestMessage.from) {
        throw new Error("From undefined.");
      }
      if (!outgoingRequestMessage.to) {
        throw new Error("To undefined.");
      }
      const localURI = outgoingRequestMessage.from.uri;
      const remoteURI = outgoingRequestMessage.to.uri;
      if (!incomingResponseMessage.statusCode) {
        throw new Error("Incoming response status code undefined.");
      }
      const early = incomingResponseMessage.statusCode < 200 ? true : false;
      const dialogState = {
        id: callId + localTag + remoteTag,
        early,
        callId,
        localTag,
        remoteTag,
        localSequenceNumber,
        remoteSequenceNumber,
        localURI,
        remoteURI,
        remoteTarget,
        routeSet,
        secure
      };
      return dialogState;
    }
    /**
     * The UAS then constructs the state of the dialog.  This state MUST be
     * maintained for the duration of the dialog.
     * https://tools.ietf.org/html/rfc3261#section-12.1.1
     * @param incomingRequestMessage - Incoming request message creating dialog.
     * @param toTag - Tag in the To field in the response to the incoming request.
     */
    static initialDialogStateForUserAgentServer(incomingRequestMessage, toTag, early = false) {
      const secure = false;
      const routeSet = incomingRequestMessage.getHeaders("record-route");
      const contact = incomingRequestMessage.parseHeader("contact");
      if (!contact) {
        throw new Error("Contact undefined.");
      }
      if (!(contact instanceof NameAddrHeader)) {
        throw new Error("Contact not instance of NameAddrHeader.");
      }
      const remoteTarget = contact.uri;
      const remoteSequenceNumber = incomingRequestMessage.cseq;
      const localSequenceNumber = void 0;
      const callId = incomingRequestMessage.callId;
      const localTag = toTag;
      const remoteTag = incomingRequestMessage.fromTag;
      const remoteURI = incomingRequestMessage.from.uri;
      const localURI = incomingRequestMessage.to.uri;
      const dialogState = {
        id: callId + localTag + remoteTag,
        early,
        callId,
        localTag,
        remoteTag,
        localSequenceNumber,
        remoteSequenceNumber,
        localURI,
        remoteURI,
        remoteTarget,
        routeSet,
        secure
      };
      return dialogState;
    }
    /** Destructor. */
    dispose() {
      this.core.dialogs.delete(this.id);
    }
    /**
     * A dialog is identified at each UA with a dialog ID, which consists of
     * a Call-ID value, a local tag and a remote tag.  The dialog ID at each
     * UA involved in the dialog is not the same.  Specifically, the local
     * tag at one UA is identical to the remote tag at the peer UA.  The
     * tags are opaque tokens that facilitate the generation of unique
     * dialog IDs.
     * https://tools.ietf.org/html/rfc3261#section-12
     */
    get id() {
      return this.dialogState.id;
    }
    /**
     * A dialog can also be in the "early" state, which occurs when it is
     * created with a provisional response, and then it transition to the
     * "confirmed" state when a 2xx final response received or is sent.
     *
     * Note: RFC 3261 is concise on when a dialog is "confirmed", but it
     * can be a point of confusion if an INVITE dialog is "confirmed" after
     * a 2xx is sent or after receiving the ACK for the 2xx response.
     * With careful reading it can be inferred a dialog is always is
     * "confirmed" when the 2xx is sent (regardless of type of dialog).
     * However a INVITE dialog does have additional considerations
     * when it is confirmed but an ACK has not yet been received (in
     * particular with regard to a callee sending BYE requests).
     */
    get early() {
      return this.dialogState.early;
    }
    /** Call identifier component of the dialog id. */
    get callId() {
      return this.dialogState.callId;
    }
    /** Local tag component of the dialog id. */
    get localTag() {
      return this.dialogState.localTag;
    }
    /** Remote tag component of the dialog id. */
    get remoteTag() {
      return this.dialogState.remoteTag;
    }
    /** Local sequence number (used to order requests from the UA to its peer). */
    get localSequenceNumber() {
      return this.dialogState.localSequenceNumber;
    }
    /** Remote sequence number (used to order requests from its peer to the UA). */
    get remoteSequenceNumber() {
      return this.dialogState.remoteSequenceNumber;
    }
    /** Local URI. */
    get localURI() {
      return this.dialogState.localURI;
    }
    /** Remote URI. */
    get remoteURI() {
      return this.dialogState.remoteURI;
    }
    /** Remote target. */
    get remoteTarget() {
      return this.dialogState.remoteTarget;
    }
    /**
     * Route set, which is an ordered list of URIs. The route set is the
     * list of servers that need to be traversed to send a request to the peer.
     */
    get routeSet() {
      return this.dialogState.routeSet;
    }
    /**
     * If the request was sent over TLS, and the Request-URI contained
     * a SIPS URI, the "secure" flag is set to true. *NOT IMPLEMENTED*
     */
    get secure() {
      return this.dialogState.secure;
    }
    /** The user agent core servicing this dialog. */
    get userAgentCore() {
      return this.core;
    }
    /** Confirm the dialog. Only matters if dialog is currently early. */
    confirm() {
      this.dialogState.early = false;
    }
    /**
     * Requests sent within a dialog, as any other requests, are atomic.  If
     * a particular request is accepted by the UAS, all the state changes
     * associated with it are performed.  If the request is rejected, none
     * of the state changes are performed.
     *
     *    Note that some requests, such as INVITEs, affect several pieces of
     *    state.
     *
     * https://tools.ietf.org/html/rfc3261#section-12.2.2
     * @param message - Incoming request message within this dialog.
     */
    receiveRequest(message) {
      if (message.method === C.ACK) {
        return;
      }
      if (this.remoteSequenceNumber) {
        if (message.cseq <= this.remoteSequenceNumber) {
          throw new Error("Out of sequence in dialog request. Did you forget to call sequenceGuard()?");
        }
        this.dialogState.remoteSequenceNumber = message.cseq;
      }
      if (!this.remoteSequenceNumber) {
        this.dialogState.remoteSequenceNumber = message.cseq;
      }
    }
    /**
     * If the dialog identifier in the 2xx response matches the dialog
     * identifier of an existing dialog, the dialog MUST be transitioned to
     * the "confirmed" state, and the route set for the dialog MUST be
     * recomputed based on the 2xx response using the procedures of Section
     * 12.2.1.2.  Otherwise, a new dialog in the "confirmed" state MUST be
     * constructed using the procedures of Section 12.1.2.
     *
     * Note that the only piece of state that is recomputed is the route
     * set.  Other pieces of state such as the highest sequence numbers
     * (remote and local) sent within the dialog are not recomputed.  The
     * route set only is recomputed for backwards compatibility.  RFC
     * 2543 did not mandate mirroring of the Record-Route header field in
     * a 1xx, only 2xx.  However, we cannot update the entire state of
     * the dialog, since mid-dialog requests may have been sent within
     * the early dialog, modifying the sequence numbers, for example.
     *
     *  https://tools.ietf.org/html/rfc3261#section-13.2.2.4
     */
    recomputeRouteSet(message) {
      this.dialogState.routeSet = message.getHeaders("record-route").reverse();
    }
    /**
     * A request within a dialog is constructed by using many of the
     * components of the state stored as part of the dialog.
     * https://tools.ietf.org/html/rfc3261#section-12.2.1.1
     * @param method - Outgoing request method.
     */
    createOutgoingRequestMessage(method, options) {
      const toUri = this.remoteURI;
      const toTag = this.remoteTag;
      const fromUri = this.localURI;
      const fromTag = this.localTag;
      const callId = this.callId;
      let cseq;
      if (options && options.cseq) {
        cseq = options.cseq;
      } else if (!this.dialogState.localSequenceNumber) {
        cseq = this.dialogState.localSequenceNumber = 1;
      } else {
        cseq = this.dialogState.localSequenceNumber += 1;
      }
      const ruri = this.remoteTarget;
      const routeSet = this.routeSet;
      const extraHeaders = options && options.extraHeaders;
      const body = options && options.body;
      const message = this.userAgentCore.makeOutgoingRequestMessage(method, ruri, fromUri, toUri, {
        callId,
        cseq,
        fromTag,
        toTag,
        routeSet
      }, extraHeaders, body);
      return message;
    }
    /**
     * Increment the local sequence number by one.
     * It feels like this should be protected, but the current authentication handling currently
     * needs this to keep the dialog in sync when "auto re-sends" request messages.
     * @internal
     */
    incrementLocalSequenceNumber() {
      if (!this.dialogState.localSequenceNumber) {
        throw new Error("Local sequence number undefined.");
      }
      this.dialogState.localSequenceNumber += 1;
    }
    /**
     * If the remote sequence number was not empty, but the sequence number
     * of the request is lower than the remote sequence number, the request
     * is out of order and MUST be rejected with a 500 (Server Internal
     * Error) response.
     * https://tools.ietf.org/html/rfc3261#section-12.2.2
     * @param request - Incoming request to guard.
     * @returns True if the program execution is to continue in the branch in question.
     *          Otherwise a 500 Server Internal Error was stateless sent and request processing must stop.
     */
    sequenceGuard(message) {
      if (message.method === C.ACK) {
        return true;
      }
      if (this.remoteSequenceNumber && message.cseq <= this.remoteSequenceNumber) {
        this.core.replyStateless(message, { statusCode: 500 });
        return false;
      }
      return true;
    }
  };

  // node_modules/sip.js/lib/core/transactions/invite-client-transaction.js
  var InviteClientTransaction = class extends ClientTransaction {
    /**
     * Constructor.
     * Upon construction, the outgoing request's Via header is updated by calling `setViaHeader`.
     * Then `toString` is called on the outgoing request and the message is sent via the transport.
     * After construction the transaction will be in the "calling" state and the transaction id
     * will equal the branch parameter set in the Via header of the outgoing request.
     * https://tools.ietf.org/html/rfc3261#section-17.1.1
     * @param request - The outgoing INVITE request.
     * @param transport - The transport.
     * @param user - The transaction user.
     */
    constructor(request, transport, user) {
      super(request, transport, user, TransactionState.Calling, "sip.transaction.ict");
      this.ackRetransmissionCache = /* @__PURE__ */ new Map();
      this.B = setTimeout(() => this.timerB(), Timers.TIMER_B);
      this.send(request.toString()).catch((error) => {
        this.logTransportError(error, "Failed to send initial outgoing request.");
      });
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this.B) {
        clearTimeout(this.B);
        this.B = void 0;
      }
      if (this.D) {
        clearTimeout(this.D);
        this.D = void 0;
      }
      if (this.M) {
        clearTimeout(this.M);
        this.M = void 0;
      }
      super.dispose();
    }
    /** Transaction kind. Deprecated. */
    get kind() {
      return "ict";
    }
    /**
     * ACK a 2xx final response.
     *
     * The transaction includes the ACK only if the final response was not a 2xx response (the
     * transaction will generate and send the ACK to the transport automagically). If the
     * final response was a 2xx, the ACK is not considered part of the transaction (the
     * transaction user needs to generate and send the ACK).
     *
     * This library is not strictly RFC compliant with regard to ACK handling for 2xx final
     * responses. Specifically, retransmissions of ACKs to a 2xx final responses is handled
     * by the transaction layer (instead of the UAC core). The "standard" approach is for
     * the UAC core to receive all 2xx responses and manage sending ACK retransmissions to
     * the transport directly. Herein the transaction layer manages sending ACKs to 2xx responses
     * and any retransmissions of those ACKs as needed.
     *
     * @param ack - The outgoing ACK request.
     */
    ackResponse(ack) {
      const toTag = ack.toTag;
      if (!toTag) {
        throw new Error("To tag undefined.");
      }
      const id = "z9hG4bK" + Math.floor(Math.random() * 1e7);
      ack.setViaHeader(id, this.transport.protocol);
      this.ackRetransmissionCache.set(toTag, ack);
      this.send(ack.toString()).catch((error) => {
        this.logTransportError(error, "Failed to send ACK to 2xx response.");
      });
    }
    /**
     * Handler for incoming responses from the transport which match this transaction.
     * @param response - The incoming response.
     */
    receiveResponse(response) {
      const statusCode = response.statusCode;
      if (!statusCode || statusCode < 100 || statusCode > 699) {
        throw new Error(`Invalid status code ${statusCode}`);
      }
      switch (this.state) {
        case TransactionState.Calling:
          if (statusCode >= 100 && statusCode <= 199) {
            this.stateTransition(TransactionState.Proceeding);
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          if (statusCode >= 200 && statusCode <= 299) {
            this.ackRetransmissionCache.set(response.toTag, void 0);
            this.stateTransition(TransactionState.Accepted);
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          if (statusCode >= 300 && statusCode <= 699) {
            this.stateTransition(TransactionState.Completed);
            this.ack(response);
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          break;
        case TransactionState.Proceeding:
          if (statusCode >= 100 && statusCode <= 199) {
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          if (statusCode >= 200 && statusCode <= 299) {
            this.ackRetransmissionCache.set(response.toTag, void 0);
            this.stateTransition(TransactionState.Accepted);
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          if (statusCode >= 300 && statusCode <= 699) {
            this.stateTransition(TransactionState.Completed);
            this.ack(response);
            if (this.user.receiveResponse) {
              this.user.receiveResponse(response);
            }
            return;
          }
          break;
        case TransactionState.Accepted:
          if (statusCode >= 200 && statusCode <= 299) {
            if (!this.ackRetransmissionCache.has(response.toTag)) {
              this.ackRetransmissionCache.set(response.toTag, void 0);
              if (this.user.receiveResponse) {
                this.user.receiveResponse(response);
              }
              return;
            }
            const ack = this.ackRetransmissionCache.get(response.toTag);
            if (ack) {
              this.send(ack.toString()).catch((error) => {
                this.logTransportError(error, "Failed to send retransmission of ACK to 2xx response.");
              });
              return;
            }
            return;
          }
          break;
        case TransactionState.Completed:
          if (statusCode >= 300 && statusCode <= 699) {
            this.ack(response);
            return;
          }
          break;
        case TransactionState.Terminated:
          break;
        default:
          throw new Error(`Invalid state ${this.state}`);
      }
      const message = `Received unexpected ${statusCode} response while in state ${this.state}.`;
      this.logger.warn(message);
      return;
    }
    /**
     * The client transaction SHOULD inform the TU that a transport failure
     * has occurred, and the client transaction SHOULD transition directly
     * to the "Terminated" state.  The TU will handle the failover
     * mechanisms described in [4].
     * https://tools.ietf.org/html/rfc3261#section-17.1.4
     * @param error - The error.
     */
    onTransportError(error) {
      if (this.user.onTransportError) {
        this.user.onTransportError(error);
      }
      this.stateTransition(TransactionState.Terminated, true);
    }
    /** For logging. */
    typeToString() {
      return "INVITE client transaction";
    }
    ack(response) {
      const ruri = this.request.ruri;
      const callId = this.request.callId;
      const cseq = this.request.cseq;
      const from = this.request.getHeader("from");
      const to = response.getHeader("to");
      const via = this.request.getHeader("via");
      const route = this.request.getHeader("route");
      if (!from) {
        throw new Error("From undefined.");
      }
      if (!to) {
        throw new Error("To undefined.");
      }
      if (!via) {
        throw new Error("Via undefined.");
      }
      let ack = `ACK ${ruri} SIP/2.0\r
`;
      if (route) {
        ack += `Route: ${route}\r
`;
      }
      ack += `Via: ${via}\r
`;
      ack += `To: ${to}\r
`;
      ack += `From: ${from}\r
`;
      ack += `Call-ID: ${callId}\r
`;
      ack += `CSeq: ${cseq} ACK\r
`;
      ack += `Max-Forwards: 70\r
`;
      ack += `Content-Length: 0\r
\r
`;
      this.send(ack).catch((error) => {
        this.logTransportError(error, "Failed to send ACK to non-2xx response.");
      });
      return;
    }
    /**
     * Execute a state transition.
     * @param newState - New state.
     */
    stateTransition(newState, dueToTransportError = false) {
      const invalidStateTransition = () => {
        throw new Error(`Invalid state transition from ${this.state} to ${newState}`);
      };
      switch (newState) {
        case TransactionState.Calling:
          invalidStateTransition();
          break;
        case TransactionState.Proceeding:
          if (this.state !== TransactionState.Calling) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Accepted:
        case TransactionState.Completed:
          if (this.state !== TransactionState.Calling && this.state !== TransactionState.Proceeding) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Terminated:
          if (this.state !== TransactionState.Calling && this.state !== TransactionState.Accepted && this.state !== TransactionState.Completed) {
            if (!dueToTransportError) {
              invalidStateTransition();
            }
          }
          break;
        default:
          invalidStateTransition();
      }
      if (this.B) {
        clearTimeout(this.B);
        this.B = void 0;
      }
      if (newState === TransactionState.Proceeding) {
      }
      if (newState === TransactionState.Completed) {
        this.D = setTimeout(() => this.timerD(), Timers.TIMER_D);
      }
      if (newState === TransactionState.Accepted) {
        this.M = setTimeout(() => this.timerM(), Timers.TIMER_M);
      }
      if (newState === TransactionState.Terminated) {
        this.dispose();
      }
      this.setState(newState);
    }
    /**
     * When timer A fires, the client transaction MUST retransmit the
     * request by passing it to the transport layer, and MUST reset the
     * timer with a value of 2*T1.
     * When timer A fires 2*T1 seconds later, the request MUST be
     * retransmitted again (assuming the client transaction is still in this
     * state). This process MUST continue so that the request is
     * retransmitted with intervals that double after each transmission.
     * These retransmissions SHOULD only be done while the client
     * transaction is in the "Calling" state.
     * https://tools.ietf.org/html/rfc3261#section-17.1.1.2
     */
    timerA() {
    }
    /**
     * If the client transaction is still in the "Calling" state when timer
     * B fires, the client transaction SHOULD inform the TU that a timeout
     * has occurred.  The client transaction MUST NOT generate an ACK.
     * https://tools.ietf.org/html/rfc3261#section-17.1.1.2
     */
    timerB() {
      this.logger.debug(`Timer B expired for INVITE client transaction ${this.id}.`);
      if (this.state === TransactionState.Calling) {
        this.onRequestTimeout();
        this.stateTransition(TransactionState.Terminated);
      }
    }
    /**
     * If Timer D fires while the client transaction is in the "Completed" state,
     * the client transaction MUST move to the "Terminated" state.
     * https://tools.ietf.org/html/rfc6026#section-8.4
     */
    timerD() {
      this.logger.debug(`Timer D expired for INVITE client transaction ${this.id}.`);
      if (this.state === TransactionState.Completed) {
        this.stateTransition(TransactionState.Terminated);
      }
    }
    /**
     * If Timer M fires while the client transaction is in the "Accepted"
     * state, the client transaction MUST move to the "Terminated" state.
     * https://tools.ietf.org/html/rfc6026#section-8.4
     */
    timerM() {
      this.logger.debug(`Timer M expired for INVITE client transaction ${this.id}.`);
      if (this.state === TransactionState.Accepted) {
        this.stateTransition(TransactionState.Terminated);
      }
    }
  };

  // node_modules/sip.js/lib/core/user-agents/user-agent-client.js
  var UserAgentClient = class _UserAgentClient {
    constructor(transactionConstructor, core, message, delegate) {
      this.transactionConstructor = transactionConstructor;
      this.core = core;
      this.message = message;
      this.delegate = delegate;
      this.challenged = false;
      this.stale = false;
      this.logger = this.loggerFactory.getLogger("sip.user-agent-client");
      this.init();
    }
    dispose() {
      this.transaction.dispose();
    }
    get loggerFactory() {
      return this.core.loggerFactory;
    }
    /** The transaction associated with this request. */
    get transaction() {
      if (!this._transaction) {
        throw new Error("Transaction undefined.");
      }
      return this._transaction;
    }
    /**
     * Since requests other than INVITE are responded to immediately, sending a
     * CANCEL for a non-INVITE request would always create a race condition.
     * A CANCEL request SHOULD NOT be sent to cancel a request other than INVITE.
     * https://tools.ietf.org/html/rfc3261#section-9.1
     * @param options - Cancel options bucket.
     */
    cancel(reason, options = {}) {
      if (!this.transaction) {
        throw new Error("Transaction undefined.");
      }
      if (!this.message.to) {
        throw new Error("To undefined.");
      }
      if (!this.message.from) {
        throw new Error("From undefined.");
      }
      const message = this.core.makeOutgoingRequestMessage(C.CANCEL, this.message.ruri, this.message.from.uri, this.message.to.uri, {
        toTag: this.message.toTag,
        fromTag: this.message.fromTag,
        callId: this.message.callId,
        cseq: this.message.cseq
      }, options.extraHeaders);
      message.branch = this.message.branch;
      if (this.message.headers.Route) {
        message.headers.Route = this.message.headers.Route;
      }
      if (reason) {
        message.setHeader("Reason", reason);
      }
      if (this.transaction.state === TransactionState.Proceeding) {
        new _UserAgentClient(NonInviteClientTransaction, this.core, message);
      } else {
        this.transaction.addStateChangeListener(() => {
          if (this.transaction && this.transaction.state === TransactionState.Proceeding) {
            new _UserAgentClient(NonInviteClientTransaction, this.core, message);
          }
        }, { once: true });
      }
      return message;
    }
    /**
     * If a 401 (Unauthorized) or 407 (Proxy Authentication Required)
     * response is received, the UAC SHOULD follow the authorization
     * procedures of Section 22.2 and Section 22.3 to retry the request with
     * credentials.
     * https://tools.ietf.org/html/rfc3261#section-8.1.3.5
     * 22 Usage of HTTP Authentication
     * https://tools.ietf.org/html/rfc3261#section-22
     * 22.1 Framework
     * https://tools.ietf.org/html/rfc3261#section-22.1
     * 22.2 User-to-User Authentication
     * https://tools.ietf.org/html/rfc3261#section-22.2
     * 22.3 Proxy-to-User Authentication
     * https://tools.ietf.org/html/rfc3261#section-22.3
     *
     * FIXME: This "guard for and retry the request with credentials"
     * implementation is not complete and at best minimally passable.
     * @param response - The incoming response to guard.
     * @param dialog - If defined, the dialog within which the response was received.
     * @returns True if the program execution is to continue in the branch in question.
     *          Otherwise the request is retried with credentials and current request processing must stop.
     */
    authenticationGuard(message, dialog) {
      const statusCode = message.statusCode;
      if (!statusCode) {
        throw new Error("Response status code undefined.");
      }
      if (statusCode !== 401 && statusCode !== 407) {
        return true;
      }
      let challenge;
      let authorizationHeaderName;
      if (statusCode === 401) {
        challenge = message.parseHeader("www-authenticate");
        authorizationHeaderName = "authorization";
      } else {
        challenge = message.parseHeader("proxy-authenticate");
        authorizationHeaderName = "proxy-authorization";
      }
      if (!challenge) {
        this.logger.warn(statusCode + " with wrong or missing challenge, cannot authenticate");
        return true;
      }
      if (this.challenged && (this.stale || challenge.stale !== true)) {
        this.logger.warn(statusCode + " apparently in authentication loop, cannot authenticate");
        return true;
      }
      if (!this.credentials) {
        this.credentials = this.core.configuration.authenticationFactory();
        if (!this.credentials) {
          this.logger.warn("Unable to obtain credentials, cannot authenticate");
          return true;
        }
      }
      if (!this.credentials.authenticate(this.message, challenge)) {
        return true;
      }
      this.challenged = true;
      if (challenge.stale) {
        this.stale = true;
      }
      let cseq = this.message.cseq += 1;
      if (dialog && dialog.localSequenceNumber) {
        dialog.incrementLocalSequenceNumber();
        cseq = this.message.cseq = dialog.localSequenceNumber;
      }
      this.message.setHeader("cseq", cseq + " " + this.message.method);
      this.message.setHeader(authorizationHeaderName, this.credentials.toString());
      this.init();
      return false;
    }
    /**
     * 8.1.3.1 Transaction Layer Errors
     * In some cases, the response returned by the transaction layer will
     * not be a SIP message, but rather a transaction layer error.  When a
     * timeout error is received from the transaction layer, it MUST be
     * treated as if a 408 (Request Timeout) status code has been received.
     * If a fatal transport error is reported by the transport layer
     * (generally, due to fatal ICMP errors in UDP or connection failures in
     * TCP), the condition MUST be treated as a 503 (Service Unavailable)
     * status code.
     * https://tools.ietf.org/html/rfc3261#section-8.1.3.1
     */
    onRequestTimeout() {
      this.logger.warn("User agent client request timed out. Generating internal 408 Request Timeout.");
      const message = new IncomingResponseMessage();
      message.statusCode = 408;
      message.reasonPhrase = "Request Timeout";
      this.receiveResponse(message);
      return;
    }
    /**
     * 8.1.3.1 Transaction Layer Errors
     * In some cases, the response returned by the transaction layer will
     * not be a SIP message, but rather a transaction layer error.  When a
     * timeout error is received from the transaction layer, it MUST be
     * treated as if a 408 (Request Timeout) status code has been received.
     * If a fatal transport error is reported by the transport layer
     * (generally, due to fatal ICMP errors in UDP or connection failures in
     * TCP), the condition MUST be treated as a 503 (Service Unavailable)
     * status code.
     * https://tools.ietf.org/html/rfc3261#section-8.1.3.1
     * @param error - Transport error
     */
    onTransportError(error) {
      this.logger.error(error.message);
      this.logger.error("User agent client request transport error. Generating internal 503 Service Unavailable.");
      const message = new IncomingResponseMessage();
      message.statusCode = 503;
      message.reasonPhrase = "Service Unavailable";
      this.receiveResponse(message);
    }
    /**
     * Receive a response from the transaction layer.
     * @param message - Incoming response message.
     */
    receiveResponse(message) {
      if (!this.authenticationGuard(message)) {
        return;
      }
      const statusCode = message.statusCode ? message.statusCode.toString() : "";
      if (!statusCode) {
        throw new Error("Response status code undefined.");
      }
      switch (true) {
        case /^100$/.test(statusCode):
          if (this.delegate && this.delegate.onTrying) {
            this.delegate.onTrying({ message });
          }
          break;
        case /^1[0-9]{2}$/.test(statusCode):
          if (this.delegate && this.delegate.onProgress) {
            this.delegate.onProgress({ message });
          }
          break;
        case /^2[0-9]{2}$/.test(statusCode):
          if (this.delegate && this.delegate.onAccept) {
            this.delegate.onAccept({ message });
          }
          break;
        case /^3[0-9]{2}$/.test(statusCode):
          if (this.delegate && this.delegate.onRedirect) {
            this.delegate.onRedirect({ message });
          }
          break;
        case /^[4-6][0-9]{2}$/.test(statusCode):
          if (this.delegate && this.delegate.onReject) {
            this.delegate.onReject({ message });
          }
          break;
        default:
          throw new Error(`Invalid status code ${statusCode}`);
      }
    }
    init() {
      const user = {
        loggerFactory: this.loggerFactory,
        onRequestTimeout: () => this.onRequestTimeout(),
        onStateChange: (newState) => {
          if (newState === TransactionState.Terminated) {
            this.core.userAgentClients.delete(userAgentClientId);
            if (transaction === this._transaction) {
              this.dispose();
            }
          }
        },
        onTransportError: (error) => this.onTransportError(error),
        receiveResponse: (message) => this.receiveResponse(message)
      };
      const transaction = new this.transactionConstructor(this.message, this.core.transport, user);
      this._transaction = transaction;
      const userAgentClientId = transaction.id + transaction.request.method;
      this.core.userAgentClients.set(userAgentClientId, this);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/bye-user-agent-client.js
  var ByeUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.BYE, options);
      super(NonInviteClientTransaction, dialog.userAgentCore, message, delegate);
      dialog.dispose();
    }
  };

  // node_modules/sip.js/lib/core/transactions/non-invite-server-transaction.js
  var NonInviteServerTransaction = class extends ServerTransaction {
    /**
     * Constructor.
     * After construction the transaction will be in the "trying": state and the transaction
     * `id` will equal the branch parameter set in the Via header of the incoming request.
     * https://tools.ietf.org/html/rfc3261#section-17.2.2
     * @param request - Incoming Non-INVITE request from the transport.
     * @param transport - The transport.
     * @param user - The transaction user.
     */
    constructor(request, transport, user) {
      super(request, transport, user, TransactionState.Trying, "sip.transaction.nist");
    }
    /**
     * Destructor.
     */
    dispose() {
      if (this.J) {
        clearTimeout(this.J);
        this.J = void 0;
      }
      super.dispose();
    }
    /** Transaction kind. Deprecated. */
    get kind() {
      return "nist";
    }
    /**
     * Receive requests from transport matching this transaction.
     * @param request - Request matching this transaction.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    receiveRequest(request) {
      switch (this.state) {
        case TransactionState.Trying:
          break;
        case TransactionState.Proceeding:
          if (!this.lastResponse) {
            throw new Error("Last response undefined.");
          }
          this.send(this.lastResponse).catch((error) => {
            this.logTransportError(error, "Failed to send retransmission of provisional response.");
          });
          break;
        case TransactionState.Completed:
          if (!this.lastResponse) {
            throw new Error("Last response undefined.");
          }
          this.send(this.lastResponse).catch((error) => {
            this.logTransportError(error, "Failed to send retransmission of final response.");
          });
          break;
        case TransactionState.Terminated:
          break;
        default:
          throw new Error(`Invalid state ${this.state}`);
      }
    }
    /**
     * Receive responses from TU for this transaction.
     * @param statusCode - Status code of response. 101-199 not allowed per RFC 4320.
     * @param response - Response to send.
     */
    receiveResponse(statusCode, response) {
      if (statusCode < 100 || statusCode > 699) {
        throw new Error(`Invalid status code ${statusCode}`);
      }
      if (statusCode > 100 && statusCode <= 199) {
        throw new Error("Provisional response other than 100 not allowed.");
      }
      switch (this.state) {
        case TransactionState.Trying:
          this.lastResponse = response;
          if (statusCode >= 100 && statusCode < 200) {
            this.stateTransition(TransactionState.Proceeding);
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send provisional response.");
            });
            return;
          }
          if (statusCode >= 200 && statusCode <= 699) {
            this.stateTransition(TransactionState.Completed);
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send final response.");
            });
            return;
          }
          break;
        case TransactionState.Proceeding:
          this.lastResponse = response;
          if (statusCode >= 200 && statusCode <= 699) {
            this.stateTransition(TransactionState.Completed);
            this.send(response).catch((error) => {
              this.logTransportError(error, "Failed to send final response.");
            });
            return;
          }
          break;
        case TransactionState.Completed:
          return;
        case TransactionState.Terminated:
          break;
        default:
          throw new Error(`Invalid state ${this.state}`);
      }
      const message = `Non-INVITE server transaction received unexpected ${statusCode} response from TU while in state ${this.state}.`;
      this.logger.error(message);
      throw new Error(message);
    }
    /**
     * First, the procedures in [4] are followed, which attempt to deliver the response to a backup.
     * If those should all fail, based on the definition of failure in [4], the server transaction SHOULD
     * inform the TU that a failure has occurred, and SHOULD transition to the terminated state.
     * https://tools.ietf.org/html/rfc3261#section-17.2.4
     */
    onTransportError(error) {
      if (this.user.onTransportError) {
        this.user.onTransportError(error);
      }
      this.stateTransition(TransactionState.Terminated, true);
    }
    /** For logging. */
    typeToString() {
      return "non-INVITE server transaction";
    }
    stateTransition(newState, dueToTransportError = false) {
      const invalidStateTransition = () => {
        throw new Error(`Invalid state transition from ${this.state} to ${newState}`);
      };
      switch (newState) {
        case TransactionState.Trying:
          invalidStateTransition();
          break;
        case TransactionState.Proceeding:
          if (this.state !== TransactionState.Trying) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Completed:
          if (this.state !== TransactionState.Trying && this.state !== TransactionState.Proceeding) {
            invalidStateTransition();
          }
          break;
        case TransactionState.Terminated:
          if (this.state !== TransactionState.Proceeding && this.state !== TransactionState.Completed) {
            if (!dueToTransportError) {
              invalidStateTransition();
            }
          }
          break;
        default:
          invalidStateTransition();
      }
      if (newState === TransactionState.Completed) {
        this.J = setTimeout(() => this.timerJ(), Timers.TIMER_J);
      }
      if (newState === TransactionState.Terminated) {
        this.dispose();
      }
      this.setState(newState);
    }
    /**
     * The server transaction remains in this state until Timer J fires,
     * at which point it MUST transition to the "Terminated" state.
     * https://tools.ietf.org/html/rfc3261#section-17.2.2
     */
    timerJ() {
      this.logger.debug(`Timer J expired for NON-INVITE server transaction ${this.id}.`);
      if (this.state === TransactionState.Completed) {
        this.stateTransition(TransactionState.Terminated);
      }
    }
  };

  // node_modules/sip.js/lib/core/user-agents/user-agent-server.js
  var UserAgentServer = class {
    constructor(transactionConstructor, core, message, delegate) {
      this.transactionConstructor = transactionConstructor;
      this.core = core;
      this.message = message;
      this.delegate = delegate;
      this.logger = this.loggerFactory.getLogger("sip.user-agent-server");
      this.toTag = message.toTag ? message.toTag : newTag();
      this.init();
    }
    dispose() {
      this.transaction.dispose();
    }
    get loggerFactory() {
      return this.core.loggerFactory;
    }
    /** The transaction associated with this request. */
    get transaction() {
      if (!this._transaction) {
        throw new Error("Transaction undefined.");
      }
      return this._transaction;
    }
    accept(options = { statusCode: 200 }) {
      if (!this.acceptable) {
        throw new TransactionStateError(`${this.message.method} not acceptable in state ${this.transaction.state}.`);
      }
      const statusCode = options.statusCode;
      if (statusCode < 200 || statusCode > 299) {
        throw new TypeError(`Invalid statusCode: ${statusCode}`);
      }
      const response = this.reply(options);
      return response;
    }
    progress(options = { statusCode: 180 }) {
      if (!this.progressable) {
        throw new TransactionStateError(`${this.message.method} not progressable in state ${this.transaction.state}.`);
      }
      const statusCode = options.statusCode;
      if (statusCode < 101 || statusCode > 199) {
        throw new TypeError(`Invalid statusCode: ${statusCode}`);
      }
      const response = this.reply(options);
      return response;
    }
    redirect(contacts, options = { statusCode: 302 }) {
      if (!this.redirectable) {
        throw new TransactionStateError(`${this.message.method} not redirectable in state ${this.transaction.state}.`);
      }
      const statusCode = options.statusCode;
      if (statusCode < 300 || statusCode > 399) {
        throw new TypeError(`Invalid statusCode: ${statusCode}`);
      }
      const contactHeaders = new Array();
      contacts.forEach((contact) => contactHeaders.push(`Contact: ${contact.toString()}`));
      options.extraHeaders = (options.extraHeaders || []).concat(contactHeaders);
      const response = this.reply(options);
      return response;
    }
    reject(options = { statusCode: 480 }) {
      if (!this.rejectable) {
        throw new TransactionStateError(`${this.message.method} not rejectable in state ${this.transaction.state}.`);
      }
      const statusCode = options.statusCode;
      if (statusCode < 400 || statusCode > 699) {
        throw new TypeError(`Invalid statusCode: ${statusCode}`);
      }
      const response = this.reply(options);
      return response;
    }
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    trying(options) {
      if (!this.tryingable) {
        throw new TransactionStateError(`${this.message.method} not tryingable in state ${this.transaction.state}.`);
      }
      const response = this.reply({ statusCode: 100 });
      return response;
    }
    /**
     * If the UAS did not find a matching transaction for the CANCEL
     * according to the procedure above, it SHOULD respond to the CANCEL
     * with a 481 (Call Leg/Transaction Does Not Exist).  If the transaction
     * for the original request still exists, the behavior of the UAS on
     * receiving a CANCEL request depends on whether it has already sent a
     * final response for the original request.  If it has, the CANCEL
     * request has no effect on the processing of the original request, no
     * effect on any session state, and no effect on the responses generated
     * for the original request.  If the UAS has not issued a final response
     * for the original request, its behavior depends on the method of the
     * original request.  If the original request was an INVITE, the UAS
     * SHOULD immediately respond to the INVITE with a 487 (Request
     * Terminated).  A CANCEL request has no impact on the processing of
     * transactions with any other method defined in this specification.
     * https://tools.ietf.org/html/rfc3261#section-9.2
     * @param request - Incoming CANCEL request.
     */
    receiveCancel(message) {
      if (this.delegate && this.delegate.onCancel) {
        this.delegate.onCancel(message);
      }
    }
    get acceptable() {
      if (this.transaction instanceof InviteServerTransaction) {
        return this.transaction.state === TransactionState.Proceeding || this.transaction.state === TransactionState.Accepted;
      }
      if (this.transaction instanceof NonInviteServerTransaction) {
        return this.transaction.state === TransactionState.Trying || this.transaction.state === TransactionState.Proceeding;
      }
      throw new Error("Unknown transaction type.");
    }
    get progressable() {
      if (this.transaction instanceof InviteServerTransaction) {
        return this.transaction.state === TransactionState.Proceeding;
      }
      if (this.transaction instanceof NonInviteServerTransaction) {
        return false;
      }
      throw new Error("Unknown transaction type.");
    }
    get redirectable() {
      if (this.transaction instanceof InviteServerTransaction) {
        return this.transaction.state === TransactionState.Proceeding;
      }
      if (this.transaction instanceof NonInviteServerTransaction) {
        return this.transaction.state === TransactionState.Trying || this.transaction.state === TransactionState.Proceeding;
      }
      throw new Error("Unknown transaction type.");
    }
    get rejectable() {
      if (this.transaction instanceof InviteServerTransaction) {
        return this.transaction.state === TransactionState.Proceeding;
      }
      if (this.transaction instanceof NonInviteServerTransaction) {
        return this.transaction.state === TransactionState.Trying || this.transaction.state === TransactionState.Proceeding;
      }
      throw new Error("Unknown transaction type.");
    }
    get tryingable() {
      if (this.transaction instanceof InviteServerTransaction) {
        return this.transaction.state === TransactionState.Proceeding;
      }
      if (this.transaction instanceof NonInviteServerTransaction) {
        return this.transaction.state === TransactionState.Trying;
      }
      throw new Error("Unknown transaction type.");
    }
    /**
     * When a UAS wishes to construct a response to a request, it follows
     * the general procedures detailed in the following subsections.
     * Additional behaviors specific to the response code in question, which
     * are not detailed in this section, may also be required.
     *
     * Once all procedures associated with the creation of a response have
     * been completed, the UAS hands the response back to the server
     * transaction from which it received the request.
     * https://tools.ietf.org/html/rfc3261#section-8.2.6
     * @param statusCode - Status code to reply with.
     * @param options - Reply options bucket.
     */
    reply(options) {
      if (!options.toTag && options.statusCode !== 100) {
        options.toTag = this.toTag;
      }
      options.userAgent = options.userAgent || this.core.configuration.userAgentHeaderFieldValue;
      options.supported = options.supported || this.core.configuration.supportedOptionTagsResponse;
      const response = constructOutgoingResponse(this.message, options);
      this.transaction.receiveResponse(options.statusCode, response.message);
      return response;
    }
    init() {
      const user = {
        loggerFactory: this.loggerFactory,
        onStateChange: (newState) => {
          if (newState === TransactionState.Terminated) {
            this.core.userAgentServers.delete(userAgentServerId);
            this.dispose();
          }
        },
        onTransportError: (error) => {
          this.logger.error(error.message);
          if (this.delegate && this.delegate.onTransportError) {
            this.delegate.onTransportError(error);
          } else {
            this.logger.error("User agent server response transport error.");
          }
        }
      };
      const transaction = new this.transactionConstructor(this.message, this.core.transport, user);
      this._transaction = transaction;
      const userAgentServerId = transaction.id;
      this.core.userAgentServers.set(transaction.id, this);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/bye-user-agent-server.js
  var ByeUserAgentServer = class extends UserAgentServer {
    constructor(dialog, message, delegate) {
      super(NonInviteServerTransaction, dialog.userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/info-user-agent-client.js
  var InfoUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.INFO, options);
      super(NonInviteClientTransaction, dialog.userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/info-user-agent-server.js
  var InfoUserAgentServer = class extends UserAgentServer {
    constructor(dialog, message, delegate) {
      super(NonInviteServerTransaction, dialog.userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/message-user-agent-client.js
  var MessageUserAgentClient = class extends UserAgentClient {
    constructor(core, message, delegate) {
      super(NonInviteClientTransaction, core, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/message-user-agent-server.js
  var MessageUserAgentServer = class extends UserAgentServer {
    constructor(core, message, delegate) {
      super(NonInviteServerTransaction, core, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/notify-user-agent-client.js
  var NotifyUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.NOTIFY, options);
      super(NonInviteClientTransaction, dialog.userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/notify-user-agent-server.js
  function instanceOfDialog(object) {
    return object.userAgentCore !== void 0;
  }
  var NotifyUserAgentServer = class extends UserAgentServer {
    /**
     * NOTIFY UAS constructor.
     * @param dialogOrCore - Dialog for in dialog NOTIFY, UserAgentCore for out of dialog NOTIFY (deprecated).
     * @param message - Incoming NOTIFY request message.
     */
    constructor(dialogOrCore, message, delegate) {
      const userAgentCore = instanceOfDialog(dialogOrCore) ? dialogOrCore.userAgentCore : dialogOrCore;
      super(NonInviteServerTransaction, userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/prack-user-agent-client.js
  var PrackUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.PRACK, options);
      super(NonInviteClientTransaction, dialog.userAgentCore, message, delegate);
      dialog.signalingStateTransition(message);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/prack-user-agent-server.js
  var PrackUserAgentServer = class extends UserAgentServer {
    constructor(dialog, message, delegate) {
      super(NonInviteServerTransaction, dialog.userAgentCore, message, delegate);
      dialog.signalingStateTransition(message);
      this.dialog = dialog;
    }
    /**
     * Update the dialog signaling state on a 2xx response.
     * @param options - Options bucket.
     */
    accept(options = { statusCode: 200 }) {
      if (options.body) {
        this.dialog.signalingStateTransition(options.body);
      }
      return super.accept(options);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/re-invite-user-agent-client.js
  var ReInviteUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.INVITE, options);
      super(InviteClientTransaction, dialog.userAgentCore, message, delegate);
      this.delegate = delegate;
      dialog.signalingStateTransition(message);
      dialog.reinviteUserAgentClient = this;
      this.dialog = dialog;
    }
    receiveResponse(message) {
      if (!this.authenticationGuard(message, this.dialog)) {
        return;
      }
      const statusCode = message.statusCode ? message.statusCode.toString() : "";
      if (!statusCode) {
        throw new Error("Response status code undefined.");
      }
      switch (true) {
        case /^100$/.test(statusCode):
          if (this.delegate && this.delegate.onTrying) {
            this.delegate.onTrying({ message });
          }
          break;
        case /^1[0-9]{2}$/.test(statusCode):
          if (this.delegate && this.delegate.onProgress) {
            this.delegate.onProgress({
              message,
              session: this.dialog,
              // eslint-disable-next-line @typescript-eslint/no-unused-vars
              prack: (options) => {
                throw new Error("Unimplemented.");
              }
            });
          }
          break;
        case /^2[0-9]{2}$/.test(statusCode):
          this.dialog.signalingStateTransition(message);
          if (this.delegate && this.delegate.onAccept) {
            this.delegate.onAccept({
              message,
              session: this.dialog,
              ack: (options) => {
                const outgoingAckRequest = this.dialog.ack(options);
                return outgoingAckRequest;
              }
            });
          }
          break;
        case /^3[0-9]{2}$/.test(statusCode):
          this.dialog.signalingStateRollback();
          this.dialog.reinviteUserAgentClient = void 0;
          if (this.delegate && this.delegate.onRedirect) {
            this.delegate.onRedirect({ message });
          }
          break;
        case /^[4-6][0-9]{2}$/.test(statusCode):
          this.dialog.signalingStateRollback();
          this.dialog.reinviteUserAgentClient = void 0;
          if (this.delegate && this.delegate.onReject) {
            this.delegate.onReject({ message });
          } else {
          }
          break;
        default:
          throw new Error(`Invalid status code ${statusCode}`);
      }
    }
  };

  // node_modules/sip.js/lib/core/user-agents/re-invite-user-agent-server.js
  var ReInviteUserAgentServer = class extends UserAgentServer {
    constructor(dialog, message, delegate) {
      super(InviteServerTransaction, dialog.userAgentCore, message, delegate);
      dialog.reinviteUserAgentServer = this;
      this.dialog = dialog;
    }
    /**
     * Update the dialog signaling state on a 2xx response.
     * @param options - Options bucket.
     */
    accept(options = { statusCode: 200 }) {
      options.extraHeaders = options.extraHeaders || [];
      options.extraHeaders = options.extraHeaders.concat(this.dialog.routeSet.map((route) => `Record-Route: ${route}`));
      const response = super.accept(options);
      const session = this.dialog;
      const result = Object.assign(Object.assign({}, response), { session });
      if (options.body) {
        this.dialog.signalingStateTransition(options.body);
      }
      this.dialog.reConfirm();
      return result;
    }
    /**
     * Update the dialog signaling state on a 1xx response.
     * @param options - Progress options bucket.
     */
    progress(options = { statusCode: 180 }) {
      const response = super.progress(options);
      const session = this.dialog;
      const result = Object.assign(Object.assign({}, response), { session });
      if (options.body) {
        this.dialog.signalingStateTransition(options.body);
      }
      return result;
    }
    /**
     * TODO: Not Yet Supported
     * @param contacts - Contacts to redirect to.
     * @param options - Redirect options bucket.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    redirect(contacts, options = { statusCode: 302 }) {
      this.dialog.signalingStateRollback();
      this.dialog.reinviteUserAgentServer = void 0;
      throw new Error("Unimplemented.");
    }
    /**
     * 3.1 Background on Re-INVITE Handling by UASs
     * An error response to a re-INVITE has the following semantics.  As
     * specified in Section 12.2.2 of RFC 3261 [RFC3261], if a re-INVITE is
     * rejected, no state changes are performed.
     * https://tools.ietf.org/html/rfc6141#section-3.1
     * @param options - Reject options bucket.
     */
    reject(options = { statusCode: 488 }) {
      this.dialog.signalingStateRollback();
      this.dialog.reinviteUserAgentServer = void 0;
      return super.reject(options);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/refer-user-agent-client.js
  var ReferUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.REFER, options);
      super(NonInviteClientTransaction, dialog.userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/refer-user-agent-server.js
  function instanceOfSessionDialog(object) {
    return object.userAgentCore !== void 0;
  }
  var ReferUserAgentServer = class extends UserAgentServer {
    /**
     * REFER UAS constructor.
     * @param dialogOrCore - Dialog for in dialog REFER, UserAgentCore for out of dialog REFER.
     * @param message - Incoming REFER request message.
     */
    constructor(dialogOrCore, message, delegate) {
      const userAgentCore = instanceOfSessionDialog(dialogOrCore) ? dialogOrCore.userAgentCore : dialogOrCore;
      super(NonInviteServerTransaction, userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/dialogs/session-dialog.js
  var SessionDialog = class extends Dialog {
    constructor(initialTransaction, core, state, delegate) {
      super(core, state);
      this.initialTransaction = initialTransaction;
      this._signalingState = SignalingState.Initial;
      this.ackWait = false;
      this.ackProcessing = false;
      this.delegate = delegate;
      if (initialTransaction instanceof InviteServerTransaction) {
        this.ackWait = true;
      }
      if (!this.early) {
        this.start2xxRetransmissionTimer();
      }
      this.signalingStateTransition(initialTransaction.request);
      this.logger = core.loggerFactory.getLogger("sip.invite-dialog");
      this.logger.log(`INVITE dialog ${this.id} constructed`);
    }
    dispose() {
      super.dispose();
      this._signalingState = SignalingState.Closed;
      this._offer = void 0;
      this._answer = void 0;
      if (this.invite2xxTimer) {
        clearTimeout(this.invite2xxTimer);
        this.invite2xxTimer = void 0;
      }
      this.logger.log(`INVITE dialog ${this.id} destroyed`);
    }
    // FIXME: Need real state machine
    get sessionState() {
      if (this.early) {
        return SessionState.Early;
      } else if (this.ackWait) {
        return SessionState.AckWait;
      } else if (this._signalingState === SignalingState.Closed) {
        return SessionState.Terminated;
      } else {
        return SessionState.Confirmed;
      }
    }
    /** The state of the offer/answer exchange. */
    get signalingState() {
      return this._signalingState;
    }
    /** The current offer. Undefined unless signaling state HaveLocalOffer, HaveRemoteOffer, of Stable. */
    get offer() {
      return this._offer;
    }
    /** The current answer. Undefined unless signaling state Stable. */
    get answer() {
      return this._answer;
    }
    /** Confirm the dialog. Only matters if dialog is currently early. */
    confirm() {
      if (this.early) {
        this.start2xxRetransmissionTimer();
      }
      super.confirm();
    }
    /** Re-confirm the dialog. Only matters if handling re-INVITE request. */
    reConfirm() {
      if (this.reinviteUserAgentServer) {
        this.startReInvite2xxRetransmissionTimer();
      }
    }
    /**
     * The UAC core MUST generate an ACK request for each 2xx received from
     * the transaction layer.  The header fields of the ACK are constructed
     * in the same way as for any request sent within a dialog (see Section
     * 12) with the exception of the CSeq and the header fields related to
     * authentication.  The sequence number of the CSeq header field MUST be
     * the same as the INVITE being acknowledged, but the CSeq method MUST
     * be ACK.  The ACK MUST contain the same credentials as the INVITE.  If
     * the 2xx contains an offer (based on the rules above), the ACK MUST
     * carry an answer in its body.  If the offer in the 2xx response is not
     * acceptable, the UAC core MUST generate a valid answer in the ACK and
     * then send a BYE immediately.
     * https://tools.ietf.org/html/rfc3261#section-13.2.2.4
     * @param options - ACK options bucket.
     */
    ack(options = {}) {
      this.logger.log(`INVITE dialog ${this.id} sending ACK request`);
      let transaction;
      if (this.reinviteUserAgentClient) {
        if (!(this.reinviteUserAgentClient.transaction instanceof InviteClientTransaction)) {
          throw new Error("Transaction not instance of InviteClientTransaction.");
        }
        transaction = this.reinviteUserAgentClient.transaction;
        this.reinviteUserAgentClient = void 0;
      } else {
        if (!(this.initialTransaction instanceof InviteClientTransaction)) {
          throw new Error("Initial transaction not instance of InviteClientTransaction.");
        }
        transaction = this.initialTransaction;
      }
      const message = this.createOutgoingRequestMessage(C.ACK, {
        cseq: transaction.request.cseq,
        extraHeaders: options.extraHeaders,
        body: options.body
      });
      transaction.ackResponse(message);
      this.signalingStateTransition(message);
      return { message };
    }
    /**
     * Terminating a Session
     *
     * This section describes the procedures for terminating a session
     * established by SIP.  The state of the session and the state of the
     * dialog are very closely related.  When a session is initiated with an
     * INVITE, each 1xx or 2xx response from a distinct UAS creates a
     * dialog, and if that response completes the offer/answer exchange, it
     * also creates a session.  As a result, each session is "associated"
     * with a single dialog - the one which resulted in its creation.  If an
     * initial INVITE generates a non-2xx final response, that terminates
     * all sessions (if any) and all dialogs (if any) that were created
     * through responses to the request.  By virtue of completing the
     * transaction, a non-2xx final response also prevents further sessions
     * from being created as a result of the INVITE.  The BYE request is
     * used to terminate a specific session or attempted session.  In this
     * case, the specific session is the one with the peer UA on the other
     * side of the dialog.  When a BYE is received on a dialog, any session
     * associated with that dialog SHOULD terminate.  A UA MUST NOT send a
     * BYE outside of a dialog.  The caller's UA MAY send a BYE for either
     * confirmed or early dialogs, and the callee's UA MAY send a BYE on
     * confirmed dialogs, but MUST NOT send a BYE on early dialogs.
     *
     * However, the callee's UA MUST NOT send a BYE on a confirmed dialog
     * until it has received an ACK for its 2xx response or until the server
     * transaction times out.  If no SIP extensions have defined other
     * application layer states associated with the dialog, the BYE also
     * terminates the dialog.
     *
     * https://tools.ietf.org/html/rfc3261#section-15
     * FIXME: Make these proper Exceptions...
     * @param options - BYE options bucket.
     * @returns
     * Throws `Error` if callee's UA attempts a BYE on an early dialog.
     * Throws `Error` if callee's UA attempts a BYE on a confirmed dialog
     *                while it's waiting on the ACK for its 2xx response.
     */
    bye(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending BYE request`);
      if (this.initialTransaction instanceof InviteServerTransaction) {
        if (this.early) {
          throw new Error("UAS MUST NOT send a BYE on early dialogs.");
        }
        if (this.ackWait && this.initialTransaction.state !== TransactionState.Terminated) {
          throw new Error("UAS MUST NOT send a BYE on a confirmed dialog until it has received an ACK for its 2xx response or until the server transaction times out.");
        }
      }
      return new ByeUserAgentClient(this, delegate, options);
    }
    /**
     * An INFO request can be associated with an Info Package (see
     * Section 5), or associated with a legacy INFO usage (see Section 2).
     *
     * The construction of the INFO request is the same as any other
     * non-target refresh request within an existing invite dialog usage as
     * described in Section 12.2 of RFC 3261.
     * https://tools.ietf.org/html/rfc6086#section-4.2.1
     * @param options - Options bucket.
     */
    info(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending INFO request`);
      if (this.early) {
        throw new Error("Dialog not confirmed.");
      }
      return new InfoUserAgentClient(this, delegate, options);
    }
    /**
     * Modifying an Existing Session
     *
     * A successful INVITE request (see Section 13) establishes both a
     * dialog between two user agents and a session using the offer-answer
     * model.  Section 12 explains how to modify an existing dialog using a
     * target refresh request (for example, changing the remote target URI
     * of the dialog).  This section describes how to modify the actual
     * session.  This modification can involve changing addresses or ports,
     * adding a media stream, deleting a media stream, and so on.  This is
     * accomplished by sending a new INVITE request within the same dialog
     * that established the session.  An INVITE request sent within an
     * existing dialog is known as a re-INVITE.
     *
     *    Note that a single re-INVITE can modify the dialog and the
     *    parameters of the session at the same time.
     *
     * Either the caller or callee can modify an existing session.
     * https://tools.ietf.org/html/rfc3261#section-14
     * @param options - Options bucket
     */
    invite(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending INVITE request`);
      if (this.early) {
        throw new Error("Dialog not confirmed.");
      }
      if (this.reinviteUserAgentClient) {
        throw new Error("There is an ongoing re-INVITE client transaction.");
      }
      if (this.reinviteUserAgentServer) {
        throw new Error("There is an ongoing re-INVITE server transaction.");
      }
      return new ReInviteUserAgentClient(this, delegate, options);
    }
    /**
     * A UAC MAY associate a MESSAGE request with an existing dialog.  If a
     * MESSAGE request is sent within a dialog, it is "associated" with any
     * media session or sessions associated with that dialog.
     * https://tools.ietf.org/html/rfc3428#section-4
     * @param options - Options bucket.
     */
    message(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending MESSAGE request`);
      if (this.early) {
        throw new Error("Dialog not confirmed.");
      }
      const message = this.createOutgoingRequestMessage(C.MESSAGE, options);
      return new MessageUserAgentClient(this.core, message, delegate);
    }
    /**
     * The NOTIFY mechanism defined in [2] MUST be used to inform the agent
     * sending the REFER of the status of the reference.
     * https://tools.ietf.org/html/rfc3515#section-2.4.4
     * @param options - Options bucket.
     */
    notify(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending NOTIFY request`);
      if (this.early) {
        throw new Error("Dialog not confirmed.");
      }
      return new NotifyUserAgentClient(this, delegate, options);
    }
    /**
     * Assuming the response is to be transmitted reliably, the UAC MUST
     * create a new request with method PRACK.  This request is sent within
     * the dialog associated with the provisional response (indeed, the
     * provisional response may have created the dialog).  PRACK requests
     * MAY contain bodies, which are interpreted according to their type and
     * disposition.
     * https://tools.ietf.org/html/rfc3262#section-4
     * @param options - Options bucket.
     */
    prack(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending PRACK request`);
      return new PrackUserAgentClient(this, delegate, options);
    }
    /**
     * REFER is a SIP request and is constructed as defined in [1].  A REFER
     * request MUST contain exactly one Refer-To header field value.
     * https://tools.ietf.org/html/rfc3515#section-2.4.1
     * @param options - Options bucket.
     */
    refer(delegate, options) {
      this.logger.log(`INVITE dialog ${this.id} sending REFER request`);
      if (this.early) {
        throw new Error("Dialog not confirmed.");
      }
      return new ReferUserAgentClient(this, delegate, options);
    }
    /**
     * Requests sent within a dialog, as any other requests, are atomic.  If
     * a particular request is accepted by the UAS, all the state changes
     * associated with it are performed.  If the request is rejected, none
     * of the state changes are performed.
     * https://tools.ietf.org/html/rfc3261#section-12.2.2
     * @param message - Incoming request message within this dialog.
     */
    receiveRequest(message) {
      this.logger.log(`INVITE dialog ${this.id} received ${message.method} request`);
      if (message.method === C.ACK) {
        if (this.ackWait) {
          if (this.initialTransaction instanceof InviteClientTransaction) {
            this.logger.warn(`INVITE dialog ${this.id} received unexpected ${message.method} request, dropping.`);
            return;
          }
          if (this.initialTransaction.request.cseq !== message.cseq) {
            this.logger.warn(`INVITE dialog ${this.id} received unexpected ${message.method} request, dropping.`);
            return;
          }
          this.ackWait = false;
        } else {
          if (!this.reinviteUserAgentServer) {
            this.logger.warn(`INVITE dialog ${this.id} received unexpected ${message.method} request, dropping.`);
            return;
          }
          if (this.reinviteUserAgentServer.transaction.request.cseq !== message.cseq) {
            this.logger.warn(`INVITE dialog ${this.id} received unexpected ${message.method} request, dropping.`);
            return;
          }
          this.reinviteUserAgentServer = void 0;
        }
        this.signalingStateTransition(message);
        if (this.delegate && this.delegate.onAck) {
          const promiseOrVoid = this.delegate.onAck({ message });
          if (promiseOrVoid instanceof Promise) {
            this.ackProcessing = true;
            promiseOrVoid.then(() => this.ackProcessing = false).catch(() => this.ackProcessing = false);
          }
        }
        return;
      }
      if (!this.sequenceGuard(message)) {
        this.logger.log(`INVITE dialog ${this.id} rejected out of order ${message.method} request.`);
        return;
      }
      super.receiveRequest(message);
      if (message.method === C.INVITE) {
        const warning = () => {
          const reason = this.ackWait ? "waiting for initial ACK" : "processing initial ACK";
          this.logger.warn(`INVITE dialog ${this.id} received re-INVITE while ${reason}`);
          let msg = "RFC 5407 suggests the following to avoid this race condition... ";
          msg += " Note: Implementation issues are outside the scope of this document,";
          msg += " but the following tip is provided for avoiding race conditions of";
          msg += " this type.  The caller can delay sending re-INVITE F6 for some period";
          msg += " of time (2 seconds, perhaps), after which the caller can reasonably";
          msg += " assume that its ACK has been received.  Implementors can decouple the";
          msg += " actions of the user (e.g., pressing the hold button) from the actions";
          msg += " of the protocol (the sending of re-INVITE F6), so that the UA can";
          msg += " behave like this.  In this case, it is the implementor's choice as to";
          msg += " how long to wait.  In most cases, such an implementation may be";
          msg += " useful to prevent the type of race condition shown in this section.";
          msg += " This document expresses no preference about whether or not they";
          msg += " should wait for an ACK to be delivered.  After considering the impact";
          msg += " on user experience, implementors should decide whether or not to wait";
          msg += " for a while, because the user experience depends on the";
          msg += " implementation and has no direct bearing on protocol behavior.";
          this.logger.warn(msg);
          return;
        };
        const retryAfter = Math.floor(Math.random() * 10) + 1;
        const extraHeaders = [`Retry-After: ${retryAfter}`];
        if (this.ackProcessing) {
          this.core.replyStateless(message, { statusCode: 500, extraHeaders });
          warning();
          return;
        }
        if (this.ackWait && this.signalingState !== SignalingState.Stable) {
          this.core.replyStateless(message, { statusCode: 500, extraHeaders });
          warning();
          return;
        }
        if (this.reinviteUserAgentServer) {
          this.core.replyStateless(message, { statusCode: 500, extraHeaders });
          return;
        }
        if (this.reinviteUserAgentClient) {
          this.core.replyStateless(message, { statusCode: 491 });
          return;
        }
      }
      if (message.method === C.INVITE) {
        const contact = message.parseHeader("contact");
        if (!contact) {
          throw new Error("Contact undefined.");
        }
        if (!(contact instanceof NameAddrHeader)) {
          throw new Error("Contact not instance of NameAddrHeader.");
        }
        this.dialogState.remoteTarget = contact.uri;
      }
      switch (message.method) {
        case C.BYE:
          {
            const uas = new ByeUserAgentServer(this, message);
            this.delegate && this.delegate.onBye ? this.delegate.onBye(uas) : uas.accept();
            this.dispose();
          }
          break;
        case C.INFO:
          {
            const uas = new InfoUserAgentServer(this, message);
            this.delegate && this.delegate.onInfo ? this.delegate.onInfo(uas) : uas.reject({
              statusCode: 469,
              extraHeaders: ["Recv-Info:"]
            });
          }
          break;
        case C.INVITE:
          {
            const uas = new ReInviteUserAgentServer(this, message);
            this.signalingStateTransition(message);
            this.delegate && this.delegate.onInvite ? this.delegate.onInvite(uas) : uas.reject({ statusCode: 488 });
          }
          break;
        case C.MESSAGE:
          {
            const uas = new MessageUserAgentServer(this.core, message);
            this.delegate && this.delegate.onMessage ? this.delegate.onMessage(uas) : uas.accept();
          }
          break;
        case C.NOTIFY:
          {
            const uas = new NotifyUserAgentServer(this, message);
            this.delegate && this.delegate.onNotify ? this.delegate.onNotify(uas) : uas.accept();
          }
          break;
        case C.PRACK:
          {
            const uas = new PrackUserAgentServer(this, message);
            this.delegate && this.delegate.onPrack ? this.delegate.onPrack(uas) : uas.accept();
          }
          break;
        case C.REFER:
          {
            const uas = new ReferUserAgentServer(this, message);
            this.delegate && this.delegate.onRefer ? this.delegate.onRefer(uas) : uas.reject();
          }
          break;
        default:
          {
            this.logger.log(`INVITE dialog ${this.id} received unimplemented ${message.method} request`);
            this.core.replyStateless(message, { statusCode: 501 });
          }
          break;
      }
    }
    /**
     * Guard against out of order reliable provisional responses and retransmissions.
     * Returns false if the response should be discarded, otherwise true.
     * @param message - Incoming response message within this dialog.
     */
    reliableSequenceGuard(message) {
      const statusCode = message.statusCode;
      if (!statusCode) {
        throw new Error("Status code undefined");
      }
      if (statusCode > 100 && statusCode < 200) {
        const requireHeader = message.getHeader("require");
        const rseqHeader = message.getHeader("rseq");
        const rseq = requireHeader && requireHeader.includes("100rel") && rseqHeader ? Number(rseqHeader) : void 0;
        if (rseq) {
          if (this.rseq && this.rseq + 1 !== rseq) {
            return false;
          }
          this.rseq = this.rseq ? this.rseq + 1 : rseq;
        }
      }
      return true;
    }
    /**
     * If not in a stable signaling state, rollback to prior stable signaling state.
     */
    signalingStateRollback() {
      if (this._signalingState === SignalingState.HaveLocalOffer || this.signalingState === SignalingState.HaveRemoteOffer) {
        if (this._rollbackOffer && this._rollbackAnswer) {
          this._signalingState = SignalingState.Stable;
          this._offer = this._rollbackOffer;
          this._answer = this._rollbackAnswer;
        }
      }
    }
    /**
     * Update the signaling state of the dialog.
     * @param message - The message to base the update off of.
     */
    signalingStateTransition(message) {
      const body = getBody(message);
      if (!body || body.contentDisposition !== "session") {
        return;
      }
      if (this._signalingState === SignalingState.Stable) {
        this._rollbackOffer = this._offer;
        this._rollbackAnswer = this._answer;
      }
      if (message instanceof IncomingRequestMessage) {
        switch (this._signalingState) {
          case SignalingState.Initial:
          case SignalingState.Stable:
            this._signalingState = SignalingState.HaveRemoteOffer;
            this._offer = body;
            this._answer = void 0;
            break;
          case SignalingState.HaveLocalOffer:
            this._signalingState = SignalingState.Stable;
            this._answer = body;
            break;
          case SignalingState.HaveRemoteOffer:
            break;
          case SignalingState.Closed:
            break;
          default:
            throw new Error("Unexpected signaling state.");
        }
      }
      if (message instanceof IncomingResponseMessage) {
        switch (this._signalingState) {
          case SignalingState.Initial:
          case SignalingState.Stable:
            this._signalingState = SignalingState.HaveRemoteOffer;
            this._offer = body;
            this._answer = void 0;
            break;
          case SignalingState.HaveLocalOffer:
            this._signalingState = SignalingState.Stable;
            this._answer = body;
            break;
          case SignalingState.HaveRemoteOffer:
            break;
          case SignalingState.Closed:
            break;
          default:
            throw new Error("Unexpected signaling state.");
        }
      }
      if (message instanceof OutgoingRequestMessage) {
        switch (this._signalingState) {
          case SignalingState.Initial:
          case SignalingState.Stable:
            this._signalingState = SignalingState.HaveLocalOffer;
            this._offer = body;
            this._answer = void 0;
            break;
          case SignalingState.HaveLocalOffer:
            break;
          case SignalingState.HaveRemoteOffer:
            this._signalingState = SignalingState.Stable;
            this._answer = body;
            break;
          case SignalingState.Closed:
            break;
          default:
            throw new Error("Unexpected signaling state.");
        }
      }
      if (isBody(message)) {
        switch (this._signalingState) {
          case SignalingState.Initial:
          case SignalingState.Stable:
            this._signalingState = SignalingState.HaveLocalOffer;
            this._offer = body;
            this._answer = void 0;
            break;
          case SignalingState.HaveLocalOffer:
            break;
          case SignalingState.HaveRemoteOffer:
            this._signalingState = SignalingState.Stable;
            this._answer = body;
            break;
          case SignalingState.Closed:
            break;
          default:
            throw new Error("Unexpected signaling state.");
        }
      }
    }
    start2xxRetransmissionTimer() {
      if (this.initialTransaction instanceof InviteServerTransaction) {
        const transaction = this.initialTransaction;
        let timeout = Timers.T1;
        const retransmission = () => {
          if (!this.ackWait) {
            this.invite2xxTimer = void 0;
            return;
          }
          this.logger.log("No ACK for 2xx response received, attempting retransmission");
          transaction.retransmitAcceptedResponse();
          timeout = Math.min(timeout * 2, Timers.T2);
          this.invite2xxTimer = setTimeout(retransmission, timeout);
        };
        this.invite2xxTimer = setTimeout(retransmission, timeout);
        const stateChanged = () => {
          if (transaction.state === TransactionState.Terminated) {
            transaction.removeStateChangeListener(stateChanged);
            if (this.invite2xxTimer) {
              clearTimeout(this.invite2xxTimer);
              this.invite2xxTimer = void 0;
            }
            if (this.ackWait) {
              if (this.delegate && this.delegate.onAckTimeout) {
                this.delegate.onAckTimeout();
              } else {
                this.bye();
              }
            }
          }
        };
        transaction.addStateChangeListener(stateChanged);
      }
    }
    // FIXME: Refactor
    startReInvite2xxRetransmissionTimer() {
      if (this.reinviteUserAgentServer && this.reinviteUserAgentServer.transaction instanceof InviteServerTransaction) {
        const transaction = this.reinviteUserAgentServer.transaction;
        let timeout = Timers.T1;
        const retransmission = () => {
          if (!this.reinviteUserAgentServer) {
            this.invite2xxTimer = void 0;
            return;
          }
          this.logger.log("No ACK for 2xx response received, attempting retransmission");
          transaction.retransmitAcceptedResponse();
          timeout = Math.min(timeout * 2, Timers.T2);
          this.invite2xxTimer = setTimeout(retransmission, timeout);
        };
        this.invite2xxTimer = setTimeout(retransmission, timeout);
        const stateChanged = () => {
          if (transaction.state === TransactionState.Terminated) {
            transaction.removeStateChangeListener(stateChanged);
            if (this.invite2xxTimer) {
              clearTimeout(this.invite2xxTimer);
              this.invite2xxTimer = void 0;
            }
            if (this.reinviteUserAgentServer) {
            }
          }
        };
        transaction.addStateChangeListener(stateChanged);
      }
    }
  };

  // node_modules/sip.js/lib/core/user-agents/invite-user-agent-client.js
  var InviteUserAgentClient = class extends UserAgentClient {
    constructor(core, message, delegate) {
      super(InviteClientTransaction, core, message, delegate);
      this.confirmedDialogAcks = /* @__PURE__ */ new Map();
      this.confirmedDialogs = /* @__PURE__ */ new Map();
      this.earlyDialogs = /* @__PURE__ */ new Map();
      this.delegate = delegate;
    }
    dispose() {
      this.earlyDialogs.forEach((earlyDialog) => earlyDialog.dispose());
      this.earlyDialogs.clear();
      super.dispose();
    }
    /**
     * Special case for transport error while sending ACK.
     * @param error - Transport error
     */
    onTransportError(error) {
      if (this.transaction.state === TransactionState.Calling) {
        return super.onTransportError(error);
      }
      this.logger.error(error.message);
      this.logger.error("User agent client request transport error while sending ACK.");
    }
    /**
     * Once the INVITE has been passed to the INVITE client transaction, the
     * UAC waits for responses for the INVITE.
     * https://tools.ietf.org/html/rfc3261#section-13.2.2
     * @param incomingResponse - Incoming response to INVITE request.
     */
    receiveResponse(message) {
      if (!this.authenticationGuard(message)) {
        return;
      }
      const statusCode = message.statusCode ? message.statusCode.toString() : "";
      if (!statusCode) {
        throw new Error("Response status code undefined.");
      }
      switch (true) {
        case /^100$/.test(statusCode):
          if (this.delegate && this.delegate.onTrying) {
            this.delegate.onTrying({ message });
          }
          return;
        case /^1[0-9]{2}$/.test(statusCode):
          {
            if (!message.toTag) {
              this.logger.warn("Non-100 1xx INVITE response received without a to tag, dropping.");
              return;
            }
            const contact = message.parseHeader("contact");
            if (!contact) {
              this.logger.error("Non-100 1xx INVITE response received without a Contact header field, dropping.");
              return;
            }
            const dialogState = Dialog.initialDialogStateForUserAgentClient(this.message, message);
            let earlyDialog = this.earlyDialogs.get(dialogState.id);
            if (!earlyDialog) {
              const transaction = this.transaction;
              if (!(transaction instanceof InviteClientTransaction)) {
                throw new Error("Transaction not instance of InviteClientTransaction.");
              }
              earlyDialog = new SessionDialog(transaction, this.core, dialogState);
              this.earlyDialogs.set(earlyDialog.id, earlyDialog);
            }
            if (!earlyDialog.reliableSequenceGuard(message)) {
              this.logger.warn("1xx INVITE reliable response received out of order or is a retransmission, dropping.");
              return;
            }
            if (earlyDialog.signalingState === SignalingState.Initial || earlyDialog.signalingState === SignalingState.HaveLocalOffer) {
              earlyDialog.signalingStateTransition(message);
            }
            const session = earlyDialog;
            if (this.delegate && this.delegate.onProgress) {
              this.delegate.onProgress({
                message,
                session,
                prack: (options) => {
                  const outgoingPrackRequest = session.prack(void 0, options);
                  return outgoingPrackRequest;
                }
              });
            }
          }
          return;
        case /^2[0-9]{2}$/.test(statusCode):
          {
            if (!message.toTag) {
              this.logger.error("2xx INVITE response received without a to tag, dropping.");
              return;
            }
            const contact = message.parseHeader("contact");
            if (!contact) {
              this.logger.error("2xx INVITE response received without a Contact header field, dropping.");
              return;
            }
            const dialogState = Dialog.initialDialogStateForUserAgentClient(this.message, message);
            let dialog = this.confirmedDialogs.get(dialogState.id);
            if (dialog) {
              const outgoingAckRequest = this.confirmedDialogAcks.get(dialogState.id);
              if (outgoingAckRequest) {
                const transaction = this.transaction;
                if (!(transaction instanceof InviteClientTransaction)) {
                  throw new Error("Client transaction not instance of InviteClientTransaction.");
                }
                transaction.ackResponse(outgoingAckRequest.message);
              } else {
              }
              return;
            }
            dialog = this.earlyDialogs.get(dialogState.id);
            if (dialog) {
              dialog.confirm();
              dialog.recomputeRouteSet(message);
              this.earlyDialogs.delete(dialog.id);
              this.confirmedDialogs.set(dialog.id, dialog);
            } else {
              const transaction = this.transaction;
              if (!(transaction instanceof InviteClientTransaction)) {
                throw new Error("Transaction not instance of InviteClientTransaction.");
              }
              dialog = new SessionDialog(transaction, this.core, dialogState);
              this.confirmedDialogs.set(dialog.id, dialog);
            }
            if (dialog.signalingState === SignalingState.Initial || dialog.signalingState === SignalingState.HaveLocalOffer) {
              dialog.signalingStateTransition(message);
            }
            const session = dialog;
            if (this.delegate && this.delegate.onAccept) {
              this.delegate.onAccept({
                message,
                session,
                ack: (options) => {
                  const outgoingAckRequest = session.ack(options);
                  this.confirmedDialogAcks.set(session.id, outgoingAckRequest);
                  return outgoingAckRequest;
                }
              });
            } else {
              const outgoingAckRequest = session.ack();
              this.confirmedDialogAcks.set(session.id, outgoingAckRequest);
            }
          }
          return;
        case /^3[0-9]{2}$/.test(statusCode):
          this.earlyDialogs.forEach((earlyDialog) => earlyDialog.dispose());
          this.earlyDialogs.clear();
          if (this.delegate && this.delegate.onRedirect) {
            this.delegate.onRedirect({ message });
          }
          return;
        case /^[4-6][0-9]{2}$/.test(statusCode):
          this.earlyDialogs.forEach((earlyDialog) => earlyDialog.dispose());
          this.earlyDialogs.clear();
          if (this.delegate && this.delegate.onReject) {
            this.delegate.onReject({ message });
          }
          return;
        default:
          throw new Error(`Invalid status code ${statusCode}`);
      }
      throw new Error(`Executing what should be an unreachable code path receiving ${statusCode} response.`);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/invite-user-agent-server.js
  var InviteUserAgentServer = class extends UserAgentServer {
    constructor(core, message, delegate) {
      super(InviteServerTransaction, core, message, delegate);
      this.core = core;
    }
    dispose() {
      if (this.earlyDialog) {
        this.earlyDialog.dispose();
      }
      super.dispose();
    }
    /**
     * 13.3.1.4 The INVITE is Accepted
     * The UAS core generates a 2xx response.  This response establishes a
     * dialog, and therefore follows the procedures of Section 12.1.1 in
     * addition to those of Section 8.2.6.
     * https://tools.ietf.org/html/rfc3261#section-13.3.1.4
     * @param options - Accept options bucket.
     */
    accept(options = { statusCode: 200 }) {
      if (!this.acceptable) {
        throw new TransactionStateError(`${this.message.method} not acceptable in state ${this.transaction.state}.`);
      }
      if (!this.confirmedDialog) {
        if (this.earlyDialog) {
          this.earlyDialog.confirm();
          this.confirmedDialog = this.earlyDialog;
          this.earlyDialog = void 0;
        } else {
          const transaction = this.transaction;
          if (!(transaction instanceof InviteServerTransaction)) {
            throw new Error("Transaction not instance of InviteClientTransaction.");
          }
          const state = Dialog.initialDialogStateForUserAgentServer(this.message, this.toTag);
          this.confirmedDialog = new SessionDialog(transaction, this.core, state);
        }
      }
      const recordRouteHeader = this.message.getHeaders("record-route").map((header) => `Record-Route: ${header}`);
      const contactHeader = `Contact: ${this.core.configuration.contact.toString()}`;
      const allowHeader = "Allow: " + AllowedMethods.toString();
      if (!options.body) {
        if (this.confirmedDialog.signalingState === SignalingState.Stable) {
          options.body = this.confirmedDialog.answer;
        } else if (this.confirmedDialog.signalingState === SignalingState.Initial || this.confirmedDialog.signalingState === SignalingState.HaveRemoteOffer) {
          throw new Error("Response must have a body.");
        }
      }
      options.statusCode = options.statusCode || 200;
      options.extraHeaders = options.extraHeaders || [];
      options.extraHeaders = options.extraHeaders.concat(recordRouteHeader);
      options.extraHeaders.push(allowHeader);
      options.extraHeaders.push(contactHeader);
      const response = super.accept(options);
      const session = this.confirmedDialog;
      const result = Object.assign(Object.assign({}, response), { session });
      if (options.body) {
        if (this.confirmedDialog.signalingState !== SignalingState.Stable) {
          this.confirmedDialog.signalingStateTransition(options.body);
        }
      }
      return result;
    }
    /**
     * 13.3.1.1 Progress
     * If the UAS is not able to answer the invitation immediately, it can
     * choose to indicate some kind of progress to the UAC (for example, an
     * indication that a phone is ringing).  This is accomplished with a
     * provisional response between 101 and 199.  These provisional
     * responses establish early dialogs and therefore follow the procedures
     * of Section 12.1.1 in addition to those of Section 8.2.6.  A UAS MAY
     * send as many provisional responses as it likes.  Each of these MUST
     * indicate the same dialog ID.  However, these will not be delivered
     * reliably.
     *
     * If the UAS desires an extended period of time to answer the INVITE,
     * it will need to ask for an "extension" in order to prevent proxies
     * from canceling the transaction.  A proxy has the option of canceling
     * a transaction when there is a gap of 3 minutes between responses in a
     * transaction.  To prevent cancellation, the UAS MUST send a non-100
     * provisional response at every minute, to handle the possibility of
     * lost provisional responses.
     * https://tools.ietf.org/html/rfc3261#section-13.3.1.1
     * @param options - Progress options bucket.
     */
    progress(options = { statusCode: 180 }) {
      if (!this.progressable) {
        throw new TransactionStateError(`${this.message.method} not progressable in state ${this.transaction.state}.`);
      }
      if (!this.earlyDialog) {
        const transaction = this.transaction;
        if (!(transaction instanceof InviteServerTransaction)) {
          throw new Error("Transaction not instance of InviteClientTransaction.");
        }
        const state = Dialog.initialDialogStateForUserAgentServer(this.message, this.toTag, true);
        this.earlyDialog = new SessionDialog(transaction, this.core, state);
      }
      const recordRouteHeader = this.message.getHeaders("record-route").map((header) => `Record-Route: ${header}`);
      const contactHeader = `Contact: ${this.core.configuration.contact}`;
      options.extraHeaders = options.extraHeaders || [];
      options.extraHeaders = options.extraHeaders.concat(recordRouteHeader);
      options.extraHeaders.push(contactHeader);
      const response = super.progress(options);
      const session = this.earlyDialog;
      const result = Object.assign(Object.assign({}, response), { session });
      if (options.body) {
        if (this.earlyDialog.signalingState !== SignalingState.Stable) {
          this.earlyDialog.signalingStateTransition(options.body);
        }
      }
      return result;
    }
    /**
     * 13.3.1.2 The INVITE is Redirected
     * If the UAS decides to redirect the call, a 3xx response is sent.  A
     * 300 (Multiple Choices), 301 (Moved Permanently) or 302 (Moved
     * Temporarily) response SHOULD contain a Contact header field
     * containing one or more URIs of new addresses to be tried.  The
     * response is passed to the INVITE server transaction, which will deal
     * with its retransmissions.
     * https://tools.ietf.org/html/rfc3261#section-13.3.1.2
     * @param contacts - Contacts to redirect to.
     * @param options - Redirect options bucket.
     */
    redirect(contacts, options = { statusCode: 302 }) {
      return super.redirect(contacts, options);
    }
    /**
     * 13.3.1.3 The INVITE is Rejected
     * A common scenario occurs when the callee is currently not willing or
     * able to take additional calls at this end system.  A 486 (Busy Here)
     * SHOULD be returned in such a scenario.
     * https://tools.ietf.org/html/rfc3261#section-13.3.1.3
     * @param options - Reject options bucket.
     */
    reject(options = { statusCode: 486 }) {
      return super.reject(options);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/publish-user-agent-client.js
  var PublishUserAgentClient = class extends UserAgentClient {
    constructor(core, message, delegate) {
      super(NonInviteClientTransaction, core, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/register-user-agent-client.js
  var RegisterUserAgentClient = class extends UserAgentClient {
    constructor(core, message, delegate) {
      super(NonInviteClientTransaction, core, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/register-user-agent-server.js
  var RegisterUserAgentServer = class extends UserAgentServer {
    constructor(core, message, delegate) {
      super(NonInviteServerTransaction, core, message, delegate);
      this.core = core;
    }
  };

  // node_modules/sip.js/lib/core/user-agents/re-subscribe-user-agent-client.js
  var ReSubscribeUserAgentClient = class extends UserAgentClient {
    constructor(dialog, delegate, options) {
      const message = dialog.createOutgoingRequestMessage(C.SUBSCRIBE, options);
      super(NonInviteClientTransaction, dialog.userAgentCore, message, delegate);
      this.dialog = dialog;
    }
    waitNotifyStop() {
      return;
    }
    /**
     * Receive a response from the transaction layer.
     * @param message - Incoming response message.
     */
    receiveResponse(message) {
      if (message.statusCode && message.statusCode >= 200 && message.statusCode < 300) {
        const expires = message.getHeader("Expires");
        if (!expires) {
          this.logger.warn("Expires header missing in a 200-class response to SUBSCRIBE");
        } else {
          const subscriptionExpiresReceived = Number(expires);
          if (this.dialog.subscriptionExpires > subscriptionExpiresReceived) {
            this.dialog.subscriptionExpires = subscriptionExpiresReceived;
          }
        }
      }
      if (message.statusCode && message.statusCode >= 400 && message.statusCode < 700) {
        const errorCodes = [404, 405, 410, 416, 480, 481, 482, 483, 484, 485, 489, 501, 604];
        if (errorCodes.includes(message.statusCode)) {
          this.dialog.terminate();
        }
      }
      super.receiveResponse(message);
    }
  };

  // node_modules/sip.js/lib/core/dialogs/subscription-dialog.js
  var SubscriptionDialog = class extends Dialog {
    constructor(subscriptionEvent, subscriptionExpires, subscriptionState, core, state, delegate) {
      super(core, state);
      this.delegate = delegate;
      this._autoRefresh = false;
      this._subscriptionEvent = subscriptionEvent;
      this._subscriptionExpires = subscriptionExpires;
      this._subscriptionExpiresInitial = subscriptionExpires;
      this._subscriptionExpiresLastSet = Math.floor(Date.now() / 1e3);
      this._subscriptionRefresh = void 0;
      this._subscriptionRefreshLastSet = void 0;
      this._subscriptionState = subscriptionState;
      this.logger = core.loggerFactory.getLogger("sip.subscribe-dialog");
      this.logger.log(`SUBSCRIBE dialog ${this.id} constructed`);
    }
    /**
     * When a UAC receives a response that establishes a dialog, it
     * constructs the state of the dialog.  This state MUST be maintained
     * for the duration of the dialog.
     * https://tools.ietf.org/html/rfc3261#section-12.1.2
     * @param outgoingRequestMessage - Outgoing request message for dialog.
     * @param incomingResponseMessage - Incoming response message creating dialog.
     */
    static initialDialogStateForSubscription(outgoingSubscribeRequestMessage, incomingNotifyRequestMessage) {
      const secure = false;
      const routeSet = incomingNotifyRequestMessage.getHeaders("record-route");
      const contact = incomingNotifyRequestMessage.parseHeader("contact");
      if (!contact) {
        throw new Error("Contact undefined.");
      }
      if (!(contact instanceof NameAddrHeader)) {
        throw new Error("Contact not instance of NameAddrHeader.");
      }
      const remoteTarget = contact.uri;
      const localSequenceNumber = outgoingSubscribeRequestMessage.cseq;
      const remoteSequenceNumber = void 0;
      const callId = outgoingSubscribeRequestMessage.callId;
      const localTag = outgoingSubscribeRequestMessage.fromTag;
      const remoteTag = incomingNotifyRequestMessage.fromTag;
      if (!callId) {
        throw new Error("Call id undefined.");
      }
      if (!localTag) {
        throw new Error("From tag undefined.");
      }
      if (!remoteTag) {
        throw new Error("To tag undefined.");
      }
      if (!outgoingSubscribeRequestMessage.from) {
        throw new Error("From undefined.");
      }
      if (!outgoingSubscribeRequestMessage.to) {
        throw new Error("To undefined.");
      }
      const localURI = outgoingSubscribeRequestMessage.from.uri;
      const remoteURI = outgoingSubscribeRequestMessage.to.uri;
      const early = false;
      const dialogState = {
        id: callId + localTag + remoteTag,
        early,
        callId,
        localTag,
        remoteTag,
        localSequenceNumber,
        remoteSequenceNumber,
        localURI,
        remoteURI,
        remoteTarget,
        routeSet,
        secure
      };
      return dialogState;
    }
    dispose() {
      super.dispose();
      if (this.N) {
        clearTimeout(this.N);
        this.N = void 0;
      }
      this.refreshTimerClear();
      this.logger.log(`SUBSCRIBE dialog ${this.id} destroyed`);
    }
    get autoRefresh() {
      return this._autoRefresh;
    }
    set autoRefresh(autoRefresh) {
      this._autoRefresh = true;
      this.refreshTimerSet();
    }
    get subscriptionEvent() {
      return this._subscriptionEvent;
    }
    /** Number of seconds until subscription expires. */
    get subscriptionExpires() {
      const secondsSinceLastSet = Math.floor(Date.now() / 1e3) - this._subscriptionExpiresLastSet;
      const secondsUntilExpires = this._subscriptionExpires - secondsSinceLastSet;
      return Math.max(secondsUntilExpires, 0);
    }
    set subscriptionExpires(expires) {
      if (expires < 0) {
        throw new Error("Expires must be greater than or equal to zero.");
      }
      this._subscriptionExpires = expires;
      this._subscriptionExpiresLastSet = Math.floor(Date.now() / 1e3);
      if (this.autoRefresh) {
        const refresh = this.subscriptionRefresh;
        if (refresh === void 0 || refresh >= expires) {
          this.refreshTimerSet();
        }
      }
    }
    get subscriptionExpiresInitial() {
      return this._subscriptionExpiresInitial;
    }
    /** Number of seconds until subscription auto refresh. */
    get subscriptionRefresh() {
      if (this._subscriptionRefresh === void 0 || this._subscriptionRefreshLastSet === void 0) {
        return void 0;
      }
      const secondsSinceLastSet = Math.floor(Date.now() / 1e3) - this._subscriptionRefreshLastSet;
      const secondsUntilExpires = this._subscriptionRefresh - secondsSinceLastSet;
      return Math.max(secondsUntilExpires, 0);
    }
    get subscriptionState() {
      return this._subscriptionState;
    }
    /**
     * Receive in dialog request message from transport.
     * @param message -  The incoming request message.
     */
    receiveRequest(message) {
      this.logger.log(`SUBSCRIBE dialog ${this.id} received ${message.method} request`);
      if (!this.sequenceGuard(message)) {
        this.logger.log(`SUBSCRIBE dialog ${this.id} rejected out of order ${message.method} request.`);
        return;
      }
      super.receiveRequest(message);
      switch (message.method) {
        case C.NOTIFY:
          this.onNotify(message);
          break;
        default:
          this.logger.log(`SUBSCRIBE dialog ${this.id} received unimplemented ${message.method} request`);
          this.core.replyStateless(message, { statusCode: 501 });
          break;
      }
    }
    /**
     * 4.1.2.2.  Refreshing of Subscriptions
     * https://tools.ietf.org/html/rfc6665#section-4.1.2.2
     */
    refresh() {
      const allowHeader = "Allow: " + AllowedMethods.toString();
      const options = {};
      options.extraHeaders = (options.extraHeaders || []).slice();
      options.extraHeaders.push(allowHeader);
      options.extraHeaders.push("Event: " + this.subscriptionEvent);
      options.extraHeaders.push("Expires: " + this.subscriptionExpiresInitial);
      options.extraHeaders.push("Contact: " + this.core.configuration.contact.toString());
      return this.subscribe(void 0, options);
    }
    /**
     * 4.1.2.2.  Refreshing of Subscriptions
     * https://tools.ietf.org/html/rfc6665#section-4.1.2.2
     * @param delegate - Delegate to handle responses.
     * @param options - Options bucket.
     */
    subscribe(delegate, options = {}) {
      var _a;
      if (this.subscriptionState !== SubscriptionState.Pending && this.subscriptionState !== SubscriptionState.Active) {
        throw new Error(`Invalid state ${this.subscriptionState}. May only re-subscribe while in state "pending" or "active".`);
      }
      this.logger.log(`SUBSCRIBE dialog ${this.id} sending SUBSCRIBE request`);
      const uac = new ReSubscribeUserAgentClient(this, delegate, options);
      if (this.N) {
        clearTimeout(this.N);
        this.N = void 0;
      }
      if (!((_a = options.extraHeaders) === null || _a === void 0 ? void 0 : _a.includes("Expires: 0"))) {
        this.N = setTimeout(() => this.timerN(), Timers.TIMER_N);
      }
      return uac;
    }
    /**
     * 4.4.1.  Dialog Creation and Termination
     * A subscription is destroyed after a notifier sends a NOTIFY request
     * with a "Subscription-State" of "terminated", or in certain error
     * situations described elsewhere in this document.
     * https://tools.ietf.org/html/rfc6665#section-4.4.1
     */
    terminate() {
      this.stateTransition(SubscriptionState.Terminated);
      this.onTerminated();
    }
    /**
     * 4.1.2.3.  Unsubscribing
     * https://tools.ietf.org/html/rfc6665#section-4.1.2.3
     */
    unsubscribe() {
      const allowHeader = "Allow: " + AllowedMethods.toString();
      const options = {};
      options.extraHeaders = (options.extraHeaders || []).slice();
      options.extraHeaders.push(allowHeader);
      options.extraHeaders.push("Event: " + this.subscriptionEvent);
      options.extraHeaders.push("Expires: 0");
      options.extraHeaders.push("Contact: " + this.core.configuration.contact.toString());
      return this.subscribe(void 0, options);
    }
    /**
     * Handle in dialog NOTIFY requests.
     * This does not include the first NOTIFY which created the dialog.
     * @param message - The incoming NOTIFY request message.
     */
    onNotify(message) {
      const event = message.parseHeader("Event").event;
      if (!event || event !== this.subscriptionEvent) {
        this.core.replyStateless(message, { statusCode: 489 });
        return;
      }
      if (this.N) {
        clearTimeout(this.N);
        this.N = void 0;
      }
      const subscriptionState = message.parseHeader("Subscription-State");
      if (!subscriptionState || !subscriptionState.state) {
        this.core.replyStateless(message, { statusCode: 489 });
        return;
      }
      const state = subscriptionState.state;
      const expires = subscriptionState.expires ? Math.max(subscriptionState.expires, 0) : void 0;
      switch (state) {
        case "pending":
          this.stateTransition(SubscriptionState.Pending, expires);
          break;
        case "active":
          this.stateTransition(SubscriptionState.Active, expires);
          break;
        case "terminated":
          this.stateTransition(SubscriptionState.Terminated, expires);
          break;
        default:
          this.logger.warn("Unrecognized subscription state.");
          break;
      }
      const uas = new NotifyUserAgentServer(this, message);
      if (this.delegate && this.delegate.onNotify) {
        this.delegate.onNotify(uas);
      } else {
        uas.accept();
      }
    }
    onRefresh(request) {
      if (this.delegate && this.delegate.onRefresh) {
        this.delegate.onRefresh(request);
      }
    }
    onTerminated() {
      if (this.delegate && this.delegate.onTerminated) {
        this.delegate.onTerminated();
      }
    }
    refreshTimerClear() {
      if (this.refreshTimer) {
        clearTimeout(this.refreshTimer);
        this.refreshTimer = void 0;
      }
    }
    refreshTimerSet() {
      this.refreshTimerClear();
      if (this.autoRefresh && this.subscriptionExpires > 0) {
        const refresh = this.subscriptionExpires * 900;
        this._subscriptionRefresh = Math.floor(refresh / 1e3);
        this._subscriptionRefreshLastSet = Math.floor(Date.now() / 1e3);
        this.refreshTimer = setTimeout(() => {
          this.refreshTimer = void 0;
          this._subscriptionRefresh = void 0;
          this._subscriptionRefreshLastSet = void 0;
          this.onRefresh(this.refresh());
        }, refresh);
      }
    }
    stateTransition(newState, newExpires) {
      const invalidStateTransition = () => {
        this.logger.warn(`Invalid subscription state transition from ${this.subscriptionState} to ${newState}`);
      };
      switch (newState) {
        case SubscriptionState.Initial:
          invalidStateTransition();
          return;
        case SubscriptionState.NotifyWait:
          invalidStateTransition();
          return;
        case SubscriptionState.Pending:
          if (this.subscriptionState !== SubscriptionState.NotifyWait && this.subscriptionState !== SubscriptionState.Pending) {
            invalidStateTransition();
            return;
          }
          break;
        case SubscriptionState.Active:
          if (this.subscriptionState !== SubscriptionState.NotifyWait && this.subscriptionState !== SubscriptionState.Pending && this.subscriptionState !== SubscriptionState.Active) {
            invalidStateTransition();
            return;
          }
          break;
        case SubscriptionState.Terminated:
          if (this.subscriptionState !== SubscriptionState.NotifyWait && this.subscriptionState !== SubscriptionState.Pending && this.subscriptionState !== SubscriptionState.Active) {
            invalidStateTransition();
            return;
          }
          break;
        default:
          invalidStateTransition();
          return;
      }
      if (newState === SubscriptionState.Pending) {
        if (newExpires) {
          this.subscriptionExpires = newExpires;
        }
      }
      if (newState === SubscriptionState.Active) {
        if (newExpires) {
          this.subscriptionExpires = newExpires;
        }
      }
      if (newState === SubscriptionState.Terminated) {
        this.dispose();
      }
      this._subscriptionState = newState;
    }
    /**
     * When refreshing a subscription, a subscriber starts Timer N, set to
     * 64*T1, when it sends the SUBSCRIBE request.  If this Timer N expires
     * prior to the receipt of a NOTIFY request, the subscriber considers
     * the subscription terminated.  If the subscriber receives a success
     * response to the SUBSCRIBE request that indicates that no NOTIFY
     * request will be generated -- such as the 204 response defined for use
     * with the optional extension described in [RFC5839] -- then it MUST
     * cancel Timer N.
     * https://tools.ietf.org/html/rfc6665#section-4.1.2.2
     */
    timerN() {
      this.logger.warn(`Timer N expired for SUBSCRIBE dialog. Timed out waiting for NOTIFY.`);
      if (this.subscriptionState !== SubscriptionState.Terminated) {
        this.stateTransition(SubscriptionState.Terminated);
        this.onTerminated();
      }
    }
  };

  // node_modules/sip.js/lib/core/user-agents/subscribe-user-agent-client.js
  var SubscribeUserAgentClient = class extends UserAgentClient {
    constructor(core, message, delegate) {
      const event = message.getHeader("Event");
      if (!event) {
        throw new Error("Event undefined");
      }
      const expires = message.getHeader("Expires");
      if (!expires) {
        throw new Error("Expires undefined");
      }
      super(NonInviteClientTransaction, core, message, delegate);
      this.delegate = delegate;
      this.subscriberId = message.callId + message.fromTag + event;
      this.subscriptionExpiresRequested = this.subscriptionExpires = Number(expires);
      this.subscriptionEvent = event;
      this.subscriptionState = SubscriptionState.NotifyWait;
      this.waitNotifyStart();
    }
    /**
     * Destructor.
     * Note that Timer N may live on waiting for an initial NOTIFY and
     * the delegate may still receive that NOTIFY. If you don't want
     * that behavior then either clear the delegate so the delegate
     * doesn't get called (a 200 will be sent in response to the NOTIFY)
     * or call `waitNotifyStop` which will clear Timer N and remove this
     * UAC from the core (a 481 will be sent in response to the NOTIFY).
     */
    dispose() {
      super.dispose();
    }
    /**
     * Handle out of dialog NOTIFY associated with SUBSCRIBE request.
     * This is the first NOTIFY received after the SUBSCRIBE request.
     * @param uas - User agent server handling the subscription creating NOTIFY.
     */
    onNotify(uas) {
      const event = uas.message.parseHeader("Event").event;
      if (!event || event !== this.subscriptionEvent) {
        this.logger.warn(`Failed to parse event.`);
        uas.reject({ statusCode: 489 });
        return;
      }
      const subscriptionState = uas.message.parseHeader("Subscription-State");
      if (!subscriptionState || !subscriptionState.state) {
        this.logger.warn("Failed to parse subscription state.");
        uas.reject({ statusCode: 489 });
        return;
      }
      const state = subscriptionState.state;
      switch (state) {
        case "pending":
          break;
        case "active":
          break;
        case "terminated":
          break;
        default:
          this.logger.warn(`Invalid subscription state ${state}`);
          uas.reject({ statusCode: 489 });
          return;
      }
      if (state !== "terminated") {
        const contact = uas.message.parseHeader("contact");
        if (!contact) {
          this.logger.warn("Failed to parse contact.");
          uas.reject({ statusCode: 489 });
          return;
        }
      }
      if (this.dialog) {
        throw new Error("Dialog already created. This implementation only supports install of single subscriptions.");
      }
      this.waitNotifyStop();
      this.subscriptionExpires = subscriptionState.expires ? Math.min(this.subscriptionExpires, Math.max(subscriptionState.expires, 0)) : this.subscriptionExpires;
      switch (state) {
        case "pending":
          this.subscriptionState = SubscriptionState.Pending;
          break;
        case "active":
          this.subscriptionState = SubscriptionState.Active;
          break;
        case "terminated":
          this.subscriptionState = SubscriptionState.Terminated;
          break;
        default:
          throw new Error(`Unrecognized state ${state}.`);
      }
      if (this.subscriptionState !== SubscriptionState.Terminated) {
        const dialogState = SubscriptionDialog.initialDialogStateForSubscription(this.message, uas.message);
        this.dialog = new SubscriptionDialog(this.subscriptionEvent, this.subscriptionExpires, this.subscriptionState, this.core, dialogState);
      }
      if (this.delegate && this.delegate.onNotify) {
        const request = uas;
        const subscription = this.dialog;
        this.delegate.onNotify({ request, subscription });
      } else {
        uas.accept();
      }
    }
    waitNotifyStart() {
      if (!this.N) {
        this.core.subscribers.set(this.subscriberId, this);
        this.N = setTimeout(() => this.timerN(), Timers.TIMER_N);
      }
    }
    waitNotifyStop() {
      if (this.N) {
        this.core.subscribers.delete(this.subscriberId);
        clearTimeout(this.N);
        this.N = void 0;
      }
    }
    /**
     * Receive a response from the transaction layer.
     * @param message - Incoming response message.
     */
    receiveResponse(message) {
      if (!this.authenticationGuard(message)) {
        return;
      }
      if (message.statusCode && message.statusCode >= 200 && message.statusCode < 300) {
        const expires = message.getHeader("Expires");
        if (!expires) {
          this.logger.warn("Expires header missing in a 200-class response to SUBSCRIBE");
        } else {
          const subscriptionExpiresReceived = Number(expires);
          if (subscriptionExpiresReceived > this.subscriptionExpiresRequested) {
            this.logger.warn("Expires header in a 200-class response to SUBSCRIBE with a higher value than the one in the request");
          }
          if (subscriptionExpiresReceived < this.subscriptionExpires) {
            this.subscriptionExpires = subscriptionExpiresReceived;
          }
        }
        if (this.dialog) {
          if (this.dialog.subscriptionExpires > this.subscriptionExpires) {
            this.dialog.subscriptionExpires = this.subscriptionExpires;
          }
        }
      }
      if (message.statusCode && message.statusCode >= 300 && message.statusCode < 700) {
        this.waitNotifyStop();
      }
      super.receiveResponse(message);
    }
    /**
     * To ensure that subscribers do not wait indefinitely for a
     * subscription to be established, a subscriber starts a Timer N, set to
     * 64*T1, when it sends a SUBSCRIBE request.  If this Timer N expires
     * prior to the receipt of a NOTIFY request, the subscriber considers
     * the subscription failed, and cleans up any state associated with the
     * subscription attempt.
     * https://tools.ietf.org/html/rfc6665#section-4.1.2.4
     */
    timerN() {
      this.logger.warn(`Timer N expired for SUBSCRIBE user agent client. Timed out waiting for NOTIFY.`);
      this.waitNotifyStop();
      if (this.delegate && this.delegate.onNotifyTimeout) {
        this.delegate.onNotifyTimeout();
      }
    }
  };

  // node_modules/sip.js/lib/core/user-agents/subscribe-user-agent-server.js
  var SubscribeUserAgentServer = class extends UserAgentServer {
    constructor(core, message, delegate) {
      super(NonInviteServerTransaction, core, message, delegate);
      this.core = core;
    }
  };

  // node_modules/sip.js/lib/core/user-agent-core/user-agent-core.js
  var acceptedBodyTypes = ["application/sdp", "application/dtmf-relay"];
  var UserAgentCore = class {
    /**
     * Constructor.
     * @param configuration - Configuration.
     * @param delegate - Delegate.
     */
    constructor(configuration, delegate = {}) {
      this.userAgentClients = /* @__PURE__ */ new Map();
      this.userAgentServers = /* @__PURE__ */ new Map();
      this.configuration = configuration;
      this.delegate = delegate;
      this.dialogs = /* @__PURE__ */ new Map();
      this.subscribers = /* @__PURE__ */ new Map();
      this.logger = configuration.loggerFactory.getLogger("sip.user-agent-core");
    }
    /** Destructor. */
    dispose() {
      this.reset();
    }
    /** Reset. */
    reset() {
      this.dialogs.forEach((dialog) => dialog.dispose());
      this.dialogs.clear();
      this.subscribers.forEach((subscriber) => subscriber.dispose());
      this.subscribers.clear();
      this.userAgentClients.forEach((uac) => uac.dispose());
      this.userAgentClients.clear();
      this.userAgentServers.forEach((uac) => uac.dispose());
      this.userAgentServers.clear();
    }
    /** Logger factory. */
    get loggerFactory() {
      return this.configuration.loggerFactory;
    }
    /** Transport. */
    get transport() {
      const transport = this.configuration.transportAccessor();
      if (!transport) {
        throw new Error("Transport undefined.");
      }
      return transport;
    }
    /**
     * Send INVITE.
     * @param request - Outgoing request.
     * @param delegate - Request delegate.
     */
    invite(request, delegate) {
      return new InviteUserAgentClient(this, request, delegate);
    }
    /**
     * Send MESSAGE.
     * @param request - Outgoing request.
     * @param delegate - Request delegate.
     */
    message(request, delegate) {
      return new MessageUserAgentClient(this, request, delegate);
    }
    /**
     * Send PUBLISH.
     * @param request - Outgoing request.
     * @param delegate - Request delegate.
     */
    publish(request, delegate) {
      return new PublishUserAgentClient(this, request, delegate);
    }
    /**
     * Send REGISTER.
     * @param request - Outgoing request.
     * @param delegate - Request delegate.
     */
    register(request, delegate) {
      return new RegisterUserAgentClient(this, request, delegate);
    }
    /**
     * Send SUBSCRIBE.
     * @param request - Outgoing request.
     * @param delegate - Request delegate.
     */
    subscribe(request, delegate) {
      return new SubscribeUserAgentClient(this, request, delegate);
    }
    /**
     * Send a request.
     * @param request - Outgoing request.
     * @param delegate - Request delegate.
     */
    request(request, delegate) {
      return new UserAgentClient(NonInviteClientTransaction, this, request, delegate);
    }
    /**
     * Outgoing request message factory function.
     * @param method - Method.
     * @param requestURI - Request-URI.
     * @param fromURI - From URI.
     * @param toURI - To URI.
     * @param options - Request options.
     * @param extraHeaders - Extra headers to add.
     * @param body - Message body.
     */
    makeOutgoingRequestMessage(method, requestURI, fromURI, toURI, options, extraHeaders, body) {
      const callIdPrefix = this.configuration.sipjsId;
      const fromDisplayName = this.configuration.displayName;
      const forceRport = this.configuration.viaForceRport;
      const hackViaTcp = this.configuration.hackViaTcp;
      const optionTags = this.configuration.supportedOptionTags.slice();
      if (method === C.REGISTER) {
        optionTags.push("path", "gruu");
      }
      if (method === C.INVITE && (this.configuration.contact.pubGruu || this.configuration.contact.tempGruu)) {
        optionTags.push("gruu");
      }
      const routeSet = this.configuration.routeSet;
      const userAgentString = this.configuration.userAgentHeaderFieldValue;
      const viaHost = this.configuration.viaHost;
      const defaultOptions = {
        callIdPrefix,
        forceRport,
        fromDisplayName,
        hackViaTcp,
        optionTags,
        routeSet,
        userAgentString,
        viaHost
      };
      const requestOptions = Object.assign(Object.assign({}, defaultOptions), options);
      return new OutgoingRequestMessage(method, requestURI, fromURI, toURI, requestOptions, extraHeaders, body);
    }
    /**
     * Handle an incoming request message from the transport.
     * @param message - Incoming request message from transport layer.
     */
    receiveIncomingRequestFromTransport(message) {
      this.receiveRequestFromTransport(message);
    }
    /**
     * Handle an incoming response message from the transport.
     * @param message - Incoming response message from transport layer.
     */
    receiveIncomingResponseFromTransport(message) {
      this.receiveResponseFromTransport(message);
    }
    /**
     * A stateless UAS is a UAS that does not maintain transaction state.
     * It replies to requests normally, but discards any state that would
     * ordinarily be retained by a UAS after a response has been sent.  If a
     * stateless UAS receives a retransmission of a request, it regenerates
     * the response and re-sends it, just as if it were replying to the first
     * instance of the request. A UAS cannot be stateless unless the request
     * processing for that method would always result in the same response
     * if the requests are identical. This rules out stateless registrars,
     * for example.  Stateless UASs do not use a transaction layer; they
     * receive requests directly from the transport layer and send responses
     * directly to the transport layer.
     * https://tools.ietf.org/html/rfc3261#section-8.2.7
     * @param message - Incoming request message to reply to.
     * @param statusCode - Status code to reply with.
     */
    replyStateless(message, options) {
      const userAgent = this.configuration.userAgentHeaderFieldValue;
      const supported = this.configuration.supportedOptionTagsResponse;
      options = Object.assign(Object.assign({}, options), { userAgent, supported });
      const response = constructOutgoingResponse(message, options);
      this.transport.send(response.message).catch((error) => {
        if (error instanceof Error) {
          this.logger.error(error.message);
        }
        this.logger.error(`Transport error occurred sending stateless reply to ${message.method} request.`);
      });
      return response;
    }
    /**
     * In Section 18.2.1, replace the last paragraph with:
     *
     * Next, the server transport attempts to match the request to a
     * server transaction.  It does so using the matching rules described
     * in Section 17.2.3.  If a matching server transaction is found, the
     * request is passed to that transaction for processing.  If no match
     * is found, the request is passed to the core, which may decide to
     * construct a new server transaction for that request.
     * https://tools.ietf.org/html/rfc6026#section-8.10
     * @param message - Incoming request message from transport layer.
     */
    receiveRequestFromTransport(message) {
      const transactionId = message.viaBranch;
      const uas = this.userAgentServers.get(transactionId);
      if (message.method === C.ACK) {
        if (uas && uas.transaction.state === TransactionState.Accepted) {
          if (uas instanceof InviteUserAgentServer) {
            this.logger.warn(`Discarding out of dialog ACK after 2xx response sent on transaction ${transactionId}.`);
            return;
          }
        }
      }
      if (message.method === C.CANCEL) {
        if (uas) {
          this.replyStateless(message, { statusCode: 200 });
          if (uas.transaction instanceof InviteServerTransaction && uas.transaction.state === TransactionState.Proceeding) {
            if (uas instanceof InviteUserAgentServer) {
              uas.receiveCancel(message);
            }
          }
        } else {
          this.replyStateless(message, { statusCode: 481 });
        }
        return;
      }
      if (uas) {
        uas.transaction.receiveRequest(message);
        return;
      }
      this.receiveRequest(message);
      return;
    }
    /**
     * UAC and UAS procedures depend strongly on two factors.  First, based
     * on whether the request or response is inside or outside of a dialog,
     * and second, based on the method of a request.  Dialogs are discussed
     * thoroughly in Section 12; they represent a peer-to-peer relationship
     * between user agents and are established by specific SIP methods, such
     * as INVITE.
     * @param message - Incoming request message.
     */
    receiveRequest(message) {
      if (!AllowedMethods.includes(message.method)) {
        const allowHeader = "Allow: " + AllowedMethods.toString();
        this.replyStateless(message, {
          statusCode: 405,
          extraHeaders: [allowHeader]
        });
        return;
      }
      if (!message.ruri) {
        throw new Error("Request-URI undefined.");
      }
      if (message.ruri.scheme !== "sip") {
        this.replyStateless(message, { statusCode: 416 });
        return;
      }
      const ruri = message.ruri;
      const ruriMatches = (uri) => {
        return !!uri && uri.user === ruri.user;
      };
      if (!ruriMatches(this.configuration.aor) && !(ruriMatches(this.configuration.contact.uri) || ruriMatches(this.configuration.contact.pubGruu) || ruriMatches(this.configuration.contact.tempGruu))) {
        this.logger.warn("Request-URI does not point to us.");
        if (message.method !== C.ACK) {
          this.replyStateless(message, { statusCode: 404 });
        }
        return;
      }
      if (message.method === C.INVITE) {
        if (!message.hasHeader("Contact")) {
          this.replyStateless(message, {
            statusCode: 400,
            reasonPhrase: "Missing Contact Header"
          });
          return;
        }
      }
      if (!message.toTag) {
        const transactionId = message.viaBranch;
        if (!this.userAgentServers.has(transactionId)) {
          const mergedRequest = Array.from(this.userAgentServers.values()).some((uas) => uas.transaction.request.fromTag === message.fromTag && uas.transaction.request.callId === message.callId && uas.transaction.request.cseq === message.cseq);
          if (mergedRequest) {
            this.replyStateless(message, { statusCode: 482 });
            return;
          }
        }
      }
      if (message.toTag) {
        this.receiveInsideDialogRequest(message);
      } else {
        this.receiveOutsideDialogRequest(message);
      }
      return;
    }
    /**
     * Once a dialog has been established between two UAs, either of them
     * MAY initiate new transactions as needed within the dialog.  The UA
     * sending the request will take the UAC role for the transaction.  The
     * UA receiving the request will take the UAS role.  Note that these may
     * be different roles than the UAs held during the transaction that
     * established the dialog.
     * https://tools.ietf.org/html/rfc3261#section-12.2
     * @param message - Incoming request message.
     */
    receiveInsideDialogRequest(message) {
      if (message.method === C.NOTIFY) {
        const event = message.parseHeader("Event");
        if (!event || !event.event) {
          this.replyStateless(message, { statusCode: 489 });
          return;
        }
        const subscriberId = message.callId + message.toTag + event.event;
        const subscriber = this.subscribers.get(subscriberId);
        if (subscriber) {
          const uas = new NotifyUserAgentServer(this, message);
          subscriber.onNotify(uas);
          return;
        }
      }
      const dialogId = message.callId + message.toTag + message.fromTag;
      const dialog = this.dialogs.get(dialogId);
      if (dialog) {
        if (message.method === C.OPTIONS) {
          const allowHeader = "Allow: " + AllowedMethods.toString();
          const acceptHeader = "Accept: " + acceptedBodyTypes.toString();
          this.replyStateless(message, {
            statusCode: 200,
            extraHeaders: [allowHeader, acceptHeader]
          });
          return;
        }
        dialog.receiveRequest(message);
        return;
      }
      if (message.method === C.ACK) {
        return;
      }
      this.replyStateless(message, { statusCode: 481 });
      return;
    }
    /**
     * Assuming all of the checks in the previous subsections are passed,
     * the UAS processing becomes method-specific.
     *  https://tools.ietf.org/html/rfc3261#section-8.2.5
     * @param message - Incoming request message.
     */
    receiveOutsideDialogRequest(message) {
      switch (message.method) {
        case C.ACK:
          break;
        case C.BYE:
          this.replyStateless(message, { statusCode: 481 });
          break;
        case C.CANCEL:
          throw new Error(`Unexpected out of dialog request method ${message.method}.`);
          break;
        case C.INFO:
          this.replyStateless(message, { statusCode: 405 });
          break;
        case C.INVITE:
          {
            const uas = new InviteUserAgentServer(this, message);
            this.delegate.onInvite ? this.delegate.onInvite(uas) : uas.reject();
          }
          break;
        case C.MESSAGE:
          {
            const uas = new MessageUserAgentServer(this, message);
            this.delegate.onMessage ? this.delegate.onMessage(uas) : uas.accept();
          }
          break;
        case C.NOTIFY:
          {
            const uas = new NotifyUserAgentServer(this, message);
            this.delegate.onNotify ? this.delegate.onNotify(uas) : uas.reject({ statusCode: 405 });
          }
          break;
        case C.OPTIONS:
          {
            const allowHeader = "Allow: " + AllowedMethods.toString();
            const acceptHeader = "Accept: " + acceptedBodyTypes.toString();
            this.replyStateless(message, {
              statusCode: 200,
              extraHeaders: [allowHeader, acceptHeader]
            });
          }
          break;
        case C.REFER:
          {
            const uas = new ReferUserAgentServer(this, message);
            this.delegate.onRefer ? this.delegate.onRefer(uas) : uas.reject({ statusCode: 405 });
          }
          break;
        case C.REGISTER:
          {
            const uas = new RegisterUserAgentServer(this, message);
            this.delegate.onRegister ? this.delegate.onRegister(uas) : uas.reject({ statusCode: 405 });
          }
          break;
        case C.SUBSCRIBE:
          {
            const uas = new SubscribeUserAgentServer(this, message);
            this.delegate.onSubscribe ? this.delegate.onSubscribe(uas) : uas.reject({ statusCode: 480 });
          }
          break;
        default:
          throw new Error(`Unexpected out of dialog request method ${message.method}.`);
      }
      return;
    }
    /**
     * Responses are first processed by the transport layer and then passed
     * up to the transaction layer.  The transaction layer performs its
     * processing and then passes the response up to the TU.  The majority
     * of response processing in the TU is method specific.  However, there
     * are some general behaviors independent of the method.
     * https://tools.ietf.org/html/rfc3261#section-8.1.3
     * @param message - Incoming response message from transport layer.
     */
    receiveResponseFromTransport(message) {
      if (message.getHeaders("via").length > 1) {
        this.logger.warn("More than one Via header field present in the response, dropping");
        return;
      }
      const userAgentClientId = message.viaBranch + message.method;
      const userAgentClient = this.userAgentClients.get(userAgentClientId);
      if (userAgentClient) {
        userAgentClient.transaction.receiveResponse(message);
      } else {
        this.logger.warn(`Discarding unmatched ${message.statusCode} response to ${message.method} ${userAgentClientId}.`);
      }
    }
  };

  // node_modules/sip.js/lib/platform/web/session-description-handler/media-stream-factory-default.js
  function defaultMediaStreamFactory() {
    return (constraints) => {
      if (!constraints.audio && !constraints.video) {
        return Promise.resolve(new MediaStream());
      }
      if (navigator.mediaDevices === void 0) {
        return Promise.reject(new Error("Media devices not available in insecure contexts."));
      }
      return navigator.mediaDevices.getUserMedia.call(navigator.mediaDevices, constraints);
    };
  }

  // node_modules/sip.js/lib/platform/web/session-description-handler/peer-connection-configuration-default.js
  function defaultPeerConnectionConfiguration() {
    const configuration = {
      bundlePolicy: "balanced",
      certificates: void 0,
      iceCandidatePoolSize: 0,
      iceServers: [{ urls: "stun:stun.l.google.com:19302" }],
      iceTransportPolicy: "all",
      rtcpMuxPolicy: "require"
    };
    return configuration;
  }

  // node_modules/sip.js/lib/platform/web/session-description-handler/session-description-handler.js
  var SessionDescriptionHandler = class _SessionDescriptionHandler {
    /**
     * Constructor
     * @param logger - A logger
     * @param mediaStreamFactory - A factory to provide a MediaStream
     * @param options - Options passed from the SessionDescriptionHandleFactory
     */
    constructor(logger, mediaStreamFactory, sessionDescriptionHandlerConfiguration) {
      logger.debug("SessionDescriptionHandler.constructor");
      this.logger = logger;
      this.mediaStreamFactory = mediaStreamFactory;
      this.sessionDescriptionHandlerConfiguration = sessionDescriptionHandlerConfiguration;
      this._localMediaStream = new MediaStream();
      this._remoteMediaStream = new MediaStream();
      this._peerConnection = new RTCPeerConnection(sessionDescriptionHandlerConfiguration === null || sessionDescriptionHandlerConfiguration === void 0 ? void 0 : sessionDescriptionHandlerConfiguration.peerConnectionConfiguration);
      this.initPeerConnectionEventHandlers();
    }
    /**
     * The local media stream currently being sent.
     *
     * @remarks
     * The local media stream initially has no tracks, so the presence of tracks
     * should not be assumed. Furthermore, tracks may be added or removed if the
     * local media changes - for example, on upgrade from audio only to a video session.
     * At any given time there will be at most one audio track and one video track
     * (it's possible that this restriction may not apply to sub-classes).
     * Use `MediaStream.onaddtrack` or add a listener for the `addtrack` event
     * to detect when a new track becomes available:
     * https://developer.mozilla.org/en-US/docs/Web/API/MediaStream/onaddtrack
     */
    get localMediaStream() {
      return this._localMediaStream;
    }
    /**
     * The remote media stream currently being received.
     *
     * @remarks
     * The remote media stream initially has no tracks, so the presence of tracks
     * should not be assumed. Furthermore, tracks may be added or removed if the
     * remote media changes - for example, on upgrade from audio only to a video session.
     * At any given time there will be at most one audio track and one video track
     * (it's possible that this restriction may not apply to sub-classes).
     * Use `MediaStream.onaddtrack` or add a listener for the `addtrack` event
     * to detect when a new track becomes available:
     * https://developer.mozilla.org/en-US/docs/Web/API/MediaStream/onaddtrack
     */
    get remoteMediaStream() {
      return this._remoteMediaStream;
    }
    /**
     * The data channel. Undefined before it is created.
     */
    get dataChannel() {
      return this._dataChannel;
    }
    /**
     * The peer connection. Undefined if peer connection has closed.
     *
     * @remarks
     * Use the peerConnectionDelegate to get access to the events associated
     * with the RTCPeerConnection. For example...
     *
     * Do NOT do this...
     * ```ts
     * peerConnection.onicecandidate = (event) => {
     *   // do something
     * };
     * ```
     * Instead, do this...
     * ```ts
     * peerConnection.peerConnectionDelegate = {
     *   onicecandidate: (event) => {
     *     // do something
     *   }
     * };
     * ```
     * While access to the underlying `RTCPeerConnection` is provided, note that
     * using methods which modify it may break the operation of this class.
     * In particular, this class depends on exclusive access to the
     * event handler properties. If you need access to the peer connection
     * events, either register for events using `addEventListener()` on
     * the `RTCPeerConnection` or set the `peerConnectionDelegate` on
     * this `SessionDescriptionHandler`.
     */
    get peerConnection() {
      return this._peerConnection;
    }
    /**
     * A delegate which provides access to the peer connection event handlers.
     *
     * @remarks
     * Use the peerConnectionDelegate to get access to the events associated
     * with the RTCPeerConnection. For example...
     *
     * Do NOT do this...
     * ```ts
     * peerConnection.onicecandidate = (event) => {
     *   // do something
     * };
     * ```
     * Instead, do this...
     * ```
     * peerConnection.peerConnectionDelegate = {
     *   onicecandidate: (event) => {
     *     // do something
     *   }
     * };
     * ```
     * Setting the peer connection event handlers directly is not supported
     * and may break this class. As this class depends on exclusive access
     * to them. This delegate is intended to provide access to the
     * RTCPeerConnection events in a fashion which is supported.
     */
    get peerConnectionDelegate() {
      return this._peerConnectionDelegate;
    }
    set peerConnectionDelegate(delegate) {
      this._peerConnectionDelegate = delegate;
    }
    // The addtrack event does not get fired when JavaScript code explicitly adds tracks to the stream (by calling addTrack()).
    // https://developer.mozilla.org/en-US/docs/Web/API/MediaStream/onaddtrack
    static dispatchAddTrackEvent(stream, track) {
      stream.dispatchEvent(new MediaStreamTrackEvent("addtrack", { track }));
    }
    // The removetrack event does not get fired when JavaScript code explicitly removes tracks from the stream (by calling removeTrack()).
    // https://developer.mozilla.org/en-US/docs/Web/API/MediaStream/onremovetrack
    static dispatchRemoveTrackEvent(stream, track) {
      stream.dispatchEvent(new MediaStreamTrackEvent("removetrack", { track }));
    }
    /**
     * Stop tracks and close peer connection.
     */
    close() {
      this.logger.debug("SessionDescriptionHandler.close");
      if (this._peerConnection === void 0) {
        return;
      }
      this._peerConnection.getReceivers().forEach((receiver) => {
        receiver.track && receiver.track.stop();
      });
      this._peerConnection.getSenders().forEach((sender) => {
        sender.track && sender.track.stop();
      });
      if (this._dataChannel) {
        this._dataChannel.close();
      }
      this._peerConnection.close();
      this._peerConnection = void 0;
    }
    /**
     * Helper function to enable/disable media tracks.
     * @param enable - If true enable tracks, otherwise disable tracks.
     */
    enableReceiverTracks(enable) {
      const peerConnection = this.peerConnection;
      if (!peerConnection) {
        throw new Error("Peer connection closed.");
      }
      peerConnection.getReceivers().forEach((receiver) => {
        if (receiver.track) {
          receiver.track.enabled = enable;
        }
      });
    }
    /**
     * Helper function to enable/disable media tracks.
     * @param enable - If true enable tracks, otherwise disable tracks.
     */
    enableSenderTracks(enable) {
      const peerConnection = this.peerConnection;
      if (!peerConnection) {
        throw new Error("Peer connection closed.");
      }
      peerConnection.getSenders().forEach((sender) => {
        if (sender.track) {
          sender.track.enabled = enable;
        }
      });
    }
    /**
     * Creates an offer or answer.
     * @param options - Options bucket.
     * @param modifiers - Modifiers.
     */
    getDescription(options, modifiers) {
      var _a, _b;
      this.logger.debug("SessionDescriptionHandler.getDescription");
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      this.onDataChannel = options === null || options === void 0 ? void 0 : options.onDataChannel;
      const iceRestart = (_a = options === null || options === void 0 ? void 0 : options.offerOptions) === null || _a === void 0 ? void 0 : _a.iceRestart;
      const iceTimeout = (options === null || options === void 0 ? void 0 : options.iceGatheringTimeout) === void 0 ? (_b = this.sessionDescriptionHandlerConfiguration) === null || _b === void 0 ? void 0 : _b.iceGatheringTimeout : options === null || options === void 0 ? void 0 : options.iceGatheringTimeout;
      return this.getLocalMediaStream(options).then(() => this.updateDirection(options)).then(() => this.createDataChannel(options)).then(() => this.createLocalOfferOrAnswer(options)).then((sessionDescription) => this.applyModifiers(sessionDescription, modifiers)).then((sessionDescription) => this.setLocalSessionDescription(sessionDescription)).then(() => this.waitForIceGatheringComplete(iceRestart, iceTimeout)).then(() => this.getLocalSessionDescription()).then((sessionDescription) => {
        return {
          body: sessionDescription.sdp,
          contentType: "application/sdp"
        };
      }).catch((error) => {
        this.logger.error("SessionDescriptionHandler.getDescription failed - " + error);
        throw error;
      });
    }
    /**
     * Returns true if the SessionDescriptionHandler can handle the Content-Type described by a SIP message.
     * @param contentType - The content type that is in the SIP Message.
     */
    hasDescription(contentType) {
      this.logger.debug("SessionDescriptionHandler.hasDescription");
      return contentType === "application/sdp";
    }
    /**
     * Called when ICE gathering completes and resolves any waiting promise.
     * @remarks
     * May be called prior to ICE gathering actually completing to allow the
     * session descirption handler proceed with whatever candidates have been
     * gathered up to this point in time. Use this to stop waiting on ICE to
     * complete if you are implementing your own ICE gathering completion strategy.
     */
    iceGatheringComplete() {
      this.logger.debug("SessionDescriptionHandler.iceGatheringComplete");
      if (this.iceGatheringCompleteTimeoutId !== void 0) {
        this.logger.debug("SessionDescriptionHandler.iceGatheringComplete - clearing timeout");
        clearTimeout(this.iceGatheringCompleteTimeoutId);
        this.iceGatheringCompleteTimeoutId = void 0;
      }
      if (this.iceGatheringCompletePromise !== void 0) {
        this.logger.debug("SessionDescriptionHandler.iceGatheringComplete - resolving promise");
        this.iceGatheringCompleteResolve && this.iceGatheringCompleteResolve();
        this.iceGatheringCompletePromise = void 0;
        this.iceGatheringCompleteResolve = void 0;
        this.iceGatheringCompleteReject = void 0;
      }
    }
    /**
     * Send DTMF via RTP (RFC 4733).
     * Returns true if DTMF send is successful, false otherwise.
     * @param tones - A string containing DTMF digits.
     * @param options - Options object to be used by sendDtmf.
     */
    sendDtmf(tones, options) {
      this.logger.debug("SessionDescriptionHandler.sendDtmf");
      if (this._peerConnection === void 0) {
        this.logger.error("SessionDescriptionHandler.sendDtmf failed - peer connection closed");
        return false;
      }
      const senders = this._peerConnection.getSenders();
      if (senders.length === 0) {
        this.logger.error("SessionDescriptionHandler.sendDtmf failed - no senders");
        return false;
      }
      const dtmfSender = senders[0].dtmf;
      if (!dtmfSender) {
        this.logger.error("SessionDescriptionHandler.sendDtmf failed - no DTMF sender");
        return false;
      }
      const duration = options === null || options === void 0 ? void 0 : options.duration;
      const interToneGap = options === null || options === void 0 ? void 0 : options.interToneGap;
      try {
        dtmfSender.insertDTMF(tones, duration, interToneGap);
      } catch (e) {
        this.logger.error(e.toString());
        return false;
      }
      this.logger.log("SessionDescriptionHandler.sendDtmf sent via RTP: " + tones.toString());
      return true;
    }
    /**
     * Sets an offer or answer.
     * @param sdp - The session description.
     * @param options - Options bucket.
     * @param modifiers - Modifiers.
     */
    setDescription(sdp, options, modifiers) {
      this.logger.debug("SessionDescriptionHandler.setDescription");
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      this.onDataChannel = options === null || options === void 0 ? void 0 : options.onDataChannel;
      const type = this._peerConnection.signalingState === "have-local-offer" ? "answer" : "offer";
      return this.getLocalMediaStream(options).then(() => this.applyModifiers({ sdp, type }, modifiers)).then((sessionDescription) => this.setRemoteSessionDescription(sessionDescription)).catch((error) => {
        this.logger.error("SessionDescriptionHandler.setDescription failed - " + error);
        throw error;
      });
    }
    /**
     * Applies modifiers to SDP prior to setting the local or remote description.
     * @param sdp - SDP to modify.
     * @param modifiers - Modifiers to apply.
     */
    applyModifiers(sdp, modifiers) {
      this.logger.debug("SessionDescriptionHandler.applyModifiers");
      if (!modifiers || modifiers.length === 0) {
        return Promise.resolve(sdp);
      }
      return modifiers.reduce((cur, next) => cur.then(next), Promise.resolve(sdp)).then((modified) => {
        this.logger.debug("SessionDescriptionHandler.applyModifiers - modified sdp");
        if (!modified.sdp || !modified.type) {
          throw new Error("Invalid SDP.");
        }
        return { sdp: modified.sdp, type: modified.type };
      });
    }
    /**
     * Create a data channel.
     * @remarks
     * Only creates a data channel if SessionDescriptionHandlerOptions.dataChannel is true.
     * Only creates a data channel if creating a local offer.
     * Only if one does not already exist.
     * @param options - Session description handler options.
     */
    createDataChannel(options) {
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      if ((options === null || options === void 0 ? void 0 : options.dataChannel) !== true) {
        return Promise.resolve();
      }
      if (this._dataChannel) {
        return Promise.resolve();
      }
      switch (this._peerConnection.signalingState) {
        case "stable":
          this.logger.debug("SessionDescriptionHandler.createDataChannel - creating data channel");
          try {
            this._dataChannel = this._peerConnection.createDataChannel((options === null || options === void 0 ? void 0 : options.dataChannelLabel) || "", options === null || options === void 0 ? void 0 : options.dataChannelOptions);
            if (this.onDataChannel) {
              this.onDataChannel(this._dataChannel);
            }
            return Promise.resolve();
          } catch (error) {
            return Promise.reject(error);
          }
        case "have-remote-offer":
          return Promise.resolve();
        case "have-local-offer":
        case "have-local-pranswer":
        case "have-remote-pranswer":
        case "closed":
        default:
          return Promise.reject(new Error("Invalid signaling state " + this._peerConnection.signalingState));
      }
    }
    /**
     * Depending on current signaling state, create a local offer or answer.
     * @param options - Session description handler options.
     */
    createLocalOfferOrAnswer(options) {
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      switch (this._peerConnection.signalingState) {
        case "stable":
          this.logger.debug("SessionDescriptionHandler.createLocalOfferOrAnswer - creating SDP offer");
          return this._peerConnection.createOffer(options === null || options === void 0 ? void 0 : options.offerOptions);
        case "have-remote-offer":
          this.logger.debug("SessionDescriptionHandler.createLocalOfferOrAnswer - creating SDP answer");
          return this._peerConnection.createAnswer(options === null || options === void 0 ? void 0 : options.answerOptions);
        case "have-local-offer":
        case "have-local-pranswer":
        case "have-remote-pranswer":
        case "closed":
        default:
          return Promise.reject(new Error("Invalid signaling state " + this._peerConnection.signalingState));
      }
    }
    /**
     * Get a media stream from the media stream factory and set the local media stream.
     * @param options - Session description handler options.
     */
    getLocalMediaStream(options) {
      this.logger.debug("SessionDescriptionHandler.getLocalMediaStream");
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      let constraints = Object.assign({}, options === null || options === void 0 ? void 0 : options.constraints);
      if (this.localMediaStreamConstraints) {
        constraints.audio = constraints.audio || this.localMediaStreamConstraints.audio;
        constraints.video = constraints.video || this.localMediaStreamConstraints.video;
        if (JSON.stringify(this.localMediaStreamConstraints.audio) === JSON.stringify(constraints.audio) && JSON.stringify(this.localMediaStreamConstraints.video) === JSON.stringify(constraints.video)) {
          return Promise.resolve();
        }
      } else {
        if (constraints.audio === void 0 && constraints.video === void 0) {
          constraints = { audio: true };
        }
      }
      this.localMediaStreamConstraints = constraints;
      return this.mediaStreamFactory(constraints, this, options).then((mediaStream) => this.setLocalMediaStream(mediaStream));
    }
    /**
     * Sets the peer connection's sender tracks and local media stream tracks.
     *
     * @remarks
     * Only the first audio and video tracks of the provided MediaStream are utilized.
     * Adds tracks if audio and/or video tracks are not already present, otherwise replaces tracks.
     *
     * @param stream - Media stream containing tracks to be utilized.
     */
    setLocalMediaStream(stream) {
      this.logger.debug("SessionDescriptionHandler.setLocalMediaStream");
      if (!this._peerConnection) {
        throw new Error("Peer connection undefined.");
      }
      const pc = this._peerConnection;
      const localStream = this._localMediaStream;
      const trackUpdates = [];
      const updateTrack = (newTrack) => {
        const kind = newTrack.kind;
        if (kind !== "audio" && kind !== "video") {
          throw new Error(`Unknown new track kind ${kind}.`);
        }
        const sender = pc.getSenders().find((sender2) => sender2.track && sender2.track.kind === kind);
        if (sender) {
          trackUpdates.push(new Promise((resolve) => {
            this.logger.debug(`SessionDescriptionHandler.setLocalMediaStream - replacing sender ${kind} track`);
            resolve();
          }).then(() => sender.replaceTrack(newTrack).then(() => {
            const oldTrack = localStream.getTracks().find((localTrack) => localTrack.kind === kind);
            if (oldTrack) {
              oldTrack.stop();
              localStream.removeTrack(oldTrack);
              _SessionDescriptionHandler.dispatchRemoveTrackEvent(localStream, oldTrack);
            }
            localStream.addTrack(newTrack);
            _SessionDescriptionHandler.dispatchAddTrackEvent(localStream, newTrack);
          }).catch((error) => {
            this.logger.error(`SessionDescriptionHandler.setLocalMediaStream - failed to replace sender ${kind} track`);
            throw error;
          })));
        } else {
          trackUpdates.push(new Promise((resolve) => {
            this.logger.debug(`SessionDescriptionHandler.setLocalMediaStream - adding sender ${kind} track`);
            resolve();
          }).then(() => {
            try {
              pc.addTrack(newTrack, localStream);
            } catch (error) {
              this.logger.error(`SessionDescriptionHandler.setLocalMediaStream - failed to add sender ${kind} track`);
              throw error;
            }
            localStream.addTrack(newTrack);
            _SessionDescriptionHandler.dispatchAddTrackEvent(localStream, newTrack);
          }));
        }
      };
      const audioTracks = stream.getAudioTracks();
      if (audioTracks.length) {
        updateTrack(audioTracks[0]);
      }
      const videoTracks = stream.getVideoTracks();
      if (videoTracks.length) {
        updateTrack(videoTracks[0]);
      }
      return trackUpdates.reduce((p, x) => p.then(() => x), Promise.resolve());
    }
    /**
     * Gets the peer connection's local session description.
     */
    getLocalSessionDescription() {
      this.logger.debug("SessionDescriptionHandler.getLocalSessionDescription");
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      const sdp = this._peerConnection.localDescription;
      if (!sdp) {
        return Promise.reject(new Error("Failed to get local session description"));
      }
      return Promise.resolve(sdp);
    }
    /**
     * Sets the peer connection's local session description.
     * @param sessionDescription - sessionDescription The session description.
     */
    setLocalSessionDescription(sessionDescription) {
      this.logger.debug("SessionDescriptionHandler.setLocalSessionDescription");
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      return this._peerConnection.setLocalDescription(sessionDescription);
    }
    /**
     * Sets the peer connection's remote session description.
     * @param sessionDescription - The session description.
     */
    setRemoteSessionDescription(sessionDescription) {
      this.logger.debug("SessionDescriptionHandler.setRemoteSessionDescription");
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      const sdp = sessionDescription.sdp;
      let type;
      switch (this._peerConnection.signalingState) {
        case "stable":
          type = "offer";
          break;
        case "have-local-offer":
          type = "answer";
          break;
        case "have-local-pranswer":
        case "have-remote-offer":
        case "have-remote-pranswer":
        case "closed":
        default:
          return Promise.reject(new Error("Invalid signaling state " + this._peerConnection.signalingState));
      }
      if (!sdp) {
        this.logger.error("SessionDescriptionHandler.setRemoteSessionDescription failed - cannot set null sdp");
        return Promise.reject(new Error("SDP is undefined"));
      }
      return this._peerConnection.setRemoteDescription({ sdp, type });
    }
    /**
     * Sets a remote media stream track.
     *
     * @remarks
     * Adds tracks if audio and/or video tracks are not already present, otherwise replaces tracks.
     *
     * @param track - Media stream track to be utilized.
     */
    setRemoteTrack(track) {
      this.logger.debug("SessionDescriptionHandler.setRemoteTrack");
      const remoteStream = this._remoteMediaStream;
      if (remoteStream.getTrackById(track.id)) {
        this.logger.debug(`SessionDescriptionHandler.setRemoteTrack - have remote ${track.kind} track`);
      } else if (track.kind === "audio") {
        this.logger.debug(`SessionDescriptionHandler.setRemoteTrack - adding remote ${track.kind} track`);
        remoteStream.getAudioTracks().forEach((track2) => {
          track2.stop();
          remoteStream.removeTrack(track2);
          _SessionDescriptionHandler.dispatchRemoveTrackEvent(remoteStream, track2);
        });
        remoteStream.addTrack(track);
        _SessionDescriptionHandler.dispatchAddTrackEvent(remoteStream, track);
      } else if (track.kind === "video") {
        this.logger.debug(`SessionDescriptionHandler.setRemoteTrack - adding remote ${track.kind} track`);
        remoteStream.getVideoTracks().forEach((track2) => {
          track2.stop();
          remoteStream.removeTrack(track2);
          _SessionDescriptionHandler.dispatchRemoveTrackEvent(remoteStream, track2);
        });
        remoteStream.addTrack(track);
        _SessionDescriptionHandler.dispatchAddTrackEvent(remoteStream, track);
      }
    }
    /**
     * Depending on the current signaling state and the session hold state, update transceiver direction.
     * @param options - Session description handler options.
     */
    updateDirection(options) {
      if (this._peerConnection === void 0) {
        return Promise.reject(new Error("Peer connection closed."));
      }
      switch (this._peerConnection.signalingState) {
        case "stable":
          this.logger.debug("SessionDescriptionHandler.updateDirection - setting offer direction");
          {
            const directionToOffer = (currentDirection) => {
              switch (currentDirection) {
                case "inactive":
                  return (options === null || options === void 0 ? void 0 : options.hold) ? "inactive" : "recvonly";
                case "recvonly":
                  return (options === null || options === void 0 ? void 0 : options.hold) ? "inactive" : "recvonly";
                case "sendonly":
                  return (options === null || options === void 0 ? void 0 : options.hold) ? "sendonly" : "sendrecv";
                case "sendrecv":
                  return (options === null || options === void 0 ? void 0 : options.hold) ? "sendonly" : "sendrecv";
                case "stopped":
                  return "stopped";
                default:
                  throw new Error("Should never happen");
              }
            };
            this._peerConnection.getTransceivers().forEach((transceiver) => {
              if (transceiver.direction) {
                const offerDirection = directionToOffer(transceiver.direction);
                if (transceiver.direction !== offerDirection) {
                  transceiver.direction = offerDirection;
                }
              }
            });
          }
          break;
        case "have-remote-offer":
          this.logger.debug("SessionDescriptionHandler.updateDirection - setting answer direction");
          {
            const offeredDirection = (() => {
              const description = this._peerConnection.remoteDescription;
              if (!description) {
                throw new Error("Failed to read remote offer");
              }
              const searchResult = /a=sendrecv\r\n|a=sendonly\r\n|a=recvonly\r\n|a=inactive\r\n/.exec(description.sdp);
              if (searchResult) {
                switch (searchResult[0]) {
                  case "a=inactive\r\n":
                    return "inactive";
                  case "a=recvonly\r\n":
                    return "recvonly";
                  case "a=sendonly\r\n":
                    return "sendonly";
                  case "a=sendrecv\r\n":
                    return "sendrecv";
                  default:
                    throw new Error("Should never happen");
                }
              }
              return "sendrecv";
            })();
            const answerDirection = (() => {
              switch (offeredDirection) {
                case "inactive":
                  return "inactive";
                case "recvonly":
                  return "sendonly";
                case "sendonly":
                  return (options === null || options === void 0 ? void 0 : options.hold) ? "inactive" : "recvonly";
                case "sendrecv":
                  return (options === null || options === void 0 ? void 0 : options.hold) ? "sendonly" : "sendrecv";
                default:
                  throw new Error("Should never happen");
              }
            })();
            this._peerConnection.getTransceivers().forEach((transceiver) => {
              if (transceiver.direction) {
                if (transceiver.direction !== "stopped" && transceiver.direction !== answerDirection) {
                  transceiver.direction = answerDirection;
                }
              }
            });
          }
          break;
        case "have-local-offer":
        case "have-local-pranswer":
        case "have-remote-pranswer":
        case "closed":
        default:
          return Promise.reject(new Error("Invalid signaling state " + this._peerConnection.signalingState));
      }
      return Promise.resolve();
    }
    /**
     * Wait for ICE gathering to complete.
     * @param restart - If true, waits if current state is "complete" (waits for transition to "complete").
     * @param timeout - Milliseconds after which waiting times out. No timeout if 0.
     */
    waitForIceGatheringComplete(restart = false, timeout = 0) {
      this.logger.debug("SessionDescriptionHandler.waitForIceGatheringToComplete");
      if (this._peerConnection === void 0) {
        return Promise.reject("Peer connection closed.");
      }
      if (!restart && this._peerConnection.iceGatheringState === "complete") {
        this.logger.debug("SessionDescriptionHandler.waitForIceGatheringToComplete - already complete");
        return Promise.resolve();
      }
      if (this.iceGatheringCompletePromise !== void 0) {
        this.logger.debug("SessionDescriptionHandler.waitForIceGatheringToComplete - rejecting prior waiting promise");
        this.iceGatheringCompleteReject && this.iceGatheringCompleteReject(new Error("Promise superseded."));
        this.iceGatheringCompletePromise = void 0;
        this.iceGatheringCompleteResolve = void 0;
        this.iceGatheringCompleteReject = void 0;
      }
      this.iceGatheringCompletePromise = new Promise((resolve, reject) => {
        this.iceGatheringCompleteResolve = resolve;
        this.iceGatheringCompleteReject = reject;
        if (timeout > 0) {
          this.logger.debug("SessionDescriptionHandler.waitForIceGatheringToComplete - timeout in " + timeout);
          this.iceGatheringCompleteTimeoutId = setTimeout(() => {
            this.logger.debug("SessionDescriptionHandler.waitForIceGatheringToComplete - timeout");
            this.iceGatheringComplete();
          }, timeout);
        }
      });
      return this.iceGatheringCompletePromise;
    }
    /**
     * Initializes the peer connection event handlers
     */
    initPeerConnectionEventHandlers() {
      this.logger.debug("SessionDescriptionHandler.initPeerConnectionEventHandlers");
      if (!this._peerConnection)
        throw new Error("Peer connection undefined.");
      const peerConnection = this._peerConnection;
      peerConnection.onconnectionstatechange = (event) => {
        var _a;
        const newState = peerConnection.connectionState;
        this.logger.debug(`SessionDescriptionHandler.onconnectionstatechange ${newState}`);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.onconnectionstatechange) {
          this._peerConnectionDelegate.onconnectionstatechange(event);
        }
      };
      peerConnection.ondatachannel = (event) => {
        var _a;
        this.logger.debug(`SessionDescriptionHandler.ondatachannel`);
        this._dataChannel = event.channel;
        if (this.onDataChannel) {
          this.onDataChannel(this._dataChannel);
        }
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.ondatachannel) {
          this._peerConnectionDelegate.ondatachannel(event);
        }
      };
      peerConnection.onicecandidate = (event) => {
        var _a;
        this.logger.debug(`SessionDescriptionHandler.onicecandidate`);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.onicecandidate) {
          this._peerConnectionDelegate.onicecandidate(event);
        }
      };
      peerConnection.onicecandidateerror = (event) => {
        var _a;
        this.logger.debug(`SessionDescriptionHandler.onicecandidateerror`);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.onicecandidateerror) {
          this._peerConnectionDelegate.onicecandidateerror(event);
        }
      };
      peerConnection.oniceconnectionstatechange = (event) => {
        var _a;
        const newState = peerConnection.iceConnectionState;
        this.logger.debug(`SessionDescriptionHandler.oniceconnectionstatechange ${newState}`);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.oniceconnectionstatechange) {
          this._peerConnectionDelegate.oniceconnectionstatechange(event);
        }
      };
      peerConnection.onicegatheringstatechange = (event) => {
        var _a;
        const newState = peerConnection.iceGatheringState;
        this.logger.debug(`SessionDescriptionHandler.onicegatheringstatechange ${newState}`);
        if (newState === "complete") {
          this.iceGatheringComplete();
        }
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.onicegatheringstatechange) {
          this._peerConnectionDelegate.onicegatheringstatechange(event);
        }
      };
      peerConnection.onnegotiationneeded = (event) => {
        var _a;
        this.logger.debug(`SessionDescriptionHandler.onnegotiationneeded`);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.onnegotiationneeded) {
          this._peerConnectionDelegate.onnegotiationneeded(event);
        }
      };
      peerConnection.onsignalingstatechange = (event) => {
        var _a;
        const newState = peerConnection.signalingState;
        this.logger.debug(`SessionDescriptionHandler.onsignalingstatechange ${newState}`);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.onsignalingstatechange) {
          this._peerConnectionDelegate.onsignalingstatechange(event);
        }
      };
      peerConnection.ontrack = (event) => {
        var _a;
        const kind = event.track.kind;
        const enabled = event.track.enabled ? "enabled" : "disabled";
        this.logger.debug(`SessionDescriptionHandler.ontrack ${kind} ${enabled}`);
        this.setRemoteTrack(event.track);
        if ((_a = this._peerConnectionDelegate) === null || _a === void 0 ? void 0 : _a.ontrack) {
          this._peerConnectionDelegate.ontrack(event);
        }
      };
    }
  };

  // node_modules/sip.js/lib/platform/web/session-description-handler/session-description-handler-factory-default.js
  function defaultSessionDescriptionHandlerFactory(mediaStreamFactory) {
    return (session, options) => {
      if (mediaStreamFactory === void 0) {
        mediaStreamFactory = defaultMediaStreamFactory();
      }
      const iceGatheringTimeout = (options === null || options === void 0 ? void 0 : options.iceGatheringTimeout) !== void 0 ? options === null || options === void 0 ? void 0 : options.iceGatheringTimeout : 5e3;
      const sessionDescriptionHandlerConfiguration = {
        iceGatheringTimeout,
        peerConnectionConfiguration: Object.assign(Object.assign({}, defaultPeerConnectionConfiguration()), options === null || options === void 0 ? void 0 : options.peerConnectionConfiguration)
      };
      const logger = session.userAgent.getLogger("sip.SessionDescriptionHandler");
      return new SessionDescriptionHandler(logger, mediaStreamFactory, sessionDescriptionHandlerConfiguration);
    };
  }

  // node_modules/sip.js/lib/platform/web/transport/transport.js
  var Transport = class _Transport {
    constructor(logger, options) {
      this._state = TransportState.Disconnected;
      this.transitioningState = false;
      this._stateEventEmitter = new EmitterImpl();
      this.logger = logger;
      if (options) {
        const optionsDeprecated = options;
        const wsServersDeprecated = optionsDeprecated === null || optionsDeprecated === void 0 ? void 0 : optionsDeprecated.wsServers;
        const maxReconnectionAttemptsDeprecated = optionsDeprecated === null || optionsDeprecated === void 0 ? void 0 : optionsDeprecated.maxReconnectionAttempts;
        if (wsServersDeprecated !== void 0) {
          const deprecatedMessage = `The transport option "wsServers" as has apparently been specified and has been deprecated. It will no longer be available starting with SIP.js release 0.16.0. Please update accordingly.`;
          this.logger.warn(deprecatedMessage);
        }
        if (maxReconnectionAttemptsDeprecated !== void 0) {
          const deprecatedMessage = `The transport option "maxReconnectionAttempts" as has apparently been specified and has been deprecated. It will no longer be available starting with SIP.js release 0.16.0. Please update accordingly.`;
          this.logger.warn(deprecatedMessage);
        }
        if (wsServersDeprecated && !options.server) {
          if (typeof wsServersDeprecated === "string") {
            options.server = wsServersDeprecated;
          }
          if (wsServersDeprecated instanceof Array) {
            options.server = wsServersDeprecated[0];
          }
        }
      }
      this.configuration = Object.assign(Object.assign({}, _Transport.defaultOptions), options);
      const url = this.configuration.server;
      const parsed = Grammar.parse(url, "absoluteURI");
      if (parsed === -1) {
        this.logger.error(`Invalid WebSocket Server URL "${url}"`);
        throw new Error("Invalid WebSocket Server URL");
      }
      if (!["wss", "ws", "udp"].includes(parsed.scheme)) {
        this.logger.error(`Invalid scheme in WebSocket Server URL "${url}"`);
        throw new Error("Invalid scheme in WebSocket Server URL");
      }
      this._protocol = parsed.scheme.toUpperCase();
    }
    dispose() {
      return this.disconnect();
    }
    /**
     * The protocol.
     *
     * @remarks
     * Formatted as defined for the Via header sent-protocol transport.
     * https://tools.ietf.org/html/rfc3261#section-20.42
     */
    get protocol() {
      return this._protocol;
    }
    /**
     * The URL of the WebSocket Server.
     */
    get server() {
      return this.configuration.server;
    }
    /**
     * Transport state.
     */
    get state() {
      return this._state;
    }
    /**
     * Transport state change emitter.
     */
    get stateChange() {
      return this._stateEventEmitter;
    }
    /**
     * The WebSocket.
     */
    get ws() {
      return this._ws;
    }
    /**
     * Connect to network.
     * Resolves once connected. Otherwise rejects with an Error.
     */
    connect() {
      return this._connect();
    }
    /**
     * Disconnect from network.
     * Resolves once disconnected. Otherwise rejects with an Error.
     */
    disconnect() {
      return this._disconnect();
    }
    /**
     * Returns true if the `state` equals "Connected".
     * @remarks
     * This is equivalent to `state === TransportState.Connected`.
     */
    isConnected() {
      return this.state === TransportState.Connected;
    }
    /**
     * Sends a message.
     * Resolves once message is sent. Otherwise rejects with an Error.
     * @param message - Message to send.
     */
    send(message) {
      return this._send(message);
    }
    _connect() {
      this.logger.log(`Connecting ${this.server}`);
      switch (this.state) {
        case TransportState.Connecting:
          if (this.transitioningState) {
            return Promise.reject(this.transitionLoopDetectedError(TransportState.Connecting));
          }
          if (!this.connectPromise) {
            throw new Error("Connect promise must be defined.");
          }
          return this.connectPromise;
        // Already connecting
        case TransportState.Connected:
          if (this.transitioningState) {
            return Promise.reject(this.transitionLoopDetectedError(TransportState.Connecting));
          }
          if (this.connectPromise) {
            throw new Error("Connect promise must not be defined.");
          }
          return Promise.resolve();
        // Already connected
        case TransportState.Disconnecting:
          if (this.connectPromise) {
            throw new Error("Connect promise must not be defined.");
          }
          try {
            this.transitionState(TransportState.Connecting);
          } catch (e) {
            if (e instanceof StateTransitionError) {
              return Promise.reject(e);
            }
            throw e;
          }
          break;
        case TransportState.Disconnected:
          if (this.connectPromise) {
            throw new Error("Connect promise must not be defined.");
          }
          try {
            this.transitionState(TransportState.Connecting);
          } catch (e) {
            if (e instanceof StateTransitionError) {
              return Promise.reject(e);
            }
            throw e;
          }
          break;
        default:
          throw new Error("Unknown state");
      }
      let ws;
      try {
        ws = new WebSocket(this.server, "sip");
        ws.binaryType = "arraybuffer";
        ws.addEventListener("close", (ev) => this.onWebSocketClose(ev, ws));
        ws.addEventListener("error", (ev) => this.onWebSocketError(ev, ws));
        ws.addEventListener("open", (ev) => this.onWebSocketOpen(ev, ws));
        ws.addEventListener("message", (ev) => this.onWebSocketMessage(ev, ws));
        this._ws = ws;
      } catch (error) {
        this._ws = void 0;
        this.logger.error("WebSocket construction failed.");
        this.logger.error(error.toString());
        return new Promise((resolve, reject) => {
          this.connectResolve = resolve;
          this.connectReject = reject;
          this.transitionState(TransportState.Disconnected, error);
        });
      }
      this.connectPromise = new Promise((resolve, reject) => {
        this.connectResolve = resolve;
        this.connectReject = reject;
        this.connectTimeout = setTimeout(() => {
          this.logger.warn("Connect timed out. Exceeded time set in configuration.connectionTimeout: " + this.configuration.connectionTimeout + "s.");
          ws.close(1e3);
        }, this.configuration.connectionTimeout * 1e3);
      });
      return this.connectPromise;
    }
    _disconnect() {
      this.logger.log(`Disconnecting ${this.server}`);
      switch (this.state) {
        case TransportState.Connecting:
          if (this.disconnectPromise) {
            throw new Error("Disconnect promise must not be defined.");
          }
          try {
            this.transitionState(TransportState.Disconnecting);
          } catch (e) {
            if (e instanceof StateTransitionError) {
              return Promise.reject(e);
            }
            throw e;
          }
          break;
        case TransportState.Connected:
          if (this.disconnectPromise) {
            throw new Error("Disconnect promise must not be defined.");
          }
          try {
            this.transitionState(TransportState.Disconnecting);
          } catch (e) {
            if (e instanceof StateTransitionError) {
              return Promise.reject(e);
            }
            throw e;
          }
          break;
        case TransportState.Disconnecting:
          if (this.transitioningState) {
            return Promise.reject(this.transitionLoopDetectedError(TransportState.Disconnecting));
          }
          if (!this.disconnectPromise) {
            throw new Error("Disconnect promise must be defined.");
          }
          return this.disconnectPromise;
        // Already disconnecting
        case TransportState.Disconnected:
          if (this.transitioningState) {
            return Promise.reject(this.transitionLoopDetectedError(TransportState.Disconnecting));
          }
          if (this.disconnectPromise) {
            throw new Error("Disconnect promise must not be defined.");
          }
          return Promise.resolve();
        // Already disconnected
        default:
          throw new Error("Unknown state");
      }
      if (!this._ws) {
        throw new Error("WebSocket must be defined.");
      }
      const ws = this._ws;
      this.disconnectPromise = new Promise((resolve, reject) => {
        this.disconnectResolve = resolve;
        this.disconnectReject = reject;
        try {
          ws.close(1e3);
        } catch (error) {
          this.logger.error("WebSocket close failed.");
          this.logger.error(error.toString());
          throw error;
        }
      });
      return this.disconnectPromise;
    }
    _send(message) {
      if (this.configuration.traceSip === true) {
        this.logger.log("Sending WebSocket message:\n\n" + message + "\n");
      }
      if (this._state !== TransportState.Connected) {
        return Promise.reject(new Error("Not connected."));
      }
      if (!this._ws) {
        throw new Error("WebSocket undefined.");
      }
      try {
        this._ws.send(message);
      } catch (error) {
        if (error instanceof Error) {
          return Promise.reject(error);
        }
        return Promise.reject(new Error("WebSocket send failed."));
      }
      return Promise.resolve();
    }
    /**
     * WebSocket "onclose" event handler.
     * @param ev - Event.
     */
    onWebSocketClose(ev, ws) {
      if (ws !== this._ws) {
        return;
      }
      const message = `WebSocket closed ${this.server} (code: ${ev.code})`;
      const error = !this.disconnectPromise ? new Error(message) : void 0;
      if (error) {
        this.logger.warn("WebSocket closed unexpectedly");
      }
      this.logger.log(message);
      this._ws = void 0;
      this.transitionState(TransportState.Disconnected, error);
    }
    /**
     * WebSocket "onerror" event handler.
     * @param ev - Event.
     */
    onWebSocketError(ev, ws) {
      if (ws !== this._ws) {
        return;
      }
      this.logger.error("WebSocket error occurred.");
    }
    /**
     * WebSocket "onmessage" event handler.
     * @param ev - Event.
     */
    onWebSocketMessage(ev, ws) {
      if (ws !== this._ws) {
        return;
      }
      const data = ev.data;
      let finishedData;
      if (/^(\r\n)+$/.test(data)) {
        this.clearKeepAliveTimeout();
        if (this.configuration.traceSip === true) {
          this.logger.log("Received WebSocket message with CRLF Keep Alive response");
        }
        return;
      }
      if (!data) {
        this.logger.warn("Received empty message, discarding...");
        return;
      }
      if (typeof data !== "string") {
        try {
          finishedData = new TextDecoder().decode(new Uint8Array(data));
        } catch (err) {
          this.logger.error(err.toString());
          this.logger.error("Received WebSocket binary message failed to be converted into string, message discarded");
          return;
        }
        if (this.configuration.traceSip === true) {
          this.logger.log("Received WebSocket binary message:\n\n" + finishedData + "\n");
        }
      } else {
        finishedData = data;
        if (this.configuration.traceSip === true) {
          this.logger.log("Received WebSocket text message:\n\n" + finishedData + "\n");
        }
      }
      if (this.state !== TransportState.Connected) {
        this.logger.warn("Received message while not connected, discarding...");
        return;
      }
      if (this.onMessage) {
        try {
          this.onMessage(finishedData);
        } catch (e) {
          this.logger.error(e.toString());
          this.logger.error("Exception thrown by onMessage callback");
          throw e;
        }
      }
    }
    /**
     * WebSocket "onopen" event handler.
     * @param ev - Event.
     */
    onWebSocketOpen(ev, ws) {
      if (ws !== this._ws) {
        return;
      }
      if (this._state === TransportState.Connecting) {
        this.logger.log(`WebSocket opened ${this.server}`);
        this.transitionState(TransportState.Connected);
      }
    }
    /**
     * Helper function to generate an Error.
     * @param state - State transitioning to.
     */
    transitionLoopDetectedError(state) {
      let message = `A state transition loop has been detected.`;
      message += ` An attempt to transition from ${this._state} to ${state} before the prior transition completed.`;
      message += ` Perhaps you are synchronously calling connect() or disconnect() from a callback or state change handler?`;
      this.logger.error(message);
      return new StateTransitionError("Loop detected.");
    }
    /**
     * Transition transport state.
     * @internal
     */
    transitionState(newState, error) {
      const invalidTransition = () => {
        throw new Error(`Invalid state transition from ${this._state} to ${newState}`);
      };
      if (this.transitioningState) {
        throw this.transitionLoopDetectedError(newState);
      }
      this.transitioningState = true;
      switch (this._state) {
        case TransportState.Connecting:
          if (newState !== TransportState.Connected && newState !== TransportState.Disconnecting && newState !== TransportState.Disconnected) {
            invalidTransition();
          }
          break;
        case TransportState.Connected:
          if (newState !== TransportState.Disconnecting && newState !== TransportState.Disconnected) {
            invalidTransition();
          }
          break;
        case TransportState.Disconnecting:
          if (newState !== TransportState.Connecting && newState !== TransportState.Disconnected) {
            invalidTransition();
          }
          break;
        case TransportState.Disconnected:
          if (newState !== TransportState.Connecting) {
            invalidTransition();
          }
          break;
        default:
          throw new Error("Unknown state.");
      }
      const oldState = this._state;
      this._state = newState;
      const connectResolve = this.connectResolve;
      const connectReject = this.connectReject;
      if (oldState === TransportState.Connecting) {
        this.connectPromise = void 0;
        this.connectResolve = void 0;
        this.connectReject = void 0;
      }
      const disconnectResolve = this.disconnectResolve;
      const disconnectReject = this.disconnectReject;
      if (oldState === TransportState.Disconnecting) {
        this.disconnectPromise = void 0;
        this.disconnectResolve = void 0;
        this.disconnectReject = void 0;
      }
      if (this.connectTimeout) {
        clearTimeout(this.connectTimeout);
        this.connectTimeout = void 0;
      }
      this.logger.log(`Transitioned from ${oldState} to ${this._state}`);
      this._stateEventEmitter.emit(this._state);
      if (newState === TransportState.Connected) {
        this.startSendingKeepAlives();
        if (this.onConnect) {
          try {
            this.onConnect();
          } catch (e) {
            this.logger.error(e.toString());
            this.logger.error("Exception thrown by onConnect callback");
            throw e;
          }
        }
      }
      if (oldState === TransportState.Connected) {
        this.stopSendingKeepAlives();
        if (this.onDisconnect) {
          try {
            if (error) {
              this.onDisconnect(error);
            } else {
              this.onDisconnect();
            }
          } catch (e) {
            this.logger.error(e.toString());
            this.logger.error("Exception thrown by onDisconnect callback");
            throw e;
          }
        }
      }
      if (oldState === TransportState.Connecting) {
        if (!connectResolve) {
          throw new Error("Connect resolve undefined.");
        }
        if (!connectReject) {
          throw new Error("Connect reject undefined.");
        }
        newState === TransportState.Connected ? connectResolve() : connectReject(error || new Error("Connect aborted."));
      }
      if (oldState === TransportState.Disconnecting) {
        if (!disconnectResolve) {
          throw new Error("Disconnect resolve undefined.");
        }
        if (!disconnectReject) {
          throw new Error("Disconnect reject undefined.");
        }
        newState === TransportState.Disconnected ? disconnectResolve() : disconnectReject(error || new Error("Disconnect aborted."));
      }
      this.transitioningState = false;
    }
    // TODO: Review "KeepAlive Stuff".
    // It is not clear if it works and there are no tests for it.
    // It was blindly lifted the keep alive code unchanged from earlier transport code.
    //
    // From the RFC...
    //
    // SIP WebSocket Clients and Servers may keep their WebSocket
    // connections open by sending periodic WebSocket "Ping" frames as
    // described in [RFC6455], Section 5.5.2.
    // ...
    // The indication and use of the CRLF NAT keep-alive mechanism defined
    // for SIP connection-oriented transports in [RFC5626], Section 3.5.1 or
    // [RFC6223] are, of course, usable over the transport defined in this
    // specification.
    // https://tools.ietf.org/html/rfc7118#section-6
    //
    // and...
    //
    // The Ping frame contains an opcode of 0x9.
    // https://tools.ietf.org/html/rfc6455#section-5.5.2
    //
    // ==============================
    // KeepAlive Stuff
    // ==============================
    clearKeepAliveTimeout() {
      if (this.keepAliveDebounceTimeout) {
        clearTimeout(this.keepAliveDebounceTimeout);
      }
      this.keepAliveDebounceTimeout = void 0;
    }
    /**
     * Send a keep-alive (a double-CRLF sequence).
     */
    sendKeepAlive() {
      if (this.keepAliveDebounceTimeout) {
        return Promise.resolve();
      }
      this.keepAliveDebounceTimeout = setTimeout(() => {
        this.clearKeepAliveTimeout();
      }, this.configuration.keepAliveDebounce * 1e3);
      return this.send("\r\n\r\n");
    }
    /**
     * Start sending keep-alives.
     */
    startSendingKeepAlives() {
      const computeKeepAliveTimeout = (upperBound) => {
        const lowerBound = upperBound * 0.8;
        return 1e3 * (Math.random() * (upperBound - lowerBound) + lowerBound);
      };
      if (this.configuration.keepAliveInterval && !this.keepAliveInterval) {
        this.keepAliveInterval = setInterval(() => {
          this.sendKeepAlive();
          this.startSendingKeepAlives();
        }, computeKeepAliveTimeout(this.configuration.keepAliveInterval));
      }
    }
    /**
     * Stop sending keep-alives.
     */
    stopSendingKeepAlives() {
      if (this.keepAliveInterval) {
        clearInterval(this.keepAliveInterval);
      }
      if (this.keepAliveDebounceTimeout) {
        clearTimeout(this.keepAliveDebounceTimeout);
      }
      this.keepAliveInterval = void 0;
      this.keepAliveDebounceTimeout = void 0;
    }
  };
  Transport.defaultOptions = {
    server: "",
    connectionTimeout: 5,
    keepAliveInterval: 0,
    keepAliveDebounce: 10,
    traceSip: true
  };

  // node_modules/sip.js/lib/api/user-agent.js
  var UserAgent = class _UserAgent {
    /**
     * Constructs a new instance of the `UserAgent` class.
     * @param options - Options bucket. See {@link UserAgentOptions} for details.
     */
    constructor(options = {}) {
      this._publishers = {};
      this._registerers = {};
      this._sessions = {};
      this._subscriptions = {};
      this._state = UserAgentState.Stopped;
      this._stateEventEmitter = new EmitterImpl();
      this.delegate = options.delegate;
      this.options = Object.assign(Object.assign(Object.assign(Object.assign(Object.assign({}, _UserAgent.defaultOptions()), { sipjsId: createRandomToken(5) }), { uri: new URI("sip", "anonymous." + createRandomToken(6), "anonymous.invalid") }), { viaHost: createRandomToken(12) + ".invalid" }), _UserAgent.stripUndefinedProperties(options));
      if (this.options.hackIpInContact) {
        if (typeof this.options.hackIpInContact === "boolean" && this.options.hackIpInContact) {
          const from = 1;
          const to = 254;
          const octet = Math.floor(Math.random() * (to - from + 1) + from);
          this.options.viaHost = "192.0.2." + octet;
        } else if (this.options.hackIpInContact) {
          this.options.viaHost = this.options.hackIpInContact;
        }
      }
      this.loggerFactory = new LoggerFactory();
      this.logger = this.loggerFactory.getLogger("sip.UserAgent");
      this.loggerFactory.builtinEnabled = this.options.logBuiltinEnabled;
      this.loggerFactory.connector = this.options.logConnector;
      switch (this.options.logLevel) {
        case "error":
          this.loggerFactory.level = Levels.error;
          break;
        case "warn":
          this.loggerFactory.level = Levels.warn;
          break;
        case "log":
          this.loggerFactory.level = Levels.log;
          break;
        case "debug":
          this.loggerFactory.level = Levels.debug;
          break;
        default:
          break;
      }
      if (this.options.logConfiguration) {
        this.logger.log("Configuration:");
        Object.keys(this.options).forEach((key) => {
          const value = this.options[key];
          switch (key) {
            case "uri":
            case "sessionDescriptionHandlerFactory":
              this.logger.log("\xB7 " + key + ": " + value);
              break;
            case "authorizationPassword":
              this.logger.log("\xB7 " + key + ": NOT SHOWN");
              break;
            case "transportConstructor":
              this.logger.log("\xB7 " + key + ": " + value.name);
              break;
            default:
              this.logger.log("\xB7 " + key + ": " + JSON.stringify(value));
          }
        });
      }
      if (this.options.transportOptions) {
        const optionsDeprecated = this.options.transportOptions;
        const maxReconnectionAttemptsDeprecated = optionsDeprecated.maxReconnectionAttempts;
        const reconnectionTimeoutDeprecated = optionsDeprecated.reconnectionTimeout;
        if (maxReconnectionAttemptsDeprecated !== void 0) {
          const deprecatedMessage = `The transport option "maxReconnectionAttempts" as has apparently been specified and has been deprecated. It will no longer be available starting with SIP.js release 0.16.0. Please update accordingly.`;
          this.logger.warn(deprecatedMessage);
        }
        if (reconnectionTimeoutDeprecated !== void 0) {
          const deprecatedMessage = `The transport option "reconnectionTimeout" as has apparently been specified and has been deprecated. It will no longer be available starting with SIP.js release 0.16.0. Please update accordingly.`;
          this.logger.warn(deprecatedMessage);
        }
        if (options.reconnectionDelay === void 0 && reconnectionTimeoutDeprecated !== void 0) {
          this.options.reconnectionDelay = reconnectionTimeoutDeprecated;
        }
        if (options.reconnectionAttempts === void 0 && maxReconnectionAttemptsDeprecated !== void 0) {
          this.options.reconnectionAttempts = maxReconnectionAttemptsDeprecated;
        }
      }
      if (options.reconnectionDelay !== void 0) {
        const deprecatedMessage = `The user agent option "reconnectionDelay" as has apparently been specified and has been deprecated. It will no longer be available starting with SIP.js release 0.16.0. Please update accordingly.`;
        this.logger.warn(deprecatedMessage);
      }
      if (options.reconnectionAttempts !== void 0) {
        const deprecatedMessage = `The user agent option "reconnectionAttempts" as has apparently been specified and has been deprecated. It will no longer be available starting with SIP.js release 0.16.0. Please update accordingly.`;
        this.logger.warn(deprecatedMessage);
      }
      this._transport = new this.options.transportConstructor(this.getLogger("sip.Transport"), this.options.transportOptions);
      this.initTransportCallbacks();
      this._contact = this.initContact();
      this._instanceId = this.options.instanceId ? this.options.instanceId : _UserAgent.newUUID();
      if (Grammar.parse(this._instanceId, "uuid") === -1) {
        throw new Error("Invalid instanceId.");
      }
      this._userAgentCore = this.initCore();
    }
    /**
     * Create a URI instance from a string.
     * @param uri - The string to parse.
     *
     * @remarks
     * Returns undefined if the syntax of the URI is invalid.
     * The syntax must conform to a SIP URI as defined in the RFC.
     * 25 Augmented BNF for the SIP Protocol
     * https://tools.ietf.org/html/rfc3261#section-25
     *
     * @example
     * ```ts
     * const uri = UserAgent.makeURI("sip:edgar@example.com");
     * ```
     */
    static makeURI(uri) {
      return Grammar.URIParse(uri);
    }
    /** Default user agent options. */
    static defaultOptions() {
      return {
        allowLegacyNotifications: false,
        authorizationHa1: "",
        authorizationPassword: "",
        authorizationUsername: "",
        delegate: {},
        contactName: "",
        contactParams: { transport: "ws" },
        displayName: "",
        forceRport: false,
        gracefulShutdown: true,
        hackAllowUnregisteredOptionTags: false,
        hackIpInContact: false,
        hackViaTcp: false,
        instanceId: "",
        instanceIdAlwaysAdded: false,
        logBuiltinEnabled: true,
        logConfiguration: true,
        logConnector: () => {
        },
        logLevel: "log",
        noAnswerTimeout: 60,
        preloadedRouteSet: [],
        reconnectionAttempts: 0,
        reconnectionDelay: 4,
        sendInitialProvisionalResponse: true,
        sessionDescriptionHandlerFactory: defaultSessionDescriptionHandlerFactory(),
        sessionDescriptionHandlerFactoryOptions: {},
        sipExtension100rel: SIPExtension.Unsupported,
        sipExtensionReplaces: SIPExtension.Unsupported,
        sipExtensionExtraSupported: [],
        sipjsId: "",
        transportConstructor: Transport,
        transportOptions: {},
        uri: new URI("sip", "anonymous", "anonymous.invalid"),
        userAgentString: "SIP.js/" + LIBRARY_VERSION,
        viaHost: ""
      };
    }
    // http://stackoverflow.com/users/109538/broofa
    static newUUID() {
      const UUID = "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
        const r = Math.floor(Math.random() * 16);
        const v = c === "x" ? r : r % 4 + 8;
        return v.toString(16);
      });
      return UUID;
    }
    /**
     * Strip properties with undefined values from options.
     * This is a work around while waiting for missing vs undefined to be addressed (or not)...
     * https://github.com/Microsoft/TypeScript/issues/13195
     * @param options - Options to reduce
     */
    static stripUndefinedProperties(options) {
      return Object.keys(options).reduce((object, key) => {
        if (options[key] !== void 0) {
          object[key] = options[key];
        }
        return object;
      }, {});
    }
    /**
     * User agent configuration.
     */
    get configuration() {
      return this.options;
    }
    /**
     * User agent contact.
     */
    get contact() {
      return this._contact;
    }
    /**
     * User agent instance id.
     */
    get instanceId() {
      return this._instanceId;
    }
    /**
     * User agent state.
     */
    get state() {
      return this._state;
    }
    /**
     * User agent state change emitter.
     */
    get stateChange() {
      return this._stateEventEmitter;
    }
    /**
     * User agent transport.
     */
    get transport() {
      return this._transport;
    }
    /**
     * User agent core.
     */
    get userAgentCore() {
      return this._userAgentCore;
    }
    /**
     * The logger.
     */
    getLogger(category, label) {
      return this.loggerFactory.getLogger(category, label);
    }
    /**
     * The logger factory.
     */
    getLoggerFactory() {
      return this.loggerFactory;
    }
    /**
     * True if transport is connected.
     */
    isConnected() {
      return this.transport.isConnected();
    }
    /**
     * Reconnect the transport.
     */
    reconnect() {
      if (this.state === UserAgentState.Stopped) {
        return Promise.reject(new Error("User agent stopped."));
      }
      return Promise.resolve().then(() => this.transport.connect());
    }
    /**
     * Start the user agent.
     *
     * @remarks
     * Resolves if transport connects, otherwise rejects.
     * Calling `start()` after calling `stop()` will fail if `stop()` has yet to resolve.
     *
     * @example
     * ```ts
     * userAgent.start()
     *   .then(() => {
     *     // userAgent.isConnected() === true
     *   })
     *   .catch((error: Error) => {
     *     // userAgent.isConnected() === false
     *   });
     * ```
     */
    start() {
      if (this.state === UserAgentState.Started) {
        this.logger.warn(`User agent already started`);
        return Promise.resolve();
      }
      this.logger.log(`Starting ${this.configuration.uri}`);
      this.transitionState(UserAgentState.Started);
      return this.transport.connect();
    }
    /**
     * Stop the user agent.
     *
     * @remarks
     * Resolves when the user agent has completed a graceful shutdown.
     * ```txt
     * 1) Sessions terminate.
     * 2) Registerers unregister.
     * 3) Subscribers unsubscribe.
     * 4) Publishers unpublish.
     * 5) Transport disconnects.
     * 6) User Agent Core resets.
     * ```
     * The user agent state transistions to stopped once these steps have been completed.
     * Calling `start()` after calling `stop()` will fail if `stop()` has yet to resolve.
     *
     * NOTE: While this is a "graceful shutdown", it can also be very slow one if you
     * are waiting for the returned Promise to resolve. The disposal of the clients and
     * dialogs is done serially - waiting on one to finish before moving on to the next.
     * This can be slow if there are lot of subscriptions to unsubscribe for example.
     *
     * THE SLOW PACE IS INTENTIONAL!
     * While one could spin them all down in parallel, this could slam the remote server.
     * It is bad practice to denial of service attack (DoS attack) servers!!!
     * Moreover, production servers will automatically blacklist clients which send too
     * many requests in too short a period of time - dropping any additional requests.
     *
     * If a different approach to disposing is needed, one can implement whatever is
     * needed and execute that prior to calling `stop()`. Alternatively one may simply
     * not wait for the Promise returned by `stop()` to complete.
     */
    async stop() {
      if (this.state === UserAgentState.Stopped) {
        this.logger.warn(`User agent already stopped`);
        return Promise.resolve();
      }
      this.logger.log(`Stopping ${this.configuration.uri}`);
      if (!this.options.gracefulShutdown) {
        this.logger.log(`Dispose of transport`);
        this.transport.dispose().catch((error) => {
          this.logger.error(error.message);
          throw error;
        });
        this.logger.log(`Dispose of core`);
        this.userAgentCore.dispose();
        this._publishers = {};
        this._registerers = {};
        this._sessions = {};
        this._subscriptions = {};
        this.transitionState(UserAgentState.Stopped);
        return Promise.resolve();
      }
      const publishers = Object.assign({}, this._publishers);
      const registerers = Object.assign({}, this._registerers);
      const sessions = Object.assign({}, this._sessions);
      const subscriptions = Object.assign({}, this._subscriptions);
      const transport = this.transport;
      const userAgentCore = this.userAgentCore;
      this.logger.log(`Dispose of registerers`);
      for (const id in registerers) {
        if (registerers[id]) {
          await registerers[id].dispose().catch((error) => {
            this.logger.error(error.message);
            delete this._registerers[id];
            throw error;
          });
        }
      }
      this.logger.log(`Dispose of sessions`);
      for (const id in sessions) {
        if (sessions[id]) {
          await sessions[id].dispose().catch((error) => {
            this.logger.error(error.message);
            delete this._sessions[id];
            throw error;
          });
        }
      }
      this.logger.log(`Dispose of subscriptions`);
      for (const id in subscriptions) {
        if (subscriptions[id]) {
          await subscriptions[id].dispose().catch((error) => {
            this.logger.error(error.message);
            delete this._subscriptions[id];
            throw error;
          });
        }
      }
      this.logger.log(`Dispose of publishers`);
      for (const id in publishers) {
        if (publishers[id]) {
          await publishers[id].dispose().catch((error) => {
            this.logger.error(error.message);
            delete this._publishers[id];
            throw error;
          });
        }
      }
      this.logger.log(`Dispose of transport`);
      await transport.dispose().catch((error) => {
        this.logger.error(error.message);
        throw error;
      });
      this.logger.log(`Dispose of core`);
      userAgentCore.dispose();
      this.transitionState(UserAgentState.Stopped);
    }
    /**
     * Used to avoid circular references.
     * @internal
     */
    _makeInviter(targetURI, options) {
      return new Inviter(this, targetURI, options);
    }
    /**
     * Attempt reconnection up to `maxReconnectionAttempts` times.
     * @param reconnectionAttempt - Current attempt number.
     */
    attemptReconnection(reconnectionAttempt = 1) {
      const reconnectionAttempts = this.options.reconnectionAttempts;
      const reconnectionDelay = this.options.reconnectionDelay;
      if (reconnectionAttempt > reconnectionAttempts) {
        this.logger.log(`Maximum reconnection attempts reached`);
        return;
      }
      this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - trying`);
      setTimeout(() => {
        this.reconnect().then(() => {
          this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - succeeded`);
        }).catch((error) => {
          this.logger.error(error.message);
          this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - failed`);
          this.attemptReconnection(++reconnectionAttempt);
        });
      }, reconnectionAttempt === 1 ? 0 : reconnectionDelay * 1e3);
    }
    /**
     * Initialize contact.
     */
    initContact() {
      const contactName = this.options.contactName !== "" ? this.options.contactName : createRandomToken(8);
      const contactParams = this.options.contactParams;
      const contact = {
        pubGruu: void 0,
        tempGruu: void 0,
        uri: new URI("sip", contactName, this.options.viaHost, void 0, contactParams),
        toString: (contactToStringOptions = {}) => {
          const anonymous = contactToStringOptions.anonymous || false;
          const outbound = contactToStringOptions.outbound || false;
          const register = contactToStringOptions.register || false;
          let contactString = "<";
          if (anonymous) {
            contactString += this.contact.tempGruu || `sip:anonymous@anonymous.invalid;transport=${contactParams.transport ? contactParams.transport : "ws"}`;
          } else if (register) {
            contactString += this.contact.uri;
          } else {
            contactString += this.contact.pubGruu || this.contact.uri;
          }
          if (outbound) {
            contactString += ";ob";
          }
          contactString += ">";
          if (this.options.instanceIdAlwaysAdded) {
            contactString += ';+sip.instance="<urn:uuid:' + this._instanceId + '>"';
          }
          return contactString;
        }
      };
      return contact;
    }
    /**
     * Initialize user agent core.
     */
    initCore() {
      let supportedOptionTags = [];
      supportedOptionTags.push("outbound");
      if (this.options.sipExtension100rel === SIPExtension.Supported) {
        supportedOptionTags.push("100rel");
      }
      if (this.options.sipExtensionReplaces === SIPExtension.Supported) {
        supportedOptionTags.push("replaces");
      }
      if (this.options.sipExtensionExtraSupported) {
        supportedOptionTags.push(...this.options.sipExtensionExtraSupported);
      }
      if (!this.options.hackAllowUnregisteredOptionTags) {
        supportedOptionTags = supportedOptionTags.filter((optionTag) => UserAgentRegisteredOptionTags[optionTag]);
      }
      supportedOptionTags = Array.from(new Set(supportedOptionTags));
      const supportedOptionTagsResponse = supportedOptionTags.slice();
      if (this.contact.pubGruu || this.contact.tempGruu) {
        supportedOptionTagsResponse.push("gruu");
      }
      const userAgentCoreConfiguration = {
        aor: this.options.uri,
        contact: this.contact,
        displayName: this.options.displayName,
        loggerFactory: this.loggerFactory,
        hackViaTcp: this.options.hackViaTcp,
        routeSet: this.options.preloadedRouteSet,
        supportedOptionTags,
        supportedOptionTagsResponse,
        sipjsId: this.options.sipjsId,
        userAgentHeaderFieldValue: this.options.userAgentString,
        viaForceRport: this.options.forceRport,
        viaHost: this.options.viaHost,
        authenticationFactory: () => {
          const username = this.options.authorizationUsername ? this.options.authorizationUsername : this.options.uri.user;
          const password = this.options.authorizationPassword ? this.options.authorizationPassword : void 0;
          const ha1 = this.options.authorizationHa1 ? this.options.authorizationHa1 : void 0;
          return new DigestAuthentication(this.getLoggerFactory(), ha1, username, password);
        },
        transportAccessor: () => this.transport
      };
      const userAgentCoreDelegate = {
        onInvite: (incomingInviteRequest) => {
          var _a;
          const invitation = new Invitation(this, incomingInviteRequest);
          incomingInviteRequest.delegate = {
            onCancel: (cancel) => {
              invitation._onCancel(cancel);
            },
            // eslint-disable-next-line @typescript-eslint/no-unused-vars
            onTransportError: (error) => {
              this.logger.error("A transport error has occurred while handling an incoming INVITE request.");
            }
          };
          incomingInviteRequest.trying();
          if (this.options.sipExtensionReplaces !== SIPExtension.Unsupported) {
            const message = incomingInviteRequest.message;
            const replaces = message.parseHeader("replaces");
            if (replaces) {
              const callId = replaces.call_id;
              if (typeof callId !== "string") {
                throw new Error("Type of call id is not string");
              }
              const toTag = replaces.replaces_to_tag;
              if (typeof toTag !== "string") {
                throw new Error("Type of to tag is not string");
              }
              const fromTag = replaces.replaces_from_tag;
              if (typeof fromTag !== "string") {
                throw new Error("type of from tag is not string");
              }
              const targetDialogId = callId + toTag + fromTag;
              const targetDialog = this.userAgentCore.dialogs.get(targetDialogId);
              if (!targetDialog) {
                invitation.reject({ statusCode: 481 });
                return;
              }
              if (!targetDialog.early && replaces.early_only === true) {
                invitation.reject({ statusCode: 486 });
                return;
              }
              const targetSession = this._sessions[callId + fromTag] || this._sessions[callId + toTag] || void 0;
              if (!targetSession) {
                throw new Error("Session does not exist.");
              }
              invitation._replacee = targetSession;
            }
          }
          if ((_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onInvite) {
            if (invitation.autoSendAnInitialProvisionalResponse) {
              invitation.progress().then(() => {
                var _a2;
                if (((_a2 = this.delegate) === null || _a2 === void 0 ? void 0 : _a2.onInvite) === void 0) {
                  throw new Error("onInvite undefined.");
                }
                this.delegate.onInvite(invitation);
              });
              return;
            }
            this.delegate.onInvite(invitation);
            return;
          }
          invitation.reject({ statusCode: 486 });
        },
        onMessage: (incomingMessageRequest) => {
          if (this.delegate && this.delegate.onMessage) {
            const message = new Message(incomingMessageRequest);
            this.delegate.onMessage(message);
          } else {
            incomingMessageRequest.accept();
          }
        },
        onNotify: (incomingNotifyRequest) => {
          if (this.delegate && this.delegate.onNotify) {
            const notification = new Notification(incomingNotifyRequest);
            this.delegate.onNotify(notification);
          } else {
            if (this.options.allowLegacyNotifications) {
              incomingNotifyRequest.accept();
            } else {
              incomingNotifyRequest.reject({ statusCode: 481 });
            }
          }
        },
        onRefer: (incomingReferRequest) => {
          this.logger.warn("Received an out of dialog REFER request");
          if (this.delegate && this.delegate.onReferRequest) {
            this.delegate.onReferRequest(incomingReferRequest);
          } else {
            incomingReferRequest.reject({ statusCode: 405 });
          }
        },
        onRegister: (incomingRegisterRequest) => {
          this.logger.warn("Received an out of dialog REGISTER request");
          if (this.delegate && this.delegate.onRegisterRequest) {
            this.delegate.onRegisterRequest(incomingRegisterRequest);
          } else {
            incomingRegisterRequest.reject({ statusCode: 405 });
          }
        },
        onSubscribe: (incomingSubscribeRequest) => {
          this.logger.warn("Received an out of dialog SUBSCRIBE request");
          if (this.delegate && this.delegate.onSubscribeRequest) {
            this.delegate.onSubscribeRequest(incomingSubscribeRequest);
          } else {
            incomingSubscribeRequest.reject({ statusCode: 405 });
          }
        }
      };
      return new UserAgentCore(userAgentCoreConfiguration, userAgentCoreDelegate);
    }
    initTransportCallbacks() {
      this.transport.onConnect = () => this.onTransportConnect();
      this.transport.onDisconnect = (error) => this.onTransportDisconnect(error);
      this.transport.onMessage = (message) => this.onTransportMessage(message);
    }
    onTransportConnect() {
      if (this.state === UserAgentState.Stopped) {
        return;
      }
      if (this.delegate && this.delegate.onConnect) {
        this.delegate.onConnect();
      }
    }
    onTransportDisconnect(error) {
      if (this.state === UserAgentState.Stopped) {
        return;
      }
      if (this.delegate && this.delegate.onDisconnect) {
        this.delegate.onDisconnect(error);
      }
      if (error && this.options.reconnectionAttempts > 0) {
        this.attemptReconnection();
      }
    }
    onTransportMessage(messageString) {
      const message = Parser.parseMessage(messageString, this.getLogger("sip.Parser"));
      if (!message) {
        this.logger.warn("Failed to parse incoming message. Dropping.");
        return;
      }
      if (this.state === UserAgentState.Stopped && message instanceof IncomingRequestMessage) {
        this.logger.warn(`Received ${message.method} request while stopped. Dropping.`);
        return;
      }
      const hasMinimumHeaders = () => {
        const mandatoryHeaders = ["from", "to", "call_id", "cseq", "via"];
        for (const header of mandatoryHeaders) {
          if (!message.hasHeader(header)) {
            this.logger.warn(`Missing mandatory header field : ${header}.`);
            return false;
          }
        }
        return true;
      };
      if (message instanceof IncomingRequestMessage) {
        if (!hasMinimumHeaders()) {
          this.logger.warn(`Request missing mandatory header field. Dropping.`);
          return;
        }
        if (!message.toTag && message.callId.substr(0, 5) === this.options.sipjsId) {
          this.userAgentCore.replyStateless(message, { statusCode: 482 });
          return;
        }
        const len = utf8Length(message.body);
        const contentLength = message.getHeader("content-length");
        if (contentLength && len < Number(contentLength)) {
          this.userAgentCore.replyStateless(message, { statusCode: 400 });
          return;
        }
      }
      if (message instanceof IncomingResponseMessage) {
        if (!hasMinimumHeaders()) {
          this.logger.warn(`Response missing mandatory header field. Dropping.`);
          return;
        }
        if (message.getHeaders("via").length > 1) {
          this.logger.warn("More than one Via header field present in the response. Dropping.");
          return;
        }
        if (message.via.host !== this.options.viaHost || message.via.port !== void 0) {
          this.logger.warn("Via sent-by in the response does not match UA Via host value. Dropping.");
          return;
        }
        const len = utf8Length(message.body);
        const contentLength = message.getHeader("content-length");
        if (contentLength && len < Number(contentLength)) {
          this.logger.warn("Message body length is lower than the value in Content-Length header field. Dropping.");
          return;
        }
      }
      if (message instanceof IncomingRequestMessage) {
        this.userAgentCore.receiveIncomingRequestFromTransport(message);
        return;
      }
      if (message instanceof IncomingResponseMessage) {
        this.userAgentCore.receiveIncomingResponseFromTransport(message);
        return;
      }
      throw new Error("Invalid message type.");
    }
    /**
     * Transition state.
     */
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    transitionState(newState, error) {
      const invalidTransition = () => {
        throw new Error(`Invalid state transition from ${this._state} to ${newState}`);
      };
      switch (this._state) {
        case UserAgentState.Started:
          if (newState !== UserAgentState.Stopped) {
            invalidTransition();
          }
          break;
        case UserAgentState.Stopped:
          if (newState !== UserAgentState.Started) {
            invalidTransition();
          }
          break;
        default:
          throw new Error("Unknown state.");
      }
      this.logger.log(`Transitioned from ${this._state} to ${newState}`);
      this._state = newState;
      this._stateEventEmitter.emit(this._state);
    }
  };

  // node_modules/sip.js/lib/core/index.js
  var core_exports = {};
  __export(core_exports, {
    ByeUserAgentClient: () => ByeUserAgentClient,
    ByeUserAgentServer: () => ByeUserAgentServer,
    C: () => C,
    CancelUserAgentClient: () => CancelUserAgentClient,
    ClientTransaction: () => ClientTransaction,
    Dialog: () => Dialog,
    DigestAuthentication: () => DigestAuthentication,
    Exception: () => Exception,
    Grammar: () => Grammar,
    IncomingMessage: () => IncomingMessage,
    IncomingRequestMessage: () => IncomingRequestMessage,
    IncomingResponseMessage: () => IncomingResponseMessage,
    InfoUserAgentClient: () => InfoUserAgentClient,
    InfoUserAgentServer: () => InfoUserAgentServer,
    InviteClientTransaction: () => InviteClientTransaction,
    InviteServerTransaction: () => InviteServerTransaction,
    InviteUserAgentClient: () => InviteUserAgentClient,
    InviteUserAgentServer: () => InviteUserAgentServer,
    Levels: () => Levels,
    Logger: () => Logger,
    LoggerFactory: () => LoggerFactory,
    MessageUserAgentClient: () => MessageUserAgentClient,
    MessageUserAgentServer: () => MessageUserAgentServer,
    NameAddrHeader: () => NameAddrHeader,
    NonInviteClientTransaction: () => NonInviteClientTransaction,
    NonInviteServerTransaction: () => NonInviteServerTransaction,
    NotifyUserAgentClient: () => NotifyUserAgentClient,
    NotifyUserAgentServer: () => NotifyUserAgentServer,
    OutgoingRequestMessage: () => OutgoingRequestMessage,
    Parameters: () => Parameters,
    Parser: () => Parser,
    PrackUserAgentClient: () => PrackUserAgentClient,
    PrackUserAgentServer: () => PrackUserAgentServer,
    PublishUserAgentClient: () => PublishUserAgentClient,
    ReInviteUserAgentClient: () => ReInviteUserAgentClient,
    ReInviteUserAgentServer: () => ReInviteUserAgentServer,
    ReSubscribeUserAgentClient: () => ReSubscribeUserAgentClient,
    ReSubscribeUserAgentServer: () => ReSubscribeUserAgentServer,
    ReferUserAgentClient: () => ReferUserAgentClient,
    ReferUserAgentServer: () => ReferUserAgentServer,
    RegisterUserAgentClient: () => RegisterUserAgentClient,
    RegisterUserAgentServer: () => RegisterUserAgentServer,
    ServerTransaction: () => ServerTransaction,
    SessionDialog: () => SessionDialog,
    SessionState: () => SessionState,
    SignalingState: () => SignalingState,
    SubscribeUserAgentClient: () => SubscribeUserAgentClient,
    SubscribeUserAgentServer: () => SubscribeUserAgentServer,
    SubscriptionDialog: () => SubscriptionDialog,
    SubscriptionState: () => SubscriptionState,
    Timers: () => Timers,
    Transaction: () => Transaction,
    TransactionState: () => TransactionState,
    TransactionStateError: () => TransactionStateError,
    TransportError: () => TransportError,
    URI: () => URI,
    UserAgentClient: () => UserAgentClient,
    UserAgentCore: () => UserAgentCore,
    UserAgentServer: () => UserAgentServer,
    constructOutgoingResponse: () => constructOutgoingResponse,
    equivalentURI: () => equivalentURI,
    fromBodyLegacy: () => fromBodyLegacy,
    getBody: () => getBody,
    isBody: () => isBody
  });

  // node_modules/sip.js/lib/core/user-agents/cancel-user-agent-client.js
  var CancelUserAgentClient = class extends UserAgentClient {
    constructor(core, message, delegate) {
      super(NonInviteClientTransaction, core, message, delegate);
    }
  };

  // node_modules/sip.js/lib/core/user-agents/re-subscribe-user-agent-server.js
  var ReSubscribeUserAgentServer = class extends UserAgentServer {
    constructor(dialog, message, delegate) {
      super(NonInviteServerTransaction, dialog.userAgentCore, message, delegate);
    }
  };

  // node_modules/sip.js/lib/platform/web/index.js
  var web_exports = {};
  __export(web_exports, {
    SessionDescriptionHandler: () => SessionDescriptionHandler,
    SessionManager: () => SessionManager,
    SimpleUser: () => SimpleUser,
    Transport: () => Transport,
    WebAudioSessionDescriptionHandler: () => WebAudioSessionDescriptionHandler,
    addMidLines: () => addMidLines,
    cleanJitsiSdpImageattr: () => cleanJitsiSdpImageattr,
    defaultManagedSessionFactory: () => defaultManagedSessionFactory,
    defaultMediaStreamFactory: () => defaultMediaStreamFactory,
    defaultPeerConnectionConfiguration: () => defaultPeerConnectionConfiguration,
    defaultSessionDescriptionHandlerFactory: () => defaultSessionDescriptionHandlerFactory,
    holdModifier: () => holdModifier,
    startLocalConference: () => startLocalConference,
    stripG722: () => stripG722,
    stripRtpPayload: () => stripRtpPayload,
    stripTcpCandidates: () => stripTcpCandidates,
    stripTelephoneEvent: () => stripTelephoneEvent,
    stripVideo: () => stripVideo
  });

  // node_modules/sip.js/lib/platform/web/modifiers/modifiers.js
  var stripPayload = (sdp, payload) => {
    const mediaDescs = [];
    const lines = sdp.split(/\r\n/);
    let currentMediaDesc;
    for (let i = 0; i < lines.length; ) {
      const line = lines[i];
      if (/^m=(?:audio|video)/.test(line)) {
        currentMediaDesc = {
          index: i,
          stripped: []
        };
        mediaDescs.push(currentMediaDesc);
      } else if (currentMediaDesc) {
        const rtpmap = /^a=rtpmap:(\d+) ([^/]+)\//.exec(line);
        if (rtpmap && payload === rtpmap[2]) {
          lines.splice(i, 1);
          currentMediaDesc.stripped.push(rtpmap[1]);
          continue;
        }
      }
      i++;
    }
    for (const mediaDesc of mediaDescs) {
      const mline = lines[mediaDesc.index].split(" ");
      for (let j = 3; j < mline.length; ) {
        if (mediaDesc.stripped.indexOf(mline[j]) !== -1) {
          mline.splice(j, 1);
          continue;
        }
        j++;
      }
      lines[mediaDesc.index] = mline.join(" ");
    }
    return lines.join("\r\n");
  };
  var stripMediaDescription = (sdp, description) => {
    const descriptionRegExp = new RegExp("m=" + description + ".*$", "gm");
    const groupRegExp = new RegExp("^a=group:.*$", "gm");
    if (descriptionRegExp.test(sdp)) {
      let midLineToRemove;
      sdp = sdp.split(/^m=/gm).filter((section) => {
        if (section.substr(0, description.length) === description) {
          midLineToRemove = section.match(/^a=mid:.*$/gm);
          if (midLineToRemove) {
            const step = midLineToRemove[0].match(/:.+$/g);
            if (step) {
              midLineToRemove = step[0].substr(1);
            }
          }
          return false;
        }
        return true;
      }).join("m=");
      const groupLine = sdp.match(groupRegExp);
      if (groupLine && groupLine.length === 1) {
        let groupLinePortion = groupLine[0];
        const groupRegExpReplace = new RegExp(" *" + midLineToRemove + "[^ ]*", "g");
        groupLinePortion = groupLinePortion.replace(groupRegExpReplace, "");
        sdp = sdp.split(groupRegExp).join(groupLinePortion);
      }
    }
    return sdp;
  };
  function stripTcpCandidates(description) {
    description.sdp = (description.sdp || "").replace(/^a=candidate:\d+ \d+ tcp .*?\r\n/img, "");
    return Promise.resolve(description);
  }
  function stripTelephoneEvent(description) {
    description.sdp = stripPayload(description.sdp || "", "telephone-event");
    return Promise.resolve(description);
  }
  function cleanJitsiSdpImageattr(description) {
    description.sdp = (description.sdp || "").replace(/^(a=imageattr:.*?)(x|y)=\[0-/gm, "$1$2=[1:");
    return Promise.resolve(description);
  }
  function stripG722(description) {
    description.sdp = stripPayload(description.sdp || "", "G722");
    return Promise.resolve(description);
  }
  function stripRtpPayload(payload) {
    return (description) => {
      description.sdp = stripPayload(description.sdp || "", payload);
      return Promise.resolve(description);
    };
  }
  function stripVideo(description) {
    description.sdp = stripMediaDescription(description.sdp || "", "video");
    return Promise.resolve(description);
  }
  function addMidLines(description) {
    let sdp = description.sdp || "";
    if (sdp.search(/^a=mid.*$/gm) === -1) {
      const mlines = sdp.match(/^m=.*$/gm);
      const sdpArray = sdp.split(/^m=.*$/gm);
      if (mlines) {
        mlines.forEach((elem, idx) => {
          mlines[idx] = elem + "\na=mid:" + idx;
        });
      }
      sdpArray.forEach((elem, idx) => {
        if (mlines && mlines[idx]) {
          sdpArray[idx] = elem + mlines[idx];
        }
      });
      sdp = sdpArray.join("");
      description.sdp = sdp;
    }
    return Promise.resolve(description);
  }
  function holdModifier(description) {
    if (!description.sdp || !description.type) {
      throw new Error("Invalid SDP");
    }
    let sdp = description.sdp;
    const type = description.type;
    if (sdp) {
      if (!/a=(sendrecv|sendonly|recvonly|inactive)/.test(sdp)) {
        sdp = sdp.replace(/(m=[^\r]*\r\n)/g, "$1a=sendonly\r\n");
      } else {
        sdp = sdp.replace(/a=sendrecv\r\n/g, "a=sendonly\r\n");
        sdp = sdp.replace(/a=recvonly\r\n/g, "a=inactive\r\n");
      }
    }
    return Promise.resolve({ sdp, type });
  }

  // node_modules/sip.js/lib/platform/web/session-description-handler/web-audio-session-description-handler.js
  function startLocalConference(conferenceSessions) {
    if (conferenceSessions.length < 2) {
      throw new Error("Start local conference requires at leaast 2 sessions.");
    }
    const pairs = (arr) => arr.map((v, i) => arr.slice(i + 1).map((w) => [v, w])).reduce((acc, curVal) => acc.concat(curVal), []);
    pairs(conferenceSessions.map((session) => session.sessionDescriptionHandler)).forEach(([sdh0, sdh1]) => {
      if (!(sdh0 instanceof WebAudioSessionDescriptionHandler && sdh1 instanceof WebAudioSessionDescriptionHandler)) {
        throw new Error("Session description handler not instance of SessionManagerSessionDescriptionHandler");
      }
      sdh0.joinWith(sdh1);
    });
  }
  var WebAudioSessionDescriptionHandler = class _WebAudioSessionDescriptionHandler extends SessionDescriptionHandler {
    constructor(logger, mediaStreamFactory, sessionDescriptionHandlerConfiguration) {
      super(logger, mediaStreamFactory, sessionDescriptionHandlerConfiguration);
      if (!_WebAudioSessionDescriptionHandler.audioContext) {
        _WebAudioSessionDescriptionHandler.audioContext = new AudioContext();
      }
    }
    /**
     * Helper function to enable/disable media tracks.
     * @param enable - If true enable tracks.
     */
    enableSenderTracks(enable) {
      const stream = this.localMediaStreamReal;
      if (stream === void 0) {
        throw new Error("Stream undefined.");
      }
      stream.getAudioTracks().forEach((track) => {
        track.enabled = enable;
      });
    }
    /**
     * Returns a WebRTC MediaStream proxying the provided audio media stream.
     * This allows additional Web Audio media stream source nodes to be connected
     * to the destination node assoicated with the returned stream so we can mix
     * aditional audio sorces into the local media stream (ie for 3-way conferencing).
     * @param stream - The MediaStream to proxy.
     */
    initLocalMediaStream(stream) {
      if (!_WebAudioSessionDescriptionHandler.audioContext) {
        throw new Error("SessionManagerSessionDescriptionHandler.audioContext undefined.");
      }
      this.localMediaStreamReal = stream;
      this.localMediaStreamSourceNode = _WebAudioSessionDescriptionHandler.audioContext.createMediaStreamSource(stream);
      this.localMediaStreamDestinationNode = _WebAudioSessionDescriptionHandler.audioContext.createMediaStreamDestination();
      this.localMediaStreamSourceNode.connect(this.localMediaStreamDestinationNode);
      return this.localMediaStreamDestinationNode.stream;
    }
    /**
     * Join (conference) media streams with another party.
     * @param peer - The session description handler of the peer to join with.
     */
    joinWith(peer) {
      if (!_WebAudioSessionDescriptionHandler.audioContext) {
        throw new Error("SessionManagerSessionDescriptionHandler.audioContext undefined.");
      }
      const ourNewInboundStreamSource = _WebAudioSessionDescriptionHandler.audioContext.createMediaStreamSource(this.remoteMediaStream);
      const peerOutboundStreamDestination = peer.localMediaStreamDestinationNode;
      if (peerOutboundStreamDestination === void 0) {
        throw new Error("Peer outbound (local) stream local media stream destination is undefined.");
      }
      ourNewInboundStreamSource.connect(peerOutboundStreamDestination);
      const peerNewInboundStreamSource = _WebAudioSessionDescriptionHandler.audioContext.createMediaStreamSource(peer.remoteMediaStream);
      const ourOutboundStreamDestination = this.localMediaStreamDestinationNode;
      if (ourOutboundStreamDestination === void 0) {
        throw new Error("Our outbound (local) stream local media stream destination is undefined.");
      }
      peerNewInboundStreamSource.connect(ourOutboundStreamDestination);
    }
    /**
     * Sets the original local media stream.
     * @param stream - Media stream containing tracks to be utilized.
     * @remarks
     * Only the first audio and video tracks of the provided MediaStream are utilized.
     * Adds tracks if audio and/or video tracks are not already present, otherwise replaces tracks.
     */
    setRealLocalMediaStream(stream) {
      if (!_WebAudioSessionDescriptionHandler.audioContext) {
        throw new Error("SessionManagerSessionDescriptionHandler.audioContext undefined.");
      }
      if (!this.localMediaStreamReal) {
        this.initLocalMediaStream(stream);
        return;
      }
      if (!this.localMediaStreamDestinationNode || !this.localMediaStreamSourceNode || !this.localMediaStreamReal) {
        throw new Error("Local media stream undefined.");
      }
      this.localMediaStreamReal = stream;
      this.localMediaStreamSourceNode.disconnect(this.localMediaStreamDestinationNode);
      this.localMediaStreamSourceNode = _WebAudioSessionDescriptionHandler.audioContext.createMediaStreamSource(stream);
      this.localMediaStreamSourceNode.connect(this.localMediaStreamDestinationNode);
    }
  };

  // node_modules/sip.js/lib/platform/web/session-manager/managed-session-factory-default.js
  function defaultManagedSessionFactory() {
    return (sessionManager, session) => {
      return { session, held: false, muted: false };
    };
  }

  // node_modules/sip.js/lib/platform/web/session-manager/session-manager.js
  var SessionManager = class _SessionManager {
    /**
     * Constructs a new instance of the `SessionManager` class.
     * @param server - SIP WebSocket Server URL.
     * @param options - Options bucket. See {@link SessionManagerOptions} for details.
     */
    constructor(server, options = {}) {
      this.managedSessions = [];
      this.attemptingReconnection = false;
      this.optionsPingFailure = false;
      this.optionsPingRunning = false;
      this.shouldBeConnected = false;
      this.shouldBeRegistered = false;
      this.delegate = options.delegate;
      this.options = Object.assign({
        aor: "",
        autoStop: true,
        delegate: {},
        iceStopWaitingOnServerReflexive: false,
        managedSessionFactory: defaultManagedSessionFactory(),
        maxSimultaneousSessions: 2,
        media: {},
        optionsPingInterval: -1,
        optionsPingRequestURI: "",
        reconnectionAttempts: 3,
        reconnectionDelay: 4,
        registrationRetry: false,
        registrationRetryInterval: 3,
        registerGuard: null,
        registererOptions: {},
        registererRegisterOptions: {},
        sendDTMFUsingSessionDescriptionHandler: false,
        userAgentOptions: {}
      }, _SessionManager.stripUndefinedProperties(options));
      const userAgentOptions = Object.assign({}, options.userAgentOptions);
      if (!userAgentOptions.transportConstructor) {
        userAgentOptions.transportConstructor = Transport;
      }
      if (!userAgentOptions.transportOptions) {
        userAgentOptions.transportOptions = {
          server
        };
      }
      if (!userAgentOptions.uri) {
        if (options.aor) {
          const uri = UserAgent.makeURI(options.aor);
          if (!uri) {
            throw new Error(`Failed to create valid URI from ${options.aor}`);
          }
          userAgentOptions.uri = uri;
        }
      }
      this.userAgent = new UserAgent(userAgentOptions);
      this.userAgent.delegate = {
        // Handle connection with server established
        onConnect: () => {
          this.logger.log(`Connected`);
          if (this.delegate && this.delegate.onServerConnect) {
            this.delegate.onServerConnect();
          }
          if (this.shouldBeRegistered) {
            this.register();
          }
          if (this.options.optionsPingInterval > 0) {
            this.optionsPingStart();
          }
        },
        // Handle connection with server lost
        onDisconnect: async (error) => {
          this.logger.log(`Disconnected`);
          let optionsPingFailure = false;
          if (this.options.optionsPingInterval > 0) {
            optionsPingFailure = this.optionsPingFailure;
            this.optionsPingFailure = false;
            this.optionsPingStop();
          }
          if (this.delegate && this.delegate.onServerDisconnect) {
            this.delegate.onServerDisconnect(error);
          }
          if (error || optionsPingFailure) {
            if (this.registerer) {
              this.logger.log(`Disposing of registerer...`);
              this.registerer.dispose().catch((e) => {
                this.logger.debug(`Error occurred disposing of registerer after connection with server was lost.`);
                this.logger.debug(e.toString());
              });
              this.registerer = void 0;
            }
            this.managedSessions.slice().map((el) => el.session).forEach(async (session) => {
              this.logger.log(`Disposing of session...`);
              session.dispose().catch((e) => {
                this.logger.debug(`Error occurred disposing of a session after connection with server was lost.`);
                this.logger.debug(e.toString());
              });
            });
            if (this.shouldBeConnected) {
              this.attemptReconnection();
            }
          }
        },
        // Handle incoming invitations
        onInvite: (invitation) => {
          this.logger.log(`[${invitation.id}] Received INVITE`);
          const maxSessions = this.options.maxSimultaneousSessions;
          if (maxSessions !== 0 && this.managedSessions.length > maxSessions) {
            this.logger.warn(`[${invitation.id}] Session already in progress, rejecting INVITE...`);
            invitation.reject().then(() => {
              this.logger.log(`[${invitation.id}] Rejected INVITE`);
            }).catch((error) => {
              this.logger.error(`[${invitation.id}] Failed to reject INVITE`);
              this.logger.error(error.toString());
            });
            return;
          }
          const referralInviterOptions = {
            sessionDescriptionHandlerOptions: { constraints: this.constraints }
          };
          this.initSession(invitation, referralInviterOptions);
          if (this.delegate && this.delegate.onCallReceived) {
            this.delegate.onCallReceived(invitation);
          } else {
            this.logger.warn(`[${invitation.id}] No handler available, rejecting INVITE...`);
            invitation.reject().then(() => {
              this.logger.log(`[${invitation.id}] Rejected INVITE`);
            }).catch((error) => {
              this.logger.error(`[${invitation.id}] Failed to reject INVITE`);
              this.logger.error(error.toString());
            });
          }
        },
        // Handle incoming messages
        onMessage: (message) => {
          message.accept().then(() => {
            if (this.delegate && this.delegate.onMessageReceived) {
              this.delegate.onMessageReceived(message);
            }
          });
        },
        // Handle incoming notifications
        onNotify: (notification) => {
          notification.accept().then(() => {
            if (this.delegate && this.delegate.onNotificationReceived) {
              this.delegate.onNotificationReceived(notification);
            }
          });
        }
      };
      this.registererOptions = Object.assign({}, options.registererOptions);
      this.registererRegisterOptions = Object.assign({}, options.registererRegisterOptions);
      if (this.options.registrationRetry) {
        this.registererRegisterOptions.requestDelegate = this.registererRegisterOptions.requestDelegate || {};
        const existingOnReject = this.registererRegisterOptions.requestDelegate.onReject;
        this.registererRegisterOptions.requestDelegate.onReject = (response) => {
          existingOnReject && existingOnReject(response);
          this.attemptRegistration();
        };
      }
      this.logger = this.userAgent.getLogger("sip.SessionManager");
      window.addEventListener("online", () => {
        this.logger.log(`Online`);
        if (this.shouldBeConnected) {
          this.connect();
        }
      });
      if (this.options.autoStop) {
        window.addEventListener("beforeunload", async () => {
          this.shouldBeConnected = false;
          this.shouldBeRegistered = false;
          if (this.userAgent.state !== UserAgentState.Stopped) {
            await this.userAgent.stop();
          }
        });
      }
    }
    /**
     * Strip properties with undefined values from options.
     * This is a work around while waiting for missing vs undefined to be addressed (or not)...
     * https://github.com/Microsoft/TypeScript/issues/13195
     * @param options - Options to reduce
     */
    static stripUndefinedProperties(options) {
      return Object.keys(options).reduce((object, key) => {
        if (options[key] !== void 0) {
          object[key] = options[key];
        }
        return object;
      }, {});
    }
    /**
     * The local media stream. Undefined if call not answered.
     * @param session - Session to get the media stream from.
     */
    getLocalMediaStream(session) {
      const sdh = session.sessionDescriptionHandler;
      if (!sdh) {
        return void 0;
      }
      if (!(sdh instanceof SessionDescriptionHandler)) {
        throw new Error("Session description handler not instance of web SessionDescriptionHandler");
      }
      return sdh.localMediaStream;
    }
    /**
     * The remote media stream. Undefined if call not answered.
     * @param session - Session to get the media stream from.
     */
    getRemoteMediaStream(session) {
      const sdh = session.sessionDescriptionHandler;
      if (!sdh) {
        return void 0;
      }
      if (!(sdh instanceof SessionDescriptionHandler)) {
        throw new Error("Session description handler not instance of web SessionDescriptionHandler");
      }
      return sdh.remoteMediaStream;
    }
    /**
     * The local audio track, if available.
     * @param session - Session to get track from.
     * @deprecated Use localMediaStream and get track from the stream.
     */
    getLocalAudioTrack(session) {
      var _a;
      return (_a = this.getLocalMediaStream(session)) === null || _a === void 0 ? void 0 : _a.getTracks().find((track) => track.kind === "audio");
    }
    /**
     * The local video track, if available.
     * @param session - Session to get track from.
     * @deprecated Use localMediaStream and get track from the stream.
     */
    getLocalVideoTrack(session) {
      var _a;
      return (_a = this.getLocalMediaStream(session)) === null || _a === void 0 ? void 0 : _a.getTracks().find((track) => track.kind === "video");
    }
    /**
     * The remote audio track, if available.
     * @param session - Session to get track from.
     * @deprecated Use remoteMediaStream and get track from the stream.
     */
    getRemoteAudioTrack(session) {
      var _a;
      return (_a = this.getRemoteMediaStream(session)) === null || _a === void 0 ? void 0 : _a.getTracks().find((track) => track.kind === "audio");
    }
    /**
     * The remote video track, if available.
     * @param session - Session to get track from.
     * @deprecated Use remoteMediaStream and get track from the stream.
     */
    getRemoteVideoTrack(session) {
      var _a;
      return (_a = this.getRemoteMediaStream(session)) === null || _a === void 0 ? void 0 : _a.getTracks().find((track) => track.kind === "video");
    }
    /**
     * Connect.
     * @remarks
     * If not started, starts the UserAgent connecting the WebSocket Transport.
     * Otherwise reconnects the UserAgent's WebSocket Transport.
     * Attempts will be made to reconnect as needed.
     */
    async connect() {
      this.logger.log(`Connecting UserAgent...`);
      this.shouldBeConnected = true;
      if (this.userAgent.state !== UserAgentState.Started) {
        return this.userAgent.start();
      }
      return this.userAgent.reconnect();
    }
    /**
     * Disconnect.
     * @remarks
     * If not stopped, stops the UserAgent disconnecting the WebSocket Transport.
     */
    async disconnect() {
      this.logger.log(`Disconnecting UserAgent...`);
      if (this.userAgent.state === UserAgentState.Stopped) {
        return Promise.resolve();
      }
      this.shouldBeConnected = false;
      this.shouldBeRegistered = false;
      this.registerer = void 0;
      return this.userAgent.stop();
    }
    /**
     * Return true if transport is connected.
     */
    isConnected() {
      return this.userAgent.isConnected();
    }
    /**
     * Start receiving incoming calls.
     * @remarks
     * Send a REGISTER request for the UserAgent's AOR.
     * Resolves when the REGISTER request is sent, otherwise rejects.
     * Attempts will be made to re-register as needed.
     */
    async register(registererRegisterOptions) {
      this.logger.log(`Registering UserAgent...`);
      this.shouldBeRegistered = true;
      if (registererRegisterOptions !== void 0) {
        this.registererRegisterOptions = Object.assign({}, registererRegisterOptions);
      }
      if (!this.registerer) {
        this.registerer = new Registerer(this.userAgent, this.registererOptions);
        this.registerer.stateChange.addListener((state) => {
          switch (state) {
            case RegistererState.Initial:
              break;
            case RegistererState.Registered:
              if (this.delegate && this.delegate.onRegistered) {
                this.delegate.onRegistered();
              }
              break;
            case RegistererState.Unregistered:
              if (this.delegate && this.delegate.onUnregistered) {
                this.delegate.onUnregistered();
              }
              if (this.shouldBeRegistered) {
                this.attemptRegistration();
              }
              break;
            case RegistererState.Terminated:
              break;
            default:
              throw new Error("Unknown registerer state.");
          }
        });
      }
      return this.attemptRegistration(true);
    }
    /**
     * Stop receiving incoming calls.
     * @remarks
     * Send an un-REGISTER request for the UserAgent's AOR.
     * Resolves when the un-REGISTER request is sent, otherwise rejects.
     */
    async unregister(registererUnregisterOptions) {
      this.logger.log(`Unregistering UserAgent...`);
      this.shouldBeRegistered = false;
      if (!this.registerer) {
        this.logger.warn(`No registerer to unregister.`);
        return Promise.resolve();
      }
      return this.registerer.unregister(registererUnregisterOptions).then(() => {
        return;
      });
    }
    /**
     * Make an outgoing call.
     * @remarks
     * Send an INVITE request to create a new Session.
     * Resolves when the INVITE request is sent, otherwise rejects.
     * Use `onCallAnswered` delegate method to determine if Session is established.
     * @param destination - The target destination to call. A SIP address to send the INVITE to.
     * @param inviterOptions - Optional options for Inviter constructor.
     * @param inviterInviteOptions - Optional options for Inviter.invite().
     */
    async call(destination, inviterOptions, inviterInviteOptions) {
      this.logger.log(`Beginning Session...`);
      const maxSessions = this.options.maxSimultaneousSessions;
      if (maxSessions !== 0 && this.managedSessions.length > maxSessions) {
        return Promise.reject(new Error("Maximum number of sessions already exists."));
      }
      const target = UserAgent.makeURI(destination);
      if (!target) {
        return Promise.reject(new Error(`Failed to create a valid URI from "${destination}"`));
      }
      if (!inviterOptions) {
        inviterOptions = {};
      }
      if (!inviterOptions.sessionDescriptionHandlerOptions) {
        inviterOptions.sessionDescriptionHandlerOptions = {};
      }
      if (!inviterOptions.sessionDescriptionHandlerOptions.constraints) {
        inviterOptions.sessionDescriptionHandlerOptions.constraints = this.constraints;
      }
      if (inviterOptions.earlyMedia) {
        inviterInviteOptions = inviterInviteOptions || {};
        inviterInviteOptions.requestDelegate = inviterInviteOptions.requestDelegate || {};
        const existingOnProgress = inviterInviteOptions.requestDelegate.onProgress;
        inviterInviteOptions.requestDelegate.onProgress = (response) => {
          if (response.message.statusCode === 183) {
            this.setupRemoteMedia(inviter);
          }
          existingOnProgress && existingOnProgress(response);
        };
      }
      if (this.options.iceStopWaitingOnServerReflexive) {
        inviterOptions.delegate = inviterOptions.delegate || {};
        inviterOptions.delegate.onSessionDescriptionHandler = (sessionDescriptionHandler) => {
          if (!(sessionDescriptionHandler instanceof SessionDescriptionHandler)) {
            throw new Error("Session description handler not instance of SessionDescriptionHandler");
          }
          sessionDescriptionHandler.peerConnectionDelegate = {
            onicecandidate: (event) => {
              var _a;
              if (((_a = event.candidate) === null || _a === void 0 ? void 0 : _a.type) === "srflx") {
                this.logger.log(`[${inviter.id}] Found srflx ICE candidate, stop waiting...`);
                const sdh = sessionDescriptionHandler;
                sdh.iceGatheringComplete();
              }
            }
          };
        };
      }
      const inviter = new Inviter(this.userAgent, target, inviterOptions);
      return this.sendInvite(inviter, inviterOptions, inviterInviteOptions).then(() => {
        return inviter;
      });
    }
    /**
     * Hangup a call.
     * @param session - Session to hangup.
     * @remarks
     * Send a BYE request, CANCEL request or reject response to end the current Session.
     * Resolves when the request/response is sent, otherwise rejects.
     * Use `onCallHangup` delegate method to determine if and when call is ended.
     */
    async hangup(session) {
      this.logger.log(`[${session.id}] Hangup...`);
      if (!this.sessionExists(session)) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.terminate(session);
    }
    /**
     * Answer an incoming call.
     * @param session - Session to answer.
     * @remarks
     * Accept an incoming INVITE request creating a new Session.
     * Resolves with the response is sent, otherwise rejects.
     * Use `onCallAnswered` delegate method to determine if and when call is established.
     * @param invitationAcceptOptions - Optional options for Inviter.accept().
     */
    async answer(session, invitationAcceptOptions) {
      this.logger.log(`[${session.id}] Accepting Invitation...`);
      if (!this.sessionExists(session)) {
        return Promise.reject(new Error("Session does not exist."));
      }
      if (!(session instanceof Invitation)) {
        return Promise.reject(new Error("Session not instance of Invitation."));
      }
      if (!invitationAcceptOptions) {
        invitationAcceptOptions = {};
      }
      if (!invitationAcceptOptions.sessionDescriptionHandlerOptions) {
        invitationAcceptOptions.sessionDescriptionHandlerOptions = {};
      }
      if (!invitationAcceptOptions.sessionDescriptionHandlerOptions.constraints) {
        invitationAcceptOptions.sessionDescriptionHandlerOptions.constraints = this.constraints;
      }
      return session.accept(invitationAcceptOptions);
    }
    /**
     * Decline an incoming call.
     * @param session - Session to decline.
     * @remarks
     * Reject an incoming INVITE request.
     * Resolves with the response is sent, otherwise rejects.
     * Use `onCallHangup` delegate method to determine if and when call is ended.
     */
    async decline(session) {
      this.logger.log(`[${session.id}] Rejecting Invitation...`);
      if (!this.sessionExists(session)) {
        return Promise.reject(new Error("Session does not exist."));
      }
      if (!(session instanceof Invitation)) {
        return Promise.reject(new Error("Session not instance of Invitation."));
      }
      return session.reject();
    }
    /**
     * Hold call
     * @param session - Session to hold.
     * @remarks
     * Send a re-INVITE with new offer indicating "hold".
     * Resolves when the re-INVITE request is sent, otherwise rejects.
     * Use `onCallHold` delegate method to determine if request is accepted or rejected.
     * See: https://tools.ietf.org/html/rfc6337
     */
    async hold(session) {
      this.logger.log(`[${session.id}] Holding session...`);
      return this.setHold(session, true);
    }
    /**
     * Unhold call.
     * @param session - Session to unhold.
     * @remarks
     * Send a re-INVITE with new offer indicating "unhold".
     * Resolves when the re-INVITE request is sent, otherwise rejects.
     * Use `onCallHold` delegate method to determine if request is accepted or rejected.
     * See: https://tools.ietf.org/html/rfc6337
     */
    async unhold(session) {
      this.logger.log(`[${session.id}] Unholding session...`);
      return this.setHold(session, false);
    }
    /**
     * Hold state.
     * @param session - Session to check.
     * @remarks
     * True if session is on hold.
     */
    isHeld(session) {
      const managedSession = this.sessionManaged(session);
      return managedSession ? managedSession.held : false;
    }
    /**
     * Mute call.
     * @param session - Session to mute.
     * @remarks
     * Disable sender's media tracks.
     */
    mute(session) {
      this.logger.log(`[${session.id}] Disabling media tracks...`);
      this.setMute(session, true);
    }
    /**
     * Unmute call.
     * @param session - Session to unmute.
     * @remarks
     * Enable sender's media tracks.
     */
    unmute(session) {
      this.logger.log(`[${session.id}] Enabling media tracks...`);
      this.setMute(session, false);
    }
    /**
     * Mute state.
     * @param session - Session to check.
     * @remarks
     * True if sender's media track is disabled.
     */
    isMuted(session) {
      const managedSession = this.sessionManaged(session);
      return managedSession ? managedSession.muted : false;
    }
    /**
     * Send DTMF.
     * @param session - Session to send on.
     * @remarks
     * Send an INFO request with content type application/dtmf-relay.
     * @param tone - Tone to send.
     */
    async sendDTMF(session, tone) {
      this.logger.log(`[${session.id}] Sending DTMF...`);
      if (!/^[0-9A-D#*,]$/.exec(tone)) {
        return Promise.reject(new Error("Invalid DTMF tone."));
      }
      if (!this.sessionExists(session)) {
        return Promise.reject(new Error("Session does not exist."));
      }
      this.logger.log(`[${session.id}] Sending DTMF tone: ${tone}`);
      if (this.options.sendDTMFUsingSessionDescriptionHandler) {
        if (!session.sessionDescriptionHandler) {
          return Promise.reject(new Error("Session desciption handler undefined."));
        }
        if (!session.sessionDescriptionHandler.sendDtmf(tone)) {
          return Promise.reject(new Error("Failed to send DTMF"));
        }
        return Promise.resolve();
      } else {
        const dtmf = tone;
        const duration = 2e3;
        const body = {
          contentDisposition: "render",
          contentType: "application/dtmf-relay",
          content: "Signal=" + dtmf + "\r\nDuration=" + duration
        };
        const requestOptions = { body };
        return session.info({ requestOptions }).then(() => {
          return;
        });
      }
    }
    /**
     * Transfer.
     * @param session - Session with the transferee to transfer.
     * @param target - The referral target.
     * @remarks
     * If target is a Session this is an attended transfer completion (REFER with Replaces),
     * otherwise this is a blind transfer (REFER). Attempting an attended transfer
     * completion on a call that has not been answered will be rejected. To implement
     * an attended transfer with early completion, hangup the call with the target
     * and execute a blind transfer to the target.
     */
    async transfer(session, target, options) {
      this.logger.log(`[${session.id}] Referring session...`);
      if (target instanceof Session) {
        return session.refer(target, options).then(() => {
          return;
        });
      }
      const uri = UserAgent.makeURI(target);
      if (!uri) {
        return Promise.reject(new Error(`Failed to create a valid URI from "${target}"`));
      }
      return session.refer(uri, options).then(() => {
        return;
      });
    }
    /**
     * Send a message.
     * @remarks
     * Send a MESSAGE request.
     * @param destination - The target destination for the message. A SIP address to send the MESSAGE to.
     */
    async message(destination, message) {
      this.logger.log(`Sending message...`);
      const target = UserAgent.makeURI(destination);
      if (!target) {
        return Promise.reject(new Error(`Failed to create a valid URI from "${destination}"`));
      }
      return new Messager(this.userAgent, target, message).message();
    }
    /** Media constraints. */
    get constraints() {
      let constraints = { audio: true, video: false };
      if (this.options.media.constraints) {
        constraints = Object.assign({}, this.options.media.constraints);
      }
      return constraints;
    }
    /**
     * Attempt reconnection up to `reconnectionAttempts` times.
     * @param reconnectionAttempt - Current attempt number.
     */
    attemptReconnection(reconnectionAttempt = 1) {
      const reconnectionAttempts = this.options.reconnectionAttempts;
      const reconnectionDelay = this.options.reconnectionDelay;
      if (!this.shouldBeConnected) {
        this.logger.log(`Should not be connected currently`);
        return;
      }
      if (this.attemptingReconnection) {
        this.logger.log(`Reconnection attempt already in progress`);
      }
      if (reconnectionAttempt > reconnectionAttempts) {
        this.logger.log(`Reconnection maximum attempts reached`);
        return;
      }
      if (reconnectionAttempt === 1) {
        this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - trying`);
      } else {
        this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - trying in ${reconnectionDelay} seconds`);
      }
      this.attemptingReconnection = true;
      setTimeout(() => {
        if (!this.shouldBeConnected) {
          this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - aborted`);
          this.attemptingReconnection = false;
          return;
        }
        this.userAgent.reconnect().then(() => {
          this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - succeeded`);
          this.attemptingReconnection = false;
        }).catch((error) => {
          this.logger.log(`Reconnection attempt ${reconnectionAttempt} of ${reconnectionAttempts} - failed`);
          this.logger.error(error.message);
          this.attemptingReconnection = false;
          this.attemptReconnection(++reconnectionAttempt);
        });
      }, reconnectionAttempt === 1 ? 0 : reconnectionDelay * 1e3);
    }
    /**
     * Register to receive calls.
     * @param withoutDelay - If true attempt immediately, otherwise wait `registrationRetryInterval`.
     */
    attemptRegistration(withoutDelay = false) {
      this.logger.log(`Registration attempt ${withoutDelay ? "without delay" : ""}`);
      if (!this.shouldBeRegistered) {
        this.logger.log(`Should not be registered currently`);
        return Promise.resolve();
      }
      if (this.registrationAttemptTimeout !== void 0) {
        this.logger.log(`Registration attempt already in progress`);
        return Promise.resolve();
      }
      const _register = () => {
        if (!this.registerer) {
          this.logger.log(`Registerer undefined`);
          return Promise.resolve();
        }
        if (!this.isConnected()) {
          this.logger.log(`User agent not connected`);
          return Promise.resolve();
        }
        if (this.userAgent.state === UserAgentState.Stopped) {
          this.logger.log(`User agent stopped`);
          return Promise.resolve();
        }
        if (!this.options.registerGuard) {
          return this.registerer.register(this.registererRegisterOptions).then(() => {
            return;
          });
        }
        return this.options.registerGuard().catch((error) => {
          this.logger.log(`Register guard rejected will making registration attempt`);
          throw error;
        }).then((halt) => {
          if (halt || !this.registerer) {
            return Promise.resolve();
          }
          return this.registerer.register(this.registererRegisterOptions).then(() => {
            return;
          });
        });
      };
      const computeRegistrationTimeout = (lowerBound) => {
        const upperBound = lowerBound * 2;
        return 1e3 * (Math.random() * (upperBound - lowerBound) + lowerBound);
      };
      return new Promise((resolve, reject) => {
        this.registrationAttemptTimeout = setTimeout(() => {
          _register().then(() => {
            this.registrationAttemptTimeout = void 0;
            resolve();
          }).catch((error) => {
            this.registrationAttemptTimeout = void 0;
            if (error instanceof RequestPendingError) {
              resolve();
            } else {
              reject(error);
            }
          });
        }, withoutDelay ? 0 : computeRegistrationTimeout(this.options.registrationRetryInterval));
      });
    }
    /** Helper function to remove media from html elements. */
    cleanupMedia(session) {
      const managedSession = this.sessionManaged(session);
      if (!managedSession) {
        throw new Error("Managed session does not exist.");
      }
      if (managedSession.mediaLocal) {
        if (managedSession.mediaLocal.video) {
          managedSession.mediaLocal.video.srcObject = null;
          managedSession.mediaLocal.video.pause();
        }
      }
      if (managedSession.mediaRemote) {
        if (managedSession.mediaRemote.audio) {
          managedSession.mediaRemote.audio.srcObject = null;
          managedSession.mediaRemote.audio.pause();
        }
        if (managedSession.mediaRemote.video) {
          managedSession.mediaRemote.video.srcObject = null;
          managedSession.mediaRemote.video.pause();
        }
      }
    }
    /** Helper function to enable/disable media tracks. */
    enableReceiverTracks(session, enable) {
      if (!this.sessionExists(session)) {
        throw new Error("Session does not exist.");
      }
      const sessionDescriptionHandler = session.sessionDescriptionHandler;
      if (!(sessionDescriptionHandler instanceof SessionDescriptionHandler)) {
        throw new Error("Session's session description handler not instance of SessionDescriptionHandler.");
      }
      sessionDescriptionHandler.enableReceiverTracks(enable);
    }
    /** Helper function to enable/disable media tracks. */
    enableSenderTracks(session, enable) {
      if (!this.sessionExists(session)) {
        throw new Error("Session does not exist.");
      }
      const sessionDescriptionHandler = session.sessionDescriptionHandler;
      if (!(sessionDescriptionHandler instanceof SessionDescriptionHandler)) {
        throw new Error("Session's session description handler not instance of SessionDescriptionHandler.");
      }
      sessionDescriptionHandler.enableSenderTracks(enable);
    }
    /**
     * Setup session delegate and state change handler.
     * @param session - Session to setup.
     * @param referralInviterOptions - Options for any Inviter created as result of a REFER.
     */
    initSession(session, referralInviterOptions) {
      this.sessionAdd(session);
      if (this.delegate && this.delegate.onCallCreated) {
        this.delegate.onCallCreated(session);
      }
      session.stateChange.addListener((state) => {
        this.logger.log(`[${session.id}] Session state changed to ${state}`);
        switch (state) {
          case SessionState2.Initial:
            break;
          case SessionState2.Establishing:
            break;
          case SessionState2.Established:
            this.setupLocalMedia(session);
            this.setupRemoteMedia(session);
            if (this.delegate && this.delegate.onCallAnswered) {
              this.delegate.onCallAnswered(session);
            }
            break;
          case SessionState2.Terminating:
          // fall through
          case SessionState2.Terminated:
            if (this.sessionExists(session)) {
              this.cleanupMedia(session);
              this.sessionRemove(session);
              if (this.delegate && this.delegate.onCallHangup) {
                this.delegate.onCallHangup(session);
              }
            }
            break;
          default:
            throw new Error("Unknown session state.");
        }
      });
      session.delegate = session.delegate || {};
      session.delegate.onInfo = (info) => {
        var _a;
        if (((_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallDTMFReceived) === void 0) {
          info.reject();
          return;
        }
        const contentType = info.request.getHeader("content-type");
        if (!contentType || !/^application\/dtmf-relay/i.exec(contentType)) {
          info.reject();
          return;
        }
        const body = info.request.body.split("\r\n", 2);
        if (body.length !== 2) {
          info.reject();
          return;
        }
        let tone;
        const toneRegExp = /^(Signal\s*?=\s*?)([0-9A-D#*]{1})(\s)?.*/;
        if (body[0] !== void 0 && toneRegExp.test(body[0])) {
          tone = body[0].replace(toneRegExp, "$2");
        }
        if (!tone) {
          info.reject();
          return;
        }
        let duration;
        const durationRegExp = /^(Duration\s?=\s?)([0-9]{1,4})(\s)?.*/;
        if (body[1] !== void 0 && durationRegExp.test(body[1])) {
          duration = parseInt(body[1].replace(durationRegExp, "$2"), 10);
        }
        if (!duration) {
          info.reject();
          return;
        }
        info.accept().then(() => {
          if (this.delegate && this.delegate.onCallDTMFReceived) {
            if (!tone || !duration) {
              throw new Error("Tone or duration undefined.");
            }
            this.delegate.onCallDTMFReceived(session, tone, duration);
          }
        }).catch((error) => {
          this.logger.error(error.message);
        });
      };
      session.delegate.onRefer = (referral) => {
        referral.accept().then(() => this.sendInvite(referral.makeInviter(referralInviterOptions), referralInviterOptions)).catch((error) => {
          this.logger.error(error.message);
        });
      };
    }
    /**
     * Periodically send OPTIONS pings and disconnect when a ping fails.
     * @param requestURI - Request URI to target
     * @param fromURI - From URI
     * @param toURI - To URI
     */
    optionsPingRun(requestURI, fromURI, toURI) {
      if (this.options.optionsPingInterval < 1) {
        throw new Error("Invalid options ping interval.");
      }
      if (this.optionsPingRunning) {
        return;
      }
      this.optionsPingRunning = true;
      this.optionsPingTimeout = setTimeout(() => {
        this.optionsPingTimeout = void 0;
        const onPingSuccess = () => {
          this.optionsPingFailure = false;
          if (this.optionsPingRunning) {
            this.optionsPingRunning = false;
            this.optionsPingRun(requestURI, fromURI, toURI);
          }
        };
        const onPingFailure = () => {
          this.logger.error("OPTIONS ping failed");
          this.optionsPingFailure = true;
          this.optionsPingRunning = false;
          this.userAgent.transport.disconnect().catch((error) => this.logger.error(error));
        };
        const core = this.userAgent.userAgentCore;
        const message = core.makeOutgoingRequestMessage("OPTIONS", requestURI, fromURI, toURI, {});
        this.optionsPingRequest = core.request(message, {
          onAccept: () => {
            this.optionsPingRequest = void 0;
            onPingSuccess();
          },
          onReject: (response) => {
            this.optionsPingRequest = void 0;
            if (response.message.statusCode === 408 || response.message.statusCode === 503) {
              onPingFailure();
            } else {
              onPingSuccess();
            }
          }
        });
      }, this.options.optionsPingInterval * 1e3);
    }
    /**
     * Start sending OPTIONS pings.
     */
    optionsPingStart() {
      this.logger.log(`OPTIONS pings started`);
      let requestURI, fromURI, toURI;
      if (this.options.optionsPingRequestURI) {
        requestURI = UserAgent.makeURI(this.options.optionsPingRequestURI);
        if (!requestURI) {
          throw new Error("Failed to create Request URI.");
        }
        fromURI = this.userAgent.contact.uri.clone();
        toURI = this.userAgent.contact.uri.clone();
      } else if (this.options.aor) {
        const uri = UserAgent.makeURI(this.options.aor);
        if (!uri) {
          throw new Error("Failed to create URI.");
        }
        requestURI = uri.clone();
        requestURI.user = void 0;
        fromURI = uri.clone();
        toURI = uri.clone();
      } else {
        this.logger.error("You have enabled sending OPTIONS pings and as such you must provide either a) an AOR to register, or b) an RURI to use for the target of the OPTIONS ping requests. ");
        return;
      }
      this.optionsPingRun(requestURI, fromURI, toURI);
    }
    /**
     * Stop sending OPTIONS pings.
     */
    optionsPingStop() {
      this.logger.log(`OPTIONS pings stopped`);
      this.optionsPingRunning = false;
      this.optionsPingFailure = false;
      if (this.optionsPingRequest) {
        this.optionsPingRequest.dispose();
        this.optionsPingRequest = void 0;
      }
      if (this.optionsPingTimeout) {
        clearTimeout(this.optionsPingTimeout);
        this.optionsPingTimeout = void 0;
      }
    }
    /** Helper function to init send then send invite. */
    async sendInvite(inviter, inviterOptions, inviterInviteOptions) {
      this.initSession(inviter, inviterOptions);
      return inviter.invite(inviterInviteOptions).then(() => {
        this.logger.log(`[${inviter.id}] Sent INVITE`);
      });
    }
    /** Helper function to add a session to the ones we are managing. */
    sessionAdd(session) {
      const managedSession = this.options.managedSessionFactory(this, session);
      this.managedSessions.push(managedSession);
    }
    /** Helper function to check if the session is one we are managing. */
    sessionExists(session) {
      return this.sessionManaged(session) !== void 0;
    }
    /** Helper function to check if the session is one we are managing. */
    sessionManaged(session) {
      return this.managedSessions.find((el) => el.session.id === session.id);
    }
    /** Helper function to remoce a session from the ones we are managing. */
    sessionRemove(session) {
      this.managedSessions = this.managedSessions.filter((el) => el.session.id !== session.id);
    }
    /**
     * Puts Session on hold.
     * @param session - The session to set.
     * @param hold - Hold on if true, off if false.
     */
    async setHold(session, hold) {
      if (!this.sessionExists(session)) {
        return Promise.reject(new Error("Session does not exist."));
      }
      if (this.isHeld(session) === hold) {
        return Promise.resolve();
      }
      const sessionDescriptionHandler = session.sessionDescriptionHandler;
      if (!(sessionDescriptionHandler instanceof SessionDescriptionHandler)) {
        throw new Error("Session's session description handler not instance of SessionDescriptionHandler.");
      }
      const options = {
        requestDelegate: {
          onAccept: () => {
            const managedSession2 = this.sessionManaged(session);
            if (managedSession2 !== void 0) {
              managedSession2.held = hold;
              this.enableReceiverTracks(session, !managedSession2.held);
              this.enableSenderTracks(session, !managedSession2.held && !managedSession2.muted);
              if (this.delegate && this.delegate.onCallHold) {
                this.delegate.onCallHold(session, managedSession2.held);
              }
            }
          },
          onReject: () => {
            this.logger.warn(`[${session.id}] Re-invite request was rejected`);
            const managedSession2 = this.sessionManaged(session);
            if (managedSession2 !== void 0) {
              managedSession2.held = !hold;
              this.enableReceiverTracks(session, !managedSession2.held);
              this.enableSenderTracks(session, !managedSession2.held && !managedSession2.muted);
              if (this.delegate && this.delegate.onCallHold) {
                this.delegate.onCallHold(session, managedSession2.held);
              }
            }
          }
        }
      };
      const sessionDescriptionHandlerOptions = session.sessionDescriptionHandlerOptionsReInvite;
      sessionDescriptionHandlerOptions.hold = hold;
      session.sessionDescriptionHandlerOptionsReInvite = sessionDescriptionHandlerOptions;
      const managedSession = this.sessionManaged(session);
      if (!managedSession) {
        throw new Error("Managed session is undefiend.");
      }
      managedSession.held = hold;
      return session.invite(options).then(() => {
        const managedSession2 = this.sessionManaged(session);
        if (managedSession2 !== void 0) {
          this.enableReceiverTracks(session, !managedSession2.held);
          this.enableSenderTracks(session, !managedSession2.held && !managedSession2.muted);
        }
      }).catch((error) => {
        managedSession.held = !hold;
        if (error instanceof RequestPendingError) {
          this.logger.error(`[${session.id}] A hold request is already in progress.`);
        }
        throw error;
      });
    }
    /**
     * Puts Session on mute.
     * @param session - The session to mute.
     * @param mute - Mute on if true, off if false.
     */
    setMute(session, mute) {
      if (!this.sessionExists(session)) {
        this.logger.warn(`[${session.id}] A session is required to enabled/disable media tracks`);
        return;
      }
      if (session.state !== SessionState2.Established) {
        this.logger.warn(`[${session.id}] An established session is required to enable/disable media tracks`);
        return;
      }
      const managedSession = this.sessionManaged(session);
      if (managedSession !== void 0) {
        managedSession.muted = mute;
        this.enableSenderTracks(session, !managedSession.held && !managedSession.muted);
      }
    }
    /** Helper function to attach local media to html elements. */
    setupLocalMedia(session) {
      const managedSession = this.sessionManaged(session);
      if (!managedSession) {
        throw new Error("Managed session does not exist.");
      }
      const mediaLocal = typeof this.options.media.local === "function" ? this.options.media.local(session) : this.options.media.local;
      managedSession.mediaLocal = mediaLocal;
      const mediaElement = mediaLocal === null || mediaLocal === void 0 ? void 0 : mediaLocal.video;
      if (mediaElement) {
        const localStream = this.getLocalMediaStream(session);
        if (!localStream) {
          throw new Error("Local media stream undefiend.");
        }
        mediaElement.srcObject = localStream;
        mediaElement.volume = 0;
        mediaElement.play().catch((error) => {
          this.logger.error(`[${session.id}] Failed to play local media`);
          this.logger.error(error.message);
        });
      }
    }
    /** Helper function to attach remote media to html elements. */
    setupRemoteMedia(session) {
      const managedSession = this.sessionManaged(session);
      if (!managedSession) {
        throw new Error("Managed session does not exist.");
      }
      const mediaRemote = typeof this.options.media.remote === "function" ? this.options.media.remote(session) : this.options.media.remote;
      managedSession.mediaRemote = mediaRemote;
      const mediaElement = (mediaRemote === null || mediaRemote === void 0 ? void 0 : mediaRemote.video) || (mediaRemote === null || mediaRemote === void 0 ? void 0 : mediaRemote.audio);
      if (mediaElement) {
        const remoteStream = this.getRemoteMediaStream(session);
        if (!remoteStream) {
          throw new Error("Remote media stream undefiend.");
        }
        mediaElement.autoplay = true;
        mediaElement.srcObject = remoteStream;
        mediaElement.play().catch((error) => {
          this.logger.error(`[${session.id}] Failed to play remote media`);
          this.logger.error(error.message);
        });
        remoteStream.onaddtrack = () => {
          this.logger.log(`Remote media onaddtrack`);
          mediaElement.load();
          mediaElement.play().catch((error) => {
            this.logger.error(`[${session.id}] Failed to play remote media`);
            this.logger.error(error.message);
          });
        };
      }
    }
    /**
     * End a session.
     * @param session - The session to terminate.
     * @remarks
     * Send a BYE request, CANCEL request or reject response to end the current Session.
     * Resolves when the request/response is sent, otherwise rejects.
     * Use `onCallHangup` delegate method to determine if and when Session is terminated.
     */
    async terminate(session) {
      this.logger.log(`[${session.id}] Terminating...`);
      switch (session.state) {
        case SessionState2.Initial:
          if (session instanceof Inviter) {
            return session.cancel().then(() => {
              this.logger.log(`[${session.id}] Inviter never sent INVITE (canceled)`);
            });
          } else if (session instanceof Invitation) {
            return session.reject().then(() => {
              this.logger.log(`[${session.id}] Invitation rejected (sent 480)`);
            });
          } else {
            throw new Error("Unknown session type.");
          }
        case SessionState2.Establishing:
          if (session instanceof Inviter) {
            return session.cancel().then(() => {
              this.logger.log(`[${session.id}] Inviter canceled (sent CANCEL)`);
            });
          } else if (session instanceof Invitation) {
            return session.reject().then(() => {
              this.logger.log(`[${session.id}] Invitation rejected (sent 480)`);
            });
          } else {
            throw new Error("Unknown session type.");
          }
        case SessionState2.Established:
          return session.bye().then(() => {
            this.logger.log(`[${session.id}] Session ended (sent BYE)`);
          });
        case SessionState2.Terminating:
          break;
        case SessionState2.Terminated:
          break;
        default:
          throw new Error("Unknown state");
      }
      this.logger.log(`[${session.id}] Terminating in state ${session.state}, no action taken`);
      return Promise.resolve();
    }
  };

  // node_modules/sip.js/lib/platform/web/simple-user/simple-user.js
  var SimpleUser = class {
    /**
     * Constructs a new instance of the `SimpleUser` class.
     * @param server - SIP WebSocket Server URL.
     * @param options - Options bucket. See {@link SimpleUserOptions} for details.
     */
    constructor(server, options = {}) {
      this.session = void 0;
      this.delegate = options.delegate;
      this.options = Object.assign({}, options);
      const sessionManagerOptions = {
        aor: this.options.aor,
        delegate: {
          onCallAnswered: () => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallAnswered) === null || _b === void 0 ? void 0 : _b.call(_a);
          },
          onCallCreated: (session) => {
            var _a, _b;
            this.session = session;
            (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallCreated) === null || _b === void 0 ? void 0 : _b.call(_a);
          },
          onCallReceived: () => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallReceived) === null || _b === void 0 ? void 0 : _b.call(_a);
          },
          onCallHangup: () => {
            var _a, _b;
            this.session = void 0;
            ((_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallHangup) && ((_b = this.delegate) === null || _b === void 0 ? void 0 : _b.onCallHangup());
          },
          onCallHold: (s, held) => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallHold) === null || _b === void 0 ? void 0 : _b.call(_a, held);
          },
          onCallDTMFReceived: (s, tone, dur) => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onCallDTMFReceived) === null || _b === void 0 ? void 0 : _b.call(_a, tone, dur);
          },
          onMessageReceived: (message) => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onMessageReceived) === null || _b === void 0 ? void 0 : _b.call(_a, message.request.body);
          },
          onRegistered: () => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onRegistered) === null || _b === void 0 ? void 0 : _b.call(_a);
          },
          onUnregistered: () => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onUnregistered) === null || _b === void 0 ? void 0 : _b.call(_a);
          },
          onServerConnect: () => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onServerConnect) === null || _b === void 0 ? void 0 : _b.call(_a);
          },
          onServerDisconnect: () => {
            var _a, _b;
            return (_b = (_a = this.delegate) === null || _a === void 0 ? void 0 : _a.onServerDisconnect) === null || _b === void 0 ? void 0 : _b.call(_a);
          }
        },
        maxSimultaneousSessions: 1,
        media: this.options.media,
        reconnectionAttempts: this.options.reconnectionAttempts,
        reconnectionDelay: this.options.reconnectionDelay,
        registererOptions: this.options.registererOptions,
        sendDTMFUsingSessionDescriptionHandler: this.options.sendDTMFUsingSessionDescriptionHandler,
        userAgentOptions: this.options.userAgentOptions
      };
      this.sessionManager = new SessionManager(server, sessionManagerOptions);
      this.logger = this.sessionManager.userAgent.getLogger("sip.SimpleUser");
    }
    /**
     * Instance identifier.
     * @internal
     */
    get id() {
      return this.options.userAgentOptions && this.options.userAgentOptions.displayName || "Anonymous";
    }
    /** The local media stream. Undefined if call not answered. */
    get localMediaStream() {
      return this.session && this.sessionManager.getLocalMediaStream(this.session);
    }
    /** The remote media stream. Undefined if call not answered. */
    get remoteMediaStream() {
      return this.session && this.sessionManager.getRemoteMediaStream(this.session);
    }
    /**
     * The local audio track, if available.
     * @deprecated Use localMediaStream and get track from the stream.
     */
    get localAudioTrack() {
      return this.session && this.sessionManager.getLocalAudioTrack(this.session);
    }
    /**
     * The local video track, if available.
     * @deprecated Use localMediaStream and get track from the stream.
     */
    get localVideoTrack() {
      return this.session && this.sessionManager.getLocalVideoTrack(this.session);
    }
    /**
     * The remote audio track, if available.
     * @deprecated Use remoteMediaStream and get track from the stream.
     */
    get remoteAudioTrack() {
      return this.session && this.sessionManager.getRemoteAudioTrack(this.session);
    }
    /**
     * The remote video track, if available.
     * @deprecated Use remoteMediaStream and get track from the stream.
     */
    get remoteVideoTrack() {
      return this.session && this.sessionManager.getRemoteVideoTrack(this.session);
    }
    /**
     * Connect.
     * @remarks
     * Start the UserAgent's WebSocket Transport.
     */
    connect() {
      this.logger.log(`[${this.id}] Connecting UserAgent...`);
      return this.sessionManager.connect();
    }
    /**
     * Disconnect.
     * @remarks
     * Stop the UserAgent's WebSocket Transport.
     */
    disconnect() {
      this.logger.log(`[${this.id}] Disconnecting UserAgent...`);
      return this.sessionManager.disconnect();
    }
    /**
     * Return true if connected.
     */
    isConnected() {
      return this.sessionManager.isConnected();
    }
    /**
     * Start receiving incoming calls.
     * @remarks
     * Send a REGISTER request for the UserAgent's AOR.
     * Resolves when the REGISTER request is sent, otherwise rejects.
     */
    register(registererRegisterOptions) {
      this.logger.log(`[${this.id}] Registering UserAgent...`);
      return this.sessionManager.register(registererRegisterOptions);
    }
    /**
     * Stop receiving incoming calls.
     * @remarks
     * Send an un-REGISTER request for the UserAgent's AOR.
     * Resolves when the un-REGISTER request is sent, otherwise rejects.
     */
    unregister(registererUnregisterOptions) {
      this.logger.log(`[${this.id}] Unregistering UserAgent...`);
      return this.sessionManager.unregister(registererUnregisterOptions);
    }
    /**
     * Make an outgoing call.
     * @remarks
     * Send an INVITE request to create a new Session.
     * Resolves when the INVITE request is sent, otherwise rejects.
     * Use `onCallAnswered` delegate method to determine if Session is established.
     * @param destination - The target destination to call. A SIP address to send the INVITE to.
     * @param inviterOptions - Optional options for Inviter constructor.
     * @param inviterInviteOptions - Optional options for Inviter.invite().
     */
    call(destination, inviterOptions, inviterInviteOptions) {
      this.logger.log(`[${this.id}] Beginning Session...`);
      if (this.session) {
        return Promise.reject(new Error("Session already exists."));
      }
      return this.sessionManager.call(destination, inviterOptions, inviterInviteOptions).then(() => {
        return;
      });
    }
    /**
     * Hangup a call.
     * @remarks
     * Send a BYE request, CANCEL request or reject response to end the current Session.
     * Resolves when the request/response is sent, otherwise rejects.
     * Use `onCallHangup` delegate method to determine if and when call is ended.
     */
    hangup() {
      this.logger.log(`[${this.id}] Hangup...`);
      if (!this.session) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.sessionManager.hangup(this.session).then(() => {
        this.session = void 0;
      });
    }
    /**
     * Answer an incoming call.
     * @remarks
     * Accept an incoming INVITE request creating a new Session.
     * Resolves with the response is sent, otherwise rejects.
     * Use `onCallAnswered` delegate method to determine if and when call is established.
     * @param invitationAcceptOptions - Optional options for Inviter.accept().
     */
    answer(invitationAcceptOptions) {
      this.logger.log(`[${this.id}] Accepting Invitation...`);
      if (!this.session) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.sessionManager.answer(this.session, invitationAcceptOptions);
    }
    /**
     * Decline an incoming call.
     * @remarks
     * Reject an incoming INVITE request.
     * Resolves with the response is sent, otherwise rejects.
     * Use `onCallHangup` delegate method to determine if and when call is ended.
     */
    decline() {
      this.logger.log(`[${this.id}] rejecting Invitation...`);
      if (!this.session) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.sessionManager.decline(this.session);
    }
    /**
     * Hold call
     * @remarks
     * Send a re-INVITE with new offer indicating "hold".
     * Resolves when the re-INVITE request is sent, otherwise rejects.
     * Use `onCallHold` delegate method to determine if request is accepted or rejected.
     * See: https://tools.ietf.org/html/rfc6337
     */
    hold() {
      this.logger.log(`[${this.id}] holding session...`);
      if (!this.session) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.sessionManager.hold(this.session);
    }
    /**
     * Unhold call.
     * @remarks
     * Send a re-INVITE with new offer indicating "unhold".
     * Resolves when the re-INVITE request is sent, otherwise rejects.
     * Use `onCallHold` delegate method to determine if request is accepted or rejected.
     * See: https://tools.ietf.org/html/rfc6337
     */
    unhold() {
      this.logger.log(`[${this.id}] unholding session...`);
      if (!this.session) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.sessionManager.unhold(this.session);
    }
    /**
     * Hold state.
     * @remarks
     * True if session is on hold.
     */
    isHeld() {
      return this.session ? this.sessionManager.isHeld(this.session) : false;
    }
    /**
     * Mute call.
     * @remarks
     * Disable sender's media tracks.
     */
    mute() {
      this.logger.log(`[${this.id}] disabling media tracks...`);
      return this.session && this.sessionManager.mute(this.session);
    }
    /**
     * Unmute call.
     * @remarks
     * Enable sender's media tracks.
     */
    unmute() {
      this.logger.log(`[${this.id}] enabling media tracks...`);
      return this.session && this.sessionManager.unmute(this.session);
    }
    /**
     * Mute state.
     * @remarks
     * True if sender's media track is disabled.
     */
    isMuted() {
      return this.session ? this.sessionManager.isMuted(this.session) : false;
    }
    /**
     * Send DTMF.
     * @remarks
     * Send an INFO request with content type application/dtmf-relay.
     * @param tone - Tone to send.
     */
    sendDTMF(tone) {
      this.logger.log(`[${this.id}] sending DTMF...`);
      if (!this.session) {
        return Promise.reject(new Error("Session does not exist."));
      }
      return this.sessionManager.sendDTMF(this.session, tone);
    }
    /**
     * Send a message.
     * @remarks
     * Send a MESSAGE request.
     * @param destination - The target destination for the message. A SIP address to send the MESSAGE to.
     */
    message(destination, message) {
      this.logger.log(`[${this.id}] sending message...`);
      return this.sessionManager.message(destination, message);
    }
  };

  // node_modules/sip.js/lib/index.js
  var version = LIBRARY_VERSION;
  var name = "sip.js";
  return __toCommonJS(entry_exports);
})();
