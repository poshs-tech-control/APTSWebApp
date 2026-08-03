import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
const input = await FileBlob.load("../APTS_Module_Tables.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("Sheet1");
for (const addr of ["A6:C8", "A7:C7", "A8:C8"]) {
  const r = sheet.getRange(addr);
  console.log(addr, JSON.stringify(r.values));
}
