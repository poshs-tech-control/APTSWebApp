import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const files = [
  "../APTS_Module_Procedures_Module_Wise.xlsx",
  "../APTS_Module_Tables.xlsx",
];

for (const file of files) {
  const input = await FileBlob.load(file);
  const workbook = await SpreadsheetFile.importXlsx(input);
  const summary = await workbook.inspect({
    kind: "workbook,sheet,table",
    maxChars: 12000,
    tableMaxRows: 8,
    tableMaxCols: 8,
    tableMaxCellChars: 120,
  });
  console.log(`\n=== ${file} ===`);
  console.log(summary.ndjson);
}
