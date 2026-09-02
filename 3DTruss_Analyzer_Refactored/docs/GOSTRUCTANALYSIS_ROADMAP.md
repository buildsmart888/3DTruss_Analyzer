# GOStructAnalysis Product And Model3D Roadmap

This roadmap defines the controlled transition from the current C# 3D Structural Analyzer and the
separate Python GO Struct Desktop engineering work into one long-term product direction named
`GOStructAnalysis`.

The C#/.NET application is the intended production Windows desktop product and the owner of the
qualified `Model3D` analysis contract. Python remains an engineering toolchain for independent
comparison, optimization, parametric studies, and research prototypes. The plan does not require an
immediate source-code or repository merge.

## Roadmap Status

- Status: active. Milestone A governance and Milestone B Model3D V1 artifacts are implemented for review; approval gates remain open.
- Current production candidate: the C# `StructuralModel` and native linear 3D frame pipeline.
- Current Python contribution: mature 2D workflows, reporting, qualification patterns, OpenSeesPy
  comparisons, optimization, and preliminary Warehouse3D workflows.
- Engineering qualification: incomplete. Existing analysis/design limitations remain applicable.
- Public product name: `GOStructAnalysis`.
- Existing namespaces and file formats remain compatible until their migration milestones pass.

## Product Objective

Deliver a maintainable Windows desktop structural engineering application with this primary workflow:

```text
Project -> Physical -> Loading -> Analysis -> Results -> Design -> Report
```

The first qualified 3D release will support user-authored linear-elastic Frame3D and Truss3D models,
traceable loads and combinations, inspectable reactions/displacements/member forces, preliminary or
qualified design modules according to their declared code profiles, and auditable reports.

## Product Ownership Decision

### C#/.NET owns

- The production `ProjectDocument` and `Model3D` domain contracts.
- The Windows WPF application and HelixToolkit 3D workspace.
- The native validated linear 3D Frame-Truss analysis path.
- Production project persistence, migration, results, design orchestration, and reporting.
- Installer, recovery, localization, and Windows desktop release quality.

### Python owns

- OpenSeesPy and other independent-solver comparison adapters.
- Benchmark generation, numerical investigations, and qualification evidence.
- Optimization, Pareto search, and high-level parametric studies where Python libraries add value.
- Research prototypes that do not become production behavior until ported or integrated through an
  approved versioned process boundary.

### Merge rules

- Do not maintain two production solvers for the same advertised capability.
- Do not call Python for the standard native linear analysis required to open and solve a project.
- Exchange data through versioned JSON contracts, never by sharing mutable runtime objects.
- Preserve native solver signs; presentation controls may change graph placement only.
- A Python prototype becomes a C# product feature only after requirements, migration, tests,
  qualification, and report behavior are defined.
- Keep the repositories separate initially. Consider a monorepo only after the shared contracts and
  release ownership are stable.

## Release Trains

| Release | Purpose | Required milestone |
| --- | --- | --- |
| Internal Foundation | Shared product identity and approved Model3D contract | A-C |
| 3D Authoring Alpha | Create, edit, save, reopen, and validate a general spatial model | D-E |
| 3D Analysis Beta | Solve and inspect qualified linear Frame-Truss cases | F-G |
| Engineering Preview | Loading, results, design, and report workflow connected end-to-end | H-I |
| Qualified 1.0 Candidate | Independent comparisons, installer, migration, and reviewer evidence | J |

Release names are capability gates, not calendar commitments.

## Cross-Cutting Requirements

### Engineering contracts

- `ENG-001`: Core calculations use canonical SI units: m, N, N-m, Pa, kg, rad, and K differences.
- `ENG-002`: The global system is right-handed and Z-up; gravity acts in global `-Z`.
- `ENG-003`: Member local x runs from end I to end J; local y/z form a right-handed basis.
- `ENG-004`: Input models are separate from analysis and design results.
- `ENG-005`: Every result records the source model checksum, case/combination identity, solver name,
  solver version, assumptions, timestamp, and warnings.
- `ENG-006`: Unsupported behavior fails explicitly and must not return plausible-looking results.
- `ENG-007`: UI, report, CSV, XLSX, and JSON use one result-formatting and unit-conversion contract.
- `ENG-008`: Analysis equations remain outside UI event handlers and view models.
- `ENG-009`: Design-code equations remain outside the generic analysis solver.
- `ENG-010`: Preliminary checks and generators are labeled as preliminary in UI and reports.

