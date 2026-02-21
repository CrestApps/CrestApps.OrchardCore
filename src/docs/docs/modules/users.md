---
sidebar_label: Users
sidebar_position: 1
title: Users
description: Enhanced user management with display name customization and avatar support for Orchard Core.
---

## Table of Contents

- [Features](#features)
  - [User Display Name](#user-display-name)
  - [User Avatar](#user-avatar)
- [Extensions](#extensions)
  - [Liquid](#liquid)
  - [Users](#users)

## Features

### User Display Name

Provides a way to change how the user name is displayed. To set the display name format, navigate to `Configuration` >> `Settings` >> `User Display Name`.

If you want to display the user display in your project via code, you may do so my using the `IDisplayNameProvider` interface.

### User Avatar

Provides a way to display an avatar for each user. To change the default settings, navigate to `Configuration` >> `Settings` >> `User Avatars`.

## Extensions

### Liquid

 When the "Liquid" feature is enabled, a new helper to display the user's display name will become available (i.e, `display_name`). Here is an example

 ```
 {{ Model.User | display_name }}
 ```

 ### Users

When both the `Users` and `User Display Name` featured are enabled, the search functionality within the Users UI will expand to include fields such as display name, first name, middle name, or last name in the search results.

Additionally, when the `UserPickerField` field is used, the display text will show the display name. 

Lastly, since we added a recipe step to allow you to re-index users. This step will update all enabled user by default using a batch size of 250. To re-index all of your users run the following recipe

```
{
  "steps": [
    {
        "name": "indexUsers",
        "includeDisabledUsers": false,
        "batchSize": 250
    }
  ]
}
```

The `includeDisabledUsers` parameter within the `indexUsers` step is optional and allows for the indexing of disabled users if desired. Moreover, the `batchSize` parameter provides the ability to adjust the update batch size. The default value is set at 250 and can be increased to 1000 if necessary.
