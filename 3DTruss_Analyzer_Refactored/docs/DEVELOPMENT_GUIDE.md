# Development Guide

## Project Structure

```
3DTruss_Analyzer_Refactored/
├── src/
│   ├── Core/           # FEM solver engine
│   │   └── TrussSolver.cs
│   ├── Models/         # Data structures
│   │   ├── Geometry.cs      (Point3D, Vector3D)
│   │   ├── Node.cs
│   │   ├── Element.cs
│   │   ├── Material.cs
│   │   └── Constraint.cs
│   ├── UI/             # User interface (to be implemented)
│   └── Utilities/      # Helper classes
│       └── Matrix.cs        (Matrix operations, solver)
├── tests/
│   ├── Unit/           # Unit tests
│   └── Integration/    # Integration tests
├── docs/               # Documentation
├── examples/           # Example models
└── 3DTrussAnalyzer.sln # Solution file
```

## Getting Started

### Prerequisites
- .NET 6.0 SDK or later
- Visual Studio 2022 or VS Code
- Git

### Build Instructions

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run
```

## Coding Standards

### 1. Naming Conventions
- **Classes**: PascalCase (e.g., `TrussSolver`, `AnalysisResult`)
- **Methods**: PascalCase (e.g., `Analyze`, `CalculateReactions`)
- **Properties**: PascalCase (e.g., `YoungsModulus`, `AxialForce`)
- **Private fields**: _camelCase (e.g., `_nodes`, `_elements`)
- **Parameters**: camelCase (e.g., `startNodeId`, `includeSelfWeight`)

### 2. Documentation
- All public APIs must have XML documentation comments
- Include units for all physical quantities
- Document assumptions and limitations

### 3. Error Handling
- Validate inputs at method boundaries
- Throw specific exceptions with meaningful messages
- Never silently swallow exceptions

### 4. Testing Requirements
- All core algorithms must have unit tests
- Integration tests for complete workflows
- Minimum 80% code coverage

## Adding New Features

### 1. New Element Type
1. Create class in `src/Models/` inheriting from base element
2. Implement stiffness matrix calculation
3. Add unit tests
4. Update documentation

### 2. New Solver Algorithm
1. Create class in `src/Core/`
2. Implement `IAnalysisStrategy` interface
3. Add validation tests
4. Compare results with existing solver

### 3. New UI Component
1. Create in `src/UI/`
2. Follow MVVM pattern
3. Add UI tests
4. Ensure accessibility

## Git Workflow

### Branch Naming
- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation updates
- `test/description` - Test additions

### Commit Messages
```
type(scope): brief description

[Optional detailed explanation]

Fixes: #issue_number
```

Types: feat, fix, docs, style, refactor, test, chore

Example:
```
feat(core): add self-weight calculation

Implemented correct self-weight formula: W = ρALg
Distributed equally to both end nodes

Fixes: #42
```

## Quality Assurance Checklist

Before submitting PR:
- [ ] Code follows naming conventions
- [ ] All public methods documented
- [ ] Units specified for physical quantities
- [ ] Unit tests written and passing
- [ ] Integration tests updated
- [ ] No compiler warnings
- [ ] Equilibrium check passes
- [ ] Code reviewed by team member

## Debugging Tips

### Common Issues

1. **Singular Matrix Error**
   - Check for insufficient constraints
   - Verify node connectivity
   - Look for zero-length elements

2. **Unexpected Displacements**
   - Verify load directions
   - Check material properties (units!)
   - Confirm boundary conditions

3. **Equilibrium Not Satisfied**
   - Review load application
   - Check reaction calculations
   - Verify numerical precision

### Logging
Enable debug logging in appsettings:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "TrussSolver": "Trace"
    }
  }
}
```

## Performance Considerations

- For large models (>1000 DOF), consider sparse matrix storage
- Profile before optimizing
- Cache repeated calculations
- Use parallel processing for independent operations

## References

- [Engineering Principles](ENGINEERING_PRINCIPLES.md)
- [API Documentation](API_REFERENCE.md)
- [Testing Guide](TESTING_GUIDE.md)
