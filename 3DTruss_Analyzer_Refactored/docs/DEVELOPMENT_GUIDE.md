# Development Guide

## Project Structure

```text
3DTruss_Analyzer_Refactored/
├── src/
│   ├── Core/                 # FEM/direct stiffness solver
│   │   ├── Models/           # Node, Element, Material, LoadCase
│   │   ├── IO/               # JSON/CSV import and export
│   │   ├── Reporting/        # Basic report generation
│   │   ├── Utilities/        # Matrix operations
│   │   └── TrussSolver.cs
│   └── UI/WinForms/          # Desktop UI
├── tests/                    # Unit and integration tests
├── docs/                     # Documentation
├── examples/                 # Example models
└── TrussAnalyzer.sln
```

## Getting Started

Prerequisites:

- .NET 8 SDK or later
- Visual Studio 2022, VS Code, or Rider

Build and test:

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

Run the UI:

```bash
dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj
```

## Engineering Assumptions

- Units are SI: m, N, Pa, kg/m3.
- Members are pin-jointed truss elements that carry axial force only.
- Analysis is linear elastic and small displacement.
- A node has three translational DOFs: X, Y, Z.
- 2D models must explicitly constrain out-of-plane Z DOFs.
- Self-weight is included only through a `LoadCase` with `IncludeSelfWeight = true`.

## Known Limitations

- No bending, shear, moment releases, frame elements, or plate/shell elements.
- No nonlinear geometry, buckling, plasticity, or dynamic analysis.
- No AISC/ASCE design-code safety checks yet.
- The WinForms UI is a functional baseline; advanced model editing and real 3D visualization are future work.
- The WinForms UI supports grid editing, validation, and a basic projected structure view.
- The PDF generator is intentionally minimal and should not be treated as a full report-layout engine.
- Safety checks are basic stress/yield utilization checks, not code-compliant design checks.
- The current `Matrix.SolveAuto` path still uses dense Gaussian elimination; sparse solver work is future optimization.

## Coding Standards

- Use PascalCase for public types, methods, and properties.
- Use `_camelCase` for private fields.
- Include units in XML comments for physical quantities.
- Keep Core independent from UI.
- Prefer explicit exceptions over silent fallback for invalid engineering input.

## Testing Expectations

Core changes should include focused tests for:

- Matrix solve behavior
- Model validation
- Nodal load analysis
- Self-weight analysis
- Reactions and equilibrium residuals
- Load cases and load combinations
- JSON import/export round trips
- Safety utilization and validation messages

Before handing off a change, run:

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

## Debugging Tips

Singular matrix errors usually mean:

- Missing supports
- Unconstrained out-of-plane DOFs in a 2D model
- A mechanism caused by insufficient triangulation
- Zero-length or disconnected elements

Unexpected reactions usually mean:

- Loads were applied in a different load case than expected
- Self-weight was included or omitted unintentionally
- Boundary conditions do not match the intended support model

Large-model warnings mean:

- The model can still run, but dense matrix memory/time may grow quickly.
- Consider reducing the model size or implementing a sparse solver before production use on large structures.
