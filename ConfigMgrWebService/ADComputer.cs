using System;
using System.Collections.Generic;
using System.DirectoryServices;

namespace ConfigMgrWebService
{
    public class ADComputer
    {
        public string SamAccountName { get; set; }
        public string CanonicalName { get; set; }
        public string DnsHostName { get; set; }
        public string DistinguishedName { get; set; }
        public string RespondingDC { get; }

        public ADComputer() { }

        /// <summary>
        /// The 2nd constructor for <see cref="ADComputer"/>.  Using the specified <see cref="DirectoryEntry"/>,
        /// this will populate the class's properties.  It also specfies the DomainController that responding with the
        /// <see cref="DirectoryEntry"/>.
        /// </summary>
        /// <param name="dirEntry">The <see cref="DirectoryEntry"/> to use when populating this class's properties.</param>
        /// <param name="dc">The responding domain controller of the <see cref="DirectoryEntry"/>.</param>
        public ADComputer(DirectoryEntry dirEntry, string dc)
        {
            this.RespondingDC = dc;
            using (dirEntry)
            {
                this.DistinguishedName = dirEntry.Properties[ConfigMgrWebService.DISTINGUISHED_NAME].Value as string;
                this.CanonicalName = dirEntry.Properties[ConfigMgrWebService.COMMON_NAME].Value as string;
                this.DnsHostName = dirEntry.Properties[ConfigMgrWebService.DNS_HOST_NAME].Value as string;
                this.SamAccountName = dirEntry.Properties[ConfigMgrWebService.SAM_ACCOUNT_NAME].Value as string;
            }
        }
    }
}