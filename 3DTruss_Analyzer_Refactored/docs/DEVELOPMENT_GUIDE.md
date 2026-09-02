# Development Guide

This guide describes how to develop the project safely as it grows from a truss/frame MVP into a building analysis and design application.

## Development Rule Of Thumb

New work should target the `StructuralModel` path unless the task is specifically about legacy `TrussSolver` compatibility.

Keep `TrussSolver` stable for old examples/tests. Add new capabilities through the newer structural pipeline.

For future project/domain work, target `Core/Domain/V1/ProjectDocument` and `Model3D` only after reading
`MODEL3D_V1_SPEC.md`. Do not connect Model3D directly to the solver or replace current JSON persistence;
use a separately reviewed adapter/migration slice and preserve all unsupported-data diagnostics.

## Build And Test

Run before handing off any code change:

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

Migrate an existing C# JSON file to a reviewable `.gosa` package:

```bash
dotnet run --project tools/ProjectMigration/ProjectMigration.csproj -- input.json output.gosa
```

Review every migration warning before replacing an existing project. The CLI does not modify its input.

Run the desktop UI:

```bash
dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj
```

## Current Core Pipeline

`StructuralSolver` currently follows this high-level order:

1. Model validation through `Core/Analysis/Validation/ModelValidator`.
2. Node and element lookup.
3. DOF numbering with 6 DOF per node: UX, UY, UZ, RX, RY, RZ.
4. `LinearAnalysisRunner` performs global stiffness assembly, load vector assembly, boundary condition application, and linear solve.
5. Reaction recovery from the original stiffness matrix and force vector.
6. Element force recovery.
7. Preliminary design/safety checks.
8. Global translational equilibrium check.
9. Solver diagnostics.

## Application Foundation

New document lifecycle or UI workflow work must use `Core/Application` rather than call persistence or
`StructuralSolver` from a control event handler. `ProjectAnalysisService` is the supported route for a
`ProjectDocument` analysis request; it returns a GUID-keyed `AnalysisSnapshot` only after Model3D,
adapter, and solver preflight succeeds. Do not replace this with a direct Model3D-to-solver call.

The staged WPF shell may be launched explicitly while the WinForms workspace remains the default:

```bash
dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj -- --wpf-shell
```
10. Analysis result DTO assembly.

## Refactoring Direction

As the project grows, split solver responsibilities into smaller services:

- `ModelValidator` (implemented under `Core/Analysis/Validation`)
- `DofIndexer`
- `FrameElementStiffnessProvider` (implemented for local truss/frame stiffness, optional Timoshenko shear deformation, and condensed moment releases)
- `FrameElementGeometryResolver` (implemented for rigid-end zones, local insertion points, and connection kinematics)
- `GlobalStiffnessAssembler` (implemented for dense local-to-global stiffness accumulation)
- `LoadVectorAssembler` (implemented for nodal, member, and self-weight load assembly with equivalent local member-load recovery data)
- `BoundaryConditionApplier` (implemented for dense-matrix support constraints and prescribed displacement/rotation load correction)
- `LinearSystemSolver`
- `LinearAnalysisRunner` (implemented for common load-case/load-combination analysis and solve flow)
- `ReactionRecoveryService` (implemented for node reactions from the original global stiffness matrix, load vector, and solved displacements)
- `EquilibriumCheckService` (implemented for the current translational force equilibrium and tolerance calculation)
- `ElementForceRecoveryService` (implemented for local displacement recovery, equivalent load subtraction, end forces, and load-aware axial/shear/torsion/bending station results for member point/UDL loads, with compatibility interpolation for unsupported load types)
- `SolverDiagnosticsService` (implemented for solver metrics, dense-path warnings, and diagnostic notes)
- `AnalysisResultBuilder` (implemented for node/element result construction, node-state updates, and final analysis result DTO assembly)
- `SteelDesignService`
- `ConcreteDesignService`
- `DesignCheckRunner` (implemented for material-based design routing plus preliminary RC axial and shear checks)

Do not add more design-code logic or design routing directly inside `StructuralSolver`.

## Engineering Assumptions

- Internal units are SI: m, N, Pa, kg, kg/m3.
- Analysis is currently linear elastic and small displacement.
- Truss elements are pin-jointed axial-only members.
- Frame elements are MVP 3D Euler-Bernoulli beam-column elements.
- Self-weight is based on material density, section area, member length, and gravity.
- Load combinations use load case factors and include self-weight when a referenced load case has `IncludeSelfWeight = true`.
- Frame members can use local roll angle and simple end moment releases about local Y/Z.

## Coding Standards

- Prefer clear C# names over abbreviations.
- Keep engineering formulas close to named variables.
- Add comments only where an equation or convention is not obvious.
- Keep UI event handlers thin.
- Keep solver code independent from UI controls.
- Keep report formatting independent from solver internals.
- Keep Thai user-facing text in resources/templates when practical.
- Avoid adding hidden unit conversions inside analysis code.

## Testing Expectations

Core changes should include focused tests for:

- matrix solve behavior
- model validation
- truss compatibility
- frame cantilever benchmark displacement
- axial frame behavior
- nodal moment loads
- member distributed loads
- member point loads and fixed-end force recovery
- local axis roll and frame release validation
- self-weight
- load combinations
- section property creation
- design checks
- schema v1 and schema v2 JSON import/export
- solver diagnostics
- report/export content

## Debugging Tips

Singular matrix errors usually mean:

- missing supports
- unconstrained rotational DOFs
- truss-only mechanism
- zero-length or disconnected elements
- frame element missing positive A, Iy, Iz, or J
- releases creating an unintended mechanism

Unexpected reactions usually mean:

- loads were applied to a different load case than expected
- self-weight was included or omitted unintentionally
- support constraints do not match the intended model
- a member load was entered in global direction but expected to be local
- local axes or roll angle are not what the user intended

## Documentation Updates

Update documentation when behavior changes:

- README for user-visible capabilities.
- `docs/ROADMAP.md` for roadmap status.
- `docs/ARCHITECTURE.md` for module boundaries.
- `docs/ENGINEERING_STANDARDS.md` for engineering assumptions.
- `docs/QUALITY_PLAN.md` for testing expectations.
