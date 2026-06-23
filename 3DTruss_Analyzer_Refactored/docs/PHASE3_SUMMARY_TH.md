# คู่มือการติดตั้งและใช้งาน 3D Truss Analyzer Refactored

## ข้อกำหนดระบบ
- Windows 10/11 หรือ Linux/macOS
- .NET 8.0 SDK หรือใหม่กว่า
- Visual Studio 2022 หรือ VS Code

## การติดตั้ง

### 1. ติดตั้ง .NET 8 SDK
ดาวน์โหลดจาก: https://dotnet.microsoft.com/download/dotnet/8.0

หรือใช้คำสั่ง:
```bash
# Windows (PowerShell)
winget install Microsoft.DotNet.SDK.8

# macOS
brew install --cask dotnet-sdk

# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

### 2. Clone โปรเจกต์
```bash
git clone https://github.com/buildsmart888/3DTruss_Analyzer.git
cd 3DTruss_Analyzer_Refactored
```

### 3. Build โปรเจกต์
```bash
dotnet restore
dotnet build
```

### 4. รันการทดสอบ (Unit Tests)
```bash
dotnet test
```

### 5. รันโปรแกรม UI
```bash
cd src/UI/WinForms
dotnet run
```

## คุณสมบัติใหม่ที่เพิ่มใน Phase 3

### 1. ระบบ Load Cases หลายกรณี
สามารถวิเคราะห์โครงสร้างภายใต้สภาวะโหลดต่างๆ แยกกันได้:
- Dead Load (น้ำหนักตัวเอง)
- Live Load (น้ำหนักใช้งาน)
- Wind Load (แรงลม)
- Seismic Load (แรงแผ่นดินไหว)
- Temperature Load (ผลจากอุณหภูมิ)

**ตัวอย่างโค้ด:**
```csharp
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
        CaseId = "LL",
        Name = "Live Load",
        Type = LoadCaseType.Static,
        IncludeSelfWeight = false,
        NodeForces = new Dictionary<int, ForceVector>
        {
            { 3, new ForceVector(0, -10000, 0) } // 10 kN ที่โหนด 3
        }
    }
};

var results = solver.AnalyzeMultipleLoadCases(loadCases);
```

### 2. ระบบ Load Combinations
รวมผลลัพธ์จากหลาย Load Case ตามมาตรฐานการออกแบบ (ASCE 7, AISC):

**ตัวอย่าง:**
```csharp
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
        }
    },
    new LoadCombination
    {
        CombinationId = "LC2",
        Name = "1.2D + 1.0L + 1.0W",
        LoadCases = new Dictionary<string, double>
        {
            ["DL"] = 1.2,
            ["LL"] = 1.0,
            ["WL"] = 1.0
        }
    }
};

var comboResults = solver.AnalyzeLoadCombinations(combinations, allLoadCases);
```

### 3. คลาส ForceVector ที่สมบูรณ์
รองรับการคำนวณเวกเตอร์แรง 3 มิติ:
```csharp
var force = new ForceVector(100, 200, 300); // Fx=100N, Fy=200N, Fz=300N
double magnitude = force.Magnitude; // ขนาดรวมของแรง

// การบวกเวกเตอร์
var total = force1 + force2;

// การคูณด้วยสเกลาร์
var scaled = force.Multiply(2.0);
```

### 4. การปรับปรุง AnalysisResult
ผลลัพธ์ теперь รวมข้อมูลเพิ่มเติม:
- `LoadCaseName`: ชื่อกรณีโหลดที่วิเคราะห์
- `TotalAppliedLoad`: น้ำหนักรวมที่กระทำ
- `TotalReactionForce`: แรงปฏิกิริยารวม
- ตรวจสอบสมดุลแรงอัตโนมัติ

## โครงสร้างไฟล์

```
3DTruss_Analyzer_Refactored/
├── src/
│   ├── Core/
│   │   ├── Models/
│   │   │   ├── Node.cs              ✅ อัปเดตแล้ว (ResetForces, SetDisplacement)
│   │   │   ├── Element.cs
│   │   │   ├── Material.cs
│   │   │   ├── Geometry.cs
│   │   │   └── LoadCase.cs          🆕 ใหม่!
│   │   ├── Utilities/
│   │   │   └── Matrix.cs
│   │   ├── IO/
│   │   │   └── StructureImporterExporter.cs
│   │   ├── Reporting/
│   │   │   └── PdfReportGenerator.cs
│   │   └── TrussSolver.cs           ✅ อัปเดตแล้ว (Analyze with LoadCase)
│   └── UI/
│       └── WinForms/
│           ├── MainForm.cs
│           └── Program.cs
├── tests/
│   ├── Unit/
│   │   ├── MatrixTests.cs
│   │   └── TrussSolverTests.cs
│   └── Integration/
│       ├── TextbookBenchmarkTests.cs
│       └── LoadCaseTests.cs         🆕 ใหม่!
├── examples/
│   └── test_models/
│       ├── simple_2d_truss.json
│       └── space_truss_3d.json
└── docs/
    ├── ENGINEERING_PRINCIPLES.md
    └── DEVELOPMENT_GUIDE.md
