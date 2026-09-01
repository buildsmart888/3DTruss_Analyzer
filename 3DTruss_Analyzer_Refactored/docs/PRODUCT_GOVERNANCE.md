# GOStructAnalysis Product Governance

Status: Milestone A implementation complete; owner approval and engineering qualification remain pending.

## Product Identity

`GOStructAnalysis` is the public product name. `TrussAnalyzer` remains the compatibility name for the
solution, assemblies, namespaces, project files, and legacy APIs until Milestone C supplies tested
migration and rollback paths.

### Naming checklist

| Surface | Required name | Current state |
| --- | --- | --- |
| Window title and About dialog | GOStructAnalysis | Applied |
| Analysis report title | GOStructAnalysis | Applied |
| Assembly product metadata | GOStructAnalysis | Applied |
| README and primary roadmap | GOStructAnalysis | Applied |
| Installer/display metadata | GOStructAnalysis | Required when installer exists |
| Namespaces and assembly filenames | TrussAnalyzer.* | Retained intentionally |
| Current JSON schema v1/v2 | Existing identity/version | Retained intentionally |

The rename must not change numeric behavior, schema interpretation, file extensions, public C# API,
or legacy example loading. A future namespace/assembly rename requires a dedicated compatibility plan,
API shims where practical, file migration tests, and release notes. It must not be bundled with UI work.

## Status Labels

| Label | Meaning | Minimum evidence |
| --- | --- | --- |
| Prototype | Exploration; behavior and contract may change | Named owner and explicit unsupported limitations |
| Preliminary | Implemented for controlled evaluation, not final professional use | Unit/regression tests, assumptions, and visible preliminary notice |
| Validated | Matches approved analytical references for its declared scope | Closed-form/textbook tests, tolerances, versioned evidence, and engineering review |
| Qualified | Approved for an advertised release workflow | Validated evidence plus independent comparison, release checklist, traceable version, and reviewer approval |

No feature may be described with a stronger label than its evidence. “Engineering Preview” is a
release-train label and does not imply that every included calculation is Validated or Qualified.

## Engineering Glossary

| Term | Controlled meaning |
| --- | --- |
| ProjectDocument | Versioned project root containing engineering input, display preferences, and audit metadata; analysis results are separate snapshots. |
| Model3D | Solver-independent spatial engineering model using stable IDs and canonical SI data. |
| StructuralModel | Current C# solver input retained for compatibility; it is not Model3D V1. |
| Node | Point in right-handed Z-up space with six global DOFs: UX, UY, UZ, RX, RY, RZ. |
| Frame3D | Line object capable of axial, torsion, biaxial bending, and shear behavior when adapted to a qualified solver. |
| Truss3D | Pin-jointed axial-only line object; end-release behavior is intrinsic rather than assigned. |
| Local axis | Right-handed member basis with local x from end I/start to end J/end and y/z derived from a nonparallel reference vector plus roll. |
| Canonical SI | m, N, N-m, Pa, kg, rad, and temperature differences in K. |
| Label | User-editable display name; never a persistent reference key. |
| ID | Immutable, non-empty GUID used by every persistent reference. |
| AnalysisSnapshot | Immutable output tied to model checksum, selection, solver/version, warnings, and time; not part of Model3D input. |
| Unsupported | Data may be stored but cannot enter a solver path; preflight must report it before results are presented. |

## Capability Ownership Matrix

| Capability | Production owner | Python role | Current evidence/status |
| --- | --- | --- | --- |
| ProjectDocument and Model3D | C#/.NET | Contract consumer/reviewer | V1 contract implemented; cross-discipline approval pending |
| Native linear 3D Frame-Truss solve | C#/.NET | Independent comparison | Preliminary; automated C# benchmarks, external comparison partly manual |
| OpenSeesPy comparisons | Python | Owner | Engineering toolchain; not production project solve path |
| Optimization/Pareto/parametric studies | Python | Owner | Research/prototype unless promoted through versioned boundary |
| Windows desktop application | C#/.NET | None | WinForms/WPF legacy shell; target WPF workflow not started |
| Persistence, migration, recovery | C#/.NET | Fixture/provider adapters | Existing legacy JSON only; Model3D persistence is Milestone C |
| Results/report orchestration | C#/.NET | Evidence generation | Preliminary basic report path |
| Thai load generation | C#/.NET | Independent studies | Preliminary templates; load magnitude generators unsupported |
| Steel/RC/GO Pile design | C#/.NET | Independent calculations | Preliminary and not code-qualified |
| Shell/plate/solid analysis | Unassigned until approved milestone | Research only | Unsupported |

One advertised production capability has one owner. Python and C# exchange immutable, versioned JSON;
they do not share mutable runtime objects and do not maintain competing production solvers.

## Governance And Approval

Contract-affecting changes require review from these roles before the relevant milestone can exit:

- Domain: identity, references, schema semantics, and unsupported fields.
- Analysis: axes, releases, offsets, constraints, units, and solver adaptation feasibility.
- UI: no UI-only state in the engineering model and editable labels do not break references.
- Reporting: traceability and unit/sign metadata can be represented without solver access.
- Python/qualification: deterministic versioned interchange is sufficient for independent evidence.

Approvals are recorded as named review entries in the milestone section of
`GOSTRUCTANALYSIS_ROADMAP.md`. Absence of an objection is not approval.

## Architecture Decision Records

- [ADR-0001: Product identity and compatibility](adr/0001-product-identity-and-compatibility.md)
- [ADR-0002: Engineering units, axes, and signs](adr/0002-engineering-units-axes-and-signs.md)
- [ADR-0003: Stable identity and persistence boundary](adr/0003-stable-identity-and-persistence-boundary.md)
- [ADR-0004: Solver ownership and Python boundary](adr/0004-solver-ownership-and-python-boundary.md)
