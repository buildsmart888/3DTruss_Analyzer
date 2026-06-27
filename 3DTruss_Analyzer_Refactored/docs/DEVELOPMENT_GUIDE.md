# Development Guide

## Architecture

The project now has two supported analysis layers:

- `TrussSolver`: legacy compatibility facade for axial-only truss analysis and existing examples/tests.
- `StructuralSolver`: MVP 3D structural solver for `StructuralModel`, including truss and 3D frame elements.

Keep new features on the `StructuralModel` path unless the change is explicitly about legacy truss compatibility.

## Core Pipeline

`StructuralSolver` follows this order:

1. Model validation.
2. Node and element lookup.
3. DOF numbering with 6 DOF per node: UX, UY, UZ, RX, RY, RZ.
4. Global stiffness assembly.
5. Load vector assembly.
6. Boundary condition application.
7. Dense solve through `Matrix.SolveAuto()`.
8. Reaction recovery from the original stiffness matrix and force vector.
9. Element force recovery.
10. Solver diagnostics and preliminary design/safety checks.

## Engineering Assumptions

- Units are SI: m, N, Pa, kg/m3.
- Analysis is linear elastic and small displacement.
- Truss elements are pin-jointed axial-only members.
- Frame elements are MVP 3D Euler-Bernoulli beam-column elements.
- Self-weight is based on material density, section area, member length, and gravity.
- Load combinations use load case factors and include self-weight when a referenced load case has `IncludeSelfWeight = true`.
- Frame members can use local roll angle and simple end moment releases about local Y/Z.

## Known Limitations

- No shell, slab, wall, plate, solid, cable, nonlinear concrete cracking, plastic hinge, P-Delta, dynamic, modal, or seismic response spectrum analysis.
- No automatic code wind/seismic load generator.
- No rigid offsets, shear deformation, warping torsion, true sparse storage, or nonlinear release behavior yet.
- Distributed and point member loads are converted through equivalent nodal loads and recovered as fixed-end force effects.
- Preliminary design checks are not final AISC/ACI code checks.
- The OpenGL viewer uses immediate-mode drawing for MVP simplicity.
- The dense matrix solver is fine for small models; `ILinearSystemSolver` allows replacement, but the sparse implementation is currently a placeholder.

## UI Notes

- The WinForms editor uses tabs for geometry, materials, sections, loads, combinations, and validation.
- Element rows can be set to `Truss` or `Frame3D`.
- Element rows reference material and section IDs, with inline fallback values for quick entry.
- The OpenTK/OpenGL viewer supports left-drag rotation, wheel zoom, labels, load/support glyphs, local axes, view presets, color modes, and deformed-shape display.

## Testing Expectations

Core changes should include focused tests for:

- Matrix solve behavior.
- Model validation.
- Truss compatibility.
- Frame cantilever benchmark displacement.
- Axial frame behavior.
- Nodal moment loads.
- Member distributed loads.
- Member point loads and fixed-end force recovery.
- Local axis roll and frame release validation.
- Self-weight.
- Load combinations.
- Section property creation.
- Preliminary steel/RC design checks.
- Schema v1 and schema v2 JSON import/export.
- Solver diagnostics and report/export content.

Before handing off a change, run:

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

## Debugging Tips

Singular matrix errors usually mean:

- Missing supports.
- Unconstrained rotational DOFs.
- A truss-only mechanism.
- Zero-length or disconnected elements.
- A frame element missing positive A, Iy, Iz, or J.

Unexpected reactions usually mean:

- Loads were applied to a different load case than expected.
- Self-weight was included or omitted unintentionally.
- Support constraints do not match the intended model.
- A member load was entered in global direction but expected to be local.
