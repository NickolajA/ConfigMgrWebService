using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADOrganizationalUnit
    {
        public string Name { set; get; }
        public string DistinguishedName { set; get; }
        public bool HasChildren { set; get; }
    }
}