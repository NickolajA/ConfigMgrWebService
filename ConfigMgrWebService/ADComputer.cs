using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADComputer
    {
        public string SamAccountName { get; set; }
        public string CanonicalName { get; set; }
        public string DnsHostName { get; set; }
        public string DistinguishedName { get; set; }
    }
}