### Project and identity contracts

- `MOD-001`: Every persistent object has an immutable `Guid Id` and a user-editable `Label`.
- `MOD-002`: References use stable IDs, not list indexes or display labels.
- `MOD-003`: Every schema change increments a version and supplies deterministic migration/defaulting.
- `MOD-004`: Save is atomic and never replaces the last valid project with a partial file.
- `MOD-005`: Legacy C# JSON and supported Python project formats import through explicit adapters.
- `MOD-006`: Unknown or lossy data produces a warning/error rather than silent deletion.
- `MOD-007`: Undo/redo, autosave, and recovery use the same versioned project contract.
- `MOD-008`: Presentation settings are persisted separately from engineering model state.

### User experience contracts

- `UX-001`: The primary stage navigator is Physical, Loading, Analysis, Results, Design, and Report.
- `UX-002`: Unsupported actions are hidden or disabled with a reason.
- `UX-003`: Table, tree, 3D, diagnostics, and result selections remain synchronized.
- `UX-004`: Every load/result view shows active case/combination, units, coordinate basis, and scale.
- `UX-005`: Result views clearly state whether results are current, stale, preliminary, or failed.
- `UX-006`: All model-changing commands participate in a common undo/redo history.
- `UX-007`: Long analysis, report, import, and optimization work runs without blocking the UI.
- `UX-008`: Thai and English text, 100/125/150% DPI, keyboard focus, and minimum supported layouts
  are included in release verification.
- `UX-009`: Colors are accompanied by labels, symbols, or line styles and are not the only status cue.
- `UX-010`: Positive/negative result diagrams always include a legend and native sign statement.

### Quality contracts

- `QA-001`: Every solver feature has closed-form, textbook, regression, or external comparison evidence.
- `QA-002`: Every schema feature has creation, validation, round-trip, malformed-file, and migration tests.
- `QA-003`: Every load type has resultant, equilibrium, coordinate-transform, and diagram tests.
- `QA-004`: Every design profile has published demand/capacity examples and governing-case tests.
- `QA-005`: Dense behavior remains the reference until a sparse path passes equivalence and performance gates.
- `QA-006`: Public engineering releases require a completed release checklist and independent review record.

## Target Solution Structure

```text
GOStructAnalysis.sln
  src/
    GOStructAnalysis.Domain/
      Projects/ Geometry/ Materials/ Sections/ Objects/
      Supports/ Constraints/ Loads/ Analysis/ Results/ Design/ Units/ Validation/
    GOStructAnalysis.Application/
      Documents/ Commands/ History/ Workspaces/ Analysis/ Design/ Reporting/
    GOStructAnalysis.Analysis/
      Frame3D/ Truss3D/ Assembly/ Solvers/ Recovery/ Diagnostics/
    GOStructAnalysis.Design/
      Steel/ Concrete/ Foundation/ ThaiCode/
    GOStructAnalysis.Infrastructure/
      Persistence/ Migration/ Reporting/ Export/ Logging/ Settings/ PythonBridge/
    GOStructAnalysis.Presentation.Wpf/
      Shell/ Workspaces/ ViewModels/ Helix3D/ Dialogs/ Resources/
  tests/
    GOStructAnalysis.Domain.Tests/
    GOStructAnalysis.Analysis.Tests/
    GOStructAnalysis.Integration.Tests/
    GOStructAnalysis.Benchmarks/
    GOStructAnalysis.UI.SmokeTests/
  tools/
    python-qualification/
```

Do not split the solution into every target project in one change. First introduce stable boundaries
inside the existing `TrussAnalyzer.Core`, then extract projects when dependency direction is covered
by tests.

## Target ProjectDocument Contract

```text
ProjectDocument
  SchemaVersion
  ProjectInfo
  UnitPreferences
  Model3D
    Nodes
    LineObjects
    AreaObjects
    Materials
    Sections
    SupportsAndSprings
    RigidLinksAndConstraints
    LevelsAndGrids
    Groups
  LoadDefinitions
    LoadPatterns
    LoadAssignments
    MassSource
    LoadCombinations
  AnalysisDefinitions
    AnalysisCases
    SolverOptions
    ResultRequests
  DesignDefinitions
    CodeProfiles
    DesignGroups
    Overwrites
  PresentationSettings
  AuditMetadata
```

