using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using APTS_WEB_APP.Models;
using Newtonsoft.Json.Linq;

namespace APTS_WEB_APP.Helpers
{
    public class OcrReadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public string Text { get; set; }
    }

    public static class QrOcrHelper
    {
        private static readonly HttpClient VisionClient = new HttpClient();

        public static async Task<OcrReadResult> ReadGoogleVisionTextAsync(Stream requestStream, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GOOGLE_VISION_API_KEY")
            {
                return new OcrReadResult { Success = false, Message = "Google Vision API key is not configured." };
            }

            string imageData;
            using (var reader = new StreamReader(requestStream))
            {
                string body = await reader.ReadToEndAsync();
                imageData = JObject.Parse(body).Value<string>("imageData");
            }

            string base64Image = NormalizeBase64Image(imageData);
            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return new OcrReadResult { Success = false, Message = "Camera image was not received." };
            }

            var requestBody = new JObject
            {
                ["requests"] = new JArray
                {
                    new JObject
                    {
                        ["image"] = new JObject { ["content"] = base64Image },
                        ["features"] = new JArray
                        {
                            new JObject { ["type"] = "TEXT_DETECTION" }
                        }
                    }
                }
            };

            string endpoint = "https://vision.googleapis.com/v1/images:annotate?key=" + Uri.EscapeDataString(apiKey);
            var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await VisionClient.PostAsync(endpoint, content);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new OcrReadResult
                {
                    Success = false,
                    Message = "Google Vision OCR failed.",
                    Details = responseText
                };
            }

            JObject result = JObject.Parse(responseText);
            string detectedText = result["responses"]?[0]?["fullTextAnnotation"]?["text"]?.ToString()
                ?? result["responses"]?[0]?["textAnnotations"]?[0]?["description"]?.ToString()
                ?? string.Empty;

            return new OcrReadResult
            {
                Success = true,
                Text = detectedText
            };
        }

        public static string NormalizeQrScanValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = Regex.Replace(value, @"\s+", " ").Trim();
            string extracted = ExtractQrPayloadValue(normalized);
            return Regex.Replace(extracted, @"\s+", string.Empty).Trim().ToUpperInvariant();
        }

        public static object ParsePreDispatchFields(string text)
        {
            return new
            {
                clientBatchNo = FindTextValue(text, "Client\\s*Batch\\s*No|Batch\\s*No|Client\\s*Batch"),
                sheetLengthH1 = FindNumberValue(text, "Sheet\\s*Length\\s*L1|Length\\s*L1|L1|Sheet\\s*Length\\s*H1|Length\\s*H1|H1"),
                sheetLengthH2 = FindNumberValue(text, "Sheet\\s*Length\\s*L2|Length\\s*L2|L2|Sheet\\s*Length\\s*H2|Length\\s*H2|H2"),
                sheetWidth = FindNumberValue(text, "Sheet\\s*Width|Width"),
                thickness = FindNumberValue(text, "Thickness"),
                weight = FindNumberValue(text, "Weight")
            };
        }

        public static object ParseDispatchFields(string text)
        {
            List<PalletDispatcch.DispatchOcrItem> dispatchItems = ExtractLoadingSlipItems(text);
            string firstBatch = dispatchItems.Count > 0 ? dispatchItems[0].ClientBatchNo : string.Empty;
            string labeledBatch = FindTextValue(text, "Client\\s*Batch\\s*No|Batch\\s*No|Client\\s*Batch|Product\\s*No|Product\\s*Number");

            return new
            {
                preDispatchCode = FindTextValue(text, "Pre\\s*Dis(?:patch)?\\s*Code|PreDispatch\\s*Code|Dispatch\\s*Code"),
                clientBatchNo = firstBatch.Length > 0 ? firstBatch : labeledBatch,
                productNo = firstBatch.Length > 0 ? firstBatch : FindTextValue(text, "Product\\s*No|Product\\s*Number"),
                sheetSize = FindSheetSize(text),
                memberId = FindTextValue(text, "Member\\s*Id|Member\\s*No|Member"),
                address = FindAddress(text),
                dispatchItems = dispatchItems.Select(item => new
                {
                    preDispatchCode = item.PreDispatchCode,
                    clientBatchNo = item.ClientBatchNo,
                    sheetSize = item.SheetSize
                }).ToArray()
            };
        }

        private static string NormalizeBase64Image(string imageData)
        {
            if (string.IsNullOrWhiteSpace(imageData))
            {
                return string.Empty;
            }

            int commaIndex = imageData.IndexOf(',');
            return commaIndex >= 0 ? imageData.Substring(commaIndex + 1) : imageData;
        }

        private static string ExtractQrPayloadValue(string value)
        {
            Match labeled = Regex.Match(value, @"(?:RFID|Tag\s*RFID|Member\s*(?:Id|No|Code)|QR\s*Code|Code)\s*[:=#-]\s*([A-Za-z0-9_./-]+)", RegexOptions.IgnoreCase);
            if (labeled.Success)
            {
                return labeled.Groups[1].Value;
            }

            Match urlValue = Regex.Match(value, @"(?:rfid|tag|member|code)=([A-Za-z0-9_./-]+)", RegexOptions.IgnoreCase);
            if (urlValue.Success)
            {
                return urlValue.Groups[1].Value;
            }

            Match token = Regex.Match(value, @"[A-Za-z0-9][A-Za-z0-9_./-]*");
            return token.Success ? token.Value : value;
        }

        private static List<PalletDispatcch.DispatchOcrItem> ExtractLoadingSlipItems(string text)
        {
            var items = new List<PalletDispatcch.DispatchOcrItem>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return items;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddBatchHeaderLines(text, items, seen);

            foreach (Match match in Regex.Matches(text, @"(?:Client\s*Batch\s*No|Batch\s*No|Client\s*Batch|Product\s*No|Product\s*Number)\s*[:#-]?\s*([A-Za-z0-9][A-Za-z0-9_./-]*)", RegexOptions.IgnoreCase))
            {
                string batchNo = match.Groups[1].Value.Trim().ToUpperInvariant();
                if (IsHeaderWord(batchNo))
                {
                    continue;
                }

                if (!seen.Add(batchNo))
                {
                    continue;
                }

                items.Add(new PalletDispatcch.DispatchOcrItem
                {
                    PreDispatchCode = "-",
                    ClientBatchNo = batchNo,
                    SheetSize = FindSheetSizeNearBatch(text, match.Index)
                });
            }

            foreach (Match match in Regex.Matches(text, @"\b[0-9]{2}\s*P\s*[0-9]{6}\s*[A-Z]\s*[0-9A-Z]+\b", RegexOptions.IgnoreCase))
            {
                string batchNo = Regex.Replace(match.Value, @"\s+", string.Empty).ToUpperInvariant();
                if (!seen.Add(batchNo))
                {
                    continue;
                }

                items.Add(new PalletDispatcch.DispatchOcrItem
                {
                    PreDispatchCode = "-",
                    ClientBatchNo = batchNo,
                    SheetSize = FindSheetSizeNearBatch(text, match.Index)
                });
            }

            return items;
        }

        private static void AddBatchHeaderLines(string text, List<PalletDispatcch.DispatchOcrItem> items, HashSet<string> seen)
        {
            string[] lines = Regex.Split(text, @"\r?\n");
            bool readBatchLines = false;

            for (int index = 0; index < lines.Length; index += 1)
            {
                string line = Regex.Replace(lines[index] ?? string.Empty, @"\s+", " ").Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Match header = Regex.Match(line, @"^(?:Client\s*Batch\s*No|ClientBatchNo|Batch\s*No|Product\s*No|ProductNo|Product\s*Number)\s*[:#-]?\s*(.*)$", RegexOptions.IgnoreCase);
                if (header.Success)
                {
                    readBatchLines = true;
                    AddBatchToken(header.Groups[1].Value, text, text.IndexOf(lines[index], StringComparison.OrdinalIgnoreCase), items, seen);
                    continue;
                }

                if (!readBatchLines)
                {
                    continue;
                }

                if (IsNonBatchHeader(line))
                {
                    readBatchLines = false;
                    continue;
                }

                AddBatchToken(line, text, text.IndexOf(lines[index], StringComparison.OrdinalIgnoreCase), items, seen);
            }
        }

        private static void AddBatchToken(string value, string fullText, int textIndex, List<PalletDispatcch.DispatchOcrItem> items, HashSet<string> seen)
        {
            foreach (string part in Regex.Split(value ?? string.Empty, @"[|,]+"))
            {
                string batchNo = Regex.Replace(part, @"\s+", string.Empty).Trim().ToUpperInvariant();
                if (!IsBatchToken(batchNo) || !seen.Add(batchNo))
                {
                    continue;
                }

                items.Add(new PalletDispatcch.DispatchOcrItem
                {
                    PreDispatchCode = "-",
                    ClientBatchNo = batchNo,
                    SheetSize = textIndex >= 0 ? FindSheetSizeNearBatch(fullText, textIndex) : string.Empty
                });
            }
        }

        private static bool IsBatchToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || IsHeaderWord(value))
            {
                return false;
            }

            return Regex.IsMatch(value, @"^[A-Z0-9][A-Z0-9_./-]{3,249}$", RegexOptions.IgnoreCase)
                && Regex.IsMatch(value, @"[0-9]", RegexOptions.IgnoreCase);
        }

        private static bool IsNonBatchHeader(string line)
        {
            return Regex.IsMatch(line, @"^(Pre\s*Dis|Dispatch|Sheet|Size|Width|Length|Weight|Thickness|Thikness|Address|Customer|Location|Member|Date)\b", RegexOptions.IgnoreCase);
        }

        private static bool IsHeaderWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return Regex.IsMatch(value, @"^(CLIENT|BATCH|PRODUCT|NUMBER|NO|DATE|SIZE)$", RegexOptions.IgnoreCase);
        }

        private static string FindSheetSizeNearBatch(string text, int batchIndex)
        {
            int start = Math.Max(0, batchIndex - 120);
            int length = Math.Min(text.Length - start, 260);
            string rowText = text.Substring(start, length);
            var widthLength = Regex.Match(rowText, @"\b([0-9]{3,4}(?:\.[0-9]+)?)\s+([0-9]{3,4}(?:\.[0-9]+)?)\b");

            if (widthLength.Success)
            {
                return widthLength.Groups[1].Value + " x " + widthLength.Groups[2].Value;
            }

            return string.Empty;
        }

        private static string FindTextValue(string text, string labelPattern)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = Regex.Match(text, labelPattern + @"\s*[:#-]?\s*([A-Za-z0-9][A-Za-z0-9_./-]*)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return string.Empty;
            }

            string value = match.Groups[1].Value.Trim();
            return IsHeaderWord(value) ? string.Empty : value;
        }

        private static string FindNumberValue(string text, string labelPattern)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = Regex.Match(text, labelPattern + @"\s*[:#-]?\s*([0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static string FindSheetSize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var labeled = Regex.Match(text, @"(?:Sheet\s*Size|Size)\s*[:#-]?\s*([0-9]+(?:\.[0-9]+)?\s*[xX*]\s*[0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
            if (labeled.Success)
            {
                return labeled.Groups[1].Value.Trim();
            }

            var anySize = Regex.Match(text, @"\b([0-9]+(?:\.[0-9]+)?\s*[xX*]\s*[0-9]+(?:\.[0-9]+)?)\b");
            return anySize.Success ? anySize.Groups[1].Value.Trim() : string.Empty;
        }

        private static string FindAddress(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = Regex.Match(text, @"Address\s*[:#-]?\s*(.+?)(?:\r?\n(?:Pre\s*Dis|Client\s*Batch|Sheet\s*Size|Member)|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim() : string.Empty;
        }
    }
}
