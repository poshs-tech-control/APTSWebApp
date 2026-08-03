import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
const input = await FileBlob.load("./APTS_Module_Procedures_Module_Wise_Updated_20260708.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);
const login = workbook.worksheets.getItem("Login");
const all = workbook.worksheets.getItem("All Procedures");
console.log(JSON.stringify({ loginRows: login.getRange("A5:E9").values }, null, 2));
console.log(JSON.stringify({ allLoginRows: all.getRange("A5:E9").values }, null, 2));
