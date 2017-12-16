using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMApplication
    {
        public string ApplicationName { get; set; }
        public string ApplicationManufacturer { get; set; }
        public string ApplicationDescription { get; set; }
        public string ApplicationVersion { get; set; }
        public DateTime ApplicationCreated { get; set; }
        public string ApplicationExecutionContext { get; set; }
        public string CollectionName { get; set; }
    }
}