# ADR-0004: Solver Ownership And Python Boundary

- Status: Accepted for implementation; qualification-owner approval pending
- Date: 2026-09-01

## Decision

C#/.NET owns the production Windows product, Model3D contract, native linear Frame-Truss path,
persistence, results, design orchestration, and reporting. Python owns independent comparisons,
OpenSeesPy adapters, numerical studies, optimization, and research prototypes.

The standard project solve must not require Python. Cross-runtime exchange uses immutable versioned JSON.
A Python prototype becomes advertised production behavior only after requirements, migration, tests,
qualification evidence, UI semantics, and reporting semantics are approved.

## Consequences

The repositories need not merge. Duplicate production solvers for the same advertised capability are
not permitted, while independent Python calculation remains valuable qualification evidence.
