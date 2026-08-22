using Xunit;

// Json.Schema exposes a process-wide, non-thread-safe schema registry (SchemaRegistry.Global) that a
// JsonSchemaBuilder mutates whenever it builds a schema carrying an absolute identifier. A large number of
// tests in this assembly (the whole Core/Schemas suite and the AI Agent schema tools) build such schemas,
// so running test collections in parallel let two builds mutate that shared dictionary concurrently and
// intermittently corrupted it ("Operations that change non-concurrent collections must have exclusive
// access"), which failed the build on CI. Disabling test-collection parallelization serializes every schema
// build across the assembly and removes the race. Tests within a collection already run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
