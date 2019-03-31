using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADComputer : BaseADObject
    {
        internal override string[] PropertiesToLoad => new string[4]
        {
            "distinguishedName", "cn", "sAMAccountName", "dNSHostName"
        };

        public string SamAccountName { get; set; }
        public string CanonicalName { get; set; }
        public string DnsHostName { get; set; }
        public string DistinguishedName { get; set; }
        public string RespondingDC { get; }

        public ADComputer() { }
        public ADComputer(DirectoryEntry de, string dc)
        {
            this.RespondingDC = dc;
            using (de)
            {
                this.DistinguishedName = de.Properties["distinguishedName"].Value as string;
                this.CanonicalName = de.Properties["cn"].Value as string;
                this.DnsHostName = de.Properties["dNSHostName"].Value as string;
                this.SamAccountName = de.Properties["sAMAccountName"].Value as string;
            }
        }
    }
}