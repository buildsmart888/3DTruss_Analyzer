# ADR-0002: Engineering Units, Axes, And Signs

- Status: Accepted for implementation; engineering approval pending
- Date: 2026-09-01

## Decision

Persist and calculate in canonical SI: m, N, N-m, Pa, kg, rad, and temperature differences in K.
Use a right-handed global system with Z up and gravity in -Z. Member local x runs start/I to end/J;
local y/z are a right-handed basis derived from an explicit nonparallel reference vector and roll.
Native solver signs remain authoritative. Display controls may change diagram placement but not values.

## Consequences

Display-unit preferences are metadata and never rescale stored engineering values. Near-parallel local
axis references fail validation instead of allowing numerically unstable orientation.
