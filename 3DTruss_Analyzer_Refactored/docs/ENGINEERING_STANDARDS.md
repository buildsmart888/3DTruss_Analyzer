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
- Shell/slab/wall elements.

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

## Engineering Output Policy

Until validated design modules exist, reports must state:

- analysis assumptions
- current limitations
- whether design checks are preliminary
- whether output is suitable for final professional design
