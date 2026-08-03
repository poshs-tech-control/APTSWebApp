using System;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

namespace APTS_WEB_APP.Controllers
{
    public class MemberMasterController : Controller
    {
        private const string ExistingWithQrMode = "existing-with-qr";
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult MemberMaster()
        {
            ViewBag.SkuList = dataAccess.LoadSkuList();
            return View();
        }

        [HttpGet]
        public JsonResult GetSkewDetails(string skewCode)
        {
            if (HtmlInjectionGuard.ContainsHtmlInjection(skewCode))
            {
                return Json(new { success = false, message = "HTML markup is not allowed in SKU code." }, JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrWhiteSpace(skewCode))
            {
                return Json(new { success = false, message = "SKU code is required." }, JsonRequestBehavior.AllowGet);
            }

            SkewDetailsItem details = dataAccess.LoadSkewDetails(skewCode);
            if (details == null)
            {
                return Json(new { success = false, message = "SKU details not found." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                weight = details.Weight,
                memberType = details.MemberType,
                memberSize = details.MemberSize
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult ValidateNewBatchNo(string batchNo)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(batchNo);
            if (HtmlInjectionGuard.ContainsHtmlInjection(batchNo) || HtmlInjectionGuard.ContainsHtmlInjection(scanValue))
            {
                return Json(new { success = true, isValid = false, message = "HTML markup is not allowed in New Batch No." }, JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, isValid = false, message = "Please scan New Batch No." }, JsonRequestBehavior.AllowGet);
            }

            string validationMessage;
            if (!ValidateNewBatchNoValue(scanValue, out validationMessage))
            {
                return Json(new { success = true, isValid = false, message = validationMessage }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, isValid = true, batchNo = scanValue, message = "New Batch No available." }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult ShowExistingQr(string qrCode)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(qrCode);
            if (HtmlInjectionGuard.ContainsHtmlInjection(qrCode) || HtmlInjectionGuard.ContainsHtmlInjection(scanValue))
            {
                return Json(new { success = false, message = "HTML markup is not allowed in QR Code." }, JsonRequestBehavior.AllowGet);
            }

            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan QR Code." }, JsonRequestBehavior.AllowGet);
            }

            string originalRfid = QrOcrHelper.NormalizeQrScanValue(dataAccess.LoadOriginalRfidByTagRfid(scanValue));
            if (string.IsNullOrWhiteSpace(originalRfid))
            {
                return Json(new { success = false, message = "Existing RFID not found for scanned QR." }, JsonRequestBehavior.AllowGet);
            }

            DataAccess.ExistingMemberForBatchMapping member = dataAccess.LoadExistingMemberForBatchMapping(originalRfid);
            if (member == null)
            {
                return Json(new { success = false, message = "Existing Batch No not found in Member Master." }, JsonRequestBehavior.AllowGet);
            }

            SkewDetailsItem details = dataAccess.LoadSkewDetails(member.SkuCode);
            return Json(new
            {
                success = true,
                existingRfid = member.Rfid,
                memberCode = member.MemberCode,
                sku = member.SkuCode,
                weight = details == null ? null : details.Weight,
                memberType = details == null ? string.Empty : details.MemberType,
                memberSize = details == null ? string.Empty : details.MemberSize
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> ReadBatchText()
        {
            OcrReadResult result = await QrOcrHelper.ReadGoogleVisionTextAsync(Request.InputStream, GetGoogleVisionApiKey());
            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message, details = result.Details });
            }

            string batchNo = ExtractBatchNoFromText(result.Text);
            return Json(new
            {
                success = true,
                text = result.Text,
                batchNo = batchNo
            });
        }

        [HttpPost]
        public JsonResult SaveMemberMaster(MemberMasterSaveRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Member details are required." });
            }

            request.MemberMode = string.IsNullOrWhiteSpace(request.MemberMode) ? string.Empty : request.MemberMode.Trim();
            if (string.IsNullOrWhiteSpace(request.MemberMode))
            {
                return Json(new { success = false, message = "Please select Member Type." });
            }

            string htmlValidationMessage;
            if (!ValidateMemberMasterPlainText(request, out htmlValidationMessage))
            {
                return Json(new { success = false, message = htmlValidationMessage });
            }

            request.NewBatchNo = QrOcrHelper.NormalizeQrScanValue(request.NewBatchNo);
            string validationMessage;
            if (!ValidateNewBatchNoValue(request.NewBatchNo, out validationMessage))
            {
                return Json(new { success = false, message = validationMessage });
            }

            string userCode = dataAccess.GetCurrentUserCode(this, "SYSTEM");
            string userFullName = dataAccess.GetCurrentUserFullName(this);
            string ipAddress = dataAccess.GetIpAddress(this);
            bool reserved = false;

            try
            {
                string reserveMessage;
                if (!dataAccess.TryMarkNewBatchNumberUsed(request.NewBatchNo, userCode, ipAddress, out reserveMessage))
                {
                    return Json(new { success = false, message = string.IsNullOrWhiteSpace(reserveMessage) ? "New Batch No already used." : reserveMessage });
                }

                reserved = true;

                if (string.Equals(request.MemberMode, ExistingWithQrMode, StringComparison.OrdinalIgnoreCase))
                {
                    JsonResult existingResult = SaveExistingQrMember(request, userCode, ipAddress, userFullName);
                    reserved = false;
                    return existingResult;
                }

                if (string.IsNullOrWhiteSpace(request.Sku))
                {
                    throw new InvalidOperationException("Please select SKU.");
                }

                if (!PopulateSkuDetails(request))
                {
                    throw new InvalidOperationException("SKU details not found.");
                }

                request.OldBatchNo = request.NewBatchNo;
                request.Remarks = "Batch No Mapping";
                DataAccess.MemberSaveResult result = dataAccess.SaveMember(request, userCode, userFullName, ipAddress);
                if (result == null || string.IsNullOrWhiteSpace(result.MemberCode))
                {
                    throw new InvalidOperationException(result == null || string.IsNullOrWhiteSpace(result.Message) ? "Unable to save member." : result.Message);
                }

                reserved = false;
                return Json(new
                {
                    success = true,
                    memberCode = result.MemberCode,
                    message = string.IsNullOrWhiteSpace(result.Message) ? "Member Created Successfully" : result.Message
                });
            }
            catch (Exception ex)
            {
                if (reserved)
                {
                    dataAccess.ReleaseNewBatchNumber(request.NewBatchNo, userCode, ipAddress);
                }

                return Json(new { success = false, message = ex.Message });
            }
        }

        private JsonResult SaveExistingQrMember(MemberMasterSaveRequest request, string userCode, string ipAddress, string userFullName)
        {
            request.QrCode = QrOcrHelper.NormalizeQrScanValue(request.QrCode);
            if (string.IsNullOrWhiteSpace(request.QrCode))
            {
                throw new InvalidOperationException("Please scan or enter QR Code.");
            }

            string originalRfid = QrOcrHelper.NormalizeQrScanValue(dataAccess.LoadOriginalRfidByTagRfid(request.QrCode));
            if (string.IsNullOrWhiteSpace(originalRfid))
            {
                request.ExistingRfid = string.Empty;
                request.ExistingMemberCode = string.Empty;
                throw new InvalidOperationException("Existing RFID not found for scanned QR.");
            }

            if (!dataAccess.CheckMemberExists(originalRfid))
            {
                request.ExistingRfid = string.Empty;
                request.ExistingMemberCode = string.Empty;
                throw new InvalidOperationException("Existing Batch No not found in Member Master.");
            }

            DataAccess.ExistingMemberForBatchMapping member = dataAccess.LoadExistingMemberForBatchMapping(originalRfid);
            if (member == null)
            {
                throw new InvalidOperationException("Member not found.");
            }

            request.ExistingRfid = originalRfid;
            request.ExistingMemberCode = string.IsNullOrWhiteSpace(request.ExistingMemberCode) ? member.MemberCode : request.ExistingMemberCode;
            if (string.IsNullOrWhiteSpace(request.ExistingMemberCode))
            {
                request.ExistingMemberCode = member.MemberCode;
            }

            if (string.IsNullOrWhiteSpace(request.ExistingMemberCode))
            {
                throw new InvalidOperationException("Member not found.");
            }

            request.Sku = member.SkuCode;
            if (!PopulateSkuDetails(request))
            {
                throw new InvalidOperationException("SKU details not found.");
            }

            request.Remarks = "Batch No Mapping";
            DataAccess.MemberSaveResult result = dataAccess.SaveExistingMemberForBatchMapping(request, userCode, userFullName, ipAddress);
            string message = result == null || string.IsNullOrWhiteSpace(result.Message) ? "Member Updated Successfully" : result.Message;
            bool isSuccess = result != null
                && (!string.IsNullOrWhiteSpace(result.MemberCode)
                    || message.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(message, "Member Updated Successfully", StringComparison.OrdinalIgnoreCase));

            if (!isSuccess)
            {
                throw new InvalidOperationException(message);
            }

            string taggingMessage;
            if (!dataAccess.UpdateMemberTaggingOriginalRfid(originalRfid, request.NewBatchNo, out taggingMessage))
            {
                return Json(new { success = false, memberCode = result.MemberCode, message = string.IsNullOrWhiteSpace(taggingMessage) ? "Member tagging details not found." : taggingMessage });
            }

            return Json(new { success = true, memberCode = result.MemberCode, message = message });
        }
        private bool PopulateSkuDetails(MemberMasterSaveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Sku))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.MemberTypeCode)
                && !string.IsNullOrWhiteSpace(request.MemberWeight)
                && !string.IsNullOrWhiteSpace(request.SkuMemberSize))
            {
                return true;
            }

