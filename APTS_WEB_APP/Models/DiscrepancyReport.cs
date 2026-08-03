using System;
using System.Collections.Generic;

namespace APTS_WEB_APP.Models
{
    public class DiscrepancyReport
    {
        public class Request
        {
            public string DiscrepancyType { get; set; }

            public DateTime FromDate { get; set; }

            public DateTime ToDate { get; set; }
        }

        public class Result
        {
            public int DamageCount { get; set; }

            public int LossCount { get; set; }

            public List<Row> Rows { get; set; }

            public Result()
            {
                Rows = new List<Row>();
            }
        }

        public class Row
        {
            public string RFIDNo { get; set; }

            public string FromLocation { get; set; }

            public string ToLocation { get; set; }

            public string LastEntryBy { get; set; }

            public string ActionType { get; set; }

            public string Date { get; set; }
        }
    }
}
