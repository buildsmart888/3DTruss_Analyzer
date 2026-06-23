using System;

namespace _3DTrussAnalyzer.Models
{
    /// <summary>
    /// Represents material properties for truss elements
    /// </summary>
    public class Material
    {
        /// <summary>
        /// Material name (e.g., "Steel A36", "Aluminum 6061")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Young's Modulus (Modulus of Elasticity)
        /// Units: Pascals (Pa) or N/m²
        /// Typical values:
        /// - Steel: 200 GPa = 200e9 Pa
        /// - Aluminum: 69 GPa = 69e9 Pa
        /// - Timber: 10-13 GPa
        /// </summary>
        public double YoungsModulus { get; set; }

        /// <summary>
        /// Density of the material
        /// Units: kg/m³
        /// Typical values:
        /// - Steel: 7850 kg/m³
        /// - Aluminum: 2700 kg/m³
        /// - Timber: 500-800 kg/m³
        /// </summary>
        public double Density { get; set; }

        /// <summary>
        /// Poisson's ratio (optional, for advanced analysis)
        /// Typical values:
        /// - Steel: 0.3
        /// - Aluminum: 0.33
        /// </summary>
        public double PoissonsRatio { get; set; } = 0.3;

        /// <summary>
        /// Creates a new material with specified properties
        /// </summary>
        public Material(string name, double youngsModulus, double density, double poissonsRatio = 0.3)
        {
            Name = name;
            YoungsModulus = youngsModulus;
            Density = density;
            PoissonsRatio = poissonsRatio;
        }

        /// <summary>
        /// Creates a standard structural steel material (A36)
        /// E = 200 GPa, ρ = 7850 kg/m³
        /// </summary>
        public static Material StructuralSteel()
        {
            return new Material("Structural Steel A36", 200e9, 7850);
        }

        /// <summary>
        /// Creates a standard aluminum material (6061-T6)
        /// E = 69 GPa, ρ = 2700 kg/m³
        /// </summary>
        public static Material Aluminum6061()
        {
            return new Material("Aluminum 6061-T6", 69e9, 2700);
        }

        /// <summary>
        /// Calculates the weight per unit volume (specific weight)
        /// Units: N/m³
        /// </summary>
        public double SpecificWeight
        {
            get { return Density * 9.81; } // g = 9.81 m/s²
        }

        public override string ToString()
        {
            return $"{Name} (E = {YoungsModulus / 1e9:F1} GPa, ρ = {Density} kg/m³)";
        }
    }
}
