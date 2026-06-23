# Engineering Principles for 3D Truss Analysis

## 1. Fundamental Theory

### 1.1 Truss Assumptions
- All members are connected by frictionless pins (hinges)
- Loads are applied only at joints (nodes)
- Members carry only axial forces (tension or compression)
- Self-weight is distributed equally to end nodes

### 1.2 Finite Element Method for Trusses

The governing equation for structural analysis:
```
[K]{U} = {F}
```
Where:
- [K] = Global stiffness matrix
- {U} = Nodal displacement vector
- {F} = Nodal force vector

## 2. Element Formulation

### 2.1 Local Stiffness Matrix
For a 3D truss element with 2 nodes:
```
k_local = (AE/L) × [1 -1; -1 1]
```
Where:
- A = Cross-sectional area (m²)
- E = Young's modulus (Pa)
- L = Element length (m)

### 2.2 Transformation Matrix
Direction cosines for 3D element:
```
l = (x₂-x₁)/L
m = (y₂-y₁)/L
n = (z₂-z₁)/L
```

Transformation from local to global coordinates:
```
{T} = [ l  m  n  0  0  0 ]
      [ 0  0  0  l  m  n ]
```

### 2.3 Global Element Stiffness Matrix
```
[k_global] = [T]^T × [k_local] × [T]
```

This results in a 6×6 matrix (3 DOF per node × 2 nodes).

## 3. Load Calculations

### 3.1 Self-Weight (CORRECT FORMULA)
Total weight of element:
```
W_total = ρ × A × L × g
```
Where:
- ρ = Material density (kg/m³)
- A = Cross-sectional area (m²)
- L = Element length (m)
- g = Gravitational acceleration (9.81 m/s²)

Force at each node (distributed equally):
```
F_node = W_total / 2
```

Applied in the direction opposite to gravity vector.

**CRITICAL**: The original code incorrectly calculated self-weight using mass matrix transformation. The correct approach is direct calculation as shown above.

### 3.2 External Loads
Point loads applied directly at nodes in global coordinate system.

## 4. Boundary Conditions

### 4.1 Support Types
- **Pinned Support**: Fixed in X, Y, Z (3 restraints)
- **Roller Support**: Fixed in one direction only (1 restraint)
- **Fixed Support**: Not applicable for trusses (no moment resistance)

### 4.2 Application Method
Remove rows and columns corresponding to fixed DOFs from the global system, or use penalty method.

## 5. Solution Procedure

1. **Assemble** global stiffness matrix [K]
2. **Apply** loads to form force vector {F}
3. **Apply** boundary conditions
4. **Solve** for displacements: {U} = [K]⁻¹{F}
5. **Calculate** reactions: {R} = [K]{U} - {F}
6. **Calculate** member forces from displacements

## 6. Equilibrium Verification

After solving, verify:
```
ΣFx_applied + ΣFx_reaction = 0
ΣFy_applied + ΣFy_reaction = 0
ΣFz_applied + ΣFz_reaction = 0
```

Acceptable tolerance: < 10⁻⁶ × max(|F_applied|)

## 7. Common Errors to Avoid

### 7.1 Original Code Issues Found:
1. ❌ Self-weight calculation using incorrect mass matrix transformation
2. ❌ Force distribution dividing by connection count (wrong approach)
3. ❌ No equilibrium check after solution
4. ❌ No unit specifications
5. ❌ Option Strict Off allowing type conversions

### 7.2 Corrected Approaches:
1. ✅ Direct self-weight: W = ρALg, distributed w/2 to each node
2. ✅ Full nodal loads applied directly (no division)
3. ✅ Mandatory equilibrium verification
4. ✅ Explicit units in all documentation
5. ✅ Strong typing throughout

## 8. Validation Test Cases

### 8.1 Simple Bar (Analytical Solution Available)
- Length: 2 m
- Area: 0.001 m²
- E: 200 GPa
- Load: 10 kN axial
- Expected displacement: δ = PL/AE = 0.0001 m

### 8.2 Cantilever Truss
Compare with textbook solutions for verification.

## 9. Units Convention

All calculations use SI units:
- Length: meters (m)
- Force: Newtons (N)
- Stress: Pascals (Pa)
- Mass: kilograms (kg)
- Density: kg/m³
- Young's Modulus: Pa (N/m²)

## 10. References

1. Hibbeler, R.C. "Structural Analysis"
2. McGuire, W. et al. "Matrix Structural Analysis"
3. Logan, D.L. "A First Course in the Finite Element Method"
