# GOStructAnalysis

> C#/.NET structural analysis and design platform evolving from the original 3D Truss Analyzer into a practical 3D building analysis tool for Thai structural engineers.

This repository currently contains a working MVP for linear elastic 3D truss and 3D frame analysis. The long-term goal is to grow it into a maintainable desktop engineering application for steel buildings, reinforced-concrete buildings, warehouses, factory roofs, and other common building structures used in Thailand.

Core calculations use SI base units: meters, Newtons, Pascals, kilograms, and kg/m3. User-facing input, output, and reports may later expose Thai engineering units such as kN, tonf, kgf, m, cm, and mm through a controlled unit-conversion layer.

## Current Status

- Main solution: `TrussAnalyzer.sln`.
- Core runtime: `.NET 8`.
- UI runtime: Windows desktop with WinForms/WPF.
- `dotnet test TrussAnalyzer.sln` currently passes.
- The original `TrussSolver` API is kept as a compatibility facade.
- The newer `StructuralModel` + `StructuralSolver` pipeline supports 6-DOF frame analysis.
- The solver-independent `ProjectDocument` and `Model3D` V1 contract is implemented for cross-discipline review; it does not replace current persistence or solve models yet.
- Public product identity is `GOStructAnalysis`; legacy solution, assembly, namespace, API, and JSON names remain compatible until a tested migration milestone.

Current MVP capabilities:

- `TrussElement`: axial-only 3D truss behavior.
- `FrameElement3D`: 6 DOF per node with axial, torsion, bending, shear, and end-force recovery.
- `StructuralModel` container for nodes, elements, materials, sections, load cases, load combinations, and load items.
- Nodal force/moment loads, member point loads, member distributed loads, and self-weight.
- Preliminary Thai load case and load combination templates for DL, SDL, LL, RL, directional wind, and directional seismic placeholders.
- Schema v2 JSON import/export while still importing legacy truss JSON.
- Frame member moment releases, local roll angle, local-axis helpers, and member load recovery.
- Prescribed support translation/rotation for settlement and imposed-displacement analysis.
- Rigid-end zones, local insertion points, optional Timoshenko shear deformation, and uniform axial member temperature loads.
- Configurable frame force-result station count for denser axial/shear/torsion/moment diagram DTOs.
- Optional 3D real-section visualization for rectangular/RC, circular, pipe, I/H, C/channel, and box section placeholders.
- Preliminary steel/aluminum/custom stress checks and simplified RC axial/flexure/shear checks.
- Preliminary `ConcreteDesignService` for RC rectangular member flexure checks using analysis result moments and section reinforcement data.
- Basic PDF reporting with project summary, criteria/limitations, displacement/reaction tables, member force envelope, equilibrium, and safety check sections.
- Preliminary GO Pile eccentric pile foundation module for F1-F5 layouts, pile reaction checks, basic footing reinforcement sizing, and HelixToolkit 3D visualization.
- Solver diagnostics and a replaceable linear solver interface with dense Gaussian elimination as the default.
- WinForms engineering desktop shell with model editor panels and a WPF/HelixToolkit 3D viewer.
- Right-handed Z-up viewer convention: X/Y are plan axes, Z is vertical, and gravity acts in global `-Z`.

Design checks are preliminary MVP checks only. They are not final code-compliant engineering design.

## Product Direction

The project should no longer be treated as only a truss application. The recommended direction is:

```text
Thai structural desktop application
  -> Building/story/grid modeling workflow
  -> Linear 3D frame analysis
  -> Thai load generation and load combinations
  -> Steel and RC design modules
  -> Thai engineering reports
  -> Optional OpenSees adapter for advanced/nonlinear analysis
```

PyNite is not recommended as the core engine for this C# product. OpenSees may become useful later as an external advanced solver, but the maintainable product core should remain native C# with clear solver, model, design, reporting, and UI boundaries.

## Repository Structure

```text
3DTruss_Analyzer_Refactored/
  src/
    Core/
      Models/            # Node, element, material, section, load, result models
      IO/                # JSON/CSV import and export
      Reporting/         # Basic PDF report generation
      Utilities/         # Matrix and linear solver utilities
      TrussSolver.cs     # Legacy truss compatibility facade
      StructuralSolver.cs
    UI/WinForms/         # Desktop UI and 3D viewer shell
  tests/                 # Unit, integration, benchmark, and regression tests
  docs/                  # Architecture, roadmap, engineering, and process docs
  examples/              # Example structural JSON models
  image/README/          # README screenshots
  TrussAnalyzer.sln
```

## Documentation

