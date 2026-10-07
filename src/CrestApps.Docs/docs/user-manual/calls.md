---
sidebar_label: Placing & Handling Calls
sidebar_position: 31
title: Placing and Handling Calls
description: Dial a number or an extension from the soft phone, answer incoming calls, and use hold, mute, keypad, transfer, add call, merge and conference.
technical_manual:
  - telephony/index
  - telephony/telnyx
  - telephony/extension-dialing
---

All call controls live in the **soft phone**, which agents run in the [browser extension or the Windows app](phone-apps.md). This page walks through the everyday call tasks. See [Soft phone](soft-phone.md) for its tabs and settings.

| | |
| --- | --- |
| **Page** | `/softphone` on your site, opened by the browser extension or the Windows app |
| **Permission** | Use the telephony soft phone. Transferring a Contact Center call to an outside number also needs Transfer Contact Center calls externally. |
| **Feature** | Telephony Soft Phone Extension, and a phone provider such as Telnyx or Asterisk |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the soft phone keypad: typing a number, switching to extension dialing and searching for a colleague by name">
  <source src="/img/docs/um-manual-dial.mp4" type="video/mp4" />
</video>

The phone only shows the buttons your phone system supports. If a button described here is missing, your provider does not offer it.

## Dial a phone number

1. In the soft phone, choose the **Keypad** tab.
2. Type the number in international form, starting with `+`, or pick the country flag first and type the national number. The number is formatted as you type and turned into a full international number before it is dialed.
3. Click the green **Call** button or press **Enter**.

The number field empties as soon as the call starts, so a second press of **Enter** does not dial again. While the call is connected, the field shows the number you are talking to.

### What you hear while it rings

While the number rings, the phone plays a ringback tone in your headset (two seconds of tone, four seconds of silence) and the status reads *Ringing...* with no call timer. The tone stops the moment the other person answers, and the timer starts then. It also stops if the number is busy, does not answer or is not in service (you hear the not-in-service message instead), when you hang up, and when you put the call on hold or switch to another call. The same applies when you call an extension and your colleague's phone is ringing.

If the phone loses its connection to the site while the number rings, the tone stops rather than risk playing over the conversation, and it stops by itself after two minutes in any case.

The phone refuses some numbers before anything is dialed: emergency numbers, premium-rate numbers, and short numbers your administrator has not allowed. Use another phone for emergency calls.

A **Call with the soft phone** button also appears beside phone numbers on admin pages, for example on a contact. Click it to dial that number in your soft phone. To call a customer as part of your work, open their activity first so the call is logged against it; see [Activities](activities.md).

:::note[Telnyx: the phone answers its own line first]
On Telnyx, the phone system connects your keypad calls: your phone answers a line of its own by itself, and the number is dialed from there. Such a call can be held, transferred, merged and sent key presses like any other.

If the phone system cannot connect the call that way, for example while your phone is still connecting, the phone dials the number itself. That call cannot be transferred or merged: the transfer panel says so, and its line in **Active calls** has a disabled checkbox.
:::

## Call an extension

1. On the **Keypad** tab, click **Dial extension**. The country flag disappears and the field reads *Search a name, or enter an extension*.
2. Type the extension number and click **Call**, or start typing a name: the people who match are listed with their extension under **Matching extensions**. Click one to call them, or press **Enter** when only one person matches.
3. Click **Dial phone number** to go back to ordinary numbers.

While you type an extension, the phone shows who it rings. The call is shown with the person's name, for example *Jane Doe · ext 2*, and so is the entry on the **Recent** tab, which calls it back as an extension. Your own extension is never listed; typing it shows *That's your own extension.*

Extension calls are internal, so do-not-call lists and calling-hours rules do not apply to them. If your colleague does not answer, you are sent to their voicemail. See [Extensions](extensions.md).

## Answer an incoming call

An incoming call opens the **Incoming call** window in the soft phone, even when the phone panel is closed. It shows the caller, the queue or who transferred the call, and any **Matched records**.

| Button | What it does |
| --- | --- |
| **Answer** | Connects the call. It reads *Answering…* until the call connects. |
| **Answer & open** | Shown on each matched record. Connects the call and opens that record. |
| **Open** | Opens the matched record without answering. |
| **Voicemail** | Sends the caller to voicemail. Shown only when your phone system supports it. |
| **Ignore** | Declines the call on this phone. |

When you have the phone open in several places, answering in one stops the others ringing. A call from a colleague's extension always rings; the phone never answers it by itself.

A call from a queue arrives as an **offer** first: the workspace and the docked agent bar show the caller, the queue and a countdown, and the soft phone lists the contacts that match the number. This screencast signs in to the *Support* queue, accepts a live call, puts the caller on hold and back, mutes and unmutes, hangs up, and lands in wrap-up with **Complete activity**:

