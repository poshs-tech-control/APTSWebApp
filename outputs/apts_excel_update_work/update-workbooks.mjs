import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const updatedNote = "Updated for recent module, table and background challan changes on 22-Jul-2026.";

function rangeAddress(row, cols = 5) {
  const end = String.fromCharCode("A".charCodeAt(0) + cols - 1);
  return `A${row}:${end}${row}`;
}

function findRow(values, module, proc) {
  for (let i = 0; i < values.length; i++) {
    if (values[i][0] === module && values[i][1] === proc) {
      return i + 1;
    }
  }
  return 0;
}

function findModuleRow(values, module) {
  for (let i = 0; i < values.length; i++) {
    if (values[i][0] === module) {
      return i + 1;
    }
  }
  return 0;
}

function updateSubtitle(sheet, prefix) {
  sheet.getRange("A2").values = [[`${prefix} ${updatedNote}`]];
}

function upsertProcedureRow(sheet, rowValues) {
  const used = sheet.getUsedRange();
  const values = used.values;
  let row = findRow(values, rowValues[0], rowValues[1]);
  if (!row) {
    row = values.length + 1;
    const previous = sheet.getRange(rangeAddress(row - 1));
    sheet.getRange(rangeAddress(row)).copyFrom(previous, "all");
  }
  sheet.getRange(rangeAddress(row)).values = [rowValues];
  return row;
}

function updateSummaryCount(sheet, module, count) {
  const used = sheet.getUsedRange();
  const row = findModuleRow(used.values, module);
  if (row) {
    sheet.getRange(`B${row}`).values = [[count]];
  }
}

async function updateProceduresWorkbook() {
  const path = "../APTS_Module_Procedures_Module_Wise.xlsx";
  const input = await FileBlob.load(path);
  const workbook = await SpreadsheetFile.importXlsx(input);

  for (const sheetName of ["Summary", "All Procedures", "Pallet Dispatch", "Pallet Assemble", "Challan"]) {
    const sheet = workbook.worksheets.getItem(sheetName);
    const prefix = sheetName === "Summary"
      ? "Module-wise procedure list and purpose summary."
      : `${sheetName} module procedure list and data-access coverage.`;
    updateSubtitle(sheet, prefix);
  }

  const palletDispatchRow = [
    "Pallet Dispatch",
    "ChallanController.GeneratePalletDispatchChallan",
    "Generates pallet dispatch challan PDF and sends challan mail after the dispatch save.",
    "Background challan queue after details screen redirect",
    "SaveDispatch now returns challanPending with PalletDispCode; apts-background-challan posts palletDispatchCode so PDF/mail generation does not block the saved dispatch screen."
  ];
  const palletAssembleRow = [
    "Pallet Assemble",
    "Client Script: apts-pallet-assemble.js size-rule validation",
    "Tracks CM and LM quantity rules independently when component sizes are numerically the same.",
    "Member QR Code add validation",
    "Scanned members store sizeRuleLabel, so CM size 1100 qty 2 does not block LM1 size 1100 qty 3 for the same order."
  ];
  const challanRow = [
    "Challan",
    "ChallanMailService.GeneratePdfAndSendMail",
    "Generates challan PDF first and sends the email attachment through the configured SMTP path.",
    "Pallet Dispatch and shared challan actions",
    "SMTP failures are returned as a controlled mail-not-sent message after the PDF is generated; background runner shows success/error toast."
  ];

  upsertProcedureRow(workbook.worksheets.getItem("Pallet Dispatch"), palletDispatchRow);
  upsertProcedureRow(workbook.worksheets.getItem("Pallet Assemble"), palletAssembleRow);
  upsertProcedureRow(workbook.worksheets.getItem("Challan"), challanRow);
  upsertProcedureRow(workbook.worksheets.getItem("All Procedures"), palletDispatchRow);
  upsertProcedureRow(workbook.worksheets.getItem("All Procedures"), palletAssembleRow);
  upsertProcedureRow(workbook.worksheets.getItem("All Procedures"), challanRow);

  updateSummaryCount(workbook.worksheets.getItem("Summary"), "Pallet Dispatch", 8);
  updateSummaryCount(workbook.worksheets.getItem("Summary"), "Pallet Assemble", 12);
  updateSummaryCount(workbook.worksheets.getItem("Summary"), "Challan", 8);

  for (const sheetName of ["Summary", "All Procedures", "Pallet Dispatch", "Pallet Assemble", "Challan"]) {
    const sheet = workbook.worksheets.getItem(sheetName);
    sheet.getRange("A:E").format.wrapText = true;
    sheet.getRange("A:E").format.autofitColumns();
    sheet.getRange("A:E").format.autofitRows();
  }

  const errors = await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A", options: { useRegex: true, maxResults: 300 }, summary: "formula error scan" });
  console.log("PROCEDURE_ERRORS");
  console.log(errors.ndjson || "none");

  for (const sheetName of ["Summary", "Pallet Dispatch", "Pallet Assemble", "Challan"]) {
    const preview = await workbook.render({ sheetName, autoCrop: "all", scale: 1, format: "png" });
    await fs.writeFile(`preview-${sheetName.replace(/[^A-Za-z0-9]+/g, "-")}.png`, new Uint8Array(await preview.arrayBuffer()));
  }

  const output = await SpreadsheetFile.exportXlsx(workbook);
  await output.save(path);
}

