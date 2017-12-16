using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADGroup
    {
        public string samAccountName { get; set; }
        public string DistinguishedName { get; set; }
    }
}