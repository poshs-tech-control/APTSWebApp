import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const checks = [
  { file: "../APTS_Module_Procedures_Module_Wise.xlsx", sheet: "Summary", range: "A1:D20" },
  { file: "../APTS_Module_Procedures_Module_Wise.xlsx", sheet: "Pallet Dispatch", range: "A1:E14" },
  { file: "../APTS_Module_Procedures_Module_Wise.xlsx", sheet: "Pallet Assemble", range: "A1:E18" },
  { file: "../APTS_Module_Procedures_Module_Wise.xlsx", sheet: "Challan", range: "A1:E14" },
  { file: "../APTS_Module_Tables.xlsx", sheet: "Sheet1", range: "A1:C8" },
];
for (const check of checks) {
  const input = await FileBlob.load(check.file);
  const workbook = await SpreadsheetFile.importXlsx(input);
  const result = await workbook.inspect({ kind: "table", sheetId: check.sheet, range: check.range, maxChars: 8000, tableMaxRows: 20, tableMaxCols: 6, tableMaxCellChars: 180 });
  console.log(`\n=== ${check.file} :: ${check.sheet} ${check.range} ===`);
  console.log(result.ndjson);
}
