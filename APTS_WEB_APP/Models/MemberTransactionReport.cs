using System;
using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class MemberTransactionReport
    {
        public string LocationCode { get; set; }

        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public class ReportRequest
        {
            public string LocationCode { get; set; }

            public DateTime FromDate { get; set; }

            public DateTime ToDate { get; set; }
        }

        public class ReportRow
        {
            public string FromLocation { get; set; }

            public string ToLocation { get; set; }

            public string Status { get; set; }

            public int TotalMember { get; set; }
        }
    }
}
