namespace APTS_WEB_APP.Models
{
    using System.Collections.Generic;

    public class PalletAssemble
    {
        public class CustomerListItem
        {
            public string CustomerCode { get; set; }

            public string CustName { get; set; }

            public string CustType { get; set; }

            public string LocationCode { get; set; }
        }

        public class PalletOrderDetailItem
        {
            public string PalletOrderCode { get; set; }

            public string PalletOrderdtlCode { get; set; }

            public string OrderCounts { get; set; }

            public string AssembleCounts { get; set; }

            public string Customer { get; set; }

            public string PalletShape { get; set; }

            public string SheetWidth { get; set; }

            public string SheetLength1 { get; set; }

            public string SheetLength2 { get; set; }

            public string CMSize { get; set; }

            public string LM1Size { get; set; }

            public string LM2Size { get; set; }

            public string CMSizeQty { get; set; }

            public string LM1SizeQty { get; set; }

            public string LM2SizeQty { get; set; }

            public string TotalMemQty { get; set; }

            public string PendingCount { get; set; }
        }

        public class AssembleMemberValidationResult
        {
            public bool IsValid { get; set; }

            public string Message { get; set; }

            public string Rfid { get; set; }

            public string MemberCode { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string LastLocation { get; set; }

            public string CurrentLocation { get; set; }

            public bool LocationMismatch { get; set; }

            public bool RequiresBypass { get; set; }
        }

        public class AssembleBypassResult
        {
            public bool Success { get; set; }

            public string Message { get; set; }
        }

        public class AssembleSaveRequest
        {
            public string ClientLocation { get; set; }

            public string ToLocation { get; set; }

            public string PalletOrderCode { get; set; }

            public string PalletOrderdtlCode { get; set; }

            public string SheetWidth { get; set; }

            public string SheetLength1 { get; set; }

            public string SheetLength2 { get; set; }

            public string TotalMember { get; set; }

            public List<AssembleSaveMemberInput> Members { get; set; }
        }

        public class AssembleSaveMemberInput
        {
            public string Rfid { get; set; }

            public string MemberCode { get; set; }
        }

        public class AssembleSaveResult
        {
            public bool Success { get; set; }

            public string Message { get; set; }

            public string PalletCode { get; set; }
        }
    }
}
