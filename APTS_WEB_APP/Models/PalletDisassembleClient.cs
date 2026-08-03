using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class PalletDisassembleClient
    {
        public class MemberLastUpdateItem
        {
            public string Rfid { get; set; }

            public string LastStatus { get; set; }

            public string LastTransferTo { get; set; }

            public string LastTransactionId { get; set; }
        }

        public class DisassembleMemberItem
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string BatchNo { get; set; }

            public string PalletCode { get; set; }

            public string SheetWidth { get; set; }

            public string SheetLength1 { get; set; }

            public string SheetLength2 { get; set; }
        }

        public class ValidationResult
        {
            public bool Success { get; set; }

            public string Rfid { get; set; }

            public string PalletCode { get; set; }

            public string Message { get; set; }

            public List<DisassembleMemberItem> Members { get; set; }
        }

        public class DisassembleMemberInput
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string PalletCode { get; set; }
        }

        public class SaveRequest
        {
            public string PalletCode { get; set; }

            public List<DisassembleMemberInput> Members { get; set; }
        }

        public class SaveResult
        {
            public bool Success { get; set; }

            public string DismentleCode { get; set; }

            public int DetailCount { get; set; }

            public string Message { get; set; }
        }
    }
}
