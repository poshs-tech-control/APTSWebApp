namespace APTS_WEB_APP.Models
{
    public class QueryReport
    {
        public class Request
        {
            public string Rfid { get; set; }
        }

        public class Result
        {
            public string RFID { get; set; }

            public string AllTagRFIDs { get; set; }

            public string FromLocation { get; set; }

            public string ToLocation { get; set; }

            public string MemberType { get; set; }

            public string MemberSize { get; set; }

            public string LastEntryBy { get; set; }

            public string LastUpdatedDate { get; set; }

            public string LastStatus { get; set; }

            public string ClientBatchNo { get; set; }
        }
    }
}
