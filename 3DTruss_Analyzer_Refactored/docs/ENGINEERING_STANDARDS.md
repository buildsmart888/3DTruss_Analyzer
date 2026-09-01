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
- Frame member result stations are reported at evenly spaced relative positions along each member; the default is 5 stations and models may request a denser station count. Internal member point loads add coincident left/right stations so discontinuities can be represented without smoothing a shear jump.
- Member point loads and uniform distributed loads are retained in local coordinates after load assembly for diagram recovery. Exact station recovery covers local axial, shear, and bending diagrams, including partial uniform distributed loads. Point moments produce the appropriate local torsion or bending jump.
- Self-weight-only member diagrams and unsupported load/result components retain linear interpolation between recovered local end forces. These preliminary diagrams must not be treated as a full member-load recovery engine.

## Prescribed Support Displacement

- `Node.PrescribedDisplacement` is a global translation in metres and `Node.PrescribedRotation` is a global rotation in radians.
- A non-zero prescribed component is valid only when its matching node DOF is constrained; model validation reports an error otherwise.
- The dense boundary-condition path applies the prescribed value through the standard load correction `F_f - K_fc u_c` before solving.
- Prescribed support values are model-wide, so they apply to every analyzed load case and load combination. Load-case-specific settlement, time effects, and soil-structure interaction are outside the current MVP scope.

## Frame Offset, Release, And Thermal Assumptions

- `StartRigidEndOffset` and `EndRigidEndOffset` are non-negative local-x rigid-zone lengths. `StartInsertionPointLocal` and `EndInsertionPointLocal` are local-coordinate connection offsets in metres.
- Rigid-zone/insertion offsets use a linear node-to-connection kinematic transformation. The flexible length is the distance between offset connection points and must remain positive.
- Moment releases are statically condensed from the local stiffness and equivalent member load before assembly. Released local moment DOFs recover zero moment.
- `FrameAnalysisOptions` defaults to Euler-Bernoulli. Timoshenko uses gross section area and configured Y/Z shear correction factors; use it only after confirming the selected factors for the section family.
- `MemberTemperatureLoad` represents uniform axial temperature change only. Its thermal expansion coefficient is 1/K, and its `TemperatureChange` is a temperature difference in K or degrees C.
- Temperature gradients, through-depth curvature, staged construction, panel-zone flexibility, nonlinear joints, and soil-structure interaction are unsupported.

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

## Mechanism Diagnostics

For singular or unstable linear-solve failures, `MechanismDiagnosticsService` reports suspect node DOFs from zero-stiffness rows and the first rank-deficient Gaussian-elimination pivot.

- Diagnostics preserve the dense solver's `1e-12` absolute pivot threshold.
- Messages are troubleshooting guidance only; they do not establish the complete physical mechanism or replace engineering stability review.
- A successful solve is not proof that all stability, second-order, or serviceability requirements are satisfied.

## Area Object MVP Assumptions

Phase 8 area objects are preliminary model data:

- `AreaObject` IDs must be stable and unique within `StructuralModel.AreaObjects`.
- Boundary nodes must be triangular or quadrilateral and reference existing `Node` IDs.
- `MaterialId` must reference an existing material and `Thickness` must be positive.
- `DiaphragmId` is metadata only until a diaphragm constraint service is implemented.
- Area objects do not contribute stiffness, mass, loads, reactions, design checks, or report result contours.
- Keep conversion to shell elements behind a dedicated service boundary; do not add area/shell conversion logic directly inside UI event handlers or `StructuralSolver`.

## Model3D V1 Contract

- `ProjectDocument` and `Model3D` under `Core/Domain/V1` are specification DTOs, not current solver input.
- Persistent objects use globally unique GUIDs; labels are editable and references never use labels or list positions.
- Model3D JSON stores canonical SI values even when `UnitPreferences` request Thai engineering display units.
- A local-axis reference that is zero, parallel, or numerically near-parallel to member local x is invalid.
- Frame3D supports six explicit end-release flags; all-six release at one end is invalid. Truss3D does not accept assigned release data.
- AreaObject3D is storage/validation only and must be reported as unsupported before analysis results are presented.
- Rigid-link and master-slave dependency cycles are invalid.

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
