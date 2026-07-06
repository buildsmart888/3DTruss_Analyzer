# Development Roadmap

This roadmap describes the recommended path from the current 3D Truss Analyzer MVP to a maintainable 3D structural analysis and design program for Thai structural engineering practice.

## Guiding Principles

- Keep the product core in C#/.NET.
- Preserve legacy truss compatibility while moving new work to `StructuralModel`.
- Keep analysis, design, UI, reporting, and file IO as separate concerns.
- Use SI base units internally.
- Treat Thai engineering units as a presentation/input concern handled by a unit layer.
- Validate every analysis feature against closed-form examples, textbooks, and at least one trusted external tool.
- Add OpenSees later as an optional advanced solver, not as the first product foundation.

## Phase 0 - Foundation Cleanup

Goal: make the current MVP easier to maintain before adding large building features.

Status: recommended next phase.

Scope:

- Rename product documentation from truss-only wording to structural analysis wording.
- Keep namespaces stable until a planned migration is scheduled.
- Split the large `StructuralSolver` responsibilities into smaller services.
- Add explicit architecture boundaries:
  - model validation
  - element formulation
  - global assembly
  - load assembly
  - boundary-condition handling
  - linear system solving
  - result recovery
  - diagnostics
  - design checks
- Fix Thai text encoding in documentation.
- Prefer English identifiers and source comments.
- Move Thai UI/report text into resources or templates.
- Add coding standards and review checklist.

Deliverables:

- Updated README and docs.
- `Analysis/`, `Design/`, and `Reporting/` boundaries defined.
- Existing tests still pass.
- No behavior change unless covered by tests.

Acceptance criteria:

- `dotnet build TrussAnalyzer.sln` succeeds.
- `dotnet test TrussAnalyzer.sln` succeeds.
- Existing JSON examples still import.
- Existing truss compatibility tests still pass.

## Phase 1 - Reliable Linear 3D Frame Engine

Goal: make the native frame solver trustworthy for steel frames, roof structures, portal frames, and simple building frames.

Scope:

- Refine `FrameElement3D` stiffness and force recovery.
- Improve member end releases.
- Add rigid end offsets.
- Add member insertion point and local-axis controls.
- Improve member point and distributed load recovery.
- Add more station results for diagrams.
- Add support settlement and imposed displacement.
- Add temperature load.
- Add optional shear deformation path if Timoshenko behavior is needed.
- Add robust mechanism and instability diagnostics.
- Add large-model solver warnings with actionable messages.

Validation:

- Cantilever beam closed-form deflection and reactions.
- Fixed-fixed beam point load and UDL fixed-end actions.
- Portal frame benchmark.
- 3D frame benchmark with torsion.
- Comparison with ETABS/STAAD/OpenSees for selected elastic examples.

Deliverables:

- Production-grade linear frame analysis for small to medium models.
- Force diagrams for axial, shear Y/Z, torsion, moment Y/Z.
- Clear diagnostic messages for singular matrices and mechanisms.

## Phase 2 - Building Modeling Workflow

Goal: stop forcing users to model only nodes and elements; introduce building objects that generate analysis models.

Status: started. A minimal `BuildingModel` layer can now generate a simple frame `StructuralModel` from grid lines, stories, beams, columns, supports, and nodal loads.

Scope:

- `BuildingModel` layer above `StructuralModel`.
- Grid system.
- Story/level system.
- Beam, column, brace, truss, and roof member objects.
- Column stack tools.
- Beam layout tools.
- Common support templates.
- Floor load and roof load assignment.
- Tributary load distribution to beams.
- Rigid diaphragm approximation.
- Story drift and story shear summaries.
- Object selection and editing workflows in the UI.

Recommended data flow:

```text
BuildingModel
  -> model generation
  -> StructuralModel
  -> analysis
  -> design
  -> reporting
```

Deliverables:

- Engineers can create a simple building by grid and story rather than manually entering every node.
- Generated `StructuralModel` remains inspectable and exportable.
- Building objects keep stable IDs for editing and report traceability.

Current implemented subset:

- `GridLine`, `Story`, `BeamObject`, `ColumnObject`, `BuildingSupport`, and `BuildingNodalLoad`.
- `BuildingModel.ToStructuralModel()` for small 3D frame models.
- Tests cover a 1-bay, 1-story frame with fixed supports and a lateral nodal load.

Current limitations:

- No floor/roof area loads or tributary load distribution yet.
- No rigid diaphragm approximation or story drift summaries yet.
- No UI object editing workflow yet.
- Generated structural elements do not yet carry source building-object traceability metadata.

## Phase 3 - Thai Load Generation And Load Combinations

Goal: make the program useful for Thai building practice.

Status: started. A preliminary Thai load template service can generate standard load cases and preliminary load combination templates. Full wind pressure and seismic coefficient calculations are not implemented yet.

Scope:

