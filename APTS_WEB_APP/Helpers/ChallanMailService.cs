using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using APTS_WEB_APP.Models;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;

namespace APTS_WEB_APP.Helpers
{
    public class ChallanMailService
    {
        private readonly HttpServerUtilityBase server;

        public ChallanMailService(HttpServerUtilityBase server)
        {
            this.server = server;
        }

        public ChallanMailResult GenerateMemberDispatchAndSendMail(ChallenBll bll, DataSet dsDetail)
        {
            return GeneratePdfAndSendMail(
                bll,
                dsDetail,
                "InternalTransferMemberDispatch.html",
                bll.ChallenNo + "_DeliveryChallen.pdf",
                "MemberDispatch",
                "Delivery Challan - Member Dispatch - " + bll.Deldate + "-" + bll.ChallenNo,
                "Dear Sir,<br/>Greetings!<br/>Today we have internal transfer of members to you. Details attached.<br/>Thank you and regards,<br/>Ayasto Steelpac Pvt.Ltd.<br/>Website: www.ayasto.com",
                false);
        }

        public ChallanMailResult GeneratePalletDispatchAndSendMail(ChallenBll bll, DataSet dsDetail)
        {
            return GeneratePdfAndSendMail(
                bll,
                dsDetail,
                "PalletDispatchChallan.html",
                bll.ChallenNo + "_PalletDispatch.pdf",
                "PalletDispatch",
                "Delivery Challan - Pallet Dispatch - " + bll.Deldate + "-" + bll.ChallenNo,
                "Dear Sir, <br/>Greetings! <br/>Today we have dispatched the pallets to you. Details attached.. <br/>Thank you and regards,<br/>Ayasto Steelpac Pvt.Ltd.,<br/>Website: www.ayasto.com",
                false);
        }

        public ChallanMailResult GenerateMemberCollectionAndSendMail(ChallenBll bll, DataSet dsDetail)
        {
            return GeneratePdfAndSendMail(
                bll,
                dsDetail,
                "DCMemberCollection.html",
                bll.ChallenNo + "_DeliveryChallanCollection.pdf",
                "MemberCollection",
                "Delivery Challan - Member Collection - " + bll.Deldate + "-" + bll.ChallenNo,
                "Dear Sir, <br/>Greetings! <br/>Today we have collected the members. Details attached.<br/>Thank you and regards,<br/>Ayasto Steelpac Pvt.Ltd.,<br/>Website: www.ayasto.com",
                false);
        }

        public ChallanMailResult GenerateDismentleAndSendMail(ChallenBll bll, DataSet dsDetail)
        {
            bool hasDiscrepancy = HasRows(dsDetail, 2);
            return GeneratePdfAndSendMail(
                bll,
                dsDetail,
                "PalletDismentleDetails.html",
                bll.ChallenNo + "_DeliveryChallen.pdf",
                "MemberDispatch",
                "Delivery Challan - Pallet Dismentle - " + bll.Deldate + "-" + bll.ChallenNo,
                "Dear Sir, <br/>Greetings! <br/>Today we have dismentle pallet. Details attached.. <br/>Thank you and regards,<br/>Ayasto Steelpac Pvt.Ltd.,<br/>Website: www.ayasto.com",
                hasDiscrepancy);
        }

        public ChallanMailResult GeneratePalletOrderAndSendMail(ChallenBll bll, DataSet dsDetail)
        {
            return GeneratePdfAndSendMail(
                bll,
                dsDetail,
                "PalletOrderChallan.html",
                bll.PalletOrderCode + "_PalletOrderChallan.pdf",
                "MemberDispatch",
                "Pallet Order Challan - " + bll.OrderDate + "-" + bll.PalletOrderCode,
                "Dear Sir, <br/>Greetings! <br/>We have received the following orders for today. <br/>Thank you and regards,<br/>Ayasto Steelpac Pvt.Ltd.,<br/>Website: www.ayasto.com",
                false);
        }

        public string GENERATELETTER(string templateName, string fileName, ChallenBll docBll, DataSet dsDetail)
        {
            if (string.IsNullOrWhiteSpace(templateName) || !File.Exists(templateName))
            {
                throw new FileNotFoundException("Challan template not found.", templateName);
            }

            string output = File.ReadAllText(templateName);
            output = ReplaceHeaderPlaceholders(output, docBll);
            output = ReplaceDetailRows(output, templateName, dsDetail);

            string htmlPath = server.MapPath("~/Docs/" + docBll.FilePath + "/" + Path.GetFileNameWithoutExtension(fileName) + ".html");
            Directory.CreateDirectory(Path.GetDirectoryName(htmlPath));
            File.WriteAllText(htmlPath, output);

            return output;
        }

