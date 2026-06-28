# Engineering Principles

This document records the core structural-analysis principles currently used by the project.

## Governing Equation

Linear static structural analysis is based on:

```text
[K]{U} = {F}
```

Where:

- `[K]` is the global stiffness matrix.
- `{U}` is the global displacement vector.
- `{F}` is the global load vector.

After solving, reactions are recovered from:

```text
{R} = [K_original]{U} - {F_original}
```

## Truss Assumptions

Truss elements assume:

- members are pin-connected
- loads are applied through joints or equivalent nodal loads
- members carry axial force only
- no bending, shear, or torsion is recovered
- positive axial force means tension
- negative axial force means compression

For a 3D truss element:

```text
k = AE / L
```

Where:

- `A` is area.
- `E` is Young's modulus.
- `L` is member length.

## Frame Assumptions

Current frame elements assume:

- 6 DOF per node: UX, UY, UZ, RX, RY, RZ
- linear elastic behavior
- small displacement
- Euler-Bernoulli beam-column behavior
- axial, torsion, bending, and shear/end-force recovery

Current limitations:

- no geometric stiffness
- no P-Delta
- no nonlinear release behavior
- no rigid offsets
- no shell/slab/wall coupling

## Self-Weight

Self-weight is calculated directly:

```text
W = rho * A * L * g
```

Where:

- `rho` is material density in kg/m3.
- `A` is section area in m2.
- `L` is member length in m.
- `g` is 9.81 m/s2.

For line elements, the MVP distributes half the weight to each end node in global `-Z`.

## Coordinate Convention

- Global coordinate system is right-handed.
- Global Z is vertical.
- Gravity acts in global `-Z`.
- Member local x-axis runs from start node to end node.
- Member local y/z axes are generated as a right-handed basis and may be rotated by roll angle.

## Equilibrium Check

After analysis, the result should satisfy global force equilibrium:

```text
sum(Fx_applied + Fx_reaction) ~= 0
sum(Fy_applied + Fy_reaction) ~= 0
sum(Fz_applied + Fz_reaction) ~= 0
```

The tolerance should scale with total applied load.

## Common Mistakes To Avoid

- Mixing UI units with internal SI units.
- Applying self-weight more than once.
- Treating truss members as bending members.
- Releasing both bending ends of a member without checking mechanisms.
- Ignoring unconstrained rotational DOFs in frame models.
- Losing sign convention consistency between solver, viewer, and report.
- Adding design checks directly into the analysis solver.

## References For Validation

Use these references for analytical and benchmark examples:

- Hibbeler, Structural Analysis.
- McGuire, Gallagher, and Ziemian, Matrix Structural Analysis.
- Logan, A First Course in the Finite Element Method.
- Closed-form beam tables.
- Trusted commercial software comparison models when available.
- OpenSees comparison models after the adapter is implemented.