- Dead load and superimposed dead load templates.
- Live load templates by occupancy/use.
- Roof live load templates.
- Wind load generator for common low-rise and medium-rise buildings.
- Seismic equivalent static load generator.
- Thai load combination templates.
- Load pattern naming conventions:
  - `DL`
  - `SDL`
  - `LL`
  - `RL`
  - `WLX+`
  - `WLX-`
  - `WLY+`
  - `WLY-`
  - `EQX+`
  - `EQX-`
  - `EQY+`
  - `EQY-`
- Serviceability and strength combinations.
- User-visible assumptions and code references in reports.

Design notes:

- Keep code coefficients in versioned data files or dedicated classes.
- Do not bury code factors inside UI event handlers.
- Every generated load must be traceable to source parameters.

Deliverables:

- Load wizard for common Thai projects.
- Reviewable load summary table.
- Generated load cases and combinations.
- Report-ready calculation notes.

Current implemented subset:

- `ThaiLoadTemplateService` generates `DL`, `SDL`, `LL`, `RL`, `WLX+`, `WLX-`, `WLY+`, `WLY-`, `EQX+`, `EQX-`, `EQY+`, and `EQY-`.
- Preliminary service, strength, and uplift/overturning combinations are generated with traceable names and descriptions.

Current limitations:

- Wind and seismic load magnitudes are placeholders; no pressure, exposure, site, response, or equivalent static coefficient calculations are performed.
- Combination factors are preliminary templates and require engineering review before production design use.

## Phase 4 - Steel Design Module

Goal: support warehouses, factories, roof structures, and steel building frames first.

Status: started. Preliminary steel design checks have been moved from `StructuralSolver` into `SteelDesignService` while preserving the existing `DesignCheckResult` output shape.

Scope:

- Steel material library.
- Thai/common steel section library:
  - H/I shapes
  - channels
  - angles
  - pipes
  - rectangular/square tubes
  - built-up sections later
- Tension check.
- Compression buckling check.
- Major/minor flexure checks.
- Shear checks.
- Axial plus bending interaction.
- Unbraced length parameters.
- Effective length factors.
- Design grouping.
- Utilization contour and member color mode.
- Design report per member and governing combination.

Implementation:

- Move current preliminary steel checks out of `StructuralSolver`. Implemented for the MVP stress checks.
- Add `SteelDesignService`. Implemented as a preliminary service consuming model data and element force results.
- Add explicit design code profile/version.
- Keep demand extraction separate from capacity calculation.

Deliverables:

- Steel member design summaries.
- Member-by-member governing utilization.
- Thai/English report wording prepared for later localization.

Current implemented subset:

- `SteelDesignService` performs the existing preliminary axial yield, flexure, compression buckling, shear, and axial-plus-bending checks for steel, aluminum, and custom material members.
- `StructuralSolver` still returns `DesignCheckResult` entries, but steel equations are no longer owned directly by the solver.

Current limitations:

- Checks remain preliminary stress checks and are not a full code-calibrated Thai/AISC steel design standard.
- Section classification, unbraced length controls, detailed member capacity equations, and design grouping remain future work.

## Phase 5 - Report Engine And Thai Deliverables

Goal: produce calculation documents that engineers can review and archive.

Status: started. The legacy PDF report now has a small report snapshot boundary and includes criteria/limitations plus a member force envelope section.

Scope:

- Template-based PDF reporting.
- Project information.
- Engineer and reviewer information.
- Design criteria.
- Unit settings.
- Model summary.
- Material and section tables.
- Load case and combination tables.
- Reaction tables.
- Displacement/drift tables.
- Member force envelopes.
- Steel design results.
- RC design results when available.
- Appendix for generated loads and assumptions.

Implementation:

- Keep report templates separate from solver output.
- Use stable result DTOs. Started with `AnalysisReportSnapshot` for legacy analysis results.
- Add report snapshot tests where practical. Started with content tests for the criteria/limitations and member force envelope sections.
- Render sample reports during release QA.

Deliverables:

- Thai calculation report suitable for internal engineering review.
- Report examples in `examples/reports/` when available.

Current implemented subset:

- PDF report includes project summary, criteria/limitations, member force envelope, displacement summary, reaction summary, equilibrium check, and safety checks.
- Report text remains English but is centralized enough to move into resources/templates later.

Current limitations:

- The PDF writer is still a basic single-page text-stream generator, not a production report layout engine.
- Project metadata, engineer/reviewer information, and full Thai localization are future work.

## Phase 6 - Reinforced Concrete Frame Design

Goal: add practical RC beam and column design.

Status: started. A preliminary `ConcreteDesignService` performs RC rectangular flexure checks using existing analysis moment demand and section reinforcement properties.

Scope:

- Concrete material library.
- Rebar material library.
- Thai bar size table.
- RC rectangular beam design:
  - flexure
  - shear
  - minimum/maximum steel
  - stirrup spacing
- RC column design:
  - axial plus bending interaction
  - slenderness effects
  - tie spacing
  - preliminary P-M curve support
