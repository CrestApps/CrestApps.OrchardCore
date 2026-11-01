// @ts-check
/*
 * WebMCP page tools.
 *
 * Tools that work on the page the user sees: what the page is, where its menus lead, opening another page of the same
 * site, and reading and filling its forms. They never submit a form and never leave the site, so the user always
 * reviews and saves the changes an agent makes.
 */
import { textResult } from './webmcp-results.js';

var MAX_HEADINGS = 20;
var MAX_LINKS = 200;
var MAX_FIELDS = 200;
var MAX_VALUE_LENGTH = 2000;

// The selectors the tools read the page with.
var selectors = {
  description: 'meta[name="description"]',
  headings: 'h1, h2',
  links: 'nav a[href], #ta-left-sidebar a[href], .ta-navbar a[href], header a[href]',
  fields: 'form input, form select, form textarea',
};

// Field types an agent never reads or fills.
var SKIPPED_TYPES = ['hidden', 'password', 'file', 'submit', 'button', 'reset', 'image'];
var SKIPPED_NAMES = ['__RequestVerificationToken'];

function cleanText(value) {
  return (value == null ? '' : String(value)).replace(/\s+/g, ' ').trim();
}

function json(data) {
  return textResult(JSON.stringify(data));
}

function resolveSameOrigin(href, loc) {
  if (!href) {
    return null;
  }

  try {
    var url = new URL(href, loc.href);

    return url.origin === loc.origin ? url : null;
  } catch (error) {
    return null;
  }
}

function fieldType(field) {
  var tag = (field.tagName || '').toLowerCase();

  if (tag === 'select' || tag === 'textarea') {
    return tag;
  }

  return (field.type || 'text').toLowerCase();
}

function fieldLabel(field) {
  if (field.labels && field.labels.length > 0) {
    var label = cleanText(field.labels[0].textContent);

    if (label) {
      return label;
    }
  }

  return cleanText(field.getAttribute('aria-label')) || cleanText(field.getAttribute('placeholder'));
}

function isListed(field) {
  return !field.disabled &&
    SKIPPED_TYPES.indexOf(fieldType(field)) < 0 &&
    SKIPPED_NAMES.indexOf(field.name) < 0;
}

function listFields(doc) {
  var fields = [];
  var all = doc.querySelectorAll(selectors.fields);

  for (var i = 0; i < all.length && fields.length < MAX_FIELDS; i++) {
    if (isListed(all[i])) {
      fields.push(all[i]);
    }
  }

  return fields;
}

function describeField(field) {
  var type = fieldType(field);
  var description = {
    key: field.name || field.id,
    name: field.name,
    id: field.id,
    label: fieldLabel(field),
    type: type,
    value: type === 'checkbox' || type === 'radio'
      ? !!field.checked
      : String(field.value == null ? '' : field.value).substring(0, MAX_VALUE_LENGTH),
    required: !!field.required,
  };

  if (type === 'select') {
    description.options = Array.prototype.map.call(field.options || [], function (option) {
      return { value: option.value, text: cleanText(option.text), selected: !!option.selected };
    });
  }

  return description;
}

function findField(doc, key) {
  var wanted = cleanText(key).toLowerCase();

  if (!wanted) {
    return null;
  }

  var fields = listFields(doc);
  var matchers = [
    function (field) {
      return (field.name || '').toLowerCase() === wanted;
    },
    function (field) {
      return (field.id || '').toLowerCase() === wanted;
    },
    function (field) {
      return fieldLabel(field).toLowerCase() === wanted;
    },
  ];

  for (var m = 0; m < matchers.length; m++) {
    for (var i = 0; i < fields.length; i++) {
      if (matchers[m](fields[i])) {
        return fields[i];
      }
    }
  }

  return null;
}

function raise(field, type) {
  field.dispatchEvent(new Event(type, { bubbles: true }));
}

/**
 * Creates the page tools for a document.
 *
 * @param {{ doc: any, loc: any, isAdmin?: boolean }} options The document, its location, and whether it is an admin page.
 */
