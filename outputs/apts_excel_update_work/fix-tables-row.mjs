import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
const path = "../APTS_Module_Tables.xlsx";
const input = await FileBlob.load(path);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("Sheet1");
sheet.getRange("A7:C7").values = [[
  "Challan background mail",
  "Shared background challan queue posts the generated transaction code to ChallanController after the screen redirects, matching Collection Internal Transfer behavior.",
  "Pallet Dispatch"
]];
sheet.getRange("A7:C7").copyFrom(sheet.getRange("A6:C6"), "all");
sheet.getRange("A7:C7").values = [[
  "Challan background mail",
  "Shared background challan queue posts the generated transaction code to ChallanController after the screen redirects, matching Collection Internal Transfer behavior.",
  "Pallet Dispatch"
]];
sheet.getRange("A:C").format.wrapText = true;
sheet.getRange("A:C").format.autofitColumns();
sheet.getRange("A:C").format.autofitRows();
const preview = await workbook.render({ sheetName: "Sheet1", autoCrop: "all", scale: 1, format: "png" });
await fs.writeFile("preview-Tables-Sheet1.png", new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(path);