async function updateTablesWorkbook() {
  const path = "../APTS_Module_Tables.xlsx";
  const input = await FileBlob.load(path);
  const workbook = await SpreadsheetFile.importXlsx(input);
  const sheet = workbook.worksheets.getItem("Sheet1");
  const used = sheet.getUsedRange();
  const values = used.values;

  const updates = new Map([
    ["palletdispatch", "Added Consignee field used by Pallet Dispatch master save and challan/customer dispatch context. GenerateChallan remains the dispatch intent; challan PDF/mail now runs in the background from PalletDispCode after save."],
    ["PalletOrderDtl", "Uses Counts and AssembleCounts to mark order detail Status as Completed after Pallet Assemble reaches required quantity. Assemble scanning now keeps CM and LM size-rule counts independent even when the numeric size is the same."],
  ]);

  for (let i = 1; i < values.length; i++) {
    const tableName = values[i][0];
    if (updates.has(tableName)) {
      sheet.getRange(`B${i + 1}`).values = [[updates.get(tableName)]];
    }
  }

  const noteRow = ["Challan background mail", "Shared background challan queue posts the generated transaction code to ChallanController after the screen redirects, matching Collection Internal Transfer behavior.", "Pallet Dispatch"];
  const existingNoteRow = values.findIndex(row => row[0] === noteRow[0]) + 1;
  let targetRow = existingNoteRow;
  if (!targetRow) {
    targetRow = values.length + 1;
    sheet.getRange(`A${targetRow}:C${targetRow}`).copyFrom(sheet.getRange(`A${targetRow - 1}:C${targetRow - 1}`), "all");
  }
  sheet.getRange(`A${targetRow}:C${targetRow}`).values = [noteRow];

  sheet.getRange("A:C").format.wrapText = true;
  sheet.getRange("A:C").format.autofitColumns();
  sheet.getRange("A:C").format.autofitRows();

  const errors = await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A", options: { useRegex: true, maxResults: 300 }, summary: "formula error scan" });
  console.log("TABLE_ERRORS");
  console.log(errors.ndjson || "none");

  const preview = await workbook.render({ sheetName: "Sheet1", autoCrop: "all", scale: 1, format: "png" });
  await fs.writeFile("preview-Tables-Sheet1.png", new Uint8Array(await preview.arrayBuffer()));

  const output = await SpreadsheetFile.exportXlsx(workbook);
  await output.save(path);
}

await updateProceduresWorkbook();
await updateTablesWorkbook();
console.log("UPDATED_WORKBOOKS");