Analysis results are stored as separate `AnalysisSnapshot` data and do not mutate `Node`, line object,
load, material, or section inputs.

## Milestone A - Product Identity And Governance

Status: In Review (implementation complete; product-owner and engineering approvals pending).

Evidence: `PRODUCT_GOVERNANCE.md`, ADR-0001 through ADR-0004, product-facing application/report
identity, assembly product metadata, and passing compatibility tests. The legacy solution, assemblies,
namespaces, APIs, and schemas remain intentionally unchanged.

### Goal

Establish one product direction without breaking existing namespaces, files, or tests.

### Scope

- Adopt `GOStructAnalysis` in window title, About, report title, installer metadata, and documentation.
- Create the shared engineering glossary and feature ownership matrix.
- Record architecture decisions for units, axes, signs, IDs, persistence, solver ownership, and Python use.
- Inventory C# and Python feature overlap, test evidence, and unsupported behavior.
- Define product status labels: Prototype, Preliminary, Validated, and Qualified.

### Deliverables

- Product naming checklist and compatibility policy.
- Architecture decision records.
- C#/Python capability matrix with one owner per advertised feature.
- Updated documentation navigation.

### Tests and verification

- Existing C# build/test commands pass.
- Existing file examples still open.
- Product rename does not require namespace or schema migration yet.

### Exit criteria

- Product and engineering terminology is approved.
- No feature is presented as qualified without evidence.
- Namespace and assembly rename timing is documented.

### Out of scope

- Bulk namespace rename.
- New solver equations.
- Physical repository merge.

## Milestone B - Model3D V1 Specification

Status: In Review (contract, validation, JSON Schema, example, `StructuralModel ↔ Model3D` adapter,
and automated parity tests implemented; domain/analysis/UI/reporting/Python reviewer approvals pending).

Evidence: `MODEL3D_V1_SPEC.md`, `src/Core/Domain/V1/`, `docs/schema/model3d-v1.schema.json`,
`examples/model3d/v1/minimal-frame.json`, `tests/Model3DV1Tests.cs`, and `tests/Model3DAdapterTests.cs`.

### Goal

Approve a solver-independent 3D model contract before large UI or file-format work.

### Scope

- Define `ProjectDocument`, `Model3D`, identifiers, references, validation severity, and errors.
- Define Node with six DOF, support assignment, springs, and prescribed movement references.
- Define Frame3D and Truss3D line objects with material, section, roll/local-axis reference,
  insertion/eccentricity, rigid offsets, and six-DOF end releases.
- Define materials, section analysis properties, display dimensions, and source metadata.
- Define levels, grids, groups, object labels, rigid links, and master-slave constraints.
- Define AreaObject as storage/validation only until an approved shell milestone.
- Define load patterns, assignments, mass source, combinations, and result selections.
- Define canonical SI serialization and user display-unit metadata.
- Publish JSON Schema and example projects.

### Required validation

- Duplicate ID/label detection with labels allowed to be renamed safely.
- Missing reference, zero length, invalid section/material, invalid release, and invalid local-axis checks.
- Cyclic rigid-link/constraint detection.
- Unsupported AreaObject analysis behavior reported before solve.
- Near-reference-axis local-coordinate examples for horizontal, vertical, and inclined members.

### Tests

- DTO creation and equality semantics.
- JSON round-trip without numeric or identity drift.
- Malformed/unknown data behavior.
- Stable ID behavior under copy, delete, undo, and import.
- Units, local axes, releases, offsets, springs, and constraints.

### Exit criteria

- Domain, analysis, UI, reporting, and Python reviewers approve the contract.
- The contract represents the first 3D release without UI-specific fields.
- Unsupported and future properties have explicit semantics.

### Out of scope

- Solving Model3D.
- 3D authoring UI.
- Plate/shell/solid stiffness.

## Milestone C - Persistence, Migration, And Compatibility

