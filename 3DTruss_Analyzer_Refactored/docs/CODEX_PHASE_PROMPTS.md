# Codex Phase Prompts

This file stores reusable prompts for developing this project with the `roadmap-driven-development` skill.

Use these prompts when starting a new Codex session or when asking Codex to implement a specific roadmap phase. Prefer one focused prompt at a time.

## General Instruction

Use this when you want Codex to continue from the project documentation but you have not chosen the exact phase yet.

```text
Use $roadmap-driven-development.

Project:
3D Structural Analyzer, evolved from 3DTruss Analyzer, for Thai structural engineering workflows.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/DEVELOPMENT_GUIDE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md

Task:
Review the roadmap and recommend the next small, high-value development task. Do not implement yet unless I explicitly approve.

Scope:
- Prefer the smallest useful deliverable.
- Preserve existing behavior.
- Keep compatibility with existing tests and JSON examples.
- Explain why the proposed task should come next.
```

## Phase 0 - Foundation Cleanup

Use this to make the existing codebase easier to maintain before adding large features.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/DEVELOPMENT_GUIDE.md
- docs/QUALITY_PLAN.md

Task:
Start Phase 0 Foundation Cleanup. Refactor one responsibility out of StructuralSolver into a focused service, starting with model validation if it has not already been extracted.

Scope:
- Preserve existing analysis behavior.
- Keep the public StructuralSolver API stable where practical.
- Do not change UI behavior unless required.
- Do not rewrite legacy TrussSolver except for compatibility needs.
- Add or preserve regression tests proving behavior is unchanged.
- Update docs/ARCHITECTURE.md or docs/DEVELOPMENT_GUIDE.md if module boundaries change.

Acceptance criteria:
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Existing JSON examples still import.
- Existing validation messages remain equivalent or intentionally improved.
- Summarize changed files and any residual risks.
```

## Phase 1 - Reliable Linear 3D Frame Engine

Use this after the solver responsibilities are reasonably separated.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md
- tests/StructuralSolverMvpTests.cs

Task:
Implement one Phase 1 frame-engine improvement. Prefer the next smallest useful item from the roadmap: improved member releases, rigid end offsets, support settlement, temperature load, denser force stations, or improved frame diagnostics.

Scope:
- Keep internal units in SI.
- Preserve coordinate and sign conventions.
- Add benchmark or closed-form tests for the selected feature.
- Keep design-code logic out of the generic solver.
- Preserve schema compatibility when adding model properties.
- Update documentation if assumptions, limitations, or API behavior change.

Acceptance criteria:
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- At least one focused benchmark/regression test covers the new frame behavior.
- Existing frame and truss tests still pass.
- Summarize validation evidence and limitations.
```

## Phase 2 - Building Modeling Workflow

Use this to begin moving from node/member modeling to building-level workflows.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/DEVELOPMENT_GUIDE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md

Task:
Start Phase 2 Building Modeling Workflow. Implement the smallest useful BuildingModel layer above StructuralModel, such as GridLine, Story, BeamObject, ColumnObject, and conversion to StructuralModel for a simple 1-bay 1-story frame.

Scope:
- BuildingModel must be a layer above StructuralModel, not a replacement for StructuralModel.
- Avoid changing StructuralSolver unless absolutely necessary.
- Generated StructuralModel must remain inspectable, analyzable, and exportable.
- Add tests for BuildingModel to StructuralModel conversion.
- Preserve existing JSON import/export behavior.
- Update docs/ARCHITECTURE.md and docs/ROADMAP.md with the new workflow and limitations.

