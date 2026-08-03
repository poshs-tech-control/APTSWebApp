import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const dateLabel = "14-Jul-2026";
const outputDir = ".";
const previewDir = `${outputDir}/previews`;
await fs.mkdir(previewDir, { recursive: true });

const procInput = await FileBlob.load("../APTS_Module_Procedures_Module_Wise.xlsx");
const procWorkbook = await SpreadsheetFile.importXlsx(procInput);

function bodyRows(sheetName) {
  const values = procWorkbook.worksheets.getItem(sheetName).getUsedRange().values;
  return values.slice(4).filter(row => row.some(cell => cell !== null && cell !== undefined && String(cell).trim() !== ""));
}

const rowsByModule = new Map();
for (const summaryRow of procWorkbook.worksheets.getItem("Summary").getUsedRange().values.slice(4)) {
  const moduleName = summaryRow[0];
  if (!moduleName) continue;
  rowsByModule.set(moduleName, bodyRows(moduleName));
}

rowsByModule.set("Pallet Order", [
  ["Pallet Order", "usp_ddweb_miscparameter", "Loads active misc parameter values for dropdowns.", "Pallet Shape dropdown", "Used for pallet shape list."],
  ["Pallet Order", "WebApp_get_CustomerList", "Loads active customer list for the current user with customer LocationCode.", "Customer/session location context", "Customer and LocationCode feed the Pallet Order master save; current UI can use selected login/session location."],
  ["Pallet Order", "WebApp_Get_PalletSkuMaster", "Returns matching pallet SKU rows by shape, width, and length.", "Show Matching SKUs", "Returns PalletSkewCode plus SheetWidth, SheetLength1, SheetLength2, stacking and component sizes for selected order quantity."],
  ["Pallet Order", "WebApp_IU_PalletOrder", "Creates or updates pallet order master record.", "Submit", "Saves CustomerCode, LocationCode, CREATEDBY and IPADDRESS; returns generated PalletOrderCode."],
  ["Pallet Order", "WebApp_IU_PalletOrderdtl", "Creates pallet order detail records for selected SKU rows.", "Submit details", "Called once per selected SKU with P_Width, P_Length1, P_Length2, P_Instructions and P_Counts."],
  ["Pallet Order", "Direct Insert: palletorderdtl fallback", "Fallback insert when the stored procedure/table shape rejects new Length/Instruction columns.", "Submit details fallback", "Inserts SheetWidth, SheetLength1, SheetLength2, optional Instructions and Counts, then generates PalletOrderdtlCode from TransactionID."],
]);

rowsByModule.set("Pallet Pre-Dispatch", [
  ["Pallet Pre-Dispatch", "Gen_usp_CheckCurrentStatus", "Validates scanned member current status.", "Member scan validation", "Checks member is in Pallet Assemble."],
  ["Pallet Pre-Dispatch", "WebApp_Get_PalletOrderByPalletCode", "Loads ordered pallet dimensions for a pallet code.", "Pallet scan/details", "Returns Width, Length1 and Length2 so Pre-Dispatch can carry order dimensions forward."],
  ["Pallet Pre-Dispatch", "WebAPP_Get_PalletPreDispatchByBatchNo_OR_PalletCode", "Checks duplicate Pre-Dispatch by pallet code or client batch number.", "Sticker upload and Submit validation", "Also reads existing Width, SheetLength, ClientLocation and related Pre-Dispatch values."],
  ["Pallet Pre-Dispatch", "WebApp_IU_palletpredispatch", "Saves pallet Pre-Dispatch master and updates member last status.", "Submit", "Now sends P_StorageLocation with pallet code, client batch no, sheet length, width, weight, thikness, user and IP."],
  ["Pallet Pre-Dispatch", "WebApp_Validation_PreDispatch_In_3Days", "Checks whether a member/pallet is within the 3-day validation window.", "Member scan validation", "Pre-Dispatch allows only when procedure returns ALLOWED."],
]);

const palletDispatch = rowsByModule.get("Pallet Dispatch").map(row => [...row]);
for (const row of palletDispatch) {
  if (row[1] === "WebApp_IU_palletdispatched") {
    row[4] = "Saves ShipToLocation, Consignee, FromLocation, truck/driver/transporter fields, GenerateChallan, user and IP; returns PalletDispCode.";
  }
  if (row[1] === "WebApp_Bulk_IU_PalletDispDtl") {
    row[4] = "Called with JSON payload for all selected pallets/batches after duplicate-dispatch checks.";
  }
}
rowsByModule.set("Pallet Dispatch", palletDispatch);