Status: In Progress (`.gosa` package serializer, atomic save/backup/autosave/recovery, C# schema-v2 and
legacy JSON migration, CLI, reports, and golden fixtures implemented. Python Frame/Truss/Hybrid and
Warehouse3D adapters remain blocked on versioned source schemas and fixtures).

Evidence: `MILESTONE_C_COMPATIBILITY.md`, `src/Core/IO/Projects/`, `tools/ProjectMigration/`,
`examples/migration/`, and `tests/GosaProjectStoreTests.cs`.

### Goal

Introduce Model3D without abandoning current projects or benchmark data.

### Scope

- Add `.gosa` project packaging with a versioned `project.json` payload.
- Keep readable JSON export for review and interoperability.
- Import current C# schema v2 and legacy truss files.
- Import supported Python Frame/Truss/Hybrid projects through explicit 2D-to-3D plane mapping.
- Import preliminary Warehouse3D data with groups and load-resultant traceability.
- Add migration reports describing converted, defaulted, ignored, and unsupported fields.
- Add atomic save, backup, autosave, crash recovery, and upgrade handling.

### Deliverables

- `IProjectSerializer`, `IProjectMigration`, and format adapter boundaries.
- Migration CLI/test utility.
- Golden legacy and current example files.
- Human-readable migration report.

### Tests

- Current and legacy round-trip tests.
- Interrupted/partial save recovery.
- Unknown future version rejection.
- Deterministic repeated migration.
- Python/C# fixture conversion and load resultant preservation.

### Exit criteria

- Supported projects open and save without silent information loss.
- Every lossy conversion is visible before the converted file replaces the original.
- Recovery restores the latest valid project snapshot.

## Milestone D - Application Foundation And WPF Shell

Status: In progress. Solver/API parity audit, toolkit-independent document/history/background/settings
foundations, `ProjectDocument` analysis preflight/snapshot boundary, and a staged WPF shell hosting the legacy
WinForms workspace are implemented. The shell now has project open/save/recovery, recent-project history,
autosave scheduling, cancellation/progress, unsaved-close guard, resettable persisted panes, and Thai/English
toggle resources. Manual 100/125/150% DPI, focus traversal, and full open-save-reopen desktop smoke evidence
remain the release gate; do not mark this milestone complete without that evidence. See
`MILESTONE_D_SOLVER_API_PARITY_AUDIT.md`.

### Goal

Create the application boundary needed for a maintainable workflow-oriented desktop product.

### Scope

- Introduce WPF AppShell and MVVM without rewriting the solver.
- Add `ProjectDocumentService`, active-document state, dirty state, and recent-project history.
- Add versioned command/history contract for canvas, table, property, and batch edits.
- Add background task, cancellation, progress, notification, and error boundaries.
- Add localization resources for Thai/English.
- Add user settings, workspace layouts, autosave preferences, and crash logging.
- Temporarily host compatible legacy WinForms panels where staged migration is safer.

### Main shell

```text
Command Bar
Stage Navigator | Model Tree | Helix 3D View | Property Inspector
                | Docked Input / Results / Diagnostics / Log
Status Bar
```

### UX requirements

- Stage navigation: Physical, Loading, Analysis, Results, Design, Report.
- File/Edit/View/Model/Load/Analyze/Design/Report/Help commands.
- Icon buttons with tooltips, keyboard shortcuts, and checked/toggled states.
- Plan/elevation/ISO/perspective camera commands and view cube/axis triad.
- Persistent but resettable dock layout.
- Current units, active case/combo, selection count, solver, and stale status in the shell.

### Tests

- View-model and command tests without opening WPF windows.
- Dirty state, undo/redo, autosave, cancellation, and recovery tests.
- UI smoke tests at supported resolutions and DPI.
- Thai/English text rendering and keyboard focus traversal.

### Exit criteria

- A project can be created, opened, saved, recovered, and closed safely.
- UI event handlers do not own domain or solver equations.
- Legacy screens can be retired one workflow at a time.

## Milestone E - Physical Model Workspace

Status: In progress. The first Model3D physical-authoring slice now provides GUID-preserving node/frame/truss/group
commands, undo/redo integration, a searchable physical object tree, selected-node typed property editing, starter
material/section defaults, deterministic endpoint/grid snap service, and a direct Model3D Helix viewport with
tree/viewport/inspector selection synchronization plus presentation-only transparency/group-colour rendering.
Typed/click-based placement onto viewport work planes, advanced snapping (midpoint/intersection/perpendicular),
and high-model-count rendering optimization remain open.

