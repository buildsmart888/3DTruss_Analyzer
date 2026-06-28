# Development Guide

This guide describes how to develop the project safely as it grows from a truss/frame MVP into a building analysis and design application.

## Development Rule Of Thumb

New work should target the `StructuralModel` path unless the task is specifically about legacy `TrussSolver` compatibility.

Keep `TrussSolver` stable for old examples/tests. Add new capabilities through the newer structural pipeline.

## Build And Test

Run before handing off any code change:

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

Run the desktop UI:

```bash
dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj
```

## Current Core Pipeline

`StructuralSolver` currently follows this high-level order:

1. Model validation through `Core/Analysis/Validation/ModelValidator`.
2. Node and element lookup.
3. DOF numbering with 6 DOF per node: UX, UY, UZ, RX, RY, RZ.
4. Global stiffness assembly.
5. Load vector assembly.
6. Boundary condition application.
7. Linear solve.
8. Reaction recovery from the original stiffness matrix and force vector.
9. Element force recovery.
10. Solver diagnostics.
11. Preliminary design/safety checks.

## Refactoring Direction

As the project grows, split solver responsibilities into smaller services:

- `ModelValidator` (implemented under `Core/Analysis/Validation`)
- `DofIndexer`
- `ElementStiffnessProvider`
- `GlobalStiffnessAssembler`
- `LoadVectorAssembler`
- `BoundaryConditionApplier`
- `LinearSystemSolver`
- `ElementForceRecoveryService`
- `SolverDiagnosticsService`
- `SteelDesignService`
- `ConcreteDesignService`

Do not add more design-code logic directly inside `StructuralSolver`.

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
