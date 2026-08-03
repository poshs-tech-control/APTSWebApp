import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("./APTS_Module_Procedures_Module_Wise_Updated_20260708.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);

for (const request of [
  { sheetId: "Login", range: "A4:E10" },
  { sheetId: "All Procedures", range: "A4:E12" },
  { sheetId: "Summary", range: "A1:D8" },
]) {
  const inspect = await workbook.inspect({
    kind: "region",
    sheetId: request.sheetId,
    range: request.range,
    maxChars: 8000,
  });
  console.log(`--- ${request.sheetId} ${request.range} ---`);
  console.log(inspect.ndjson);
}

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 300 },
  summary: "saved formula error scan",
});
console.log("--- saved formula errors ---");
console.log(errors.ndjson);