- [Development Guide](docs/DEVELOPMENT_GUIDE.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Detailed Roadmap](docs/ROADMAP.md)
- [GOStructAnalysis Product And Model3D Roadmap](docs/GOSTRUCTANALYSIS_ROADMAP.md)
- [Product Governance, Glossary, And Ownership](docs/PRODUCT_GOVERNANCE.md)
- [Milestone Approval Record](docs/MILESTONE_APPROVAL_RECORD.md)
- [Milestone C Persistence And Compatibility](docs/MILESTONE_C_COMPATIBILITY.md)
- [Model3D V1 Specification](docs/MODEL3D_V1_SPEC.md)
- [Model3D V1 JSON Schema](docs/schema/model3d-v1.schema.json)
- [Engineering Standards](docs/ENGINEERING_STANDARDS.md)
- [Quality Plan](docs/QUALITY_PLAN.md)
- [Release Checklist](docs/RELEASE_CHECKLIST.md)
- [Engineering Principles](docs/ENGINEERING_PRINCIPLES.md)
- [Codex Phase Prompts](docs/CODEX_PHASE_PROMPTS.md)

## Build And Test

Prerequisites:

- .NET 8 SDK or later
- Windows desktop runtime support for WinForms/WPF

Commands:

```bash
dotnet restore TrussAnalyzer.sln
dotnet build TrussAnalyzer.sln
dotnet test TrussAnalyzer.sln
dotnet run --project src/UI/WinForms/TrussAnalyzer.UI.csproj
```

## Coordinate And Sign Convention

- Global coordinate system: right-handed, Z-up.
- Global X and Y are plan axes.
- Global Z is vertical.
- Gravity and self-weight act in global `-Z`.
- Forces `FX/FY/FZ` and moments `MX/MY/MZ` are global unless a member load direction is explicitly set to local.
- Member local `x` runs from start node `i` to end node `j`.
- Member local `y/z` are generated as a right-handed basis and can be rotated with roll angle.
- Positive truss axial force is tension.
- Negative truss axial force is compression.
- Truss elements recover axial force only.
- Shear, torsion, and bending diagrams require `FrameElement3D`.

## Minimal Structural Example

```csharp
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

var model = new StructuralModel();
model.Materials.Add(Material.StructuralSteel with { Id = 1 });
model.Sections.Add(Section.Generic(1, "Frame section", 0.003, 6e-6, 8e-6, 2e-6));

model.Nodes.Add(new Node(1, new Point3D(0, 0, 0))
{
    ConstraintX = true,
    ConstraintY = true,
    ConstraintZ = true,
    ConstraintRX = true,
    ConstraintRY = true,
    ConstraintRZ = true
});

var tip = new Node(2, new Point3D(3, 0, 0));
tip.ApplyForce(0, -10_000, 0);
model.Nodes.Add(tip);

model.Elements.Add(new FrameElement3D(1, 1, 2, materialId: 1, sectionId: 1));

var result = new StructuralSolver(model).Analyze();
Console.WriteLine(result.NodeResults.Single(n => n.NodeId == 2).Displacement.Y);
```

## Known Limitations

- Linear elastic, small-displacement analysis only.
- Slab, wall, shell, and diaphragm area objects can be stored as model placeholders, but they are not assembled into analysis stiffness, meshed, loaded, or reported as shell results yet.
- No plate solver, solid, cable, spring, or nonlinear concrete cracking elements.
- No P-Delta, plastic hinge, modal, dynamic, time-history, response-spectrum, or nonlinear analysis.
- No automatic wind/seismic load generator yet.
- Support settlement and imposed displacement are global node properties that apply to every load case and combination in the model; load-case-specific support movement and UI editing are not implemented yet.
- Rigid-end zones/insertion points are linear connection-offset transformations. Temperature loading is uniform axial member temperature only. Panel-zone effects, thermal gradients, and nonlinear joint/soil behavior are not implemented.
- Euler-Bernoulli is the default frame formulation. Timoshenko shear deformation is optional and uses configured shear correction factors with gross section area.
- For member point loads and uniform distributed loads, station recovery follows the tracked local load shape for local axial, shear, and bending diagrams. Point moments produce torsion/bending jumps where applicable. Internal point-load locations expose left and right samples so discontinuities can be drawn explicitly. Self-weight-only diagrams and unsupported load types still use end-force interpolation; this is not yet a complete member-load diagram engine.
- Real-section rendering is visual only; it does not change stiffness, capacity, section property calculation, or solver behavior.
- Dense matrix solving is currently used; true sparse storage and sparse solving are future work.
- Design checks are preliminary and must not be used as final professional design output.
- The PDF writer is a basic report generator, not a production report layout engine.

## Immediate Development Priorities

1. Complete Milestone A product/engineering approval and record named reviewers.
2. Complete Milestone B Model3D V1 domain, analysis, UI, reporting, and Python review.
3. Obtain named reviewer approvals in the Milestone Approval Record before treating A/B as complete.
4. Extend the current-C#-to-Model3D adapter and Milestone C persistence/migration foundation with fixtures, `.gosa`, atomic save, recovery, and compatibility reporting before large UI authoring work.
5. Continue solver qualification without silently changing current signs, units, or project files.

See [Detailed Roadmap](docs/ROADMAP.md) for the full phased plan.

See [Phase 1 Frame Benchmarks](docs/FRAME_BENCHMARKS.md) for the automated and manual external-comparison workflow.
