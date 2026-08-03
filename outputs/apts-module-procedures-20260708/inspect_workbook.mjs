import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "../APTS_Module_Procedures_Module_Wise.xlsx";
const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 8,
  tableMaxCols: 12,
  tableMaxCellChars: 120,
});
console.log(summary.ndjson);

for (const name of ["All Procedures", "Summary", "Login", "Common / Shared"]) {
  try {
    workbook.worksheets.getItem(name);
    const region = await workbook.inspect({
      kind: "region",
      sheetId: name,
      range: "A1:J20",
      maxChars: 5000,
    });
    console.log(`--- ${name} ---`);
    console.log(region.ndjson);
  } catch (err) {
    console.log(`--- ${name} missing ---`);
  }
}
