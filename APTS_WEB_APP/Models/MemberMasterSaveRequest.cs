namespace APTS_WEB_APP.Models
{
    public class MemberMasterSaveRequest
    {
        public string MemberMode { get; set; }

        public string QrCode { get; set; }

        public string ExistingRfid { get; set; }

        public string ExistingMemberCode { get; set; }

        public string Sku { get; set; }

        public string OldBatchNo { get; set; }

        public string NewBatchNo { get; set; }

        public string MemberTypeCode { get; set; }

        public string MemberWeight { get; set; }

        public string SkuMemberSize { get; set; }

        public string ProductionLocation { get; set; }

        public string ProductionLocationTitle { get; set; }

        public string Remarks { get; set; }
    }
}