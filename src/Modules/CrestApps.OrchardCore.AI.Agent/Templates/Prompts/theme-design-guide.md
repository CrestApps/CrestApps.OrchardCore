---
Title: Theme Design Guide
Description: How Orchard Core themes, shape templates and site resources fit together, returned to the AI by the theme design overview tool
IsListable: false
Category: Theme Design
---

{% raw %}
How to design an Orchard Core theme with these tools.

Your changes and the draft:

- Every change goes to the user's theme draft. Only the user sees it, while previewing, until it is published.
- Preview before and after you change things: previewThemeDraftPage renders a page with the draft and returns its status, the stylesheets it loads with their size and whether they are yours, the scripts, the templates that drew each part (renderedTemplates, with the name a replacement must take) and the markup. Fix what is wrong and preview again. Never report a design as done before a preview shows it working.

First decide which kind of job it is:

- A restyle: the user wants the current theme changed, such as other colors, fonts, a warmer feel or a tweak to one part. Follow "Restyle a theme" below.
- A new theme: the user asks for a new, complete, fresh or different theme, a full redesign, or says it should not look like the current one. Follow "Build a new theme" below. A few lines of CSS on top of the current theme can't do this: the theme's own stylesheet is usually hundreds of kilobytes, and it keeps the old look.

Restyle a theme:

1. Keep the theme's Layout and every {% style %} and {% script %} line it has, such as {% style name: "theblogtheme" %}, {% style name: "font-awesome", version: "7" %} and {% script name: "theblogtheme", at: "Foot" %}. saveShapeTemplate refuses a Layout that drops one, unless it loads Bootstrap itself (a new theme) or names it in dropThemeResources.
2. Put your changes in one global stylesheet (a site resource, placement Global, target Frontend, themes set to the active front-end theme so it stays out of other themes), such as "site-theme". Your rules must be specific enough to win over the theme's (for example .masthead .site-heading h1 rather than h1).
3. Replace a template only where the markup must change, starting from the theme's own source (getThemeShapeSource).

Build a new theme:

1. Preview the home page "/" and one content item's page (such as a blog post). Write down every template in renderedTemplates that draws the page's content: the banner (such as Content__Blog), the list items (such as Content_Summary__BlogPost), the item page (such as Content__BlogPost), and the Layout.
2. Write a new Layout with your own header, menu area, main area and footer, using your own class names (never the theme's, such as masthead, site-heading or post-preview, which its stylesheet still styles).
   - Leave out the theme's own stylesheets and scripts (such as "theblogtheme"), and load Bootstrap and the icons instead, because the menu and other modules render Bootstrap markup: {% style name: "bootstrap" %}, {% script name: "bootstrap", at: "Foot" %} and {% style name: "font-awesome", version: "7" %}. A Layout that loads Bootstrap is saved as a new theme.
   - Put the menu shape inside <nav class="navbar navbar-expand-lg"> with a <button class="navbar-toggler" data-bs-toggle="collapse" data-bs-target="#navbarResponsive">: the menu renders <div class="collapse navbar-collapse" id="navbarResponsive">, which is hidden without them.
   - Load web fonts with a <link> in the head, before {% resources type: "Stylesheet" %}.
3. Replace every content template you wrote down in step 1, under the same name, with your own markup and class names. Keep the data each one shows (title, link, subtitle, date, author, terms, the body through {{ Model.Content | shape_render }}), and fill the Header zone from the banner's template with {% zone "Header" %}...{% endzone %}.
4. Write one complete stylesheet named after the design, such as "site-theme" (placement Global, target Frontend, themes set to the active front-end theme): colors and fonts as CSS variables on :root, the base text, links and headings, the header and menu (including the collapsed menu on phones), the hero or banner, the cards of lists, the item page, buttons, forms, the pager, the footer, and @media rules for phones. Expect several kilobytes (4 to 12 KB), not a few lines.
5. Preview the same two pages again. For a new theme the preview returns stillToDo: the parts the theme still draws and whether your stylesheet is too small. Do every item, then preview again, until stillToDo is gone and the menu is in the markup.

What a Layout must keep (the tools refuse a Layout without them):

- In the head: {% resources type: "Meta" %}, {% resources type: "HeadLink" %}, {% resources type: "Stylesheet" %} and {% resources type: "HeadScript" %}.
- {% render_body %} where the page's content goes.
- {% resources type: "FootScript" %} just before </body>.
- The zones pages fill: {% render_section "Header", required: false %}, {% render_section "Messages", required: false %} and {% render_section "Footer", required: false %}. Content templates fill them with {% zone "Header" %}...{% endzone %}.
- The page title: <title>{{ "PageTitle" | shape_new | shape_stringify }}</title>.

Shape template names:

- Layout: the page skeleton.
- Content: a content item's own page, for every content type. Content__BlogPost: only blog posts. Content_Summary__BlogPost: a blog post in a list (such as the blog's home page). Content_Summary: every item in a list.
- Content__Alias__{alias} and Content__Slug__{url}: one item by its alias or URL, with each dash written as a double underscore, each dot as an underscore and each slash as a double underscore (the alias about-us gives Content__Alias__about__us). To change only the home page, use {% if Request.Path == "/" %} in a template instead.
- Widget__Paragraph and Widget__{WidgetType}: a widget. Menu, MenuItem and MenuItemLink__LinkMenuItem: the menu.
- Names are case-insensitive. renderedTemplates gives the exact names a page uses; listThemeShapes lists them all, and getThemeShapeSource reads the theme's version of each.

Liquid that works in Orchard Core:

- The content item: Model.ContentItem. Its title: {{ Model.ContentItem.DisplayText }}. A field: {{ Model.ContentItem.Content.BlogPost.Subtitle.Text }} (Content.{TypeOrPart}.{Field}.Text, .Html, .Value or .Paths).
- Link to an item: {% a display_for: Model.ContentItem %}...{% enda %}, or {{ Model.ContentItem | display_url }}.
- The rest of what the item's parts render: {{ Model.Content | shape_render }} (and Model.Header, Model.Meta, Model.Tags when the theme uses them).
- Dates: {{ Model.ContentItem.PublishedUtc | local | date: "%B %d, %Y" }}.
- Text the site translates: {{ "Read more" | t }}.
- Site settings: {{ Site.SiteName }}. The request: {{ Request.Path }}, so {% if Request.Path == "/" %} is true on the home page only.
- Addresses of files and pages: {{ "~/TheBlogTheme/assets/img/home-bg.jpg" | href }}. A "~/" written straight into HTML, even inside a style attribute, is not resolved.
- Liquid only: there is no Razor (@...) and no C#. Close every {% if %}, {% for %}, {% a %} and {% zone %}.

Good design:

- Keep enough contrast between text and background, visible focus outlines on links and buttons, and real headings in order.
- Make it work on phones: fluid widths, flexbox or grid, a menu that collapses behind its toggle button, and images that never overflow (max-width: 100%).
- Put styles in the stylesheet with class names, not in inline style attributes.
- When a save or a preview reports a problem, fix it and save again in the same turn; do not stop at the first refusal.
{% endraw %}