function createPageTools(options) {
  var doc = options.doc;
  var loc = options.loc;

  return [
    {
      name: 'page_get_info',
      title: 'Get page info',
      description: 'Describes the page the user has open: its title, address, description and main headings, and whether it is an admin page.',
      inputSchema: { type: 'object', properties: {} },
      annotations: { readOnlyHint: true },
      execute: function () {
        var meta = doc.querySelector(selectors.description);
        var headings = [];
        var all = doc.querySelectorAll(selectors.headings);

        for (var i = 0; i < all.length && headings.length < MAX_HEADINGS; i++) {
          var text = cleanText(all[i].textContent);

          if (text) {
            headings.push({ level: (all[i].tagName || '').toUpperCase() === 'H1' ? 1 : 2, text: text });
          }
        }

        return json({
          title: doc.title,
          url: loc.href,
          description: meta ? meta.getAttribute('content') : null,
          headings: headings,
          isAdmin: !!options.isAdmin,
        });
      },
    },
    {
      name: 'page_list_links',
      title: 'List page links',
      description: 'Lists the links in the page\'s menus and navigation that stay on this site, with their text. Use it to find the page to open for a task.',
      inputSchema: {
        type: 'object',
        properties: {
          filter: { type: 'string', description: 'Only list links whose text contains these words.' },
        },
      },
      annotations: { readOnlyHint: true },
      execute: function (input) {
        var filter = cleanText(input && input.filter).toLowerCase();
        var seen = new Set();
        var links = [];
        var all = doc.querySelectorAll(selectors.links);

        for (var i = 0; i < all.length && links.length < MAX_LINKS; i++) {
          var url = resolveSameOrigin(all[i].getAttribute('href'), loc);
          var text = cleanText(all[i].textContent);

          if (!url || !text || seen.has(url.href) || (filter && text.toLowerCase().indexOf(filter) < 0)) {
            continue;
          }

          seen.add(url.href);
          links.push({ text: text, url: url.href });
        }

        return json({ links: links });
      },
    },
    {
      name: 'page_navigate',
      title: 'Open page',
      description: 'Opens another page of this site in the current tab. Only addresses on this site are allowed. Unsaved changes on the current page are lost.',
      inputSchema: {
        type: 'object',
        properties: {
          url: { type: 'string', description: 'The address to open, relative to the site or absolute on the same site.' },
        },
        required: ['url'],
      },
      execute: function (input) {
        var url = resolveSameOrigin(input && input.url, loc);

        if (!url) {
          return textResult('Only addresses on this site can be opened.', true);
        }

        loc.assign(url.href);

        return json({ navigatedTo: url.href });
      },
    },
    {
      name: 'page_list_form_fields',
      title: 'List form fields',
      description: 'Lists the fields of the forms on the page: name, label, type, current value, whether it is required and, for lists, the choices. Hidden, password and file fields are not listed.',
      inputSchema: { type: 'object', properties: {} },
      annotations: { readOnlyHint: true },
      execute: function () {
        return json({ fields: listFields(doc).map(describeField) });
      },
    },
    {
      name: 'page_set_form_field',
      title: 'Set form field',
      description: 'Puts a value into one form field on the page, found by its name, id or label. It does not save or submit the form: the user reviews the change and saves it.',
      inputSchema: {
        type: 'object',
        properties: {
          field: { type: 'string', description: 'The field\'s name, id or label, as page_list_form_fields lists it.' },
          value: { description: 'The value. For a check box, true or false. For a list, a choice\'s value or text.' },
        },
        required: ['field', 'value'],
      },
      execute: function (input) {
        var field = findField(doc, input && input.field);

        if (!field) {
          return textResult('No editable field on the page is named "' + cleanText(input && input.field) + '".', true);
        }

        var type = fieldType(field);
        var value = input.value;

        if (type === 'checkbox' || type === 'radio') {
          field.checked = value === true || String(value).toLowerCase() === 'true';
        } else if (type === 'select') {
          var wanted = cleanText(value).toLowerCase();
          var option = Array.prototype.find.call(field.options || [], function (candidate) {
            return String(candidate.value).toLowerCase() === wanted || cleanText(candidate.text).toLowerCase() === wanted;
          });

          if (!option) {
            return textResult('The list "' + fieldLabel(field) + '" has no choice "' + cleanText(value) + '".', true);
          }

          field.value = option.value;
        } else {
          field.value = value == null ? '' : String(value);
        }

        raise(field, 'input');
        raise(field, 'change');

        return json({ field: describeField(field) });
      },
    },
  ];
}

/**
 * Registers the page tools with the page's list of tools.
 *
 * @param {{ registerTool: Function }} api The page's list of tools.
 * @param {{ doc: any, loc: any, isAdmin?: boolean }} options The document, its location, and whether it is an admin page.
 */
function register(api, options) {
  createPageTools(options).forEach(function (tool) {
    api.registerTool(tool);
  });
}

globalThis.CrestAppsWebMcpPageTools = {
  selectors: selectors,
  createPageTools: createPageTools,
  register: register,
};

if (typeof window !== 'undefined' && window.crestAppsWebMcp) {
  register(window.crestAppsWebMcp, {
    doc: document,
    loc: window.location,
    isAdmin: !!(window.crestAppsWebMcp.config && window.crestAppsWebMcp.config.isAdmin),
  });
}