            SkewDetailsItem details = dataAccess.LoadSkewDetails(request.Sku);
            if (details == null)
            {
                return false;
            }

            request.MemberTypeCode = string.IsNullOrWhiteSpace(request.MemberTypeCode) ? details.MemberType : request.MemberTypeCode;
            request.MemberWeight = string.IsNullOrWhiteSpace(request.MemberWeight) ? Convert.ToString(details.Weight) : request.MemberWeight;
            request.SkuMemberSize = string.IsNullOrWhiteSpace(request.SkuMemberSize) ? details.MemberSize : request.SkuMemberSize;
            return !string.IsNullOrWhiteSpace(request.MemberTypeCode)
                && !string.IsNullOrWhiteSpace(request.MemberWeight)
                && !string.IsNullOrWhiteSpace(request.SkuMemberSize);
        }


        private static bool ValidateMemberMasterPlainText(MemberMasterSaveRequest request, out string message)
        {
            message = string.Empty;
            if (request == null)
            {
                message = "Member details are required.";
                return false;
            }

            if (!ValidateField("Member Type", request.MemberMode, out message)
                || !ValidateField("QR Code", request.QrCode, out message)
                || !ValidateField("Existing RFID", request.ExistingRfid, out message)
                || !ValidateField("Existing Member Code", request.ExistingMemberCode, out message)
                || !ValidateField("SKU", request.Sku, out message)
                || !ValidateField("Old Batch No", request.OldBatchNo, out message)
                || !ValidateField("New Batch No", request.NewBatchNo, out message)
                || !ValidateField("Member Type Code", request.MemberTypeCode, out message)
                || !ValidateField("Member Weight", request.MemberWeight, out message)
                || !ValidateField("Member Size", request.SkuMemberSize, out message)
                || !ValidateField("Production Location", request.ProductionLocation, out message)
                || !ValidateField("Remarks", request.Remarks, out message))
            {
                return false;
            }

            request.MemberMode = HtmlInjectionGuard.TrimPlainText(request.MemberMode);
            request.QrCode = HtmlInjectionGuard.TrimPlainText(request.QrCode);
            request.ExistingRfid = HtmlInjectionGuard.TrimPlainText(request.ExistingRfid);
            request.ExistingMemberCode = HtmlInjectionGuard.TrimPlainText(request.ExistingMemberCode);
            request.Sku = HtmlInjectionGuard.TrimPlainText(request.Sku);
            request.OldBatchNo = HtmlInjectionGuard.TrimPlainText(request.OldBatchNo);
            request.NewBatchNo = HtmlInjectionGuard.TrimPlainText(request.NewBatchNo);
            request.MemberTypeCode = HtmlInjectionGuard.TrimPlainText(request.MemberTypeCode);
            request.MemberWeight = HtmlInjectionGuard.TrimPlainText(request.MemberWeight);
            request.SkuMemberSize = HtmlInjectionGuard.TrimPlainText(request.SkuMemberSize);
            request.ProductionLocation = HtmlInjectionGuard.TrimPlainText(request.ProductionLocation);
            request.Remarks = HtmlInjectionGuard.TrimPlainText(request.Remarks);
            return true;
        }

        private static bool ValidateField(string fieldName, string value, out string message)
        {
            message = string.Empty;
            if (HtmlInjectionGuard.ContainsHtmlInjection(value))
            {
                message = "HTML markup is not allowed in " + fieldName + ".";
                return false;
            }

            return true;
        }
        private bool ValidateNewBatchNoValue(string batchNo, out string message)
        {
            message = string.Empty;
            if (HtmlInjectionGuard.ContainsHtmlInjection(batchNo))
            {
                message = "HTML markup is not allowed in New Batch No.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(batchNo))
            {
                message = "Please scan New Batch No.";
                return false;
            }

            DataAccess.NewBatchNumberStatus status = dataAccess.GetNewBatchNumberStatus(batchNo);
            if (!status.TableAvailable)
            {
                message = string.IsNullOrWhiteSpace(status.Message) ? "New batch number master table not found." : status.Message;
                return false;
            }

            if (!status.Exists)
            {
                message = string.IsNullOrWhiteSpace(status.Message) ? "New Batch No not uploaded." : status.Message;
                return false;
            }

            if (!status.Active)
            {
                message = string.IsNullOrWhiteSpace(status.Message) ? "New Batch No is not active." : status.Message;
                return false;
            }

            if (status.Used)
            {
                message = string.IsNullOrWhiteSpace(status.Message) ? "New Batch No already used." : status.Message;
                return false;
            }

            if (dataAccess.CheckMemberExists(batchNo))
            {
                message = "New Batch No Already Exist.";
                return false;
            }

            return true;
        }


        private static string GetGoogleVisionApiKey()
        {
            return SecretSettingReader.Read("APTS_GOOGLE_VISION_API_KEY", "GoogleVisionApiKey");
        }

        private static string ExtractBatchNoFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            Match labeled = Regex.Match(text, @"(?:Batch\s*No|New\s*Batch\s*No|RFID|Code)\s*[:=#-]?\s*([A-Za-z0-9][A-Za-z0-9_./-]{2,})", RegexOptions.IgnoreCase);
            if (labeled.Success)
            {
                return QrOcrHelper.NormalizeQrScanValue(labeled.Groups[1].Value);
            }

            Match token = Regex.Match(text, @"[A-Za-z0-9][A-Za-z0-9_./-]{2,}", RegexOptions.IgnoreCase);
            return token.Success ? QrOcrHelper.NormalizeQrScanValue(token.Value) : string.Empty;
        }
    }
}