const palletAssemble = rowsByModule.get("Pallet Assemble").map(row => [...row]);
for (const row of palletAssemble) {
  if (row[1] === "WebApp_Get_PalletOrderDtls") {
    row[4] = "Shows pending order rows with PalletOrderdtlCode, Counts, AssembleCounts, SheetWidth, SheetLength1 and SheetLength2; closes screen when pending count becomes 0.";
  }
  if (row[1] === "WebApp_Iu_palletAssemble") {
    row[4] = "Returns generated PalletCode, stores selected order dimensions, and increments AssembleCounts for the selected order detail.";
  }
}
rowsByModule.set("Pallet Assemble", palletAssemble);

const memberReceipt = rowsByModule.get("Member Receipt").map(row => [...row]);
for (const row of memberReceipt) {
  if (row[1] === "WebApp_iu_MemberReceived") {
    row[4] = "Uses RecLoctype from session LOCATIONTYPE and receipt location/title from the login/session location context.";
  }
}
rowsByModule.set("Member Receipt", memberReceipt);

const moduleOrder = procWorkbook.worksheets.getItem("Summary").getUsedRange().values
  .slice(4)
  .map(row => row[0])
  .filter(Boolean);
const allRows = moduleOrder.flatMap(moduleName => rowsByModule.get(moduleName) ?? []);

function writeModuleSheet(moduleName, rows) {
  const sheet = procWorkbook.worksheets.getItem(moduleName);
  const totalRows = rows.length + 4;
  const current = sheet.getUsedRange().values;
  const header = current.slice(0, 4);
  header[1] = [`${moduleName} module procedure list and data-access coverage. Updated for recent table/session changes on ${dateLabel}.`, null, null, null, null];
  const matrix = [...header, ...rows];
  const target = sheet.getRange(`A1:E${Math.max(totalRows, current.length)}`);
  target.clear({ applyTo: "contents" });
  sheet.getRange(`A1:E${matrix.length}`).values = matrix;
  if (matrix.length > 5) {
    for (let rowIndex = 6; rowIndex <= matrix.length; rowIndex += 1) {
      sheet.getRange(`A${rowIndex}:E${rowIndex}`).copyFrom(sheet.getRange("A5:E5"), "formats");
    }
    sheet.getRange(`A1:E${matrix.length}`).values = matrix;
  }
  sheet.getRange(`A5:E${matrix.length}`).format.wrapText = true;
  sheet.getRange("A:E").format.autofitColumns();
  sheet.getRange(`A1:E${matrix.length}`).format.autofitRows();
}

for (const moduleName of moduleOrder) {
  writeModuleSheet(moduleName, rowsByModule.get(moduleName) ?? []);
}

const allSheet = procWorkbook.worksheets.getItem("All Procedures");
const allValues = allSheet.getUsedRange().values;
const allHeader = allValues.slice(0, 4);
allHeader[1] = [`Stored procedures, direct SQL queries, and shared service calls referenced across APTS web modules. Updated for recent module and table changes on ${dateLabel}.`, null, null, null, null];
const allMatrix = [...allHeader, ...allRows];
allSheet.getRange(`A1:E${Math.max(allValues.length, allMatrix.length)}`).clear({ applyTo: "contents" });
allSheet.getRange(`A1:E${allMatrix.length}`).values = allMatrix;
for (let rowIndex = 6; rowIndex <= allMatrix.length; rowIndex += 1) {
  allSheet.getRange(`A${rowIndex}:E${rowIndex}`).copyFrom(allSheet.getRange("A5:E5"), "formats");
}
allSheet.getRange(`A1:E${allMatrix.length}`).values = allMatrix;
allSheet.getRange(`A5:E${allMatrix.length}`).format.wrapText = true;
allSheet.getRange("A:E").format.autofitColumns();
allSheet.getRange(`A1:E${allMatrix.length}`).format.autofitRows();

const summarySheet = procWorkbook.worksheets.getItem("Summary");
const summaryValues = summarySheet.getUsedRange().values;
summaryValues[1] = [`Module-wise procedure list and purpose summary. Updated for recent module and table changes on ${dateLabel}.`, null, null, null];
for (let i = 4; i < summaryValues.length; i += 1) {
  const moduleName = summaryValues[i][0];
  if (!moduleName) continue;
  summaryValues[i][1] = (rowsByModule.get(moduleName) ?? []).length;
}
summarySheet.getRange(`A1:D${summaryValues.length}`).values = summaryValues;
summarySheet.getRange("A:D").format.autofitColumns();
summarySheet.getRange(`A1:D${summaryValues.length}`).format.autofitRows();