        public void SendHTMLFormatedMultiMail(string toAddress, string toUserMailId, string subject, string body, byte[] bytes, string attachedFilePath)
        {
            SendMail(BuildStandardRecipients(toAddress, toUserMailId), subject, body, bytes, attachedFilePath);
        }

        public void SendHTMLFormatedMultiMailDescripancy(string toAddress, string toUserMailId, string subject, string body, byte[] bytes, string attachedFilePath)
        {
            SendMail(BuildDiscrepancyRecipients(toAddress, toUserMailId), subject, body, bytes, attachedFilePath);
        }

        private ChallanMailResult GeneratePdfAndSendMail(
            ChallenBll bll,
            DataSet dsDetail,
            string templateFile,
            string fileName,
            string filePath,
            string subject,
            string body,
            bool discrepancyMail)
        {
            bll.FilePath = filePath;
            string templatePath = server.MapPath("~/ChallanTemplate/" + templateFile);
            string content = GENERATELETTER(templatePath, fileName, bll, dsDetail);

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("Generated content is empty. Cannot create PDF.");
            }

            string logoPath = server.MapPath("~/ChallanTemplate/logoimg.png");
            content = content.Replace("{LogoPath}", logoPath);
            byte[] bytes = CreatePdf(content);

            string pdfPath = server.MapPath("~/Docs/" + bll.FilePath + "/" + fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath));
            File.WriteAllBytes(pdfPath, bytes);

            string relativePath = "~/Docs/" + bll.FilePath + "/" + fileName;

            try
            {
                if (discrepancyMail)
                {
                    SendHTMLFormatedMultiMailDescripancy(bll.EmailId, bll.UserEmailId, subject, body, bytes, fileName);
                }
                else
                {
                    SendHTMLFormatedMultiMail(bll.EmailId, bll.UserEmailId, subject, body, bytes, fileName);
                }

                return new ChallanMailResult
                {
                    Success = true,
                    Message = "Challan generated and mail sent.",
                    FileName = fileName,
                    RelativePath = relativePath
                };
            }
            catch (SmtpException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return new ChallanMailResult
                {
                    Success = false,
                    Message = "Challan generated but mail not sent. SMTP send failed (" + ex.StatusCode + "): " + ex.Message + " Check SMTP service/relay, host, port, SSL and credentials.",
                    FileName = fileName,
                    RelativePath = relativePath
                };
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return new ChallanMailResult
                {
                    Success = false,
                    Message = "Challan generated but mail not sent. " + ex.Message,
                    FileName = fileName,
                    RelativePath = relativePath
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return new ChallanMailResult
                {
                    Success = false,
                    Message = "Challan generated but mail not sent. SMTP configuration issue: " + ex.Message,
                    FileName = fileName,
                    RelativePath = relativePath
                };
            }
        }

        private static byte[] CreatePdf(string content)
        {
            using (var sr = new StringReader(content))
            using (var memoryStream = new MemoryStream())
            {
                Document pdfDoc = new Document(PageSize.A4, 10f, 10f, 10f, 0f);
                PdfWriter.GetInstance(pdfDoc, memoryStream);
                pdfDoc.Open();
                new HTMLWorker(pdfDoc).Parse(sr);
                if (pdfDoc.PageNumber == 0)
                {
                    pdfDoc.Add(new Paragraph(" "));
                }
                pdfDoc.Close();
                return memoryStream.ToArray();
            }
        }

        private static string ReplaceHeaderPlaceholders(string content, ChallenBll bll)
        {
            var values = new Dictionary<string, string>
            {
                { "{cust_name}", bll.CustomerName },
                { "{Address}", bll.DelAddress },
                { "{FromLocationType}", bll.FromLocationType },
                { "{DelAdd}", bll.DelAddress },
                { "{ToLocation}", bll.ToLocation },
                { "{ChallanNo}", bll.ChallenNo },
                { "{ToAddress}", bll.ToAddress },
                { "{ToLocationType}", bll.ToLocationType },
                { "{ChallanDate}", bll.Deldate },
                { "{DriverName}", bll.DriverName },
                { "{TruckNo}", bll.TruckNo },
                { "{FromAddress}", bll.FromAddress },
                { "{FromLocation}", bll.FromLocation },
                { "{TransporterName}", bll.TransporterName },
                { "{CustsomerInToLoc}", bll.CustsomerInToLoc },
                { "{CustsomerInFromLoc}", bll.CustsomerInFromLoc },
                { "{VehicleInToloc}", bll.VehicleInToloc },
                { "{VehicleInFromloc}", bll.VehicleInFromLoc },
                { "{TruckNumber}", bll.TruckNumber },
                { "{TotalQty}", bll.TotalQty },
                { "{TotalDiscQty}", bll.TotalDiscQty },
                { "{PalletOrderCode}", bll.PalletOrderCode },
                { "{OrderDate}", bll.OrderDate }
            };

            foreach (KeyValuePair<string, string> value in values)
            {
                content = content.Replace(value.Key, Html(value.Value));
            }

            return content;
        }

