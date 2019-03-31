using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADGroup
    {
        public string SamAccountName { get; set; }
        public string DistinguishedName { get; set; }
        public string RespondingDC { get; }

        public ADGroup() { }

        /// <summary>
        /// The 2nd constructor for <see cref="ADGroup"/>.  Using the specified <see cref="DirectoryEntry"/>,
        /// this will populate the class's properties.  It also specfies the DomainController that responding with the
        /// <see cref="DirectoryEntry"/>.
        /// </summary>
        /// <param name="dirEntry">The <see cref="DirectoryEntry"/> to use when populating this class's properties.</param>
        /// <param name="dc">The responding domain controller of the <see cref="DirectoryEntry"/>.</param>
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