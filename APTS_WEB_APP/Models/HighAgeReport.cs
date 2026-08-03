using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class HighAgeReport
    {
        public string HighAgeType { get; set; }

        public string LocationCode { get; set; }

        public List<DropdownItem> LocationList { get; set; }

        public HighAgeReport()
        {
            HighAgeType = "Client";
            LocationList = new List<DropdownItem>();
        }

        public class DropdownItem
        {
            public string Value { get; set; }

            public string Text { get; set; }
        }

        public class ReportRequest
        {
            public string HighAgeType { get; set; }

            public string LocationCode { get; set; }

            public string TotalDays { get; set; }
        }

        public class ReportResult
        {
            public int Count { get; set; }

            public List<ReportRow> Rows { get; set; }

            public ReportResult()
            {
                Rows = new List<ReportRow>();
            }
        }

        public class ReportRow
        {
            public string LocationTitle { get; set; }

            public string PreDispatchDate { get; set; }

            public string TotalMember { get; set; }

            public string ClientBatchNo { get; set; }

            public string FromLocation { get; set; }

            public string ToLocation { get; set; }

            public string DispatchDate { get; set; }

            public string Members { get; set; }

            public string Status { get; set; }

            public string Ageing { get; set; }
        }
    }
}
