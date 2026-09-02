# Quality Plan

This document defines the quality process for engineering software development.

## Test Categories

### Unit Tests

Use for:

- matrix operations
- vector operations
- section property calculations
- load combination math
- unit conversions
- design equation helpers

### Integration Tests

Use for:

- complete truss analysis
- complete frame analysis
- JSON import/export round trip
- load case and load combination behavior
- report generation smoke tests

### Benchmark Tests

Use for:

- textbook examples
- closed-form beam and truss solutions
- trusted external software comparisons
- future OpenSees comparison models

### Regression Tests

Use for:

- previously fixed bugs
- project file compatibility
- sign convention issues
- self-weight and load application behavior
- prescribed support displacement/rotation, including closed-form reactions and unconstrained-DOF validation
- rigid-end/insertion-offset transformations, release condensation, Timoshenko option, and uniform temperature restraint
- member force-diagram shape, including UDL curvature and left/right values across point-load jumps
- line-element analysis behavior when non-solver model objects such as area placeholders are present

### Model Representation Tests

Use for:

- creation of core model DTOs that are not analyzed yet
- JSON import/export round trips for schema compatibility
- validation diagnostics for unsupported or incomplete model data
- stable GUID identity under rename, copy, delete/undo restoration, and import
- strict Model3D JSON round-trip, malformed/unknown input, and schema-version rejection
- local axes, releases, offsets, springs, master-slave cycles, and unsupported area preflight
- `.gosa` package/manifest round-trip, atomic-save backup, interrupted temporary save, autosave recovery, and corrupt-snapshot fallback
- deterministic legacy/schema-v2 migration against golden fixtures, with every lossy conversion visible in the report
- `ProjectDocument` analysis preflight, GUID-keyed immutable snapshot provenance, and unsupported/lossy execution blocking
- document dirty/save/autosave/recovery state and toolkit-independent undo/redo command history

## Required Checks Before Merge

Run:

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

For solver or design changes, also add or update:

- benchmark tests
- engineering notes
- example model if user-facing
- report expectations if output changes

For Milestone D shell changes, compile the WPF shell and manually smoke-test both the default WinForms launch
and `--wpf-shell` at 100/125/150% DPI before release. Automated tests cover the application services without
opening a desktop window.

For alternative linear solver paths:

- Keep dense solver regression tests passing as the default path.
- Add equivalence tests against the dense solver before enabling any new solver path for production use.
- Ensure solver diagnostics report the active solver path clearly.
- Document whether an alternative solver is a prototype, dense fallback, or validated production path.

## Numerical Tolerances

Use tolerances that match the scale of the problem.

Recommended patterns:

- exact IDs and counts should use exact assertions
- normalized engineering comparisons should use relative tolerance
- equilibrium residual should scale with total applied load
- displacement checks should use closed-form expected values where possible

## Documentation Quality

When a feature is added, update:

- README if user-facing
- `docs/ROADMAP.md` if roadmap status changes
- `docs/DEVELOPMENT_GUIDE.md` if workflow changes
- `docs/ENGINEERING_STANDARDS.md` if assumptions or conventions change

## Engineering Review

For analysis/design changes, review should check:

- sign convention
- coordinate convention
- unit consistency
- stiffness formulation
- load transformation
- boundary-condition handling
- reaction recovery
- result recovery
- member force-diagram continuity and intentional point-load discontinuities
- test coverage
- report wording
- benchmark comparisons using `docs/FRAME_BENCHMARKS.md` before accepting external-reference equivalence
