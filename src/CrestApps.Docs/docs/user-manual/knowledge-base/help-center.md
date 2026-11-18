---
sidebar_label: The Help Center
title: The Help Center and Its Widgets
description: "What readers see at /help and on each article: the search box at the top, the categories beside the page and related articles after the article, placed as widgets by the Knowledge Base recipe."
technical_manual:
  - modules/knowledge-base/widgets-and-recipe
---

The help center is at `/help` on your site. It shows the articles by category, each category with its most helpful articles, and every article has its own page under `/help/`. Readers only ever see the articles they may open.

| | |
| --- | --- |
| **Menu** | Design > Widgets (to move its widgets); the help center itself is at `/help` on your site |
| **Permission** | **Manage layers** to move or remove widgets |
| **Feature** | Knowledge Base, Widgets, Layers |

<AskYourAdmin />

## The widgets

Three widgets make the help center's page and an article's page look alike:

| Widget | Shows | Where the recipe puts it |
| --- | --- | --- |
| **Help center search** | A search box that searches the articles. On a category's page it searches within the category. | Above the page (zone **KnowledgeBaseTop**) |
| **Knowledge base categories** | The categories, with how many articles the reader may open in each. The category of the page, or of the article, is picked. | Beside the page (zone **KnowledgeBaseSidebar**) |
| **Related articles** | On an article's page, other articles about the same thing: those of its category first, most helpful first, then those whose title or summary share its words. It never lists the article itself, and shows nothing on other pages. | After the article (zone **KnowledgeBaseAfterContent**) |

The help center's page and each article's page show these three zones themselves, so the widgets show whatever theme your site uses.

## Set it up with the recipe

1. Go to **Configuration > Recipes**.
2. Find **Knowledge Base** and select **Run**.

The recipe turns on the Knowledge Base, Widgets and Layers. It adds the three zones under **Design > Settings > Zones**, keeping your own. It adds a **Help center** layer for `/help` and the pages under it, and places the three widgets on it. Run it again at any time: it changes nothing that is already there.

Without the recipe, the help center still shows its own search box and categories, and articles show without the widgets.

## Move or change the widgets

Go to **Design > Widgets** and find the **Help center** layer.

- To show related articles somewhere else, drag the **Related articles** widget to another zone. To show fewer or more of them, edit it and change **Articles to show**.
- To remove a widget, delete it. On the help center's page, removing the search or the categories widget brings back the page's own.
- To use a zone of your theme instead, such as its sidebar, move the widget there. The widget then shows on the pages of the **Help center** layer wherever your theme shows that zone.

## Was this helpful?

Each article ends with **Was this helpful?**. A reader who answers **No** may say what was missing. Each browser answers once per article. The most helpful articles come first in the help center. See [Feedback](./feedback.md) for what readers answered.