Acceptance criteria:
- A simple 1-bay 1-story frame can be generated from BuildingModel.
- Generated nodes/elements/material/section references are valid.
- StructuralSolver can analyze the generated model if supports and loads are defined.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
```

## Phase 3 - Thai Load Generation And Load Combinations

Use this for Thai project load templates and combination setup.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md

Task:
Start Phase 3 Thai Load Generation. Implement a Thai load template service that creates standard load cases and preliminary load combination templates such as DL, SDL, LL, RL, WLX/WLY, and EQX/EQY. Do not implement full wind or seismic coefficient calculations yet unless the docs already define the formulas.

Scope:
- Keep generated loads traceable with names and descriptions.
- Do not hardcode load-generation logic inside UI event handlers.
- Keep code factors and assumptions in a dedicated service or data structure.
- Do not change solver behavior.
- Add tests for load case and load combination template generation.
- Update docs/ENGINEERING_STANDARDS.md if naming conventions or assumptions are added.

Acceptance criteria:
- Template service generates predictable load cases and combinations.
- Generated combinations reference existing load cases.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Limitations are documented clearly.
```

## Phase 4 - Steel Design Module

Use this to make steel design maintainable and separate from analysis.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md
- src/Core/StructuralSolver.cs
- src/Core/Models/DesignCheck.cs

Task:
Start Phase 4 Steel Design Module. Move preliminary steel design checks out of StructuralSolver into a SteelDesignService while preserving existing result behavior as much as practical.

Scope:
- StructuralSolver should not own steel design equations directly.
- SteelDesignService should consume model data and analysis results.
- Preserve existing DesignCheckResult output shape unless a better compatible path is needed.
- Add tests proving the moved steel checks produce equivalent results.
- Do not implement a full code-calibrated steel standard in this step unless explicitly requested.
- Update docs/ARCHITECTURE.md and docs/ROADMAP.md.

Acceptance criteria:
- Existing steel design tests pass.
- New service-level tests cover OK and NG steel checks.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- StructuralSolver is smaller and has clearer responsibilities.
```

## Phase 5 - Report Engine And Thai Deliverables

Use this for calculation reports and Thai engineering output.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md
- docs/RELEASE_CHECKLIST.md
- src/Core/Reporting/PdfReportGenerator.cs

Task:
Start Phase 5 Report Engine. Improve reporting by adding one focused report section or template boundary, such as project criteria, load cases/combinations, reaction summary, displacement summary, or member force envelope.

Scope:
- Keep report generation separate from solver internals.
- Use stable result DTOs or view models.
- Include units and limitations clearly.
- Keep Thai report text ready for localization/resource extraction where practical.
- Add a smoke test or content test for the report output if feasible.
- Update docs if report workflow changes.

Acceptance criteria:
- Report generation still works.
- New report section is generated from analysis/design results.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Any manual verification steps are documented.
```

## Phase 6 - Reinforced Concrete Frame Design

Use this after steel design and reporting boundaries are clearer.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md
- src/Core/Models/Material.cs
- src/Core/Models/Section.cs
- src/Core/Models/DesignCheck.cs

Task:
Start Phase 6 Reinforced Concrete Frame Design. Implement the smallest useful ConcreteDesignService slice, such as RC rectangular beam flexure check using existing analysis result demand and existing section/material properties.

Scope:
- Do not place RC design equations in StructuralSolver.
- Keep demand extraction separate from capacity calculation.
- Clearly mark checks as preliminary unless a validated design-code profile is implemented.
- Add tests for missing data, OK, and NG cases.
- Preserve existing simplified RC check behavior if it already exists.
- Update docs/ENGINEERING_STANDARDS.md and docs/ROADMAP.md with assumptions and limitations.

Acceptance criteria:
- ConcreteDesignService has focused tests.
- Existing analysis tests still pass.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Report any unsupported RC cases clearly.
```

## Phase 7 - Sparse Solver And Performance

Use this when model size becomes a practical limitation.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/QUALITY_PLAN.md
- src/Core/Utilities/LinearSolvers.cs
- src/Core/Utilities/Matrix.cs

Task:
Start Phase 7 Sparse Solver And Performance. Implement the smallest safe performance improvement, such as a real sparse solver adapter interface boundary, sparse matrix data structure prototype, or benchmark harness for dense vs future sparse solving.

Scope:
- Preserve existing dense solver behavior as the default unless the new path is fully validated.
- Do not change numerical results without benchmark evidence.
- Add performance or regression tests appropriate for the selected slice.
- Keep solver selection explicit and diagnosable.
- Update docs/ARCHITECTURE.md and docs/QUALITY_PLAN.md if the solver path changes.

Acceptance criteria:
- Existing tests pass with dense solver.
- New sparse/performance path is covered by tests or benchmarks.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Solver diagnostics clearly report which solver path is used.
```

