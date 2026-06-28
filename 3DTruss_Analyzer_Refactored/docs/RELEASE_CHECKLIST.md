# Release Checklist

Use this checklist before tagging or distributing a build.

## Build

- `dotnet restore TrussAnalyzer.sln`
- `dotnet build TrussAnalyzer.sln`
- `dotnet test TrussAnalyzer.sln`
- UI starts successfully.
- Example models open successfully.

## Engineering Validation

- Truss benchmark models pass.
- Frame benchmark models pass.
- Load case and load combination tests pass.
- Self-weight behavior is checked.
- Reaction equilibrium is checked.
- Known limitations are documented.

## File Compatibility

- Current JSON schema exports successfully.
- Current JSON schema imports successfully.
- Legacy truss JSON imports successfully.
- Example files remain valid.

## UI Verification

- Model tree loads.
- Properties panel updates.
- 3D viewer displays nodes, members, supports, and loads.
- Deformed shape displays after analysis.
- Result tables update after analysis.
- Error messages are understandable.

## Reporting

- PDF/report generation smoke test passes.
- Units are shown clearly.
- Design limitations are included.
- Project metadata is included when available.

## Documentation

- README is current.
- Roadmap status is current.
- Development guide is current.
- Release notes are prepared.

## Packaging

- Version number is updated.
- Installer/package is generated if applicable.
- Example models are included.
- Required runtime dependencies are documented.

