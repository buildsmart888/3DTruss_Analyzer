# 3D Truss Analyzer (Refactored)

โปรเจกต์วิเคราะห์โครงสร้างโครงถัก 3 มิติ (3D Truss) ที่พัฒนาใหม่ด้วยหลักการวิศวกรรมที่ถูกต้องและสามารถบำรุงรักษาได้ในระยะยาว

## ⚠️ สถานะการพัฒนา

**กำลังอยู่ระหว่างการพัฒนา** - โค้ดชุดนี้เป็นส่วนหนึ่งของโครงการปรับปรุงใหม่ (Refactoring) เพื่อแก้ไขปัญหาทางวิศวกรรมที่พบในเวอร์ชันเดิม

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

### 🔄 กำลังดำเนินการ (Phase 2)
- [x] เพิ่มการทดสอบกับโจทย์จากตำรา (Textbook Benchmark Tests)
- [x] พัฒนา UI พื้นฐาน (WinForms)
- [x] เพิ่มฟีเจอร์นำเข้า/ส่งออกไฟล์ (JSON, CSV, Text Report)
- [ ] สร้างเอกสารการใช้งานสำหรับผู้ใช้งานทั่วไป

### 📅 แผนในอนาคต (Phase 3)
- [ ] รองรับโหลดหลายรูปแบบ (ลม, แผ่นดินไหว)
- [ ] ตรวจสอบความปลอดภัย (Safety Check) ตามมาตรฐาน AISC
- [ ] แสดงผลกราฟิก 3D (OpenGL/SharpGL)
- [ ] ส่งออกเป็นรายงาน PDF (iTextSharp/PDFsharp)

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
