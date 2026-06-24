using System;
using System.Collections.Generic;
using System.Linq;

namespace TrussAnalyzer.Core.Models
{
    /// <summary>
    /// แสดงถึงกรณีโหลดหนึ่งกรณี (Load Case)
    /// ใช้สำหรับการวิเคราะห์หลายสภาวะโหลด
    /// </summary>
    public class LoadCase
    {
        /// <summary>
        /// รหัสกรณีโหลด (เช่น "DL", "LL", "WL")
        /// </summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>
        /// ชื่อกรณีโหลด (เช่น "Dead Load", "Live Load", "Wind Load")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// ประเภทของกรณีโหลด
        /// </summary>
        public LoadCaseType Type { get; set; } = LoadCaseType.Static;

        /// <summary>
        /// แรงที่กระทำที่โหนด (Node ID, Force Vector)
        /// หน่วย: นิวตัน (N)
        /// </summary>
        public Dictionary<int, ForceVector> NodeForces { get; set; } = new();

        /// <summary>
        /// น้ำหนักตัวเองรวมหรือไม่
        /// </summary>
        public bool IncludeSelfWeight { get; set; } = false;

        /// <summary>
        /// ปัจจัยคูณโหลด (Load Factor) สำหรับการออกแบบ
        /// </summary>
        public double LoadFactor { get; set; } = 1.0;

        /// <summary>
        /// คำอธิบายเพิ่มเติม
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// วันที่สร้างกรณีโหลด
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// ประเภทของกรณีโหลด
    /// </summary>
    public enum LoadCaseType
    {
        /// <summary>
        /// โหลดสถิต (Static Load)
        /// </summary>
        Static,

        /// <summary>
        /// โหลดลม (Wind Load)
        /// </summary>
        Wind,

        /// <summary>
        /// โหลดแผ่นดินไหว (Seismic Load)
        /// </summary>
        Seismic,

        /// <summary>
        /// โหลดอุณหภูมิ (Temperature Load)
        /// </summary>
        Temperature,

        /// <summary>
        /// โหลดเคลื่อนที่ (Moving Load)
        /// </summary>
        Moving
    }

    /// <summary>
    /// เวกเตอร์แรง 3 มิติ
    /// หน่วย: นิวตัน (N)
    /// </summary>
    public class ForceVector
    {
        /// <summary>
        /// แรงในแนวแกน X
        /// </summary>
        public double Fx { get; set; } = 0.0;

        /// <summary>
        /// แรงในแนวแกน Y
        /// </summary>
        public double Fy { get; set; } = 0.0;

        /// <summary>
        /// แรงในแนวแกน Z
        /// </summary>
        public double Fz { get; set; } = 0.0;

        /// <summary>
        /// ขนาดรวมของแรง
        /// </summary>
        public double Magnitude => Math.Sqrt(Fx * Fx + Fy * Fy + Fz * Fz);

        /// <summary>
        /// คอนสตรัคเตอร์เริ่มต้น
        /// </summary>
        public ForceVector() { }

        /// <summary>
        /// คอนสตรัคเตอร์พร้อมค่าแรง
        /// </summary>
        public ForceVector(double fx, double fy, double fz)
        {
            Fx = fx;
            Fy = fy;
            Fz = fz;
        }

        /// <summary>
        /// คูณเวกเตอร์แรงด้วยสเกลาร์
        /// </summary>
        public ForceVector Multiply(double factor)
        {
            return new ForceVector(Fx * factor, Fy * factor, Fz * factor);
        }

        /// <summary>
        /// บวกเวกเตอร์แรงสองตัว
        /// </summary>
        public static ForceVector operator +(ForceVector a, ForceVector b)
        {
            return new ForceVector(a.Fx + b.Fx, a.Fy + b.Fy, a.Fz + b.Fz);
        }

        /// <summary>
        /// ToString สำหรับแสดงข้อมูล
        /// </summary>
        public override string ToString()
        {
            return $"Fx={Fx:F2} N, Fy={Fy:F2} N, Fz={Fz:F2} N (Mag={Magnitude:F2} N)";
        }
    }

    /// <summary>
    /// การรวมโหลด (Load Combination) ตามมาตรฐานการออกแบบ
    /// </summary>
    public class LoadCombination
    {
        /// <summary>
        /// รหัสการรวมโหลด
        /// </summary>
        public string CombinationId { get; set; } = string.Empty;

        /// <summary>
        /// ชื่อการรวมโหลด (เช่น "1.4D", "1.2D+1.6L")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// รายการกรณีโหลดและปัจจัยคูณ (LoadCaseId, Factor)
        /// </summary>
        public Dictionary<string, double> LoadCases { get; set; } = new();

        /// <summary>
        /// คำอธิบาย
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// คำนวณแรงรวมจากทุกกรณีโหลด
        /// </summary>
        public Dictionary<int, ForceVector> CalculateCombinedForces(
            Dictionary<string, LoadCase> allLoadCases)
        {
            var combinedForces = new Dictionary<int, ForceVector>();

            foreach (var loadCaseEntry in LoadCases)
            {
                string caseId = loadCaseEntry.Key;
                double factor = loadCaseEntry.Value;

                if (!allLoadCases.ContainsKey(caseId))
                    throw new InvalidOperationException($"Load combination '{Name}' references missing load case '{caseId}'.");

                LoadCase loadCase = allLoadCases[caseId];

                foreach (var nodeForceEntry in loadCase.NodeForces)
                {
                    int nodeId = nodeForceEntry.Key;
                    ForceVector force = nodeForceEntry.Value.Multiply(factor * loadCase.LoadFactor);

                    if (!combinedForces.ContainsKey(nodeId))
                    {
                        combinedForces[nodeId] = new ForceVector();
                    }

                    combinedForces[nodeId] = combinedForces[nodeId] + force;
                }
            }

            return combinedForces;
        }
    }
}
