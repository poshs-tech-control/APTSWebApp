import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "../APTS_Module_Procedures_Module_Wise.xlsx";
const outputDir = ".";
const outputPath = `${outputDir}/APTS_Module_Procedures_Module_Wise_Updated_20260708.xlsx`;
const previewDir = `${outputDir}/previews`;
await fs.mkdir(previewDir, { recursive: true });

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const loginRows = [
  ["Login", "WebApp_Get_LoginDetails", "Validates login id and password and returns user session data.", "Login button", "Stores USERCODE and USERFULLNAME in session for module calls."],
  ["Login", "WebApp_LocationMaster", "Loads active locations for the login location dropdown.", "Login location dropdown", "Returns LocationCode, CustomerCode, LocationTitle, and EmailId; selected LocationCode/Title are stored in session."],
  ["Login", "WebApp_Validation_LocationAccess", "Validates whether the logged-in user has access to the selected location.", "Login submit validation", "Uses UserCode and LocationCode; blocks login with popup when access is not granted."],
  ["Login", "WebApp_IU_UserLoginHistory", "Inserts a user login history row after successful login.", "Login submit after credential and location validation", "Writes UserCode, UserName, LoginLocation, LoginDateTime, IPAddress, and JSON AuditTrail; returns UserLoginHistoryID for logout tracking."],
  ["Login", "WebApp_U_UserLoginHistoryLogout", "Updates the open login history row with LogoutDateTime when the user logs out.", "Home/Logout before session clear", "Uses session UserLoginHistoryID first, then falls back to the latest open row for UserCode when the session id is unavailable."],
];

const loginSheet = workbook.worksheets.getItem("Login");
loginSheet.getRange("A2:E2").values = [["Login module procedure list and data-access coverage. Updated for login history, client IP, and logout timestamp on 08-Jul-2026.", null, null, null, null]];
loginSheet.getRange("A5:E9").values = loginRows;
loginSheet.getRange("A8:E8").copyFrom(loginSheet.getRange("A7:E7"), "formats");
loginSheet.getRange("A9:E9").copyFrom(loginSheet.getRange("A7:E7"), "formats");
loginSheet.getRange("A5:E9").values = loginRows;
loginSheet.getRange("A5:E9").format.wrapText = true;
loginSheet.getRange("A:E").format.autofitColumns();
loginSheet.getRange("A1:E9").format.autofitRows();

const allSheet = workbook.worksheets.getItem("All Procedures");
const used = allSheet.getUsedRange();
const allValues = used.values;
const header = allValues.slice(0, 4);
const currentRows = allValues.slice(4).filter(row => row.some(cell => cell !== null && cell !== undefined && String(cell).trim() !== ""));
const nonLoginRows = currentRows.filter(row => row[0] !== "Login");
const updatedAll = [...header, ...loginRows, ...nonLoginRows];
allSheet.getRange(`A1:E${updatedAll.length}`).values = updatedAll;
allSheet.getRange("A2:E2").values = [["Stored procedures, direct SQL queries, and shared service calls referenced across APTS web modules. Updated for login history, client IP, and logout timestamp on 08-Jul-2026.", null, null, null, null]];
for (let row = 8; row <= 9; row += 1) {
  allSheet.getRange(`A${row}:E${row}`).copyFrom(allSheet.getRange("A7:E7"), "formats");
}
allSheet.getRange(`A1:E${updatedAll.length}`).values = updatedAll;
allSheet.getRange(`A5:E${updatedAll.length}`).format.wrapText = true;
allSheet.getRange("A:E").format.autofitColumns();
allSheet.getRange(`A1:E${updatedAll.length}`).format.autofitRows();

const summarySheet = workbook.worksheets.getItem("Summary");
summarySheet.getRange("A2:D2").values = [["Module-wise procedure list and purpose summary. Updated for login history, client IP, and logout timestamp on 08-Jul-2026.", null, null, null]];
summarySheet.getRange("B5").values = [[loginRows.length]];
summarySheet.getRange("A:D").format.autofitColumns();
summarySheet.getRange("A1:D20").format.autofitRows();

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log("FORMULA_ERRORS");
console.log(errors.ndjson);

const sheets = await workbook.inspect({ kind: "sheet", include: "id,name", maxChars: 12000 });
console.log("SHEETS");
console.log(sheets.ndjson);

const allCheck = await workbook.inspect({
  kind: "region",
  sheetId: "All Procedures",
  range: "A1:E12",
  maxChars: 6000,
});
console.log("ALL_CHECK");
console.log(allCheck.ndjson);

const loginCheck = await workbook.inspect({
  kind: "region",
  sheetId: "Login",
  range: "A1:E10",
  maxChars: 6000,
});
console.log("LOGIN_CHECK");
console.log(loginCheck.ndjson);

const sheetNames = workbook.worksheets.items.map(ws => ws.name);
let rendered = 0;
for (const sheetName of sheetNames) {
  try {
    const preview = await workbook.render({ sheetName, autoCrop: "all", scale: 1, format: "png" });
    const bytes = new Uint8Array(await preview.arrayBuffer());
    const safeName = sheetName.replace(/[\\/:*?"<>|]/g, "_");
    await fs.writeFile(`${previewDir}/${safeName}.png`, bytes);
    if (bytes.length > 1000) rendered += 1;
  } catch (err) {
    console.log(`RENDER_WARNING ${sheetName}: ${err.message}`);
  }
}
console.log(`RENDERED_NONTRIVIAL ${rendered}/${sheetNames.length}`);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`SAVED ${outputPath}`);
console.log(`ROWS ${updatedAll.length - 4}`);
