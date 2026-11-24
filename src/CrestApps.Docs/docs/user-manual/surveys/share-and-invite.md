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
| **Menu** | **Surveys** → **Surveys**, then **Collect responses** in the survey's menu |
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

Select **Add kiosk** for a tablet at a front desk. After each response the ending shows for **Show the ending for** seconds, then the kiosk returns to the start for the next person. Kiosk responses are always recorded as guest responses and can't be reviewed or reopened, and a question page left alone for three minutes (no typing, tapping, scrolling or mouse movement) goes back to the start, so the next person doesn't see the previous person's answers. Give the survey an audience that lets guests answer, and keep the tablet signed out: a kiosk doesn't start while someone is signed in, and asks them to sign out. That way nobody can use the kiosk address at home to answer more times than the survey's limits allow.

## Invitations

1. Select **Add invitations**, then **Invite people**.
2. Enter **User names** (users get a notification), pick an **Audience** or a **Cohort**, or enter **E-mail addresses** for other people.
3. Tick **Send the invitations now**, or send them later from the list, and select **Invite**.

One invite reaches at most 5,000 people. For a bigger audience, invite the rest in another batch.

The site sends a limited number of invitation and reminder e-mails a day, set in **Settings** → **Surveys** → **Invitation e-mails a day** (2,000 unless you change it). The day starts at midnight in the site's time zone. Invitations over the limit wait and go out by themselves on a later day; nothing is lost. The limit holds even when several people send invitations at the same moment, or the site runs on several servers (your administrator turns on the Redis Lock feature for that). If another send is still going out, **Send** and **Send reminders now** tell you so instead of only showing "0 sent"; try again in a few minutes. Invitations from **Invite** then wait for the next hourly run. Two sends of the same list at the same moment, or a double click on **Send** or **Send reminders now**, never e-mail or notify anyone twice. A very large send also stops after a few minutes, and the rest go out with the hourly run. After **Invite**, the page tells you how many invitations will be sent later: because of today's limit, another send, or a mail server that didn't take them. Invitations to users of the site go to their notifications and don't count. If the mail server is down, the page tells you, and the invitations wait and are tried again every hour. While nothing at all goes out, an invitation counts a failed try at most once a day, so an outage of a few hours doesn't mark anyone **Bounced**; an outage that lasts about five days can, so invite those people again once it's over. An address the mail server keeps refusing is shown as **Bounced** after five tries, also when your list has several such addresses, and it never holds up the reminders of the rest of the list. When other e-mails go out in the same run, each refused try counts; when nothing else goes out, a try counts at most once a day, so that can take a few days. When some e-mails went out and the mail server refused others, **Send** and **Send reminders now** say how many it refused rather than saying the mail server failed. If you close the page or lose the connection while a send is going out, what was already sent stays recorded: nobody gets the same e-mail twice, and the rest go out with the hourly run. If you **Invite** people who are already on the list while another send is going out, they are left as they were and the page tells you; invite them again in a few minutes. To try a bounced or opted-out person again, invite them again. If e-mail isn't set up on the site, **Invite** tells you instead of sending.

Each person gets a personal link. **Settings** sets the e-mail **Subject** and **Message**, **Links expire after (days)**, the **Number of reminders** and the **Days between reminders**. **Send reminders now** reminds everyone who has not completed the survey, except people already reminded that day, so selecting it twice doesn't send anyone two reminders. The list shows each invitation's status.

Opening a personal link marks the invitation **Opened**, but the survey starts only when the person continues (it starts by itself in most browsers, and a **Start** button shows otherwise). So an e-mail scanner that checks the link uses nothing up, and the person can come back to an unfinished response from the link, even on another device (except on anonymous surveys).

Turning on Survey Invitations also turns on Outreach, the part of the site that sends messages in bulk.

### People who don't want e-mail

Invitations by e-mail respect the choices people made:

- Before each invitation or reminder e-mail goes out, the site checks whether the address asked not to be contacted, for example in a contact's preferences or by unsubscribing from an earlier e-mail. Such a person gets nothing, and the list shows the invitation as **Opted out**.
- Every invitation e-mail ends with an **unsubscribe** link. A person who uses it gets no more survey invitations or reminders at that address, from any survey.

People invited by user name get the invitation in their notifications instead of an e-mail.

## Show a survey in a page

With Survey Embedding, add the **Survey** part or the **Survey** widget to a page, pick an open survey and a link, and choose **Show as**: **The survey in the page**, **A button** or **A link**.
