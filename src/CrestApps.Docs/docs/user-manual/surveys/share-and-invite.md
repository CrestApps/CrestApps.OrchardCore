---
sidebar_label: Share and invite
title: Share and Invite
description: "Collect responses with public links, QR codes, kiosks, personal invitations and reminders, or a survey in a page of your site."
technical_manual:
  - modules/surveys/collecting
---

A collector is a way to reach respondents. A survey can have many.

| | |
| --- | --- |
| **Menu** | Surveys > Surveys, then **Collect responses** in the survey's menu |
| **Permission** | Manage own surveys, or Manage all surveys; for invitations, Manage survey invitations, or Manage invitations of own surveys for your own surveys |
| **Feature** | Surveys; Survey Invitations for invitations; Survey Embedding for pages and widgets |

<AskYourAdmin />

## Public links

The first publish makes a public link. Select **Add web link** for more, such as one per newsletter. Each link has:

| Field | What it does |
| --- | --- |
| **Name** | What staff see. |
| **Opens**, **Closes**, **Most responses** | When and how much the link takes. |
| **Password** | Respondents enter it before they start. |
| **Hidden variables** | One name=value per line, such as source=newsletter. |
| **Language** | The language the survey shows in. Leave empty for the visitor's language. |

Use **Copy** to copy the link and **QR code** to download its QR code.

## Kiosk

Select **Add kiosk** for a tablet at a front desk. After each response the ending shows for **Show the ending for** seconds, then the kiosk returns to the start for the next person. Kiosk responses are always recorded as guest responses and can't be reviewed or reopened, so the next person never sees the previous person's answers. Give the survey an audience that lets guests answer, and keep the tablet signed out: a kiosk doesn't start while someone is signed in, and asks them to sign out. That way nobody can use the kiosk address at home to answer more times than the survey's limits allow.

## Invitations

1. Select **Add invitations**, then **Invite people**.
2. Enter **User names** (users get a notification), pick an **Audience** or a **Cohort**, or enter **E-mail addresses** for other people.
3. Tick **Send the invitations now**, or send them later from the list, and select **Invite**.

One invite reaches at most 5,000 people. For a bigger audience, invite the rest in another batch.

The site sends a limited number of invitation and reminder e-mails a day, set in **Settings > Surveys > Invitation e-mails a day** (2,000 unless you change it). The day starts at midnight in the site's time zone. Invitations over the limit wait and go out by themselves on a later day; nothing is lost. The limit holds even when several people send invitations at the same moment, or the site runs on several servers (your administrator turns on the Redis Lock feature for that). If another send is still going out, yours waits for the next hourly run. A very large send also stops after a few minutes, and the rest go out with the hourly run. After **Invite**, the page tells you how many invitations will be sent later. Invitations to users of the site go to their notifications and don't count.

Each person gets a personal link. **Settings** sets the e-mail **Subject** and **Message**, **Links expire after (days)**, the **Number of reminders** and the **Days between reminders**. **Send reminders now** reminds everyone who has not completed the survey. The list shows each invitation's status.

Opening a personal link marks the invitation **Opened**, but the survey starts only when the person continues (it starts by itself in most browsers, and a **Start** button shows otherwise). So an e-mail scanner that checks the link uses nothing up, and the person can come back to an unfinished response from the link, even on another device (except on anonymous surveys).

Turning on Survey Invitations also turns on Outreach, the part of the site that sends messages in bulk.

### People who don't want e-mail

Invitations by e-mail respect the choices people made:

- Before each invitation or reminder e-mail goes out, the site checks whether the address asked not to be contacted, for example in a contact's preferences or by unsubscribing from an earlier e-mail. Such a person gets nothing, and the list shows the invitation as **Opted out**.
- Every invitation e-mail ends with an **unsubscribe** link. A person who uses it gets no more survey invitations or reminders at that address, from any survey.

People invited by user name get the invitation in their notifications instead of an e-mail.

## Show a survey in a page

With Survey Embedding, add the **Survey** part or the **Survey** widget to a page, pick an open survey and a link, and choose **Show as**: **The survey in the page**, **A button** or **A link**.
