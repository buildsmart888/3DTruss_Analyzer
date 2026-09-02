# Milestone D Solver/API Parity Audit

Status: baseline audit complete, implementation not started.  
Date: 2026-09-02

## Purpose and scope

This is the pre-UI audit required before introducing the WPF/MVVM shell. It compares the current
solver-facing APIs with `ProjectDocument` / `Model3D` V1. It does not change solver equations, result
values, sign conventions, or compatibility APIs.

The production analysis candidate remains `StructuralModel` + `StructuralSolver`. `TrussSolver` remains
an axial-only, stateful compatibility facade. `Model3D` V1 is a versioned project contract and is not a
direct solver input.

## Observed public analysis boundary

| Concern | `StructuralSolver` | `TrussSolver` | Model3D V1 / application consequence |
| --- | --- | --- | --- |
| Input | Constructor accepts a `StructuralModel` and optional `ILinearSystemSolver` | Mutable `AddNode` / `AddElement` API | `ProjectDocument` requires an explicit adapter before solve. |
| Validation | `ValidateModel()` returns `ModelValidationMessage` values | `ValidateModel()` returns a mutable `List<ModelValidationMessage>` | Model3D validation is separate; a future application service must aggregate adapter and solver diagnostics. |
| Single case | `Analyze(string? loadCaseId)` | `Analyze(LoadCase? loadCase)` | Model3D case IDs are GUIDs and must be resolved deterministically to solver case IDs. |
| Combination | `AnalyzeCombination(string combinationId)` | `AnalyzeLoadCombinations(...)` | `LoadCombination3D` factors map through the adapter, but no ProjectDocument-level request API exists. |
| Results | `StructuralAnalysisResult` with 6-DOF nodes, forces, equilibrium, diagnostics, design checks | `AnalysisResult`, 3 translational DOF, and mutable `LastResult` | A future result envelope must use stable Model3D GUID identities, never legacy integer IDs as its public key. |
| Geometry helpers | static local-axis / transformation helpers | no equivalent public helper | Continue to use the current `StructuralSolver` coordinate convention as authoritative. |
| Solver selection | explicit `ILinearSystemSolver`; dense is default | internal legacy matrix path | Only the structural path can be exposed through the new application boundary. |

## Current parity evidence

- `StructuralModelModel3DAdapter` is bidirectional and deterministic for tested nodes, line members,
  supports, prescribed movement, materials, sections, nodal loads, line loads, and combinations.
- `Model3DAdapterTests` prove axial and fixed-frame distributed-load/rigid-offset parity across the
  `StructuralModel -> Model3D -> StructuralModel` route.
- Existing structural-solver tests cover single cases, combinations, local axes, equilibrium, releases,
  offsets, diagnostics, and the dense-default/sparse-prototype selection behavior.
- `.gosa` persistence stores `ProjectDocument`; the migration path remains explicit and reports warnings.

## Gaps that prevent direct ProjectDocument analysis

| Gap | Effect | Required Milestone D/E action |
| --- | --- | --- |
| No `ProjectDocument` analysis service | UI would need to know adapters and legacy solver IDs | Introduce an application service that validates, adapts, selects a case/combo, invokes `StructuralSolver`, and returns an immutable application result. |
| Result identity is integer-based | Results cannot be safely joined to Model3D after reorder/import without adapter maps | Carry immutable GUID-to-legacy-ID maps in the analysis request/result boundary. |
| `StructuralAnalysisResult` lacks ENG-005 provenance | No model checksum, solver version, request identity, timestamp, or warnings snapshot | Define a separate `AnalysisSnapshot` DTO; do not mutate `ProjectDocument.Model`. |
| Model3D has solver options and result selections with no execution mapping | Saved user intent cannot yet control an analysis run | Define supported solver-option and result-request mapping; reject unsupported values explicitly. |
| Adapter loss warnings are not unified with solver diagnostics | UI could omit migration losses before showing results | Return one preflight report containing Model3D validation, adapter diagnostics, and solver validation. |
| Springs, rigid links, master-slave constraints, areas, partial releases, point/temperature loads | Some legal Model3D data is unsupported by `StructuralModel` | Block solve on errors or surface explicit preliminary/unsupported warnings according to the supported execution profile. |
| Python/Warehouse source contracts are absent | No qualified cross-runtime parity evidence | Keep those adapters blocked until versioned schemas and golden fixtures are supplied. |

## Decision

The first Milestone D code slice must be a solver-independent application boundary, not WPF views:

```text
ProjectDocument
  -> Model3D validation + compatibility preflight
  -> StructuralModelModel3DAdapter
  -> StructuralSolver
  -> AnalysisSnapshot (GUID-keyed, immutable, traceable)
```

`TrussSolver` must not be called by this boundary. It remains available only for legacy tests, examples,
and compatibility workflows.

## Acceptance criteria for the next code slice

1. A `ProjectDocument` can request one supported load pattern or combination without UI code knowing
   `StructuralModel` integer IDs.
2. The returned snapshot records document checksum, selected GUID, solver name/version, timestamp,
   warnings, and GUID-keyed node/member results.
3. Unsupported Model3D features fail before results are exposed; no warning is silently dropped.
4. Existing `StructuralSolver` and `TrussSolver` APIs retain their behavior and all current tests pass.
5. Add a round-trip parity regression for the new boundary using the existing frame fixtures.

## Deferred

WPF shell, view models, document lifecycle, undo/redo, background execution, and UI localization remain
out of this audit. They follow only after the application analysis boundary passes the criteria above.
