using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class user
    {
        public string fullUserName { get; set; }
        public string uniqueUserName { get; set; }
        public string userPrincipalName { get; set; }
        public string userName { get; set; }
        public string resourceId { get; set; }
        public string windowsNTDomain { get; set; }
        public string fullDomainName { get; set; }
    }
}