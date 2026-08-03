import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("../APTS_Module_Procedures_Module_Wise.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);

for (const sheetName of [
  "Pallet Order",
  "Pallet Pre-Dispatch",
  "Pallet Dispatch",
  "Pallet Assemble",
  "Internal Transfer",
  "Collection Internal Transfer",
  "Member Collection",
  "Member Receipt",
  "All Procedures",
]) {
  const sheet = workbook.worksheets.getItem(sheetName);
  const values = sheet.getUsedRange().values;
  console.log(`--- ${sheetName} ---`);
  console.log(JSON.stringify(values, null, 2));
}
