# Milestone C Persistence And Compatibility

Status: In progress. C# legacy/schema-v2 migration and `.gosa` storage are implemented; Python and
Warehouse3D adapters remain blocked on source contracts/fixtures.

## `.gosa` Package

`.gosa` is a ZIP package with these versioned entries:

- `manifest.json`: package format `gostructanalysis.gosa/1` and Model3D schema version.
- `project.json`: strict Model3D V1 `ProjectDocument` JSON.

`GosaProjectStore.SaveAtomic` writes to a uniquely named temporary file, opens and validates that file,
copies the prior valid package to `<name>.bak.gosa`, then promotes the temporary file. Autosaves use
`<name>.autosave.gosa`. `RecoverLatest` checks the primary, autosave, and backup in newest-first order and
rejects invalid candidates rather than returning partial data.

Results, attachments, report files, and UI layout state are not packaged in v1. They require separate
versioned payload contracts.

## Supported Migration Inputs

| Source | Status | Route | Loss handling |
| --- | --- | --- | --- |
| Current C# structural JSON schema v2 | Implemented | Existing reader → StructuralModel adapter → Model3D V1 | Every unrepresentable property is a migration warning |
| Legacy C# truss JSON | Implemented | Existing legacy reader → StructuralModel adapter → Model3D V1 | Every unrepresentable property is a migration warning |
| Python Frame/Truss/Hybrid | Blocked | Requires versioned source JSON, unit/sign declaration, and golden fixtures | No importer is advertised or guessed |
| Preliminary Warehouse3D | Blocked | Requires versioned source schema and group/load-resultant fixtures | No importer is advertised or guessed |

The implemented adapters preserve canonical SI values and explicit IDs where they have a compatible
meaning. Stable Model3D IDs are deterministic for a given C# source model. Point/temperature loads,
springs, master-slave constraints, rigid links, area objects, and design-only properties are not silently
dropped: their migration reports identify them.

## Migration CLI

For the implemented C# JSON inputs:

```bash
dotnet run --project tools/ProjectMigration/ProjectMigration.csproj -- input.json output.gosa
```

The CLI prints every migration report entry, writes nothing if the report has errors, and does not modify
the source JSON. Review warnings before treating `output.gosa` as a replacement for the source file.

## Required Python/Warehouse Handoff

Supply at least one fixture per source family together with:

- schema/version identifier and unknown-data policy;
- coordinate plane mapping and local-axis/sign convention;
- SI/unit conversion rules;
- node, line, material, section, load, combination, group, and result-resultant semantics;
- expected converted output or force/moment/resultant checks.

Once supplied, each source family can receive a deterministic adapter, golden migration test, and explicit
loss report without guessing production behavior.
