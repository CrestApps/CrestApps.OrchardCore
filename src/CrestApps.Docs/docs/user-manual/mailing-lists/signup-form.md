---
sidebar_label: Sign-up Form
title: Put a Newsletter Sign-up Form on a Page
description: "Add a Mailing List Sign-up widget to any page so visitors, signed in or not, can join one or more mailing lists, with a hidden trap, a challenge and a limit to keep robots out."
technical_manual:
  - modules/mailing-lists
---

A **Mailing List Sign-up** widget puts a newsletter sign-up form on a page of your site, such as the footer of every page. Visitors join one list, or tick the lists they want.

| | |
| --- | --- |
| **Menu** | **Design** → **Widgets** (layers), or a page's flow |
| **Permission** | Manage widgets, or edit the page |
| **Feature** | Mailing List Sign-up Widget |

<AskYourAdmin />

## Add the form

1. Create the list first, under **Mailing Lists** → **Lists**. See [Mailing Lists](index.md).
2. Add a **Mailing List Sign-up** widget where visitors should find it (a layer, or a page's flow).
3. Fill in the **Sign-up form** card (see below).
4. Publish.

| Field | What it does |
| --- | --- |
| **Mailing lists** | The lists the form offers. Tick at least one. With one list, visitors join it at once; with several, they tick the ones they want. A list that is off shows *(off)* and isn't offered. |
| **Heading** | A heading above the form, such as *Get our news*. Optional. |
| **Text** | A short text under the heading. Optional. |
| **Button label** | The button's text. Leave it empty for **Sign up**. |
| **Ask for the person's name** | Adds a **Your name** field. |
| **After signing up** | What the visitor reads once they sign up. Leave it empty for the default: *Thank you! If your address needs to be confirmed, we sent you an e-mail with a link.* |

## What visitors see

The form asks for **Your e-mail** (filled in for a signed-in visitor), and **Your name** when you ask for it. With several lists, it shows **Choose what you want to receive** with each list's name and description. It shows each list's **Consent text**.

After sending, the visitor reads your message from **After signing up**. It is the same whether the address is new, already on the list, or refused, so nobody can use the form to find out who is subscribed. Each list then does what its **Confirmation** card says: a confirmation e-mail, or a subscription at once.

The form shows a message instead when the visitor:

- enters an address that isn't valid: *Please enter a valid e-mail address.*
- doesn't pass the challenge: *Please confirm you're not a robot and send it again.*
- ticks no list: *Please choose at least one list.*

A signed-in visitor who signs up with the confirmed e-mail of their account is subscribed at once on a list that skips the confirmation for them.

The site keeps, as proof of consent, the list's consent text, the page the form was on, the time and the visitor's internet address.

## Keep robots out

- **A hidden field** that only robots fill in. A filled one is answered as if it worked and adds nobody.
- **A challenge.** Turn on **ReCaptcha** and set its keys, so every visitor has to pass it. Until you do, the widget's editor warns you.
- **A limit per visitor:** 10 sign-ups in 10 minutes.
- **A limit per address:** at most 3 confirmation e-mails per list in 15 minutes, whoever asks.

A form signs people up only while its widget is published and the visitor may see it.

## Tips

- To sign people up from another form, such as a contact form, use the **Subscribe to Mailing List** task in a workflow. Ask your administrator.
- If the form shows *This form isn't set up yet.*, none of its lists is offered. Check the widget's **Mailing lists**, and that each list's **Offer the list to people** is on.
