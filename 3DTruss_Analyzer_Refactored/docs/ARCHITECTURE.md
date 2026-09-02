# Architecture

This document describes the recommended architecture for growing the current MVP into a maintainable 3D structural analysis and design application.

## Current Architecture

The project currently has two analysis paths:

- `TrussSolver`: legacy compatibility facade for axial-only truss analysis.
- `StructuralSolver`: newer solver path for `StructuralModel`, truss elements, and 3D frame elements.

`StructuralSolver` now coordinates the analysis pipeline. Model validation, DOF indexing, stiffness/load assembly, boundary conditions, reaction recovery, equilibrium checking, element-force recovery, diagnostics, result assembly, and preliminary design routing are separate services.

The first building workflow layer is available as `Core/Models/BuildingModel.cs`. It contains grid lines, stories, beam objects, column objects, supports, and nodal loads, and generates an ordinary `StructuralModel` for analysis/export.

The first Phase 8 area-object layer is available as `Core/Models/AreaObject.cs`. Area objects are stored on `StructuralModel.AreaObjects` and serialized with the model, but they are intentionally not consumed by `StructuralSolver` until a validated shell/diaphragm path exists.

## Model3D V1 Domain Boundary

`Core/Domain/V1` contains the solver-independent `ProjectDocument` and `Model3D` specification. It uses
GUID references, canonical SI values, structured validation, strict JSON behavior, and presentation
metadata outside engineering model state. It is intentionally separate from the current integer-ID
`StructuralModel` solver contract.

Model3D V1 must not be passed directly to `StructuralSolver`. `Core/Domain/V1/Adapters` now provides a
tested `StructuralModel ↔ Model3D` boundary with deterministic legacy-ID mapping, explicit conversion
diagnostics, and frame parity tests. It is an adaptation/migration aid, not a new solver path.

`Core/IO/Projects` now owns the Milestone C serializer/migration interfaces, current-C#/legacy JSON file
migration, `.gosa` packaging, atomic save, backup, autosave, and recovery. The migration CLI lives under
`tools/ProjectMigration`. Python and Warehouse3D adapters, source-file recovery UX, and project attachments
remain pending because no versioned source fixtures/contracts are available.

## Milestone D Application Foundation

`Core/Application` owns UI-toolkit-independent document lifecycle, undo/redo command history, settings,
crash logging, background-task execution, and the only `ProjectDocument -> StructuralSolver` application
analysis route. `ProjectAnalysisService` validates Model3D, reports adapter/solver preflight diagnostics,
blocks lossy/unsupported execution, and produces an immutable GUID-keyed `AnalysisSnapshot` with checksum,
selection identity, solver identity, timestamp, warnings, and equilibrium.

The WPF `GOStructAnalysisShellWindow` is a staged shell in `UI/WinForms/AppShell`. It hosts the compatible
WinForms `MainForm` through `WindowsFormsHost`; no solver equations or engineering-model mutations live in
WPF event handlers. Start it with `dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj -- --wpf-shell`.
The default launch remains the legacy WinForms entry point during staged migration.

## Target Architecture

Recommended structure:

```text
Core/
  Models/
    Analysis model DTOs
    Building model DTOs
    Materials and sections
    Loads and combinations
    Results
  Analysis/
    Validation/
    Elements/
    Assembly/
    Solvers/
    Results/
    Diagnostics/
  Design/
    Steel/
    Concrete/
    LoadCombinations/
    ThaiCode/
  Units/
  IO/
  Reporting/
UI/
  WinForms/
tests/
docs/
examples/
```

## Layer Responsibilities

### Models

Models should hold project data and analysis input. They should avoid doing heavy calculations.

Examples:

- `StructuralModel`
- `BuildingModel`
- `Node`
- `StructuralElement`
- `AreaObject`
- `Material`
- `Section`
- `LoadCase`
- `LoadCombination`

Current building workflow boundary:

- `BuildingModel` is an object-level modeling layer above `StructuralModel`.
- `BuildingModel.ToStructuralModel()` generates nodes and `FrameElement3D` members from grid/story beam and column objects.
- Generated `StructuralModel` instances remain inspectable, analyzable by `StructuralSolver`, and exportable through the existing JSON path.
- Current scope is limited to simple frame generation with explicit supports and nodal loads; floor loads, diaphragms, object editing, and traceability metadata are future work.

