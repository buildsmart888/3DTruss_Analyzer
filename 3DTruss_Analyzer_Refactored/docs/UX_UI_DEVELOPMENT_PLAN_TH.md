# แผนพัฒนา UX/UI ของ GOStructAnalysis

สถานะ: Active  
เจ้าของแผน: Product / Engineering  
วันที่เริ่ม: 2026-09-13

## เป้าหมาย

พัฒนา desktop workflow ให้ผู้ใช้ทำงานจากข้อมูลโครงการชุดเดียวตลอดวงจร

`สร้าง → แก้ → ใส่แรง → วิเคราะห์ → เลือกผล → export → save/reopen`

การปรับ UX/UI ไม่เปลี่ยนหน่วย แกน เครื่องหมาย หรือสมการของ solver โดยเงียบ และ Milestone G
ยังเปิด qualification gate จนกว่าจะมี external comparison และ named engineering review จริง

## แผนงานและเกณฑ์ผ่าน

| ระยะ | ขอบเขต | เกณฑ์ผ่าน |
| --- | --- | --- |
| **UX-0: ความถูกต้องของ workflow** | document เดียว, New/Open/Save, dirty/stale state, export routing และ envelope | แก้โมเดลแล้วผลเก่าถูกระบุชัด; export ตรงกับผลที่เลือก; New ไม่เหลือข้อมูลเก่า |
| **UX-1: จัด workspace** | Ribbon, panel พับได้, theme, icon, keyboard shortcuts และ layout persistence | ใช้ที่ 100/125/150% DPI ได้ ไม่มีข้อความหรือปุ่มถูกตัด และ viewport ได้พื้นที่หลัก |
| **UX-2: Modeling ที่ใช้งานจริง** | Node/member tools, work plane, snapping, multi-select, move/copy/delete/array, undo/redo และ managers | สร้างและแก้ portal frame ได้ทั้งจาก viewport และตาราง โดยข้อมูลตรงกัน |
| **UX-3: Loads workspace** | Pattern/combination managers, typed load editor, local/global preview, assignment ledger และ overwrite dialog | วาง แก้ และลบ load ได้ครบ; ลูกศร หน่วย pattern และค่าตรงกัน |
| **UX-4: Analysis และ Results** | Background Run Selected/All, cancel, diagnostics, snapshot explorer, signed envelopes และ station inspection | เลือกผลแล้ว table/viewport/legend เปลี่ยนพร้อมกัน และเลือกสมาชิกไม่เด้งออกจาก Results |
| **UX-5: Export และ release checks** | CSV/JSON/XLSX/PDF จากผลชุดเดียว, PNG จาก WPF viewport และ export preview | เปิดไฟล์จริงได้; ค่า หน่วย case/component และ station side ตรงกันทุกช่องทาง |

## ข้อกำหนด Envelope ของ UX-4

Envelope ต้องเก็บค่าต่ำสุดและสูงสุดพร้อมเครื่องหมาย รวมถึง governing case แยกตาม
`member–station–component` ห้ามใช้ governing case เดียวแทนทั้งสมาชิก และต้องเก็บค่า
Left/Right แยกกันที่ตำแหน่ง point load หรือ discontinuity

## วิธีตรวจรับ

ใช้โมเดลอย่างน้อยสามระดับ:

1. Cantilever ที่ตรวจ deformation และ reaction ด้วย hand calculation ได้
2. Portal/space frame ที่มีหลาย load cases และ combinations
3. โมเดลใหญ่ที่เพิ่มจำนวนสมาชิกเป็นขั้น เพื่อวัด responsiveness, memory และ solve/export time

แต่ละโมเดลต้องผ่านวงจรเต็ม และตรวจค่าตัวเลข การตอบสนองหน้าจอ การคง stable ID การเตือนผลเก่า
รวมถึงภาพผลลัพธ์จริง หลักฐานต้องแยกสถานะดังนี้:

| Capability | มีใน Core | เชื่อม UI | Automated test | Desktop test |
| --- | --- | --- | --- | --- |
| UX-0 document/state/export | Implemented | Implemented | Passed | Pending |
| UX-1 workspace/DPI | Partial | Partial | Pending | Pending |
| UX-2 modeling | Implemented | Partial | Partial | Partial |
| UX-3 loads | Implemented | Partial | Partial | Pending |
| UX-4 results/envelope | Partial | Partial | Partial | Pending |
| UX-5 export/release | Partial | Partial | Partial | Pending |

`Implemented` ไม่เท่ากับ `Qualified`; ช่อง Desktop test ต้องมีบันทึกจากการใช้งาน native window จริง

## ลำดับดำเนินงาน

1. UX-0 — สร้าง document lifecycle และ result provenance ที่เชื่อถือได้
2. UX-1 — จัด workspace ให้ viewport เป็นพื้นที่หลักและผ่าน DPI gate
3. UX-2 — ปิด modeling workflow จาก viewport และตาราง
4. UX-3 — ปิด loading workflow และ assignment traceability
5. UX-4 — ปิด analysis/result synchronization และ signed envelope semantics
6. UX-5 — รวม export pipeline และทำ release verification

## UX-0 Implementation Slices

- **UX-0.1 lifecycle:** New ล้าง engineering/result state ทั้งหมด, Open/Save ตั้ง dirty state ถูกต้อง,
  model edit ทำให้ผลเดิม stale และห้าม export ผลเก่า
- **UX-0.2 result selection:** นิยาม selected analysis snapshot เพียงชุดเดียวสำหรับ viewport, table และ export
- **UX-0.3 export routing:** CSV/JSON/XLSX/PDF/PNG ใช้ selected snapshot และ formatting contract เดียว
- **UX-0.4 envelope contract:** signed min/max พร้อม governing case ต่อ member/station/component/side
- **UX-0.5 regression:** automated workflow tests และ desktop evidence ของ New/Open/Edit/Analyze/Export/Save/Reopen

งานเริ่มต้นของแผนนี้คือ UX-0.1 และการหยุด silent export fallback ใน UX-0.3

## Implementation Progress

- 2026-09-13 — UX-0.1: New ล้างทุก engineering grid, เพิ่ม dirty/stale state และ selection ไม่บังคับออกจาก Results
- 2026-09-13 — UX-0.4: signed envelope ต่อ member/station/component/Left-Right พร้อม governing selection
- 2026-09-14 — UX-0.2/0.3: MainForm จับ selected `AnalysisSnapshot` พร้อม document checksum;
  CSV, JSON และ XLSX ใช้ snapshot เดียวกัน และ XLSX ใช้ OpenXML cell references ที่ถูกต้อง
- 2026-09-14 — UX-0.3/0.5: PDF ใช้ selected snapshot พร้อม xref offsets ที่ตรวจสอบได้;
  automated workflow regression ผ่าน Edit → Analyze → CSV/JSON/PDF → Save/Reopen และยืนยัน checksum/stable IDs

UX-0 ยังรอ desktop acceptance ของ New → Edit → Analyze → Export → Save/Reopen ก่อนเปลี่ยนสถานะเป็น Complete
