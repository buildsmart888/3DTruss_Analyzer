# 3D Truss Analyzer

3D Truss Analyzer is a C#/.NET 8 structural analysis project for linear elastic pin-jointed truss models. The solver uses the direct stiffness method with SI units throughout: meters, Newtons, Pascals, and kg/m3.

## Current Status

The refactor baseline is now buildable and tested:

- Core solver, import/export, reporting, WinForms UI, and tests are included in `TrussAnalyzer.sln`
- `dotnet build TrussAnalyzer.sln` succeeds
- `dotnet test TrussAnalyzer.sln` succeeds
- Load cases and load combinations are supported for nodal loads and factored self-weight
- The WinForms UI builds and can load/save JSON models, edit grid-based models, validate input, run analysis, show a basic projected structure view, and export text/CSV/PDF results
- Basic element safety checks report stress utilization against material yield strength

Full AISC/ASCE code checks and interactive 3D graphics are not implemented yet.

## Project Structure

```text
3DTruss_Analyzer_Refactored/
├── src/
│   ├── Core/
│   │   ├── Models/          # Node, Element, Material, LoadCase
│   │   ├── IO/              # JSON/CSV import/export
│   │   ├── Reporting/       # Basic PDF report generation
│   │   ├── Utilities/       # Matrix solver
│   │   └── TrussSolver.cs   # Direct stiffness solver
│   └── UI/WinForms/         # Desktop UI
├── tests/                   # Unit and integration tests
├── docs/                    # Engineering and development notes
├── examples/                # Example JSON models
└── TrussAnalyzer.sln
```

## Build And Test

Prerequisite: .NET 8 SDK or later.

```bash
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
```

Run the WinForms application:

```bash
dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj
```

## Core Solver Behavior

- `Analyze()` uses the nodal forces currently stored on each `Node`.
- `Analyze(loadCase)` uses the supplied `LoadCase.NodeForces`.
- Self-weight is included only when `LoadCase.IncludeSelfWeight` is `true`.
- Load combinations include nodal forces and factored self-weight from referenced load cases.
- Reactions are calculated from the original stiffness matrix and original force vector before boundary conditions are applied.
- Equilibrium residuals are stored in `AnalysisResult.Equilibrium`.
- Basic safety checks are stored in `AnalysisResult.SafetyChecks`.

## Minimal Code Example

```csharp
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

var solver = new TrussSolver();

var node1 = new Node(1, new Point3D(0, 0, 0))
{
    ConstraintX = true,
    ConstraintY = true,
    ConstraintZ = true
};

var node2 = new Node(2, new Point3D(2, 0, 0))
{
    ConstraintY = true,
    ConstraintZ = true
};
node2.ApplyForce(10_000, 0, 0);

solver.AddNode(node1);
solver.AddNode(node2);
solver.AddElement(new Element(1, 1, 2, 0.001, Material.StructuralSteel));

var result = solver.Analyze();

Console.WriteLine(result);
Console.WriteLine($"Node 2 displacement X: {node2.Displacement.X:E4} m");
```

## Roadmap

- Phase 1: build/test baseline and API consistency - complete
- Phase 2: solver correctness for nodal loads, self-weight, reactions, and equilibrium - complete
- Phase 3: IO/report/UI functionality - functional baseline complete
- Phase 4: grid model editing, validation messages, basic projected view, and yield-stress utilization checks - complete
- Phase 5: richer reports, sparse solver, code-based safety checks, and interactive 3D visualization - planned

## Known Limitations

- Linear elastic small-displacement truss analysis only
- Pin-jointed axial members only; no bending, shear, or frame elements
- Basic yield-stress utilization only; no design-code safety checks yet
- 2D models should constrain out-of-plane DOFs explicitly
- The built-in PDF writer is a basic report generator, not a full PDF layout engine
- Large models still use a dense matrix solver path
