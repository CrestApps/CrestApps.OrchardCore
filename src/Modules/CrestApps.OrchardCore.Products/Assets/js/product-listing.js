/*
 * Product listing pages.
 *
 * The listing works without this script: the filters are an ordinary GET form and every chip, sort, layout and
 * page link is a real URL. The script only makes it quicker. On wide screens a filter applies as soon as it
 * changes, and the results are fetched and swapped in place, with the address bar kept in step so the back
 * button and bookmarks still work. On narrow screens the filters sit in an off-canvas panel, so they are applied
 * with its "Show results" button instead of on every tap.
 *
 * A listing set to "Load more" or infinite scroll keeps its numbered page links for visitors without JavaScript.
 * The script hides them and shows a button that adds the next page's products below the ones shown; with infinite
 * scroll the button presses itself when it comes into view.
 */
(function () {
  'use strict';

  var wideScreen = window.matchMedia('(min-width: 992px)');
  var pending = null;

  function containerOf(element) {
    return element.closest('[data-product-listing]');
  }

  function statusOf(container) {
    var id = container.getAttribute('data-product-listing');
    return document.querySelector('[data-product-listing-status="' + id + '"]');
  }

  function formUrl(form) {
    var params = new URLSearchParams();

    new FormData(form).forEach(function (value, key) {
      if (typeof value !== 'string' || value.trim() === '') {
        return;
      }

      // A sort, page size or layout that is already the default stays out of the address, so the unfiltered
      // listing keeps one URL.
      var element = form.elements.namedItem(key);

      if (element && element.getAttribute && element.getAttribute('data-default') === value) {
        return;
      }

      params.append(key, value.trim());
    });

    var query = params.toString();
    var action = form.getAttribute('action') || window.location.pathname;

    return query ? action + '?' + query : action;
  }

  function load(container, url, push) {
    if (pending) {
      pending.abort();
    }

    pending = new AbortController();
    container.setAttribute('aria-busy', 'true');
    container.classList.add('opacity-50');

    fetch(url, { signal: pending.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(function (response) {
        if (!response.ok) {
          throw new Error('The listing could not be loaded.');
        }

        return response.text();
      })
      .then(function (html) {
        var id = container.getAttribute('data-product-listing');
        var page = new DOMParser().parseFromString(html, 'text/html');
        var next = page.querySelector('[data-product-listing="' + id + '"]');

        if (!next) {
          window.location.assign(url);
          return;
        }

        container.innerHTML = next.innerHTML;
        container.removeAttribute('data-product-listing-appended');
        enhance(container);

        // The breadcrumb follows the categories picked, and it sits outside the listing, in the page header.
        var breadcrumbSelector = '[data-product-listing-breadcrumb="' + id + '"]';
        var breadcrumb = document.querySelector(breadcrumbSelector);
        var nextBreadcrumb = page.querySelector(breadcrumbSelector);

        if (breadcrumb && nextBreadcrumb) {
          breadcrumb.innerHTML = nextBreadcrumb.innerHTML;
        }

        if (push) {
          window.history.pushState({ productListing: id }, '', url);
        }

        var status = statusOf(container);
        var summary = container.querySelector('[data-product-listing-summary]');

        if (status && summary) {
          status.textContent = summary.textContent.trim();
        }
      })
      .catch(function (error) {
        if (error.name !== 'AbortError') {
          window.location.assign(url);
        }
      })
      .finally(function () {
        container.removeAttribute('aria-busy');
        container.classList.remove('opacity-50');
      });
  }

  // Adds the next page's products below the ones shown, and moves the button and the page links on.
  function append(container, url, button) {
    if (container.hasAttribute('data-product-listing-appending')) {
      return;
    }

    container.setAttribute('data-product-listing-appending', '');
    button.classList.add('disabled');
    button.setAttribute('aria-disabled', 'true');

    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(function (response) {
        if (!response.ok) {
          throw new Error('The next page could not be loaded.');
        }

        return response.text();
      })
      .then(function (html) {
        var id = container.getAttribute('data-product-listing');
        var page = new DOMParser().parseFromString(html, 'text/html');
        var next = page.querySelector('[data-product-listing="' + id + '"]');
        var results = container.querySelector('.product-listing-results');
        var nextResults = next && next.querySelector('.product-listing-results');

        if (!results || !nextResults) {
          window.location.assign(url);
          return;
        }

        var added = Array.prototype.slice.call(nextResults.children);

        added.forEach(function (item) {
          results.appendChild(item);
        });

        replaceWith(container, next, '[data-product-listing-more]');
        replaceWith(container, next, '[data-product-listing-pages]');
        container.setAttribute('data-product-listing-appended', '');
        updateSummary(container, next);

        // The address names the last page shown, so a reload or a shared link starts from it.
        window.history.replaceState({ productListing: id }, '', url);

        var status = statusOf(container);
        var summary = container.querySelector('[data-product-listing-summary]');

        if (status && summary) {
          status.textContent = summary.textContent.trim();
        }

        // Keyboard and screen reader users carry on from the first product just added.
        var first = added[0] && added[0].querySelector('a[href]');

        if (first && button === document.activeElement) {
          first.focus({ preventScroll: true });
        }

        enhance(container);
      })
      .catch(function () {
        window.location.assign(url);
      })
      .finally(function () {
        container.removeAttribute('data-product-listing-appending');
      });
  }

  function replaceWith(container, next, selector) {
    var current = container.querySelector(selector);
    var replacement = next.querySelector(selector);

    if (current && replacement) {
      current.replaceWith(replacement);
    } else if (current) {
      current.remove();
    }
  }

  // "1–12 of 30" becomes "1–24 of 30" once the second page is added.
  function updateSummary(container, next) {
    var summary = container.querySelector('[data-product-listing-summary]');
    var nextSummary = next.querySelector('[data-product-listing-summary]');
    var template = summary && summary.getAttribute('data-template');

    if (!summary || !nextSummary || !template) {
      return;
    }

    var first = summary.getAttribute('data-first');
    var last = nextSummary.getAttribute('data-last');
    var total = nextSummary.getAttribute('data-total');

    summary.setAttribute('data-last', last);
    summary.textContent = template.replace('{0}', first).replace('{1}', last).replace('{2}', total);
  }

  var observer = 'IntersectionObserver' in window
    ? new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          var button = entry.target.querySelector('[data-product-listing-next]');
          var container = containerOf(entry.target);

          if (button && container) {
            append(container, button.getAttribute('href'), button);
          }
        }
      });
    }, { rootMargin: '400px 0px' })
    : null;

  function enhance(container) {
    container.querySelectorAll('[data-product-listing-apply]').forEach(function (button) {
      button.classList.add('d-lg-none');
    });

    // With "Load more" or infinite scroll the page links give way to the button, but they stay when a later page
    // was opened directly (a reload, a shared link), so the visitor can still go back to the first page.
    var pages = container.querySelector('[data-product-listing-pages="replaceable"]');

    if (pages && (container.hasAttribute('data-product-listing-appended') || pages.getAttribute('data-page') === '1')) {
      pages.classList.add('d-none');
    }

    var more = container.querySelector('[data-product-listing-more]');

    if (!more) {
      return;
    }

    more.classList.remove('d-none');

    if (more.getAttribute('data-product-listing-more') === 'infinite' && observer) {
      observer.observe(more);
    }
  }

  document.addEventListener('change', function (event) {
    var input = event.target;
    var form = input.form;

    if (!form || !form.hasAttribute('data-product-listing-form') || !wideScreen.matches) {
      return;
    }

    // A minimum or a maximum is applied when the visitor leaves the box or presses Enter, not on each digit.
    if (input.type === 'number' && event.type === 'change' && document.activeElement === input) {
      return;
    }

    var container = containerOf(form);

    if (container) {
      load(container, formUrl(form), true);
    }
  });

  document.addEventListener('submit', function (event) {
    var form = event.target;

    if (!form.hasAttribute('data-product-listing-form') || !wideScreen.matches) {
      return;
    }

    var container = containerOf(form);

    if (container) {
      event.preventDefault();
      load(container, formUrl(form), true);
    }
  });

  document.addEventListener('click', function (event) {
    var next = event.target.closest('a[data-product-listing-next]');

    if (next && !event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0) {
      var listing = containerOf(next);

      if (listing) {
        event.preventDefault();
        append(listing, next.getAttribute('href'), next);
      }

      return;
    }

    var link = event.target.closest('a[data-product-listing-link]');

    if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
      return;
    }

    var container = containerOf(link);

    if (container) {
      event.preventDefault();
      load(container, link.getAttribute('href'), true);
      container.scrollIntoView({ block: 'start', behavior: 'smooth' });
    }
  });

  window.addEventListener('popstate', function () {
    var container = document.querySelector('[data-product-listing]');

    if (container) {
      load(container, window.location.href, false);
    }
  });

  document.querySelectorAll('[data-product-listing]').forEach(enhance);
})();
