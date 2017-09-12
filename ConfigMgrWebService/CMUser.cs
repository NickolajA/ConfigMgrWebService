using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMUser
    {
        public string UniqueUserName { get; set; }
        public string ResourceId { get; set; }
        public string WindowsNTDomain { get; set; }
        public string FullDomainName { get; set; }
        public string FullUserName { get; set; }
        public string UserPrincipalName { get; set; }
        public string Name { get; set; }
        public string DistinguishedName { get; set; }
    }
}