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
- preliminary design checks

Model validation has been extracted to `Core/Analysis/Validation/ModelValidator`, with `StructuralSolver.ValidateModel()` kept as the compatibility entrypoint. The remaining solver responsibilities should continue to be split before adding building, steel design, RC design, shell elements, and Thai code modules.

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
- `Material`
- `Section`
- `LoadCase`
- `LoadCombination`

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

### Design

The design layer should read analysis results and calculate code/design checks.

Suggested services:

- `SteelDesignService`
- `ConcreteDesignService`
- `ThaiLoadCombinationService`
- `ThaiWindLoadService`
- `ThaiSeismicLoadService`

Design services should not assemble stiffness matrices or mutate solver state.

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
- Thai/English wording.
- Table formatting.
- Engineering assumptions and references.

Reporting should consume result DTOs, not query solver internals directly.

## Recommended Data Flow

```text
User input
  -> BuildingModel or StructuralModel
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