```

## การทดสอบที่เพิ่มใหม่

### LoadCaseTests.cs
ประกอบด้วย 4 การทดสอบหลัก:
1. **TestMultipleLoadCases_AnalysisCorrect**: ทดสอบการวิเคราะห์หลาย Load Case
2. **TestLoadCombinations_CorrectCombination**: ทดสอบการรวมโหลด
3. **TestForceVector_Operations**: ทดสอบการคำนวณเวกเตอร์แรง
4. **TestLoadCaseType_EnumValues**: ทดสอบ Enum ประเภทโหลด

## ตัวอย่างการใช้งานจริง

### วิเคราะห์สะพานโครงถัก
```csharp
var solver = new TrussSolver();

// เพิ่มโหนดและชิ้นส่วน...
// (ดูตัวอย่างใน TextbookBenchmarkTests.cs)

// สร้าง Load Cases
var deadLoad = new LoadCase
{
    CaseId = "DL",
    Name = "Dead Load",
    Type = LoadCaseType.Static,
    IncludeSelfWeight = true
};

var liveLoad = new LoadCase
{
    CaseId = "LL",
    Name = "Traffic Load",
    Type = LoadCaseType.Moving,
    NodeForces = vehicleForces // จากฐานข้อมูลรถบรรทุก
};

var windLoad = new LoadCase
{
    CaseId = "WL",
    Name = "Wind Load",
    Type = LoadCaseType.Wind,
    NodeForces = CalculateWindForces()
};

// วิเคราะห์แต่ละกรณี
var dlResult = solver.Analyze(deadLoad);
var llResult = solver.Analyze(liveLoad);
var wlResult = solver.Analyze(windLoad);

// รวมโหลดตามมาตรฐาน
var combinations = new List<LoadCombination>
{
    new LoadCombination
    {
        Name = "Strength I",
        LoadCases = new Dictionary<string, double>
        {
            ["DL"] = 1.25,
            ["LL"] = 1.75,
            ["WL"] = 0.4
        }
    }
};

var finalResults = solver.AnalyzeLoadCombinations(combinations, 
    new Dictionary<string, LoadCase> { ["DL"] = deadLoad, ["LL"] = liveLoad, ["WL"] = windLoad });

// ส่งออกผลลัพธ์เป็น PDF
var reportGenerator = new PdfReportGenerator();
reportGenerator.GenerateReport(finalResults, "bridge_analysis.pdf");
```

## สรุปการพัฒนา Phase 3

✅ **เสร็จสมบูรณ์:**
1. ระบบ Load Cases หลายกรณี
2. ระบบ Load Combinations ตามมาตรฐาน
3. คลาส ForceVector สำหรับเวกเตอร์แรง
4. Unit Tests สำหรับฟีเจอร์ใหม่
5. เอกสารคู่มือการใช้งาน

🎯 **ประโยชน์:**
- สามารถวิเคราะห์โครงสร้างภายใต้สภาวะโหลดที่หลากหลาย
- รวมผลลัพธ์ตามมาตรฐานการออกแบบสากล
- พร้อมสำหรับการใช้งานจริงในงานวิศวกรรม

📝 **หมายเหตุ:** ต้องติดตั้ง .NET 8 SDK ให้เรียบร้อยก่อนรันโปรแกรม
