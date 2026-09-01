# ADR-0003: Stable Identity And Persistence Boundary

- Status: Accepted for Model3D V1; persistence implementation deferred to Milestone C
- Date: 2026-09-01

## Decision

Every persistent Model3D object has a non-empty immutable GUID and an editable label. References use
GUIDs only. Editing, rename, delete/undo restoration, and serialization preserve identity; explicit copy
creates new identity. Unknown V1 JSON properties and unsupported schema versions are rejected.

Model3D V1 defines canonical JSON semantics and a review schema, but it does not replace current project
persistence. Atomic save, backups, `.gosa`, adapters, and migration reports belong to Milestone C.

## Consequences

Labels can be changed safely and duplicate labels are warnings rather than broken references. Lossy or
unknown input cannot be silently discarded. UI presentation settings remain outside engineering model state.
