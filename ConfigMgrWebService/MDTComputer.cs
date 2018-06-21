using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class MDTComputer
    {
        public string ComputerName { get; set; }
        public string ComputerIdentity { get; set; }
        public string AssetTag { get; set; }
        public string SerialNumber { get; set; }
        public string UUID { get; set; }
        public string MacAddress { get; set; }
    }
}