const procErrors = await procWorkbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "procedure workbook formula error scan",
});
console.log("PROCEDURE_ERRORS");
console.log(procErrors.ndjson);

let renderedProc = 0;
for (const sheetName of procWorkbook.worksheets.items.map(ws => ws.name)) {
  try {
    const preview = await procWorkbook.render({ sheetName, autoCrop: "all", scale: 1, format: "png" });
    const bytes = new Uint8Array(await preview.arrayBuffer());
    await fs.writeFile(`${previewDir}/procedures_${sheetName.replace(/[\\/:*?"<>|]/g, "_")}.png`, bytes);
    if (bytes.length > 1000) renderedProc += 1;
  } catch (err) {
    console.log(`PROCEDURE_RENDER_WARNING ${sheetName}: ${err.message}`);
  }
}
console.log(`PROCEDURE_RENDERED_NONTRIVIAL ${renderedProc}/${procWorkbook.worksheets.items.length}`);

const procOutput = await SpreadsheetFile.exportXlsx(procWorkbook);
await procOutput.save(`${outputDir}/APTS_Module_Procedures_Module_Wise_Updated_20260714.xlsx`);
await procOutput.save("../APTS_Module_Procedures_Module_Wise.xlsx");
console.log(`PROCEDURE_ROWS ${allRows.length}`);

const tableInput = await FileBlob.load("../APTS_Module_Tables.xlsx");
const tableWorkbook = await SpreadsheetFile.importXlsx(tableInput);
const tableSheet = tableWorkbook.worksheets.getItem("Sheet1");
const tableRows = [
  ["Table Name", "Description", "Module"],
  ["UserLoginHistory", "Added login/logout audit table with UserCode, UserName, LoginLocation, LoginDateTime, IPAddress, AuditTrail and logout timestamp support.", "Login"],
  ["palletdispatch", "Added Consignee field used by Pallet Dispatch master save and challan/customer dispatch context.", "Pallet Dispatch"],
  ["palletpre_dispatch", "Added StorageLocation field passed as P_StorageLocation during Pre-Dispatch save.", "Pallet Pre-Dispatch"],
  ["palletorderdtl", "Added SheetWidth, SheetLength1, SheetLength2 and optional Instructions for pallet order detail dimensions.", "Pallet Order"],
  ["PalletOrderDtl", "Uses Counts and AssembleCounts to mark order detail Status as Completed after Pallet Assemble reaches required quantity.", "Pallet Assemble"],
];
tableSheet.getRange("A1:E30").clear({ applyTo: "contents" });
tableSheet.getRange(`A1:C${tableRows.length}`).values = tableRows;
tableSheet.getRange("A1:C1").format = {
  fill: "#1F4E78",
  font: { bold: true, color: "#FFFFFF" },
};
tableSheet.getRange(`A1:C${tableRows.length}`).format.borders = { preset: "all", style: "thin", color: "#D9D9D9" };
tableSheet.getRange(`A2:C${tableRows.length}`).format.wrapText = true;
tableSheet.getRange("A:C").format.autofitColumns();
tableSheet.getRange(`A1:C${tableRows.length}`).format.autofitRows();

const tableErrors = await tableWorkbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "table workbook formula error scan",
});
console.log("TABLE_ERRORS");
console.log(tableErrors.ndjson);

let renderedTables = 0;
for (const sheetName of tableWorkbook.worksheets.items.map(ws => ws.name)) {
  try {
    const preview = await tableWorkbook.render({ sheetName, autoCrop: "all", scale: 1, format: "png" });
    const bytes = new Uint8Array(await preview.arrayBuffer());
    await fs.writeFile(`${previewDir}/tables_${sheetName.replace(/[\\/:*?"<>|]/g, "_")}.png`, bytes);
    if (bytes.length > 1000) renderedTables += 1;
  } catch (err) {
    console.log(`TABLE_RENDER_WARNING ${sheetName}: ${err.message}`);
  }
}
console.log(`TABLE_RENDERED_NONTRIVIAL ${renderedTables}/${tableWorkbook.worksheets.items.length}`);

const tableOutput = await SpreadsheetFile.exportXlsx(tableWorkbook);
await tableOutput.save(`${outputDir}/APTS_Module_Tables_Updated_20260714.xlsx`);
await tableOutput.save("../APTS_Module_Tables.xlsx");
console.log(`TABLE_ROWS ${tableRows.length - 1}`);
