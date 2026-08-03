import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("../APTS_Module_Procedures_Module_Wise.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);
for (const sheetName of ["Pallet Pre-Dispatch", "Pallet Dispatch", "Pallet Assemble", "Challan", "Summary", "All Procedures"]) {
  const inspect = await workbook.inspect({
    kind: "region",
    sheetId: sheetName,
    range: "A1:E80",
    maxChars: 12000,
    tableMaxRows: 80,
    tableMaxCols: 5,
    tableMaxCellChars: 160,
  });
  console.log(`\n=== ${sheetName} ===`);
  console.log(inspect.ndjson);
}
