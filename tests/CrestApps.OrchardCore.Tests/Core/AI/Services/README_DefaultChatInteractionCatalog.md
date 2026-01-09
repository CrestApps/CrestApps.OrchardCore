# DefaultChatInteractionCatalog Tests

## Overview
The `DefaultChatInteractionCatalog` extends `SourceDocumentCatalog` and provides Title-based search functionality through the `PagingAsync` method. This document describes the expected search behavior and testing approach.

## Search Behavior

### Title Search Filter
The catalog implements server-side title search with the following behavior:

1. **Null/Whitespace Handling**: When `context.Name` is null, empty, or whitespace, no title filtering is applied
2. **Contains Match**: Searches using `Contains` for partial matching
3. **Case Sensitivity**: The `Contains` operation is case-sensitive (database-dependent behavior)
4. **Null Title Handling**: The filter explicitly checks `x.Title != null` before applying `Contains` to avoid null reference errors

### Filter Logic
```csharp
if (!string.IsNullOrWhiteSpace(context.Name))
{
    query = query.With<ChatInteractionIndex>(x => x.Title != null && x.Title.Contains(context.Name));
}
```

### Combined Filters
The catalog supports multiple filters applied in sequence:
- **UserId Filter**: Filters by `ChatInteractionIndex.UserId`
- **Title Filter**: Filters by `ChatInteractionIndex.Title` (described above)
- **Sorting**: Orders by `CreatedUtc DESC, Id ASC` when `Sorted` is true, otherwise by `Id ASC`

## Testing Approach

### Why Limited Unit Tests?
`DefaultChatInteractionCatalog` inherits from `SourceDocumentCatalog` which uses YesSql's `ISession` directly. YesSql uses extension methods (`Query<T, TIndex>`) which cannot be mocked with standard mocking frameworks like Moq. 

Proper testing would require:
1. **Integration Tests**: Setting up a full YesSql in-memory database with SQLite
2. **Test Infrastructure**: Creating indexes, registering mappings, and managing database lifecycle
3. **Complex Setup**: This is beyond the scope of unit tests

### Current Test Coverage
- **Base Functionality**: Covered by existing `SourceDocumentCatalog` and `DocumentCatalog` tests
- **In-Memory Catalogs**: Comprehensive tests exist for `Catalog`, `NamedCatalog`, and `NamedSourceCatalog` using `FakeDocumentManager`

### Manual Testing Scenarios
When testing the Title search functionality manually or through integration tests:

#### Test Case 1: Null/Whitespace Name
- **Input**: `context.Name` is null, empty, or whitespace
- **Expected**: All chat interactions for the user are returned (no title filtering)

#### Test Case 2: Exact Title Match
- **Input**: `context.Name = "Test Chat"`
- **Expected**: Only interactions with "Test Chat" in the title

#### Test Case 3: Partial Match
- **Input**: `context.Name = "Test"`
- **Expected**: Interactions with titles containing "Test" (e.g., "Test Chat", "Another Test")

#### Test Case 4: Case Sensitivity
- **Input**: `context.Name = "test"` vs `context.Name = "Test"`
- **Expected**: Case-sensitive matching (database collation-dependent)

#### Test Case 5: Null Titles
- **Input**: Some interactions have `Title = null`, searching for "Test"
- **Expected**: Null titles are excluded from results

#### Test Case 6: Combined UserId and Title Filters
- **Input**: `context.UserId = "user1"`, `context.Name = "Meeting"`
- **Expected**: Only user1's interactions containing "Meeting" in title

#### Test Case 7: Sorting
- **Input**: `context.Sorted = true`
- **Expected**: Results ordered by `CreatedUtc DESC, Id ASC`

## Recommendations for Future Testing

To add comprehensive tests for `DefaultChatInteractionCatalog`:

1. **Create Integration Test Project**: Set up a separate integration test project with YesSql infrastructure
2. **Use In-Memory SQLite**: Configure YesSql with SQLite in-memory database
3. **Register Indexes**: Register `ChatInteractionIndex` and `ChatInteractionIndexProvider`
4. **Seed Test Data**: Create chat interactions with various titles for testing
5. **Test All Scenarios**: Execute the manual test scenarios listed above

### Example Integration Test Setup
```csharp
public class ChatInteractionCatalogIntegrationTests : IAsyncLifetime
{
    private IStore _store;
    private ISession _session;
    
    public async Task InitializeAsync()
    {
        var configuration = new Configuration()
            .UseSqLite(":memory:")
            .UseBlockIdGenerator();
            
        configuration.RegisterIndexes<ChatInteractionIndexProvider>();
        
        _store = await StoreFactory.CreateAsync(configuration);
        _session = _store.CreateSession();
    }
    
    // ... tests here
}
```

##Conclusion
While unit tests for YesSql-based catalogs are challenging, the search functionality follows standard patterns and is well-documented. Manual testing and future integration tests can validate the complete behavior.
