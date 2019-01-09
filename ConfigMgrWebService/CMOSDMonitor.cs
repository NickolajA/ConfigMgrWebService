using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMOSDMonitor
    {
        public string UniqueID { get; set; }
        public string ComputerName { get; set; }
        public string SMBIOSGUID { get; set; }
        public string MacAddress { get; set; }
        public int Severity { get; set; }
        public DateTime ModifiedTime { get; set; }
        public string DeploymentID { get; set; }
        public string StepName { get; set; }
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Details { get; set; }
        public string DartIP { get; set; }
        public string DartPort { get; set; }
        public string DartTicket { get; set; }
    }
}