Current area-object boundary:

- `AreaObject` is separate from line `StructuralElement` members and represents slab, wall, shell, and diaphragm placeholders.
- `StructuralModel.AreaObjects` is a data/serialization path only.
- `ModelValidator` reports invalid area object references and warns that area objects are not included in frame analysis.
- `StructuralSolver` continues to assemble only existing line elements.

### Analysis

The analysis layer should transform a model into results.

Suggested services:

- `ModelValidator`
- `DofIndexer`
- `FrameElementStiffnessProvider`
- `GlobalStiffnessAssembler`
- `LoadVectorAssembler`
- `BoundaryConditionApplier`
- `LinearAnalysisSolver`
- `LinearAnalysisRunner`
- `MechanismDiagnosticsService`
- `ReactionRecoveryService`
- `EquilibriumCheckService`
- `ElementForceRecoveryService`
- `SolverDiagnosticsService`
- `AnalysisResultBuilder`

Current implemented boundary:

- `Core/Analysis/Validation/ModelValidator` owns structural model validation messages used by the structural solver and UI diagnostics.
- `Core/Analysis/DofIndexer` owns node-to-DOF numbering, element DOF maps, and constrained DOF enumeration used by `StructuralSolver`.
- `Core/Analysis/BoundaryConditionApplier` owns the dense-matrix constraint transformation. For prescribed support values it first transfers `K_fc * u_c` to the free-DOF load vector, then zeroes constrained rows and columns, sets the diagonal to one, and writes the prescribed displacement/rotation value to the constrained load entry.
- `Core/Analysis/FrameElementStiffnessProvider` owns local truss/frame stiffness, optional Timoshenko shear deformation, and static condensation for moment releases.
- `Core/Analysis/FrameElementGeometryResolver` owns flexible member endpoint geometry and the node-to-connection kinematic transformation for rigid-end zones and local insertion points.
- `Core/Analysis/GlobalStiffnessAssembler` transforms local stiffness using `T^T Klocal T` and accumulates it into the global dense matrix through the element DOF map.
- `Core/Analysis/LoadVectorAssembler` owns nodal, member point, member distributed, member temperature, and self-weight load assembly. `LoadAssemblyResult` retains the equivalent local member loads required for force recovery plus local member-load metadata for diagram recovery.
- `FrameElementStiffnessProvider` also condenses equivalent local member loads when moment releases are present, so released end moments recover as zero.
- `Core/Analysis/FrameCoordinateSystem` owns local-axis and 12-DOF transformation construction shared by stiffness and load assembly; `StructuralSolver` retains static compatibility wrappers for existing viewer callers.
- `Core/Analysis/ReactionRecoveryService` owns node reaction recovery from the original stiffness matrix, original load vector, and solved displacement vector using `K_original u - F_original`.
- `Core/Analysis/EquilibriumCheckService` owns the current global translational equilibrium summary and preserves the existing force-only tolerance rule.
- `Core/Analysis/ElementForceRecoveryService` owns local displacement recovery, equivalent member-load subtraction, end-force DTO construction, and load-aware station recovery. It produces exact local axial/shear/bending station shapes for tracked member point and uniform distributed loads, plus torsion/bending jumps from point moments; internal point loads create explicit left/right station samples. Unsupported load types retain the compatibility interpolation from local end forces.
- `Core/Analysis/SolverDiagnosticsService` owns `SolverDiagnostics` metrics, dense-solver warning selection, and solver-path notes.
- `Core/Analysis/AnalysisResultBuilder` owns structural node/element result construction, compatibility updates to model-node result state, and final `StructuralAnalysisResult` DTO assembly.
- `Core/Analysis/LinearAnalysisRunner` owns common load-case/load-combination assembly, dense constraint application, linear solver invocation, and the `LinearAnalysisRunResult` snapshot used by result recovery.
- `Node.PrescribedDisplacement` and `Node.PrescribedRotation` are global-coordinate support boundary values in metres and radians. `DofIndexer` maps them only for their matching constrained DOFs.
- `Core/Analysis/MechanismDiagnosticsService` interprets singular or unstable solve failures using zero-stiffness rows and the first rank-deficient pivot. Its messages identify suspect node DOFs but do not prove the complete physical mechanism.
- `Core/Utilities/ILinearSystemSolver` is the explicit solver boundary used by `StructuralSolver`.
- `DenseLinearSystemSolver` remains the default validated solver path.
- `SparseMatrix` and `SparsePrototypeLinearSystemSolver` provide a Phase 7 sparse-data/adapter prototype. The prototype is diagnosable through solver name metadata but still falls back to the dense solver for numerical solving.

