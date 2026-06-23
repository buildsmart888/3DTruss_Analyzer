using Xunit;
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

namespace TrussAnalyzer.Tests.Integration;

/// <summary>
/// การทดสอบระบบ Load Cases และ Load Combinations
/// </summary>
public class LoadCaseTests
{
    [Fact]
    public void TestMultipleLoadCases_AnalysisCorrect()
    {
        // Arrange: สร้างโครงสร้างง่ายๆ
        var solver = new TrussSolver();
        
        // เพิ่มโหนด (โครงสร้าง 2D Truss แบบง่าย)
        solver.AddNode(new Node(1, new Point3D(0, 0, 0)));
        solver.AddNode(new Node(2, new Point3D(3, 0, 0)));
        solver.AddNode(new Node(3, new Point3D(1.5, 2, 0)));
        
        // กำหนดเงื่อนไขขอบเขต (Supports)
        solver.GetNodes()[0].ConstraintX = true;
        solver.GetNodes()[0].ConstraintY = true;
        solver.GetNodes()[1].ConstraintY = true;
        
        // เพิ่มชิ้นส่วน
        var material = new Material("Steel", 200e9, 7850); // E=200 GPa, ρ=7850 kg/m³
        solver.AddElement(new Element(1, 1, 2, 0.005, material)); // A=5000 mm²
        solver.AddElement(new Element(2, 2, 3, 0.005, material));
        solver.AddElement(new Element(3, 3, 1, 0.005, material));
        
        // สร้าง Load Cases หลายกรณี
        var loadCases = new List<LoadCase>
        {
            new LoadCase
            {
                CaseId = "DL",
                Name = "Dead Load",
                Type = LoadCaseType.Static,
                IncludeSelfWeight = true,
                NodeForces = new Dictionary<int, ForceVector>()
            },
            new LoadCase
            {
                CaseId = "LL1",
                Name = "Live Load - Case 1",
                Type = LoadCaseType.Static,
                IncludeSelfWeight = false,
                NodeForces = new Dictionary<int, ForceVector>
                {
                    { 3, new ForceVector(0, -10000, 0) } // 10 kN ที่โหนด 3
                }
            },
            new LoadCase
            {
                CaseId = "LL2",
                Name = "Live Load - Case 2",
                Type = LoadCaseType.Static,
                IncludeSelfWeight = false,
                NodeForces = new Dictionary<int, ForceVector>
                {
                    { 2, new ForceVector(5000, -5000, 0) } // 5 kN X, 5 kN Y ที่โหนด 2
                }
            }
        };
        
        // Act: วิเคราะห์ทุก Load Case
        var results = solver.AnalyzeMultipleLoadCases(loadCases);
        
        // Assert: ตรวจสอบว่ามีผลลัพธ์ครบทุกกรณี
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.LoadCaseName == "Dead Load");
        Assert.Contains(results, r => r.LoadCaseName == "Live Load - Case 1");
        Assert.Contains(results, r => r.LoadCaseName == "Live Load - Case 2");
        
        // ตรวจสอบว่าสมดุลแรงในทุกกรณี
        foreach (var result in results)
        {
            Assert.True(result.EquilibriumSatisfied, 
                $"Equilibrium not satisfied for {result.LoadCaseName}");
        }
    }
    
    [Fact]
    public void TestLoadCombinations_CorrectCombination()
    {
        // Arrange
        var solver = new TrussSolver();
        
        solver.AddNode(new Node(1, new Point3D(0, 0, 0)));
        solver.AddNode(new Node(2, new Point3D(3, 0, 0)));
        solver.AddNode(new Node(3, new Point3D(1.5, 2, 0)));
        
        solver.GetNodes()[0].ConstraintX = true;
        solver.GetNodes()[0].ConstraintY = true;
        solver.GetNodes()[1].ConstraintY = true;
        
        var material = new Material("Steel", 200e9, 7850);
        solver.AddElement(new Element(1, 1, 2, 0.005, material));
        solver.AddElement(new Element(2, 2, 3, 0.005, material));
        solver.AddElement(new Element(3, 3, 1, 0.005, material));
        
        // สร้าง Load Cases
        var allLoadCases = new Dictionary<string, LoadCase>
        {
            ["DL"] = new LoadCase
            {
                CaseId = "DL",
                Name = "Dead Load",
                Type = LoadCaseType.Static,
                IncludeSelfWeight = true,
                NodeForces = new Dictionary<int, ForceVector>()
            },
            ["LL"] = new LoadCase
            {
                CaseId = "LL",
                Name = "Live Load",
                Type = LoadCaseType.Static,
                IncludeSelfWeight = false,
                NodeForces = new Dictionary<int, ForceVector>
                {
                    { 3, new ForceVector(0, -10000, 0) }
                }
            }
        };
        
        // สร้าง Load Combination: 1.4D + 1.6L
        var combinations = new List<LoadCombination>
        {
            new LoadCombination
            {
                CombinationId = "LC1",
                Name = "1.4D + 1.6L",
                LoadCases = new Dictionary<string, double>
                {
                    ["DL"] = 1.4,
                    ["LL"] = 1.6
                },
                Description = "Ultimate strength combination per ASCE 7"
            }
        };
        
        // Act
        var comboResults = solver.AnalyzeLoadCombinations(combinations, allLoadCases);
        
        // Assert
        Assert.Single(comboResults);
        var comboResult = comboResults[0];
        
        Assert.Equal("1.4D + 1.6L", comboResult.LoadCaseName);
        Assert.True(comboResult.EquilibriumSatisfied);
        
        // แรงรวมควรมากกว่าแต่ละกรณีแยก
        var dlResult = solver.Analyze(allLoadCases["DL"]);
        var llResult = solver.Analyze(allLoadCases["LL"]);
        
        // การเคลื่อนตัวใน Load Combination ควรมากกว่า Dead Load เพียงอย่างเดียว
        Assert.True(comboResult.MaxDisplacement > dlResult.MaxDisplacement,
            "Combined displacement should be greater than dead load only");
    }
    
    [Fact]
    public void TestForceVector_Operations()
    {
        // Arrange
        var f1 = new ForceVector(100, 200, 300);
        var f2 = new ForceVector(50, -50, 100);
        
        // Act: ทดสอบการบวก
        var sum = f1 + f2;
        
        // Assert
        Assert.Equal(150, sum.Fx);
        Assert.Equal(150, sum.Fy);
        Assert.Equal(400, sum.Fz);
        
        // Act: ทดสอบการคูณด้วยสเกลาร์
        var scaled = f1.Multiply(2.0);
        
        // Assert
        Assert.Equal(200, scaled.Fx);
        Assert.Equal(400, scaled.Fy);
        Assert.Equal(600, scaled.Fz);
        
        // Act: ทดสอบขนาดแรง
        double expectedMagnitude = Math.Sqrt(100*100 + 200*200 + 300*300);
        Assert.Equal(expectedMagnitude, f1.Magnitude, 5);
    }
    
    [Fact]
    public void TestLoadCaseType_EnumValues()
    {
        // Assert: ตรวจสอบว่ามีประเภทโหลดครบถ้วน
        Assert.Equal(LoadCaseType.Static, LoadCaseType.Static);
        Assert.Equal(LoadCaseType.Wind, LoadCaseType.Wind);
        Assert.Equal(LoadCaseType.Seismic, LoadCaseType.Seismic);
        Assert.Equal(LoadCaseType.Temperature, LoadCaseType.Temperature);
        Assert.Equal(LoadCaseType.Moving, LoadCaseType.Moving);
    }
}
