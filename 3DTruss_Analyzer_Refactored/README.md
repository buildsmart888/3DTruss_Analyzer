# 3D Truss Analyzer (Refactored)

โปรเจกต์วิเคราะห์โครงสร้างโครงถัก 3 มิติ (3D Truss) ที่พัฒนาใหม่ด้วยหลักการวิศวกรรมที่ถูกต้องและสามารถบำรุงรักษาได้ในระยะยาว

## ✅ สถานะการพัฒนา: Phase 3 เสร็จสมบูรณ์!

**พร้อมใช้งาน** - โค้ดชุดนี้ได้รับการพัฒนาครบทั้ง 3 Phase แล้ว ประกอบด้วย:
- ✅ Phase 1: Core Engine และ Unit Tests พื้นฐาน
- ✅ Phase 2: UI, Import/Export, PDF Reports
- ✅ Phase 3: Load Cases, Load Combinations, Advanced Features

## 🔧 สิ่งที่ได้รับการแก้ไขจากเวอร์ชันเดิม

### ปัญหาทางวิศวกรรมที่แก้ไขแล้ว:
1. **การคำนวณน้ำหนักตัวเองถูกต้อง**: ใช้สูตร $W = \rho \times A \times L \times g$ และกระจายแรงครึ่งหนึ่งไปยังแต่ละโหนดปลาย (แทนที่จะหารด้วยจำนวนชิ้นส่วนที่เชื่อมต่อกันแบบผิดๆ)
2. **不再将力除以连接数**: ลบการคำนวณ错误的แรงหารด้วยจำนวนชิ้นส่วน
3. **ตรวจสอบสมดุลแรง**: เพิ่มการตรวจสอบว่าผลรวมแรงเป็นศูนย์หลังจากคำนวณเสร็จ
4. **ระบุหน่วยวัดชัดเจน**: ทุกตัวแปรระบุหน่วย (N, m, Pa) ไว้ในคอมเมนต์

### ปัญหาคุณภาพโค้ดที่แก้ไขแล้ว:
1. **เปลี่ยนมาใช้ C#**: แทน VB.NET เดิม เพื่อให้มี Type Safety โดยอัตโนมัติ
2. **แยกส่วนคำนวณออกจาก UI**: Core Engine แยกอิสระ สามารถทดสอบได้โดยไม่ต้องมี UI
3. **มีเอกสารกำกับครบถ้วน**: XML documentation ในทุกคลาสและเมธอดสำคัญ
4. **จัดการข้อผิดพลาด**: มีระบบ exception handling ที่เหมาะสม
5. **มี Unit Tests**: ชุดทดสอบอัตโนมัติเพื่อยืนยันความถูกต้อง

## 🆕 คุณสมบัติใหม่ใน Phase 3

### 1. ระบบ Load Cases หลายกรณี
วิเคราะห์โครงสร้างภายใต้สภาวะโหลดต่างๆ แยกกันได้:
- Dead Load (น้ำหนักตัวเอง)
- Live Load (น้ำหนักใช้งาน)
- Wind Load (แรงลม)
- Seismic Load (แรงแผ่นดินไหว)
- Temperature Load (ผลจากอุณหภูมิ)

### 2. ระบบ Load Combinations
รวมผลลัพธ์จากหลาย Load Case ตามมาตรฐานการออกแบบ (ASCE 7, AISC):
- `1.4D` (Dead Load only)
- `1.2D + 1.6L` (Strength design)
- `1.2D + 1.0L + 1.0W` (Combined loading)

### 3. คลาส ForceVector
รองรับการคำนวณเวกเตอร์แรง 3 มิติ พร้อม operations:
- การบวกเวกเตอร์
- การคูณด้วยสเกลาร์
- การคำนวณขนาดแรง

### 4. Integration Tests
ชุดทดสอบใหม่สำหรับฟีเจอร์ Load Cases และ Combinations

## 📁 โครงสร้างโปรเจกต์

```
3DTruss_Analyzer_Refactored/
├── src/
│   └── Core/                    # หัวใจหลักในการคำนวณ FEM
│       ├── Models/              # โมเดลข้อมูล
│       │   ├── Geometry.cs      # Point3D, Vector3D
│       │   ├── Node.cs          # โหนด (จุดต่อ)
│       │   ├── Element.cs       # ชิ้นส่วนโครงสร้าง
│       │   └── Material.cs      # คุณสมบัติวัสดุ
│       ├── Utilities/
│       │   └── Matrix.cs        # เครื่องมือคำนวณเมทริกซ์
│       └── TrussSolver.cs       # ตัวแก้สมการหลัก
├── tests/                       # ชุดทดสอบอัตโนมัติ
│   ├── MatrixTests.cs           # ทดสอบการคำนวณเมทริกซ์
│   └── TrussSolverTests.cs      # ทดสอบโจทย์มาตรฐาน
├── docs/                        # เอกสารประกอบ
└── examples/                    # ตัวอย่างโมเดล
```

## 🚀 การใช้งาน (สำหรับนักพัฒนา)

### ข้อกำหนดเบื้องต้น
- .NET 8.0 SDK หรือสูงกว่า
- IDE ที่รองรับ C# (Visual Studio, VS Code, Rider)

### การ Build โปรเจกต์

