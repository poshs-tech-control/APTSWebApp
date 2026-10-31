using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Mvc;
using APTS_WEB_APP.DataIntraction;
using APTS_WEB_APP.Helpers;
using APTS_WEB_APP.Models;

using iTextSharp.text.pdf.qrcode;
namespace APTS_WEB_APP.Controllers
{
    public class PalletAssembleController : Controller
    {
        private readonly DataAccess dataAccess = new DataAccess();

        public ActionResult PalletAssemble()
        {
            ViewBag.SessionCustomerCode = Convert.ToString(Session["CustomerCode"]);
            ViewBag.SessionLocationCode = Convert.ToString(Session["LocationCode"]);
            ViewBag.SessionCustomerTitle = GetSessionLocationTitle();
            return View();
        }

        [HttpGet]
        public JsonResult GetPalletOrderDetails(string customerCode)
        {
            customerCode = Convert.ToString(Session["CustomerCode"]);
            if (string.IsNullOrWhiteSpace(customerCode))
            {
                return Json(new { success = false, message = "Customer is not found in session." }, JsonRequestBehavior.AllowGet);
            }

            var orders = dataAccess.LoadPalletAssembleOrderDetails(customerCode.Trim());
            return Json(new
            {
                success = true,
                orders = orders,
                message = orders.Count > 0 ? string.Empty : "No pending pallet orders found."
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult ValidateAssembleMember(string tagRfid, string currentLocation)
        {
            string scanValue = QrOcrHelper.NormalizeQrScanValue(tagRfid);
            if (string.IsNullOrWhiteSpace(scanValue))
            {
                return Json(new { success = false, message = "Please scan or enter a member QR code." }, JsonRequestBehavior.AllowGet);
            }

            var result = dataAccess.ValidatePalletAssembleMember(scanValue, currentLocation);
            bool isValid = result != null && result.IsValid;
            return Json(new
            {
                success = isValid,
                message = result == null || string.IsNullOrWhiteSpace(result.Message) ? "Member status not Valid." : result.Message,
                rfid = result == null ? string.Empty : result.Rfid,
                memberCode = result == null ? string.Empty : result.MemberCode,
                memberType = result == null ? string.Empty : result.MemberType,
                memberSize = result == null ? string.Empty : result.MemberSize,
                lastLocation = result == null ? string.Empty : result.LastLocation,
                currentLocation = result == null ? string.Empty : result.CurrentLocation,
                locationMismatch = result != null && result.LocationMismatch,
                requiresBypass = result != null && result.RequiresBypass
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult ContinueAssembleBypass(string rfid, string palletOrderdtlCode, string currentLocation)
        {
            var result = dataAccess.ContinuePalletAssembleBypass(
                rfid,
                palletOrderdtlCode,
                currentLocation,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetCurrentUserFullName(this));

            return Json(new
            {
                success = result != null && result.Success,
                message = result == null || string.IsNullOrWhiteSpace(result.Message) ? "Unable to member bypass." : result.Message
            });
        }
        [HttpGet]
        public ActionResult GeneratePalletQrCode(string palletCode)
        {
            palletCode = Convert.ToString(palletCode).Trim();
            if (string.IsNullOrWhiteSpace(palletCode))
            {
                return new HttpStatusCodeResult(400, "Pallet Code is required.");
            }

            const int imageSize = 220;
            ByteMatrix matrix = new QRCodeWriter().Encode(palletCode, imageSize, imageSize);

            using (var bitmap = new Bitmap(matrix.GetWidth(), matrix.GetHeight()))
            {
                for (int y = 0; y < matrix.GetHeight(); y += 1)
                {
                    for (int x = 0; x < matrix.GetWidth(); x += 1)
                    {
                        bitmap.SetPixel(x, y, matrix.Get(x, y) == 0 ? Color.Black : Color.White);
                    }
                }

                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    return File(stream.ToArray(), "image/png");
                }
            }
        }

        [HttpPost]
        public JsonResult SubmitPalletAssemble(PalletAssemble.AssembleSaveRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Pallet assemble data is required." });
            }

            ApplySessionAssembleLocation(request);

            var result = dataAccess.SavePalletAssemble(
                request,
                dataAccess.GetCurrentUserCode(this),
                dataAccess.GetCurrentUserFullName(this),
                dataAccess.GetIpAddress(this));

            return Json(new
            {
                success = result != null && result.Success,
                palletCode = result == null ? string.Empty : result.PalletCode,
                message = result == null || string.IsNullOrWhiteSpace(result.Message) ? "Unable to save Pallet Assemble." : result.Message
            });
        }

        private void ApplySessionAssembleLocation(PalletAssemble.AssembleSaveRequest request)
        {
            if (request == null)
            {
                return;
            }

            string sessionLocationCode = Convert.ToString(Session["LocationCode"]);
            string sessionLocationTitle = GetSessionLocationTitle();

            if (!string.IsNullOrWhiteSpace(sessionLocationCode))
            {
                request.ClientLocation = sessionLocationCode;
            }

            if (!string.IsNullOrWhiteSpace(sessionLocationTitle))
            {
                request.ToLocation = sessionLocationTitle;
            }
        }

        private string GetSessionLocationTitle()
        {
            string locationTitle = Convert.ToString(Session["FromLocationTitle"]);
            if (string.IsNullOrWhiteSpace(locationTitle))
            {
                locationTitle = Convert.ToString(Session["LocationTitle"]);
            }

            return locationTitle;
        }
    }
}
