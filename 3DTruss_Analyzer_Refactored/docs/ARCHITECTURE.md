# Architecture

This document describes the recommended architecture for growing the current MVP into a maintainable 3D structural analysis and design application.

## Current Architecture

The project currently has two analysis paths:

- `TrussSolver`: legacy compatibility facade for axial-only truss analysis.
- `StructuralSolver`: newer solver path for `StructuralModel`, truss elements, and 3D frame elements.

The current `StructuralSolver` still performs many responsibilities in one class:

- DOF indexing
- stiffness assembly
- load assembly
- boundary-condition handling
- linear solving
- reaction recovery
- element force recovery
- diagnostics
- preliminary RC and material-routing design checks

Model validation has been extracted to `Core/Analysis/Validation/ModelValidator`, with `StructuralSolver.ValidateModel()` kept as the compatibility entrypoint. The remaining solver responsibilities should continue to be split before adding building, steel design, RC design, shell elements, and Thai code modules.

The first building workflow layer is available as `Core/Models/BuildingModel.cs`. It contains grid lines, stories, beam objects, column objects, supports, and nodal loads, and generates an ordinary `StructuralModel` for analysis/export.

The first Phase 8 area-object layer is available as `Core/Models/AreaObject.cs`. Area objects are stored on `StructuralModel.AreaObjects` and serialized with the model, but they are intentionally not consumed by `StructuralSolver` until a validated shell/diaphragm path exists.

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
- `ElementStiffnessProvider`
- `GlobalStiffnessAssembler`
- `LoadVectorAssembler`
- `BoundaryConditionApplier`
- `LinearAnalysisSolver`
- `ElementForceRecoveryService`
- `SolverDiagnosticsService`

Current implemented boundary:

- `Core/Analysis/Validation/ModelValidator` owns structural model validation messages used by the structural solver and UI diagnostics.
- `Core/Utilities/ILinearSystemSolver` is the explicit solver boundary used by `StructuralSolver`.
- `DenseLinearSystemSolver` remains the default validated solver path.
- `SparseMatrix` and `SparsePrototypeLinearSystemSolver` provide a Phase 7 sparse-data/adapter prototype. The prototype is diagnosable through solver name metadata but still falls back to the dense solver for numerical solving.

### Design

The design layer should read analysis results and calculate code/design checks.

Suggested services:

- `SteelDesignService`
- `ConcreteDesignService`
- `ThaiLoadCombinationService`
- `ThaiWindLoadService`
- `ThaiSeismicLoadService`
- `ThaiLoadTemplateService`
- `GoPileCalculator`

Design services should not assemble stiffness matrices or mutate solver state.

Current implemented boundary:

- `Core/Design/Steel/SteelDesignService` owns preliminary steel, aluminum, and custom material stress checks and consumes `StructuralModel` plus `ElementForceResult` DTOs.
- `Core/Design/Concrete/ConcreteDesignService` owns preliminary RC rectangular member flexure checks and consumes `StructuralModel` plus `ElementForceResult` DTOs.
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
