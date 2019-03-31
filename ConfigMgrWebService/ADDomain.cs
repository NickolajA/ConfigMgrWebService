using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADDomain
    {
        public string DomainName { get; set; }
        public string DefaultNamingContext { get; set; }

        public ADDomain(Domain domain)
        {
            this.DomainName = domain.Name;
            using (var de = domain.GetDirectoryEntry())
            {
                this.DefaultNamingContext = de.Properties["distinguishedName"].Value as string;
            }
        }
    }
}