### Goal

Allow engineers to author and inspect a general spatial Frame-Truss model without editing JSON.

### Features

- Project, unit, grid, level/story, material, and section managers.
- Node, beam, column, brace, truss, and generic frame authoring.
- Draw by click, typed coordinate, length/angle/elevation, and work plane.
- Endpoint, midpoint, intersection, perpendicular, grid, and level snapping.
- Move, copy, array, mirror, divide, intersect, merge, align, and extrude-to-story.
- Supports, translational/rotational springs, releases, rigid offsets, insertion points, and local axes.
- Groups, selection sets, selection filters, lock, hide, isolate, and visibility layers.
- Table/tree/3D/property-inspector two-way selection.
- Real section rendering for supported profiles without changing analysis properties.
- Model diagnostics that select, isolate, and fit implicated objects.

### Section library requirements

- Generic properties: A, Iy, Iz, J, shear areas, weight/density, and design metadata.
- Visual dimensions for rectangular/RC, circular, pipe, H/I, C/channel, angle, box/tube, and custom.
- Catalog source, grade, units, and revision metadata.
- User-defined and project-local section libraries.

### Tests

- Authoring command and undo/redo tests.
- Picking/snapping correctness with overlapping and inclined members.
- Local-axis and section-orientation screenshots.
- Round-trip preservation of IDs and references.
- Performance/LOD tests at representative 300, 2,000, and 10,000 member models.

### Exit criteria

- A user can create, validate, save, reopen, and edit a spatial frame without direct table/JSON entry.
- Visual edits preserve Model3D identity and engineering data.
- No visual setting changes stiffness, loads, or native signs.

### Out of scope

- Shell meshing and shell stiffness.
- BIM fabrication-level geometry.
- Photorealistic rendering.

## Milestone F - Loading And Combination Workspace

### Goal

Create traceable physical loads, Thai templates, and combinations without embedding factors in UI code.

### Features

- Load pattern manager for `DL`, `SDL`, `LL`, `RL`, directional wind/seismic, temperature, settlement,
  and user-defined actions.
- Nodal force/moment and member point force/moment.
- Uniform, partial, and linearly varying distributed member loads.
- Uniform temperature, prescribed support movement, and fabrication-strain placeholders.
- Self-weight multiplier and explicit mass source.
- Floor/roof pressure objects and auditable tributary distribution to line members.
- Occupancy live-load library with source/version metadata.
- Preliminary then validated Thai wind and equivalent-static seismic generators.
- Service, strength, uplift, and envelope combination templates.
- Assignment ledger showing source object, load pattern, direction, magnitude, units, and generator trace.

### UX requirements

- Repeated graphical load placement with preview and typed values.
- Local/global basis glyphs and load-direction labels.
- Case/combo filter, load scale, hide/show, and value-label controls.
- Generated actions remain editable but retain generation audit metadata.
- Warnings when regenerated data would overwrite manual changes.

### Tests

- Resultant force/moment and equivalent nodal load checks.
- Local/global transformation tests.
- Partial/trapezoidal load continuity and point-action jump tests.
- Combination reference integrity and deterministic generator output.
- Independent review examples before a Thai generator is labeled validated.

### Exit criteria

- Every generated or assigned load is traceable and references an existing pattern/object.
- Model and FBD resultants agree within documented tolerance.
- Placeholder wind/seismic behavior remains visibly preliminary until validated.

## Milestone G - Qualified Linear 3D Analysis

### Goal

Connect Model3D to a validated first-order Frame-Truss solver through a stable application boundary.

### Scope

- Adapt current separated C# frame services to Model3D without changing validated behavior silently.
- Support Frame3D and Truss3D sharing nodes with six global DOF.
- Support axial, torsion, biaxial bending, releases, offsets, springs, settlement, temperature, and
  supported member loads.
- Add explicit analysis cases, solver selection, result requests, and diagnostics.
- Keep dense solving as the reference; enable sparse solving only after equivalence/performance gates.
- Run analysis in a cancellable background operation and preserve the last valid result on failure.
- Generate `AnalysisSnapshot` with hash, audit, warnings, equilibrium, and residual data.