- Design grouping and schedule output.
- Beam/column design reports.

Implementation:

- Do not keep RC design inside `StructuralSolver`. Started by moving the RC flexure equation out of the solver.
- Add `ConcreteDesignService`. Implemented for preliminary rectangular flexure checks.
- Separate code equations from UI and report formatting. Flexure demand and capacity helpers are in the service.
- Preserve demand source: member, station, load combination. Current slice consumes `ElementForceResult` demand.

Deliverables:

- Preliminary RC frame design workflow.
- Beam schedule and column schedule outputs.
- Clear limitations for final professional review.

Current implemented subset:

- `ConcreteDesignService` checks RC flexure for rectangular concrete members with `RebarArea` and `EffectiveDepth`.
- The solver still returns `DesignCheckResult` entries for RC axial, flexure, and shear; flexure is routed through the service.

Current limitations:

- RC flexure is preliminary and uses a simplified `phi * As * fy * d` capacity expression.
- Minimum/maximum reinforcement, compression block limits, doubly reinforced behavior, bar layout, stirrup design, torsion, column P-M interaction, slenderness, and Thai code calibration are not implemented.

## Phase 7 - Sparse Solver And Performance

Goal: support larger building models without dense matrix limits.

Status: started. A sparse matrix storage prototype and sparse solver adapter boundary exist, but numerical solving still falls back to the validated dense solver.

Scope:

- Sparse matrix assembly.
- Sparse Cholesky/LU integration.
- DOF numbering optimization.
- Batch solving for many load cases.
- Result envelope calculation.
- Performance benchmarks.
- Memory usage diagnostics.

Deliverables:

- Practical building-size linear analysis.
- Performance baseline documented in CI or benchmark docs.

Current implemented subset:

- `SparseMatrix` stores nonzero matrix entries and can round-trip to dense format.
- `SparsePrototypeLinearSystemSolver` exposes an explicit sparse solver adapter path while using dense fallback solving for validated numerical behavior.
- Solver diagnostics report the selected solver name, including dense default versus sparse prototype fallback.

Current limitations:

- No sparse factorization, sparse Cholesky/LU, ordering optimization, or production performance improvement is implemented yet.
- Dense Gaussian elimination remains the default solver path.

## Phase 8 - Slab, Wall, Shell, And Diaphragm Modeling

Goal: support more complete building models after the frame workflow is reliable.

Status: started. The current slice adds `AreaObject` model data for slab, wall, shell, and diaphragm placeholders plus JSON round-trip and validation diagnostics. No shell element formulation, meshing, diaphragm constraint, area loading, contour display, or solver integration is implemented.

Scope:

- Area object model.
- Plate/shell element.
- Mesh generation.
- Slab and wall assignments.
- Rigid and semi-rigid diaphragm behavior.
- Wall pier/spandrel result extraction.
- Contour result viewer.
- Mesh quality diagnostics.

Risk:

- This phase significantly increases solver, UI, and reporting complexity.
- It should not start before frame analysis and building workflow are stable.

Deliverables:

- Basic slab/wall analysis.
- Result contours.
- Mesh and diaphragm documentation.

Current implemented subset:

- `AreaObject` represents triangular or quadrilateral slab, wall, shell, and diaphragm placeholders separately from line `StructuralElement` members.
- `StructuralModel.AreaObjects` is exported/imported through schema v2 JSON.
- `ModelValidator` checks area object node references, duplicate boundary nodes, material reference, and thickness.
- Validation emits a warning that area objects are not included in frame analysis.

Current limitations and next steps:

- Area objects do not add mass, stiffness, loads, constraints, reactions, or design checks.
- No automatic conversion to finite shell elements or mesh quality diagnostics exists yet.
- No rigid/semi-rigid diaphragm behavior exists yet; `DiaphragmId` is metadata only.
- Next safe steps are area load representation, explicit meshing service interfaces, and external benchmark models before adding solver behavior.

## Phase 9 - OpenSees Adapter

Goal: use OpenSees for advanced validation and nonlinear analysis without making it the product core.

Scope:

- Export selected `StructuralModel` cases to OpenSees Tcl or OpenSeesPy input.
- Run OpenSees as an external process.
- Import displacement, reaction, and element-force output.
- Compare native solver and OpenSees elastic results.
- Add nonlinear analysis modes later:
  - P-Delta
  - pushover
  - nonlinear material
  - time history

Deliverables:

- `OpenSeesSolverAdapter`.
- Elastic comparison tests.
- Advanced analysis marked clearly as experimental until validated.

## Phase 10 - Productization

Goal: prepare the application for real users.

Scope:

- Installer.
- User settings.
- Project file versioning and migration.
- Crash/error reporting.
- Licensing strategy if needed.
- Example projects.
- User manual.
- Engineering validation manual.
- Release notes.
- Backward-compatible project file policy.

Deliverables:

- Installable desktop application.
- Versioned documentation.
- Repeatable release process.
