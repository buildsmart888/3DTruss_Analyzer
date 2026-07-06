# Engineering Standards

This document records engineering assumptions and standards for development. It is not a substitute for licensed professional engineering judgment.

## Internal Units

All core calculations use SI base units:

- Length: m
- Force: N
- Moment: N-m
- Stress: Pa
- Mass: kg
- Density: kg/m3
- Rotation: rad

User-facing UI and reports may display:

- kN
- tonf
- kgf
- m
- cm
- mm
- MPa

Conversions must happen at UI/report/import/export boundaries, not inside solver equations.

## Coordinate System

- Right-handed coordinate system.
- X and Y are plan axes.
- Z is vertical.
- Gravity acts in global `-Z`.
- Member local `x` runs from start node to end node.
- Member local `y/z` form a right-handed local coordinate system.

## Analysis Assumptions

Current MVP assumptions:

- Linear elastic material behavior.
- Small displacement.
- Static analysis.
- Euler-Bernoulli frame behavior.
- Truss elements are axial-only.
- Loads are applied as nodal loads or converted to equivalent nodal loads.
- Frame member result stations are reported at evenly spaced relative positions along each member; the default is 5 stations and models may request a denser station count.

Known unavailable behavior:

- Geometric nonlinearity.
- Material nonlinearity.
- Plastic hinges.
- Concrete cracking.
- P-Delta.
- Modal analysis.
- Response spectrum.
- Time history.
- Shell/slab/wall solver behavior.

## Area Object MVP Assumptions

Phase 8 area objects are preliminary model data:

- `AreaObject` IDs must be stable and unique within `StructuralModel.AreaObjects`.
- Boundary nodes must be triangular or quadrilateral and reference existing `Node` IDs.
- `MaterialId` must reference an existing material and `Thickness` must be positive.
- `DiaphragmId` is metadata only until a diaphragm constraint service is implemented.
- Area objects do not contribute stiffness, mass, loads, reactions, design checks, or report result contours.
- Keep conversion to shell elements behind a dedicated service boundary; do not add area/shell conversion logic directly inside UI event handlers or `StructuralSolver`.

## Section Visualization Assumptions

3D real-section display is visual-only:

- Analysis uses explicit section properties `Area`, `Iy`, `Iz`, and `J`; rendered geometry must not be treated as the source of stiffness.
- Rectangular and RC rectangular sections use `Width` and `Depth`.
- I/H and channel/C visual profiles use `Width`, `Depth`, and a simplified uniform `Thickness`.
- Pipe sections are rendered by outside diameter only in the current viewer slice.
- Generic sections fall back to `Diameter`, `Width/Depth`, or an equivalent square from `Area`.
- Rendered section orientation follows member local axes and `RollAngleRadians`.

## Validation Requirements

Every new analysis feature should include at least one of:

- closed-form benchmark
- textbook benchmark
- regression model
- comparison with trusted software
- comparison with OpenSees after the adapter exists

Minimum checks:

- displacement
- reaction equilibrium
- member force sign convention
- governing internal force
- load combination behavior
- project-file round trip when input schema changes

## Design Code Strategy

Design modules should be explicit about:

- code name
- code year/version
- assumptions
- load combination type
- resistance factor or safety factor
- unsupported cases

Avoid hardcoding code factors deep inside solver or UI event handlers. Prefer dedicated design-code services or versioned data tables.

## Reinforced Concrete MVP Checks

Current RC design checks are preliminary and limited:

- RC flexure supports rectangular concrete members with explicit `RebarArea` and `EffectiveDepth` section properties.
- Flexure demand comes from `ElementForceResult` as the larger of local `MomentY` and `MomentZ`.
- Flexure capacity currently uses the simplified expression `phi * As * fy * d`.
- If concrete material yield strength is not set, reinforcing steel yield defaults to `DesignSettings.DefaultRebarYieldStrength`.

Unsupported RC cases include minimum/maximum reinforcement checks, strain compatibility, compression block depth limits, doubly reinforced sections, bar spacing/layout, shear reinforcement design, torsion, punching shear, column P-M interaction, slenderness effects, and final Thai code calibration.

## Thai Engineering Scope

The product should be optimized for common Thai workflows:

- reinforced-concrete buildings
- steel buildings
- warehouses
- factory roofs
- roof trusses
- portal frames
- mixed steel/concrete structures

Future Thai code modules should cover:

- live load templates
- wind load templates
- seismic equivalent static load
- serviceability checks
- strength load combinations
- Thai report wording and unit preferences

## Thai Load Template Naming

Preliminary Thai load templates use these case IDs:

- `DL`: dead load, including self-weight when requested by the analysis model.
- `SDL`: superimposed dead load.
- `LL`: occupancy live load.
- `RL`: roof live load.
- `WLX+`, `WLX-`, `WLY+`, `WLY-`: directional wind load placeholders.
- `EQX+`, `EQX-`, `EQY+`, `EQY-`: directional seismic load placeholders.

Preliminary load combination IDs use these prefixes:

- `SVC-*`: service-level combinations.
- `STR-*`: strength-level combinations.
- `UPL-*`: uplift or overturning-oriented combinations.

The Phase 3 template service creates traceable load cases and load combinations only. It does not calculate wind pressure, seismic base shear, exposure factors, site coefficients, response coefficients, or automatic member/area load distribution. Combination factors are preliminary templates and must be reviewed before professional design use.

## Engineering Output Policy

Until validated design modules exist, reports must state:

- analysis assumptions
- current limitations
- whether design checks are preliminary
- whether output is suitable for final professional design