### Analysis UI

- Preflight validation gate with Errors, Warnings, and Information.
- Case/combination selection and Run Selected/Run All.
- DOF, matrix size, estimated memory, solver path, elapsed time, and progress.
- Mechanism diagnostics naming suspect object, node, and DOF.
- Analysis log and downloadable diagnostic package.

### Qualification tests

- Axial, torsion, strong/weak-axis bending, space cantilever, portal, and braced frame.
- Releases, rigid offsets, springs, settlement, temperature, and mixed Frame-Truss joints.
- Global force/moment equilibrium and `K*D-F` residual.
- C# versus OpenSeesPy comparison using one versioned benchmark exchange contract.
- Dense versus sparse equivalence and increasing-size runtime/memory benchmarks.

### Exit criteria

- All published benchmarks meet documented tolerances.
- Unsupported behavior fails before results are presented.
- Solver path and preliminary/qualified status are visible in UI and reports.
- External comparison records are reproducible and reviewable.

### Out of scope

- P-Delta, material nonlinearity, plastic hinges, dynamics, cables, tension-only members, and shells.

## Milestone H - Production Results And Visualization

### Goal

Make analysis output inspectable, comparable, and consistent across the UI and exports.

### Features

- Result explorer by case, combination, and envelope.
- Undeformed/deformed model, reactions, and displacement vectors.
- Member `N`, `Vy`, `Vz`, `T`, `My`, and `Mz` diagrams.
- Exact supported member-load diagrams with left/right stations at discontinuities.
- Hover/crosshair station values, end actions, extrema, and selected-member charts.
- Signed and sequential contours, global/per-member ranges, and numeric legends.
- Story displacement, drift, shear, base reaction, and overturning summaries when level data exists.
- Compare two result selections and identify governing combinations.
- Linked, sortable, filterable, searchable result tables.
- CSV, XLSX, JSON, PDF, and image export through one formatting contract.

### Visual conventions

- Positive diagram: red; negative diagram: blue; near-zero: neutral grey.
- Truss tension/compression colors may use a dedicated labeled convention.
- Model, loads, supports, selection, warnings, and design utilization use distinct palettes.
- Legends always show units, range, sign, case/combo, scale, and local/global basis.
- Auto/detail/fast LOD changes rendering only and never numerical results.

### Tests

- Diagram continuity, curvature, point-load jumps, extrema, and envelope identity.
- Table/crosshair/report/export value parity.
- Nonblank and bounds-safe screenshots for every result mode.
- Large-model interaction and memory benchmarks.

### Exit criteria

- A reviewer can trace any displayed value to object, station, result selection, units, and solver snapshot.
- No graph-side option modifies stored signs or result values.
- Exported values match UI precision and selection semantics.

## Milestone I - Design, GO Pile, And Report Workflow

### Goal

Consume qualified demand results through explicit design-code profiles and generate auditable output.

### Design features

- Versioned steel and reinforced-concrete code profiles with code name/year and factor tables.
- Steel tension, compression, flexure, shear, interaction, buckling, and LTB checks.
- RC beam flexure, shear, torsion, minimum/maximum reinforcement, and detailing checks.
- RC column axial-bending interaction and slenderness after validated capacity methods exist.
- Design groups, unbraced lengths, K factors, Cb, covers, bar data, and user overwrites.
- Governing combination, utilization contour, auto-select suggestions, and calculation audit.
- GO Pile foundation objects linked to qualified support reactions and selected combinations.
- Pile reactions, pile capacity, footing flexure, one-way shear, punching, and detailing as separately
  validated submodules.

### Reporting features

- Project criteria, assumptions, code profiles, units, signs, warnings, and limitations.
- Geometry/material/section/load/combination schedules.
- Reactions, displacement, member force envelope, equilibrium, and diagnostics.
- Steel/RC/foundation design schedules with governing demand and calculation details.
- Configurable plan/elevation/ISO/load/result figures.
- Engineer, reviewer, revision, date, model checksum, application version, and solver version.
- Thai/English resource-driven wording and Excel/PDF output.

### Tests

- Published demand/capacity examples for each code check.
- Missing-data, OK, NG, governing-combination, and overwrite tests.
- Golden report headings, units, pagination, figure bounds, warnings, and table parity.
- End-to-end reaction-to-foundation traceability tests.

