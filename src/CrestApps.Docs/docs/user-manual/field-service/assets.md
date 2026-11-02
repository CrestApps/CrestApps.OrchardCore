---
sidebar_label: Assets
title: Assets
description: "Keep the equipment, vehicles, tools and customer units you service or use, with their types, parts, warranty and meter readings, and book vehicles and equipment so they can't be double-booked."
technical_manual:
  - modules/assets
---

Assets are the things you service or use: your own vans, equipment and tools, and the units installed at your customers. Each asset has a type, which says what it records and which meters are read on it.

| | |
| --- | --- |
| **Menu** | Assets > Assets, Asset Types |
| **Permission** | Manage assets; View assets to see them |
| **Feature** | Assets; Fleet to book vehicles and equipment and keep van stock |

<AskYourAdmin />

## Add an asset type

1. Go to **Assets > Asset Types** and select **Add asset type**.
2. Enter a name, such as *Boiler* or *Service van*, and choose its category.
3. Under **Attributes**, list the values every asset of this type records, such as *Refrigerant* (text) or *Output kW* (a number).
4. Under **Meters**, list the readings taken on it, such as *Hours run* or *Odometer*, with the unit. Leave **Only goes up** ticked for a meter that only counts up; a lower reading is then refused.
5. Select **Save**.

## Add an asset

1. Go to **Assets > Assets**, select **Add asset** and choose its type.
2. Enter its name, and its serial number, manufacturer and model. Leave the asset number empty for the next one.
3. Under **Owner and place**, choose who owns it: the business, a customer or a company (with its number), the asset it is part of, if any, and its address.
4. Enter when it was installed and its warranty, and fill in the values its type records.
5. Select **Save**. The asset's page opens.

Search the list by name, number or serial number, and filter it by type and state.

## Record a meter reading

On an asset's page, under **Meter readings**, choose the meter, enter the reading and select **Record**. The readings show newest first.

## Book vehicles and equipment

With **Fleet** on, a vehicle, equipment or tool the business owns has a **Fleet** section:

- **Book it for visits** gives it its own Equipment calendar, which refuses two bookings at once.
- **It carries stock** gives it its own Inventory location, so the parts it carries are counted, and parts used on a job come off it.
- Enter its plate, VIN, capacity and fuel.

A van's stock location doesn't show among the locations on **Settings > Inventory**, and orders are never taken from it.
