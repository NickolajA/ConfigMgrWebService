using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMUser
    {
        public string uniqueUserName { get; set; }
        public string resourceId { get; set; }
        public string windowsNTDomain { get; set; }
        public string fullDomainName { get; set; }
    }
}