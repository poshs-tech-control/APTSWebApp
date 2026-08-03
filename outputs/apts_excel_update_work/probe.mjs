import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
const input = await FileBlob.load("../APTS_Module_Procedures_Module_Wise.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("Pallet Dispatch");
const range = sheet.getUsedRange();
console.log(range.address);
console.log(JSON.stringify(range.values.slice(0, 3)));
console.log(range.values.length, range.values[0].length);
