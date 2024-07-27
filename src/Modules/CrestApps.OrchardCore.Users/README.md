# Features

## User Display Name

Provides a way to change how the user name is displayed. To set the display name format, navigate to `Configuration` >> `Settings` >> `User Display Name`.

If you want to display the user display in your project via code, you may do so my using the `IDisplayNameProvider` interface.

### Shapes

The shape `UserBadgeContext` is responsible of displaying info about the user. You may implement `DisplayDriver<UserBadgeContext>` driver to inject items into the `UserBadgeContext` shape. This shape is rendered using the following display types

 - The `Summary` display type is used to render the logged user info in the navbar.
 - The `AdminSummary` display type is used to render the author info for each content item in the content item listing page.

The standard templates display the `Header` zone. Below is a list of the default views along with their respective names:

 - `UserBadgeContext.AdminSummary.cshtml`
 ```
 <span class="badge ta-badge font-weight-normal" data-bs-toggle="tooltip" title="@T["Author"]">
    @if (Model.Header != null)
    {
        @await DisplayAsync(Model.Header)
    }
</span>
 ```

 - `UserBadgeContext.Summary.cshtml`

 ```
@if (Model.Header != null)
{
    @await DisplayAsync(Model.Header)
}
 ```

## User Avatar

Provides a way to display an avatar for each user. To change the default settings, navigate to `Configuration` >> `Settings` >> `User Avatars`.

# Extensions

## Dynamic Cache

When the "Dynamic Cache" feature is enabled along with "User Avatar" or "User Display Name", it optimizes performance by caching shapes associated with user display names and avatars. If you wish to manually invalidate the cache, you can utilize the following tags:

 1. The `user-display-name` tag will clear the cached shapes for all users.
 2. The `username:{username}` tag will clear the cache for a specific user with the username `{username}`. Replace `{username}` with the actual username of the user you want to invalidate.

 ## Liquid

 When the "Liquid" feature is enabled, a new helper to display the user's display name will become available (i.e, `display_name`). Here is an example

 ```
 {{ Model.User | display_name }}
 ```

 ## Users

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
