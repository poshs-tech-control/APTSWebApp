import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

async function inspectWorkbook(path, label) {
  const input = await FileBlob.load(path);
  const workbook = await SpreadsheetFile.importXlsx(input);
  console.log(`=== ${label} ===`);
  console.log((await workbook.inspect({
    kind: "workbook,sheet,table",
    maxChars: 16000,
    tableMaxRows: 25,
    tableMaxCols: 8,
    tableMaxCellChars: 180,
  })).ndjson);

  for (const sheet of workbook.worksheets.items) {
    const used = sheet.getUsedRange();
    if (!used) continue;
    const values = used.values;
    console.log(`--- ${label} :: ${sheet.name} :: ${values.length}x${values[0]?.length ?? 0} ---`);
    console.log(JSON.stringify(values, null, 2));
  }
}

await inspectWorkbook("../APTS_Module_Procedures_Module_Wise.xlsx", "procedures");
await inspectWorkbook("../APTS_Module_Tables.xlsx", "tables");
