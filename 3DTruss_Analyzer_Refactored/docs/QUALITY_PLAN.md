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
- test coverage
- report wording