        private static string ReplaceDetailRows(string output, string templateName, DataSet dsDetail)
        {
            if (templateName.IndexOf("PalletDispatchChallan", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                output = output.Replace("{Content}", BuildRows(Table(dsDetail, 1),
                    row => "<tr><td colspan='2' style='border: 3px solid black; width: 10%; text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='7' style='border: 3px solid black;width:25%; text-align: center; vertical-align: middle;'>" + Col(row.Row, "ClientBatchNo") + "</td><td colspan='7' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "SheetCoilSize") + "</td><td colspan='7' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberSize") + "</td><td colspan='7' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberQty") + "</td></tr>"));
            }
            else if (templateName.IndexOf("PalletDismentleDetails", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                output = output.Replace("{Content}", BuildRows(Table(dsDetail, 1),
                    row => "<tr><td colspan='1' style='border: 3px solid black;width:10%;text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "ClientBatchNo") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberSize") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberQty") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "DispatchShipToCode") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "DispatchedShipToName") + "</td></tr>"));
                output = output.Replace("{Content1}", BuildRows(Table(dsDetail, 2),
                    row => "<tr><td colspan='1' style='border: 3px solid black;width:10%;text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "ClientBatchNo") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberSize") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberQty") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "ActionType") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "DispatchedShipToName") + "</td></tr>"));
            }
            else if (templateName.IndexOf("DCMemberCollection", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                output = output.Replace("{Content}", BuildRows(Table(dsDetail, 1),
                    row => "<tr><td colspan='1' style='border: 3px solid black;width:10%;text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "ClientBatchNo") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberSize") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberQty") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "DispatchShipToCode") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "DispatchedShipToName") + "</td></tr>"));
                output = output.Replace("{Content1}", BuildRows(Table(dsDetail, 2),
                    row => "<tr><td colspan='1' style='border: 3px solid black;width:10%;text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "MemberSKU") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "Qty") + "</td></tr>"));
                output = output.Replace("{Content2}", BuildRows(Table(dsDetail, 3),
                    row => "<tr><td colspan='1' style='border: 3px solid black;width:10%;text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "ClientBatchNo") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberSize") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "PalletMemberQty") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "ActionType") + "</td><td colspan='2' style='border: 3px solid black;width:25%;text-align: center; vertical-align: middle;'>" + Col(row.Row, "DispatchedShipToName") + "</td></tr>"));
            }
            else if (templateName.IndexOf("PalletOrderChallan", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                output = output.Replace("{Content}", BuildRows(Table(dsDetail, 1),
                    row => "<tr><td colspan='1' style='border: 3px solid black; width:10%; text-align:center; vertical-align:middle;'>" + row.SerialNo + "</td><td colspan='7' style='border: 3px solid black; width:25%; text-align:center; vertical-align:middle;'>" + Col(row.Row, "SheetWidth") + "</td><td colspan='7' style='border: 3px solid black; width:25%; text-align:center; vertical-align:middle;'>" + Col(row.Row, "SheetLength1") + "</td><td colspan='7' style='border: 3px solid black; width:25%; text-align:center; vertical-align:middle;'>" + Col(row.Row, "SheetLength2") + "</td><td colspan='2' style='border: 3px solid black; width:25%; text-align:center; vertical-align:middle;'>" + Col(row.Row, "Counts") + "</td></tr>"));
            }
            else
            {
                DataTable detailTable = HasRows(dsDetail, 1) ? Table(dsDetail, 1) : Table(dsDetail, 0);
                output = output.Replace("{Content}", BuildRows(detailTable,
                    row => "<tr> <td colspan='2' style='border: 3px solid black;text-align: center; vertical-align: middle;'>" + row.SerialNo + "</td><td colspan='7' style='border: 3px solid black;width:50%;text-align: center; vertical-align: middle;'>Member (" + Col(row.Row, "MemberType") + " - " + Col(row.Row, "MemberWeight") + " )</td><td colspan='2' style='border: 3px solid black;text-align: center; vertical-align: middle;'>" + Col(row.Row, "TOTAL") + "</td></tr>"));
            }

            return output
                .Replace("{Content}", string.Empty)
                .Replace("{Content1}", string.Empty)
                .Replace("{Content2}", string.Empty)
                .Replace(" , ", string.Empty);
        }

        private static string BuildRows(DataTable table, Func<RowContext, string> buildRow)
        {
            if (table == null)
            {
                return string.Empty;
            }

            var rows = new StringBuilder();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                rows.AppendLine(buildRow(new RowContext { Row = table.Rows[i], SerialNo = (i + 1).ToString() }));
            }

            return rows.ToString();
        }

        private static DataTable Table(DataSet dataSet, int index)
        {
            return dataSet != null && dataSet.Tables.Count > index ? dataSet.Tables[index] : null;
        }

        private static bool HasRows(DataSet dataSet, int index)
        {
            DataTable table = Table(dataSet, index);
            return table != null && table.Rows.Count > 0;
        }

        private static string Col(DataRow row, string columnName)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            return Html(Convert.ToString(row[columnName]));
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private void SendMail(IEnumerable<string> recipients, string subject, string body, byte[] bytes, string attachedFilePath)
        {
            string userName = SecretSettingReader.Read("APTS_SMTP_USERNAME", "UserName");
            string password = SecretSettingReader.Read("APTS_SMTP_PASSWORD", "Password");
            string host = SecretSettingReader.Read("APTS_SMTP_HOST", "Host");
            string port = SecretSettingReader.Read("APTS_SMTP_PORT", "Port");
            string enableSsl = SecretSettingReader.Read("APTS_SMTP_ENABLE_SSL", "EnableSsl");

            //string userName = "techcontrol@poshsmetal.com";
            //string password = "twcU2hozoRRz8tSw";
            //string host = "email.poshsmetal.com";
            //string port = "587";
            //string enableSsl = "True";

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) || string.IsNullOrWhiteSpace(enableSsl))
            {
                throw new ConfigurationErrorsException("SMTP appSettings UserName, Password, Host, Port and EnableSsl are required.");
            }

            int smtpPort;
            if (!int.TryParse(port, out smtpPort))
            {
                throw new ConfigurationErrorsException("SMTP Port must be a valid number.");
            }

            bool smtpEnableSsl;
            if (!bool.TryParse(enableSsl, out smtpEnableSsl))
            {
                throw new ConfigurationErrorsException("SMTP EnableSsl must be true or false.");
            }

            List<string> cleanRecipients = recipients.Where(IsValidMailAddress).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!cleanRecipients.Any())
            {
                throw new InvalidOperationException("No valid mail recipient found.");
            }

            using (var mailMessage = new MailMessage())
            {
                mailMessage.From = new MailAddress(userName, "Ayasto Steelpac Private Limited");
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                foreach (string recipient in cleanRecipients)
                {
                    mailMessage.To.Add(recipient);
                }

                if (bytes != null && bytes.Length > 0)
                {
                    mailMessage.Attachments.Add(new Attachment(new MemoryStream(bytes), attachedFilePath));
                }

                using (var smtp = new SmtpClient(host, smtpPort))
                {
                    try {
                        smtp.EnableSsl = smtpEnableSsl;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(userName, password);
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                        smtp.Timeout = 30000;
                        smtp.Send(mailMessage);
                    
                    } catch {
                    
                    }
                    
                }
            }
        }

        private static IEnumerable<string> BuildStandardRecipients(string toAddress, string toUserMailId)
        {
            return SplitEmails("pallet.return@ayasto.com,dev.effocess@gmail.com," + toAddress + "," + toUserMailId);
            //return SplitEmails("dev.effocess@gmail.com");
        }

        private static IEnumerable<string> BuildDiscrepancyRecipients(string toAddress, string toUserMailId)
        {
            return SplitEmails("pallet.return@ayasto.com,executivelogistic@ayasto.com,dev.effocess@gmail.com," + toAddress + "," + toUserMailId);
            //return SplitEmails("dev.effocess@gmail.com");
        }

        private static IEnumerable<string> SplitEmails(string emails)
        {
            return (emails ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(email => email.Trim())
                .Where(email => !string.IsNullOrWhiteSpace(email));
        }

        private static bool IsValidMailAddress(string email)
        {
            try
            {
                new MailAddress(email);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private class RowContext
        {
            public DataRow Row { get; set; }
            public string SerialNo { get; set; }
        }
    }
}