<video controls preload="metadata" width="100%" aria-label="Screencast of an agent signing in to a queue, accepting an inbound call from the queue, using hold and mute, hanging up and entering wrap-up">
  <source src="/img/docs/um-answer-queue.mp4" type="video/mp4" />
</video>

### Receive a transferred call

A call a colleague hands to you rings with **Answer** and **Ignore** and shows *Transferred by* and your colleague's name. It has no **Voicemail** button. **Ignore** gives the caller back to your colleague. Once answered, it is an ordinary call on your phone: you can hold it, transfer it again or merge it.

## During a call

<video controls preload="metadata" width="100%" aria-label="Screencast of a live call in the soft phone window: mute, hold, keypad, the transfer panel and hang up">
  <source src="/img/docs/um-preview-dial.mp4" type="video/mp4" />
</video>

The buttons sit in two rows. The first row holds your own controls, each an icon over its label. The second row holds the actions that bring someone else in or hand the call over.

| Button | What it does |
| --- | --- |
| **Mute** / **Unmute** | Stops the other person hearing you. Shown once the call is connected. **Unmute** is filled with color while you are muted. |
| **Hold** / **Resume** | Puts the other person on hold; they hear hold music or a soft tone. **Resume** is filled with color while the call is held. |
| **Keypad** | Opens the keypad to send key presses (touch tones), for example to choose an option in another company's phone menu. During a call the keypad stays closed until you open it. |
| **Hang up** | Ends the selected call. It is the only red button. In a conference it reads **Leave** (see [Leave or end a conference](#leave-or-end-a-conference)). |
| **Transfer** | Opens the transfer panel (see [Transfer a call](#transfer-a-call)). |
| **Add call** | Puts this call on hold so you can dial another (see [Add a second call](#add-a-second-call)). |
| **End all** | Shown with two or more calls. Ends every call after you confirm. The confirmation starts on **Keep talking**, so pressing **Enter** does not hang up by accident. In a conference it reads **End for all**. |

### The Active calls list

With a call up, the **Keypad** tab lists your calls under **Active calls**. Each line shows the name or number, its state (**Active**, **On hold** or the call's own state) and how long it has lasted. Click a line to choose which call the buttons act on.

### Send key presses

1. Click **Keypad** during the call.
2. Press the digits you need. Each press plays a touch tone to the other end, for example to pick an option in a phone menu.

## Add a second call

1. During a call, click **Add call**. The current call goes on hold and stays in the **Active calls** list, so you can see who is waiting. The keypad opens with an empty number field.
2. Type the number and press **Enter**, or click **Call**.
3. Changed your mind? Click **Back to call** or press **Escape** to take the held call off hold.

While you add a call, the keypad types the new number and never sends key presses to the held call, and the site's own number cannot be added. **Hold** only holds a call; use **Add call** to place a second one. A phone system that cannot hold a call cannot add one either, and the button's tooltip says so.

## Transfer a call

1. Click **Transfer**. The transfer panel opens inside the phone, in place of the keypad. The back arrow returns to the keypad.
2. When your phone system offers both, choose **Blind** (*The call is sent straight to them.*) or **Warm** (*You speak to them before the call is handed over.*). A call that supports only one shows no choice.
3. Pick who to send the call to from the **Directory**, or type in the field. Like the keypad, the panel has a **Transfer to an extension** / **Transfer to a phone number** toggle under the field:
   - In phone-number mode the field has the keypad's country flag. The number is checked the way the keypad checks one, so an incomplete number and the site's own number are refused before anything is sent.
   - In extension mode, type the colleague's extension (for example `2`). It is sent as an extension, never dialed as a phone number.

   In either mode you can type a name: the directory narrows as you type, and **Enter** transfers to the one person left. Typed digits offer, for example, **Transfer to extension 2 · Jane Doe**.
4. Click **Transfer** (or press **Enter**).

A call that your phone dialed by itself cannot be transferred. For such a call the panel explains this instead of offering a destination: ask the other person to call the destination, or hang up and dial it.

### Transferring a Contact Center call

When the call came to you through the Contact Center (a queue, a direct line or a campaign), the Contact Center carries the transfer, and the panel lists:

- **Agents**: every other agent, with their extension and presence. Only agents who are **Available** can be picked; the others are listed so you can see why.
- **Queues**: every enabled queue, with how many callers are waiting in it.
- **Outside numbers**: the [approved external destinations](contact-center-settings.md#external-transfer-destinations), when you have the **Transfer Contact Center calls externally** permission. You can type a number that is not on the list only when your administrator turned on **Let agents transfer to numbers that are not on this list**. Emergency numbers, premium-rate numbers and the contact center's own numbers are always refused.

An extension typed in extension mode stands for the agent it rings: the transfer goes to that agent exactly as if you had picked them from the list. An extension that belongs to nobody, or to someone who is not a Contact Center agent, is refused with the reason. You cannot transfer a call to yourself.

**Blind transfer**

- **To an agent:** the caller hears the queue's hold music while the call rings that agent's phone as a normal offer. If they do not answer in time, the caller goes to that agent's voicemail.
- **To a queue:** the caller joins the queue, keeping at least the priority they had, and is offered to the next available agent. With nobody free, they hear the queue's treatment.
- **To an outside number:** the call leaves the contact center and is connected to that number from your company's number.

In every case the call leaves your phone at once, and you go into wrap-up, or straight back to ready for a direct call, exactly as when a call ends.

**Warm (consult) transfer**

1. Choose **Warm**, pick an available agent or an outside number, and click **Transfer**. The caller is put on hold with the queue's hold music and the person you picked is rung. The panel shows *Calling* and their name; you can click **Cancel transfer** at any time.
2. When they answer, the panel shows *Talking to* their name, *The caller is on hold.* You speak to them privately while the caller waits.
3. Click **Complete transfer** to hand the caller to them and leave the call, or **Cancel transfer** to drop them and go back to the caller.

If the person you consulted hangs up or does not answer within 30 seconds, the caller comes straight back to you and the panel says so. If the caller hangs up during the consult, the consult ends and the person you consulted is released. A queue cannot be consulted: send the call to a queue with a blind transfer.

### Provider differences

The buttons you see depend on your phone system:

- **Asterisk** supports blind transfer and conferences, but not warm transfer of a Contact Center call.
- **Telnyx** supports every transfer on this page, including the consult.

### Transferring a keypad call on Telnyx

A number you dialed from the keypad on Telnyx is transferred like this:

- **Blind:** the caller is put on hold (they hear the hold tone) and the destination is rung. The panel shows *Calling* and the destination, with **Cancel transfer**. When the destination answers, the call is handed to them and leaves your phone. If they decline or do not answer within 30 seconds, the panel says so and the caller is still with you, on hold: click **Resume**. If you hung up in the meantime, a colleague's caller is sent to that colleague's voicemail.
- **Warm:** the caller is put on hold and your phone answers a second, consult call by itself; you talk to the destination on it. Once they answer, click **Complete transfer** to hand them the caller, or **Cancel transfer** to drop them and take the caller off hold. If they hang up first, the caller is still with you, on hold. If the caller hangs up during the consult, the consult carries on as an ordinary call. Hanging up the consult after they answered hands them the caller, as on most phone systems.

A colleague receives the call as described in [Receive a transferred call](#receive-a-transferred-call).

## Merge calls into a conference

1. With two or more calls up, the **Active calls** list shows a checkbox beside every call and a **Merge calls** button. The button stays disabled until two or more calls are ticked.
2. Tick the calls to join, or tick **Select all**. The button then reads **Merge 2 calls**, **Merge 3 calls** and so on, and the line beside it names the calls it joins.
3. Click the button.
4. The calls are listed under *Conference · N participants*, and each line shows **In conference**. Each participant has their own hang-up button, which ends only that participant's call.

To add another call to a running conference, tick any participant of the conference and the new call. The button reads **Add to conference**, and the new call joins the same conference instead of starting a second one.

A call whose other end has not answered yet cannot be merged (*Waiting for them to answer before this call can be merged.*). A call your phone dialed by itself cannot be merged either; its checkbox is disabled.

**Transfer** is hidden for a conference until you click one call's line, so you cannot transfer the whole conference by accident.

### Leave or end a conference

- **Leave:** in a conference with two or more other people, **Hang up** reads **Leave**. You drop out and everyone else stays connected to each other. A Contact Center caller is never disconnected by you leaving; their call stays on your phone as a line of its own.
- **Only one other person left:** **Hang up** simply ends the call, so nobody is left alone on the line.
- **End for all:** ends the call for everyone after you confirm (*End the call for everyone?*). The confirmation starts on **Keep talking**.

## Hang up

- Click the red **Hang up** to end the selected call.
- With two or more calls up, click **End all** and confirm to end every call.

## After the call

A queue or campaign call puts you in **Wrap-up** until you complete the activity. A direct call to you, or a call you dialed yourself, skips wrap-up. See [Agent workspace](agent-workspace.md#after-the-call-wrap-up).

:::note[About the screencast]
The first screencast shows the keypad and the extension search and stops before a call is placed; the others are live calls. Completing a transfer and conferencing use the buttons described above.
:::
