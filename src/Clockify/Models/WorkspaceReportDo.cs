using System.Collections.Generic;

namespace Bot.Clockify.Models
{
    public class WorkspaceReportDo
    {
        public List<WorkspaceReportTotal> totals { get; set; }
        public List<WorkspaceReportGroupOne> groupOne { get; set; }
    }
    
        public class WorkspaceReportTotal
        {
            public string _id { get; set; }
            public int totalTime { get; set; }
            public int entriesCount { get; set; }
            public object totalAmount { get; set; }
        }

        public class WorkspaceReportGroupOne
        {
            public int duration { get; set; }
            public string _id { get; set; }
            public string name { get; set; }
            public string nameLowerCase { get; set; }
            public string color { get; set; }
            public string clientName { get; set; }
        }


}