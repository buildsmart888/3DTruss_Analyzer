# Model3D V1 Specification

Status: implemented for review. Domain, analysis, UI, reporting, and Python approvals are still required
before Milestone B exits.

Normative artifacts:

- C# contract: `src/Core/Domain/V1/Model3DContracts.cs`
- JSON behavior: `src/Core/Domain/V1/ProjectDocumentJson.cs`
- Validation: `src/Core/Domain/V1/Model3DValidation.cs`
- JSON Schema: `docs/schema/model3d-v1.schema.json`
- Example: `examples/model3d/v1/minimal-frame.json`

## Purpose And Boundary

`ProjectDocument` is the versioned root and `Model3D` is solver-independent engineering input. V1 does
not solve, save production `.gosa` packages, migrate legacy projects, implement authoring UI, or define
plate/shell/solid stiffness. Existing `StructuralModel` and schema v1/v2 remain the operational solver and
file path until Milestone C/G adapters are approved.

Analysis output is not stored in `Model3D`. Future `AnalysisSnapshot` data must reference a checksum and
the selected pattern/combination, solver/version, assumptions, timestamp, and warnings.

## Identity And Equality

- Every persistent object implements `IPersistentModelObject` with a non-empty `Guid Id` and editable `Label`.
- IDs are globally unique within a `ProjectDocument`; all references use IDs.
- Value DTOs are records and use value equality.
- Record updates, rename, deletion followed by undo restoration, and import preserve IDs.
- Explicit copies receive new IDs; `Model3DIdentity.Copy` demonstrates the rule for nodes.
- Duplicate IDs are errors. Duplicate labels within one object type are warnings because labels can be renamed safely.

## Units, Coordinates, And Serialization

- Stored engineering values are canonical SI: m, N, N-m, Pa, kg, rad, and temperature differences in K.
- Display units are `UnitPreferences` metadata only.
- Global coordinates are right-handed and Z-up. Gravity is global -Z.
- JSON property and enum names use camelCase. GUIDs use standard JSON strings.
- Unknown properties, integer enum values, malformed JSON, and non-V1 schema versions are rejected.
- Round-trip must preserve IEEE-754 numeric values and IDs; JSON member order is not semantic.
- Collection order is presentation/order metadata only and must never be used as identity.

## Geometry And Objects

### Node3D

A node stores its position and references a support, zero or more springs, and zero or more prescribed
movement definitions. The six DOFs are UX, UY, UZ in metres and RX, RY, RZ in radians.

Support restraints are booleans. Spring UX-UZ stiffness uses N/m; RX-RZ uses N-m/rad. Prescribed movement
references a load pattern and uses metres/radians. Case-specific solver application is deferred.

### Frame3D And Truss3D

Both line objects reference start/end nodes, material, and section. Local x is start/I to end/J. A global
reference vector is projected normal to x, then `rollRadians` rotates y/z about x. Zero, parallel, or
near-parallel reference vectors are validation errors. `LocalAxisBasis.Create` is the normative V1 basis
algorithm and is tested for horizontal, vertical, inclined, rolled, and near-parallel cases.

Insertion offsets are local vectors in metres. Rigid offsets are non-negative local-x lengths in metres.
Frame ends expose six release flags plus optional non-negative finite partial-fixity values. Releasing all
six DOFs at one frame end is invalid. Truss rotational freedom is intrinsic; user-assigned Truss3D release
data is invalid to avoid two competing meanings.

### Material3D And Section3D

Materials store E, G, Poisson ratio, density, thermal expansion, kind, and source metadata. Sections store
explicit A, Iy, Iz, J, shear areas, shape, display dimensions, and source metadata. Display dimensions
never derive or silently replace analysis properties.

### Organization And Constraints

Levels store SI elevation; X/Y grids store SI coordinate; groups store stable object IDs. Rigid links and
master-slave constraints name master/slave node IDs and coupled DOFs. Self-reference, empty coupling,
duplicate slaves, missing nodes, and dependency cycles are errors.

### AreaObject3D

V1 stores and validates triangular/quadrilateral area boundaries and optional material/section references.
Its only behavior is `storageOnly`. Every area object produces an unsupported-analysis preflight warning;
an analysis adapter must turn this into a blocking error before presenting line-analysis results if the
area object is expected to participate structurally. No stiffness, mass, load distribution, diaphragm,
meshing, or results are implied.

## Loads, Mass, Combinations, And Results Selection

- Load patterns own type, self-weight multiplier, label, and source metadata.
- Assignments have stable IDs and reference both a load pattern and target object.
- V1 includes nodal force/moment and uniform/partial line force-per-length DTOs. Values use N, N-m, and N/m.
- Mass source explicitly names element mass inclusion and load-pattern factors.
- Linear combinations reference load-pattern IDs with numeric factors.
- Analysis cases reference patterns; result selections reference patterns, combinations, or an envelope set.
- Missing references and invalid relative line-load extents are errors.

Properties for future load shapes, nonlinear analysis, design codes, shell behavior, and result snapshots
must not be guessed from V1 fields. They require a new schema version or an explicitly backward-compatible
extension with deterministic defaulting and migration rules.

## Validation Contract

`Model3DValidator` returns structured `ModelValidationIssue` values with severity, code, object ID/type,
and message. Errors block the consuming operation. Warnings require visible review but may not block
storage-only workflows. Information is advisory.

V1 detects duplicate/empty identity, duplicate/empty labels, missing references, zero length, invalid
material/section/spring/release/local axis, invalid load extents, invalid/cyclic constraints, and unsupported
area analysis. Import syntax/schema failure is reported by `ProjectDocumentFormatException` rather than
returning a partially defaulted document.

## Review Checklist

- [ ] Domain reviewer approves identity, references, defaulting, and version semantics.
- [ ] Analysis reviewer approves axes, units, releases, offsets, springs, and constraint semantics.
- [ ] UI reviewer confirms there are no control/view-specific engineering fields.
- [ ] Reporting reviewer confirms traceability and unit/sign metadata are sufficient.
- [ ] Python reviewer round-trips the schema without identity or numeric drift.
- [x] C# creation, equality, round-trip, malformed/unknown JSON, identity, units, geometry, release,
      spring, constraint, and unsupported-area tests pass.
- [x] Current C# `StructuralModel ↔ Model3D` adapter has frame parity coverage and explicit lossy-data diagnostics.
- [ ] Adapter behavior is reviewed by the Domain and Analysis reviewers before it is used in a migration workflow.
