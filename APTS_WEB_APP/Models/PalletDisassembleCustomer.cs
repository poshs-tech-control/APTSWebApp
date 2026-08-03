using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class PalletDisassembleCustomer
    {
        public class DispatchDetailItem
        {
            public string PalletDispDTCode { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string PalletCode { get; set; }

            public string Rfid { get; set; }

            public string ClientBatchNo { get; set; }

            public string MemberSize { get; set; }

            public string SheetWidth { get; set; }

            public string SheetLength1 { get; set; }

            public string SheetLength2 { get; set; }

            public string Discrepancy { get; set; }
        }

        public class ValidationResult
        {
            public bool Success { get; set; }

            public string Rfid { get; set; }

            public string PalletCode { get; set; }

            public string Message { get; set; }

            public List<DispatchDetailItem> Members { get; set; }
        }

        public class TempSaveMemberInput
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberSize { get; set; }

            public string PalletCode { get; set; }
        }

        public class TempSaveRequest
        {
            public string CustDismentleCode { get; set; }

            public List<TempSaveMemberInput> Members { get; set; }
        }

        public class TempSaveResult
        {
            public bool Success { get; set; }

            public string Message { get; set; }

            public int TotalRecords { get; set; }
        }

        public class TempDetailItem
        {
            public string CustDismentleCode { get; set; }

            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string CreatedDate { get; set; }
        }

        public class MemberLastUpdateItem
        {
            public string Rfid { get; set; }

            public string LastLocation { get; set; }

            public string LastStatus { get; set; }

            public string LastTransferTo { get; set; }

            public string LastTransactionId { get; set; }
        }

        public class FinalSaveGroupInput
        {
            public string CustDismentleCode { get; set; }

            public string PalletCode { get; set; }

            public List<FinalSaveMemberInput> Members { get; set; }
        }

        public class FinalSaveMemberInput
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string PalletCode { get; set; }

            public string ClientBatchNo { get; set; }

            public string ActionType { get; set; }

            public string Comment { get; set; }
        }

        public class FinalSaveRequest
        {
            public string CustomerCode { get; set; }

            public List<FinalSaveGroupInput> Groups { get; set; }
        }

        public class FinalSaveResult
        {
            public bool Success { get; set; }

            public string CustDismentleCode { get; set; }

            public string Message { get; set; }

            public int TotalRecords { get; set; }
        }
    }
}
