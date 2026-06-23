using System;

namespace _3DTrussAnalyzer.Models
{
    /// <summary>
    /// Represents boundary constraints at a node
    /// Indicates which degrees of freedom are fixed (restrained)
    /// </summary>
    public class Constraint
    {
        /// <summary>
        /// Is translation in X direction fixed?
        /// </summary>
        public bool IsXFixed { get; set; } = false;

        /// <summary>
        /// Is translation in Y direction fixed?
        /// </summary>
        public bool IsYFixed { get; set; } = false;

        /// <summary>
        /// Is translation in Z direction fixed?
        /// </summary>
        public bool IsZFixed { get; set; } = false;

        /// <summary>
        /// Creates a free constraint (no restraints)
        /// </summary>
        public Constraint() { }

        /// <summary>
        /// Creates a constraint with specified fixities
        /// </summary>
        public Constraint(bool xFixed, bool yFixed, bool zFixed)
        {
            IsXFixed = xFixed;
            IsYFixed = yFixed;
            IsZFixed = zFixed;
        }

        /// <summary>
        /// Creates a pinned support (fixed in all translations)
        /// </summary>
        public static Constraint Pinned()
        {
            return new Constraint(true, true, true);
        }

        /// <summary>
        /// Creates a roller support on XY plane (fixed in Z only)
        /// </summary>
        public static Constraint RollerXY()
        {
            return new Constraint(false, false, true);
        }

        /// <summary>
        /// Creates a roller support on XZ plane (fixed in Y only)
        /// </summary>
        public static Constraint RollerXZ()
        {
            return new Constraint(false, true, false);
        }

        /// <summary>
        /// Creates a roller support on YZ plane (fixed in X only)
        /// </summary>
        public static Constraint RollerYZ()
        {
            return new Constraint(true, false, false);
        }

        /// <summary>
        /// Returns the number of restrained degrees of freedom
        /// </summary>
        public int RestrainedDOFCount
        {
            get
            {
                int count = 0;
                if (IsXFixed) count++;
                if (IsYFixed) count++;
                if (IsZFixed) count++;
                return count;
            }
        }

        /// <summary>
        /// Checks if this constraint has any restraints
        /// </summary>
        public bool HasAnyRestraint => IsXFixed || IsYFixed || IsZFixed;

        public override string ToString()
        {
            string x = IsXFixed ? "X" : "-";
            string y = IsYFixed ? "Y" : "-";
            string z = IsZFixed ? "Z" : "-";
            return $"Constraint({x}, {y}, {z})";
        }
    }
}