## Phase 8 - Slab, Wall, Shell, And Diaphragm Modeling

Use this only after frame/building workflow is stable.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md

Task:
Start Phase 8 Slab, Wall, Shell, And Diaphragm Modeling. Implement only a planning or smallest prototype slice, such as AreaObject model definitions and conversion placeholders, without adding a full shell solver unless explicitly requested.

Scope:
- Do not destabilize the existing frame solver.
- Keep area/shell objects separate from line elements.
- Document assumptions and unsupported behavior.
- Add tests for model creation, serialization, and validation.
- Avoid UI-heavy work until the core model path is stable.

Acceptance criteria:
- Area/slab/wall model objects can be represented safely.
- Existing line-element analysis behavior is unchanged.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Limitations and next steps are documented.
```

## Phase 9 - OpenSees Adapter

Use this for external advanced solver integration after the native solver is reliable.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/ENGINEERING_STANDARDS.md
- docs/QUALITY_PLAN.md

Task:
Start Phase 9 OpenSees Adapter. Implement the smallest safe adapter slice, such as an OpenSees export model writer for a simple truss/frame model or an interface like IStructuralSolverAdapter with a native adapter and an OpenSees placeholder.

Scope:
- Do not replace the native solver.
- Keep OpenSees integration optional and isolated.
- Prefer file/process boundary for OpenSees rather than embedding Python unless explicitly requested.
- Add tests for generated OpenSees input text where possible.
- Do not require OpenSees installed for normal test runs unless guarded/skipped.
- Update docs/ARCHITECTURE.md with the adapter boundary.

Acceptance criteria:
- Native solver remains the default.
- Adapter boundary is clear.
- Existing tests pass without OpenSees installed.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Generated/exported OpenSees input is covered by at least one test if implemented.
```

## Phase 10 - Productization

Use this when preparing the application for real users or distribution.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/RELEASE_CHECKLIST.md
- docs/QUALITY_PLAN.md
- docs/DEVELOPMENT_GUIDE.md

Task:
Start Phase 10 Productization. Prepare one release-readiness slice, such as versioning, release checklist automation, example model verification, project file migration notes, or installer/package documentation.

Scope:
- Do not introduce unrelated features.
- Verify build/test state.
- Keep release documentation accurate.
- Preserve project-file compatibility.
- Report unchecked manual items clearly.

Acceptance criteria:
- Release checklist has clear pass/fail/not-checked status for the selected slice.
- dotnet build TrussAnalyzer.sln passes.
- dotnet test TrussAnalyzer.sln passes.
- Any packaging or installer limitations are documented.
```

## Review Existing Work

Use this when you want Codex to inspect what has already been done before choosing the next task.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/DEVELOPMENT_GUIDE.md
- docs/QUALITY_PLAN.md

Task:
Review the current repository against the roadmap. Produce a concise status report: completed, partial, missing, risky, and recommended next task. Do not modify files.

Scope:
- Use local files as the source of truth.
- Mention specific files/classes where relevant.
- Prefer actionable next steps over broad theory.
```

## Continue Previous Phase

Use this when a previous Codex session already started a phase.

```text
Use $roadmap-driven-development.

Read first:
- README.md
- docs/ROADMAP.md
- docs/ARCHITECTURE.md
- docs/DEVELOPMENT_GUIDE.md
- docs/QUALITY_PLAN.md
- git status and recent diff

Task:
Continue the current in-progress phase from the existing working tree. First inspect uncommitted changes, identify what is already done, then complete the next smallest missing piece.

Scope:
- Do not revert user changes.
- Work with existing uncommitted changes.
- Keep the deliverable small.
- Run build/test when done.
- Update docs only if the completed work changes architecture, behavior, or roadmap status.
```