### Exit criteria

- Preliminary and qualified modules are visually distinguishable.
- Every design result identifies demand source, capacity method, factors, code profile, and limitations.
- Report content is reproducible from stable snapshots and does not query solver internals.

## Milestone J - Qualification, Packaging, And 1.0 Candidate

### Goal

Prove that the complete supported workflow is installable, repeatable, recoverable, and reviewable.

### Scope

- Publish supported-feature matrix, limitations, benchmark pack, and comparison procedure.
- Complete independent comparison with at least one qualified external solver for representative models.
- Add clean-install, upgrade, uninstall, file association, and shortcut verification.
- Verify Thai/English, DPI, supported Windows versions, focus order, and keyboard navigation.
- Verify crash logging, autosave, recovery, corrupted project handling, and migration backups.
- Ship example projects, engineering manual, quick-start workflow, and release notes.
- Complete security/dependency review and deterministic version stamping.

### Release gates

- `dotnet build GOStructAnalysis.sln` passes in the supported build environment.
- All unit, integration, benchmark, migration, report, and UI smoke tests pass.
- Packaged-runtime smoke tests create/open/analyze/report/reopen a project.
- No unresolved critical/high solver, sign, units, equilibrium, migration, or report defect remains.
- Completed release checklist is attached to the candidate build.
- Engineering-review notices and limitations are visible in application, manual, and reports.

### Exit criteria

- A clean Windows machine can install, launch, model, load, analyze, inspect, report, upgrade, and recover.
- Published benchmark and report values are reproducible within documented tolerances.
- Product naming, assembly metadata, file associations, documentation, and installer agree.

## Post-1.0 Capability Roadmap

Each capability requires its own specification, tests, benchmark evidence, UI behavior, and report
limitations before implementation.

1. Geometric nonlinearity and P-Delta.
2. Eigenvalue buckling.
3. Modal and response-spectrum analysis.
4. Tension/compression-only and cable elements.
5. Rigid diaphragm constraints and advanced floor behavior.
6. Plate/shell formulation, meshing, area loads, and contour results.
7. Staged construction and time-dependent material behavior.
8. Nonlinear hinges and material models.
9. Connections, base plates, anchors, and advanced foundation modules.
10. BIM/Revit/SketchUp/import-export adapters against the versioned Model3D contract.

## Recommended Implementation Sequence

The immediate sequence should be intentionally narrow:

1. Approve Milestone A terminology and ownership decisions.
2. Implement only the Milestone B `ProjectDocument` and `Model3D` contracts with tests.
3. Implement current-C#-to-Model3D adapters while preserving existing solver results.
4. Add persistence/migration before allowing the new UI to write production projects.
5. Build the WPF shell and Physical workspace against the stable contract.
6. Connect Loading, Analysis, Results, Design, and Report in that order.

Do not start a full WPF rewrite, shell solver, or national design-code expansion before the Model3D
contract and compatibility path are approved.

## Milestone Tracking Template

Use this block when starting a roadmap slice:

```text
Milestone:
Deliverable:
Status: Not Started | In Progress | Blocked | Complete

Scope:
-

Out of scope:
-

Dependencies:
-

Acceptance criteria:
- dotnet build TrussAnalyzer.sln passes
- dotnet test TrussAnalyzer.sln passes
- relevant migration/benchmark/UI checks pass
- documentation and limitations are updated

Evidence:
- changed files
- test output
- benchmark/comparison record
- manual verification record, if required

Residual risks:
-
```

## Current Limitations Carried Into This Roadmap

- Current analysis is linear elastic, first order, and small displacement.
- Current sparse solver is a prototype dense fallback.
- Current AreaObject data has no shell stiffness, meshing, diaphragm, or result behavior.
- Current Thai wind/seismic generators and combination factors are preliminary templates.
- Current steel, RC, and GO Pile checks are preliminary and not final code-calibrated design.
- Current PDF generation is a basic report engine.
- Current support movements are model-wide rather than load-case-specific.
- Current external ETABS/STAAD/OpenSees comparison remains partly manual.
- Current WinForms/WPF UI is not yet the target workflow-oriented WPF/MVVM application.
