# Phase 1 Frame Benchmarks

This document defines the repeatable comparison workflow for the native linear frame solver. All models use SI units: metres, Newtons, Pascals, and radians.

## Automated Baselines

`tests/Phase1FrameEnhancementTests.cs` covers rigid-end axial displacement, insertion-point kinematics, Euler-Bernoulli versus Timoshenko stiffness, restrained temperature loading, released-end UDL recovery, cantilever torsion, and portal-frame equilibrium. Existing tests cover cantilever bending, fixed-fixed point load/UDL actions, settlement, and force diagrams.

## External Comparison Workflow

1. Build the same geometry, material, section properties, supports, local axes, releases, offsets, and load cases in the external program.
2. Use linear static analysis. Disable P-Delta, nonlinear material behavior, automatic rigid zones, auto meshing, diaphragm constraints, and design checks unless the native model explicitly includes them.
3. Match units before entering values. Temperature coefficient is 1/K and temperature change is K or degrees C difference.
4. Compare node translations/rotations, reactions, local member-end forces, and force diagrams for every benchmark load case.
5. Record the external software version, analysis settings, and differences in the project validation record.

## Acceptance Targets

- Closed-form cases: relative difference no greater than `1e-6` where numerical conditioning permits.
- External elastic comparisons: target relative difference no greater than `1e-3`; investigate anything larger before engineering use.
- For releases, verify released end moments are zero within solver tolerance.
- For Timoshenko comparisons, match shear-area and correction-factor assumptions because external defaults commonly differ.

## Current Limits

- No ETABS, STAAD, or OpenSees executable is invoked by this repository, so external comparisons remain manual validation records.
- Rigid ends and insertion points use a linear kinematic connection-offset transformation. They do not model panel-zone deformation, nonlinear joint behavior, or automatic offset rules.
- Temperature load is uniform axial member temperature only. Gradients, curvature, staged temperature, and thermal restraint by soil/foundation are unsupported.
