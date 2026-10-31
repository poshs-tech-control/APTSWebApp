namespace APTS_WEB_APP.Models
{
    public class MemberByClientBatchNoReport
    {
        public class Request
        {
            public string ClientBatchNo { get; set; }
        }

        public class ReportRow
        {
            public string RFID { get; set; }
            public string LastEntryBy { get; set; }
            public string LastUpdatedDate { get; set; }
            public string LastTransferTo { get; set; }
            public string LastStatus { get; set; }
        }
    }
}