### Design

The design layer should read analysis results and calculate code/design checks.

Suggested services:

- `SteelDesignService`
- `ConcreteDesignService`
- `DesignCheckRunner`
- `ThaiLoadCombinationService`
- `ThaiWindLoadService`
- `ThaiSeismicLoadService`
- `ThaiLoadTemplateService`
- `GoPileCalculator`

Design services should not assemble stiffness matrices or mutate solver state.

Current implemented boundary:

- `Core/Design/Steel/SteelDesignService` owns preliminary steel, aluminum, and custom material stress checks and consumes `StructuralModel` plus `ElementForceResult` DTOs.
- `Core/Design/Concrete/ConcreteDesignService` owns preliminary RC rectangular member flexure checks and consumes `StructuralModel` plus `ElementForceResult` DTOs.
- `Core/Design/DesignCheckRunner` owns material-based design-check routing and the existing preliminary RC axial and shear checks; it preserves `DesignCheckResult` ordering and delegates steel/flexure equations to their dedicated services.
- `Core/Design/Foundation/GoPileCalculator` owns preliminary eccentric pile foundation calculations for F1-F5 pile layouts.
- GO Pile consumes explicit foundation input DTOs and returns result DTOs for UI/reporting; it does not depend on `StructuralSolver`.
- `Core/Design/ThaiCode/ThaiLoadTemplateService` owns preliminary Thai load case and load combination templates. It creates model DTOs only and does not generate wind or seismic forces.

### Units

The unit layer should convert between internal SI values and user-facing units.

Rules:

- Internal length: m.
- Internal force: N.
- Internal stress: Pa.
- Internal mass: kg.
- Internal density: kg/m3.
- UI/report units are explicitly converted at boundaries.

### IO

The IO layer should own:

- JSON schema versioning.
- Legacy truss import.
- Project-file migration.
- CSV export.
- Future import/export to external solvers.

### Reporting

The reporting layer should own:

- PDF layout.
- Report templates.
- Report snapshot/view models.
- Thai/English wording.
- Table formatting.
- Engineering assumptions and references.

Reporting should consume result DTOs, not query solver internals directly.

Current implemented boundary:

- `Core/Reporting/AnalysisReportSnapshot` converts stable analysis result DTOs into report-ready summary data.
- `Core/Reporting/PdfReportGenerator` remains a simple PDF writer and now consumes the report snapshot for criteria/limitations and member force envelope sections.

## Visualization

Visualization services may derive display-only geometry from model data, but must not change analysis behavior.

Current implemented boundary:

- `Core/Visualization/SectionVisualProfileService` converts `Section` DTOs into simple visual profiles for rectangular/RC, circular, pipe, I/H, channel/C, box, and generic sections.
- `HelixStructuralView` consumes those profiles when the `Real Sections` layer is enabled.
- Real-section geometry is display-only; `StructuralSolver` continues to use the stored section properties `A`, `Iy`, `Iz`, and `J`.
- Pipe visual rendering currently uses outside diameter only and does not show wall hollowing.

## Recommended Data Flow

```text
User input
  -> BuildingModel
  -> StructuralModel generation
  -> validation
  -> analysis
  -> result DTOs
  -> design services
  -> report/view models
  -> UI and PDF output
```

For lower-level workflows, users and tests may still create a `StructuralModel` directly:

```text
User input
  -> StructuralModel
  -> validation
  -> analysis
  -> result DTOs
  -> design services
  -> report/view models
  -> UI and PDF output
```

## External Solver Strategy

OpenSees should be added as an adapter after the native elastic solver is stable.

Recommended boundary:

```text
IStructuralSolverAdapter
  NativeLinearSolverAdapter
  OpenSeesSolverAdapter
```

The OpenSees adapter should:

- export model data
- run OpenSees as a separate process
- parse result files
- return the same result DTOs used by the native solver

It should not replace `StructuralModel` or the UI data model.
