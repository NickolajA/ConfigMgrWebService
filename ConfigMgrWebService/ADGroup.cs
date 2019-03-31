using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADGroup : BaseADObject
    {
        internal override string[] PropertiesToLoad => null;

        public string SamAccountName { get; set; }
        public string DistinguishedName { get; set; }
        public string RespondingDC { get; }

        public ADGroup() { }

        public ADGroup(DirectoryEntry dirEntry, string dc)
        {
            this.RespondingDC = dc;
            using (dirEntry)
            {
                this.SamAccountName = dirEntry.Properties["sAMAccountName"].Value as string;
                this.DistinguishedName = dirEntry.Properties["distinguishedName"].Value as string;
            }
        }
    }
}