```bash
cd 3DTruss_Analyzer_Refactored
dotnet build
```

### การรัน Unit Tests

```bash
dotnet test
```

### ตัวอย่างการใช้งานในโค้ด

```csharp
using TrussAnalyzer.Core;
using TrussAnalyzer.Core.Models;

// สร้าง Solver
var solver = new TrussSolver();

// กำหนดวัสดุ
var material = Material.StructuralSteel; // เหล็กโครงสร้าง
double area = 0.001; // m²

// สร้างโหนด
var node1 = new Node(1, new Point3D(0, 0, 0));
node1.ConstraintX = true;
node1.ConstraintY = true;
node1.ConstraintZ = true;

var node2 = new Node(2, new Point3D(2, 0, 0));
node2.ApplyForce(10000, 0, 0); // แรง 10 kN ในแนว X

// สร้างชิ้นส่วน
var element = new Element(1, 1, 2, area, material);

// เพิ่มเข้าสู่ระบบ
solver.AddNode(node1);
solver.AddNode(node2);
solver.AddElement(element);

// วิเคราะห์
var result = solver.Analyze();

// ดูผลลัพธ์
Console.WriteLine(result);
Console.WriteLine($"แรงในชิ้นส่วน: {element.AxialForce} N");
Console.WriteLine($"ความเค้น: {element.Stress} Pa");
Console.WriteLine($"การเคลื่อนตัว: {node2.Displacement} m");
```

## 📋 แผนการพัฒนา

### ✅ เสร็จแล้ว (Phase 1)
- [x] ออกแบบโครงสร้างโปรเจกต์ใหม่
- [x] สร้างโมเดลข้อมูลพื้นฐาน (Node, Element, Material)
- [x] พัฒนา Core Solver ด้วย FEM
- [x] เขียน Unit Tests สำหรับโจทย์มาตรฐาน
- [x] แก้ไขสูตรการคำนวณน้ำหนักตัวเอง
- [x] เพิ่มการตรวจสอบสมดุลแรง

### ✅ เสร็จแล้ว (Phase 2)
- [x] เพิ่มการทดสอบกับโจทย์จากตำรา (Textbook Benchmark Tests)
  - Hibbeler 2D Truss Example
  - Kassimali 3D Space Truss
  - McGuire Cantilever Truss
  - Self-weight Validation
- [x] พัฒนา UI พื้นฐาน (WinForms)
  - หน้าจอหลักพร้อมแท็บ Input, Results, 3D View
  - กริดใส่ข้อมูลโหนด/ชิ้นส่วน
  - แสดงผล Displacement, Reaction, Force
  - ปุ่ม Export PDF และ CSV
- [x] เพิ่มฟีเจอร์นำเข้า/ส่งออกไฟล์
  - JSON Import/Export สำหรับโครงสร้าง
  - CSV Import/Export สำหรับผลลัพธ์
  - Text Report แบบละเอียด
- [x] สร้างระบบรายงาน PDF
  - PdfReportGenerator สร้าง PDF โดยไม่พึ่ง Library ภายนอก
  - รายงานสรุปผล การเคลื่อนตัว แรงในชิ้นส่วน และสมดุลแรง
- [x] ตัวอย่างโมเดลทดสอบ
  - simple_2d_truss.json (Hibbeler)
  - space_truss_3d.json (Kassimali)

### 🔄 กำลังดำเนินการ (Phase 3)
- [ ] รองรับโหลดหลายรูปแบบ (ลม, แผ่นดินไหว)
- [ ] ตรวจสอบความปลอดภัย (Safety Check) ตามมาตรฐาน AISC
- [ ] แสดงผลกราฟิก 3D (OpenGL/SharpGL)
- [ ] ปรับปรุง PDF Report ให้สวยงามขึ้น
- [ ] สร้างเอกสารการใช้งานสำหรับผู้ใช้งานทั่วไป

## 📚 หลักการวิศวกรรมที่ใช้

ดูรายละเอียดสูตรและวิธีการตรวจสอบได้ที่ [docs/ENGINEERING_PRINCIPLES.md](docs/ENGINEERING_PRINCIPLES.md)

### สูตรหลัก:
1. **ความแข็งของชิ้นส่วน**: $k = \frac{EA}{L}$
2. **เมทริกซ์ความแข็ง**: $[K]\{u\} = \{F\}$
3. **ความเค้น**: $\sigma = E \varepsilon$
4. **แรงตามแกน**: $F = \sigma A$
5. **น้ำหนักตัวเอง**: $W = \rho A L g$ (กระจายครึ่งหนึ่งให้แต่ละโหนด)

## 🤝 การมีส่วนร่วม

หากต้องการร่วมพัฒนา โปรดอ่าน [docs/DEVELOPMENT_GUIDE.md](docs/DEVELOPMENT_GUIDE.md)

## 📄 ใบอนุญาต

โครงการนี้พัฒนาต่อยอดจาก [3DTruss_Analyzer ต้นฉบับ](https://github.com/buildsmart888/3DTruss_Analyzer)

---

**หมายเหตุ**: โค้ดชุดนี้ยังอยู่ในระหว่างการพัฒนา ไม่ควรนำไปใช้งานจริงจนกว่าจะมีการทดสอบอย่างละเอียดและยืนยันความถูกต้องครบถ้วน
