using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADOrganizationalUnit
    {

        private string Path { get; set; }

        public string Name { set; get; }
        public string DistinguishedName { set; get; }
        public bool HasChildren { set; get; }
        
        public ADOrganizationalUnit() { }

        /// <summary>
        /// The 2nd constructor for <see cref="ADOrganizationalUnit"/>.  Using the specified <see cref="DirectoryEntry"/>,
        /// this will populate the class's properties.
        /// </summary>
        /// <param name="dirEntry">The <see cref="DirectoryEntry"/> to use when populating this class's properties.</param>
        public ADOrganizationalUnit(DirectoryEntry dirEntry)
        {
            using (dirEntry)
            {
                if (dirEntry.SchemaClassName != ConfigMgrWebService.ORG_UNIT)
                    throw new ArgumentException("The provided DirectoryEntry is not a valid Organizational Unit!");

                this.HasChildren = false;
                this.Name = dirEntry.Properties[ConfigMgrWebService.NAME].Value as string;
                this.DistinguishedName = dirEntry.Properties[ConfigMgrWebService.DISTINGUISHED_NAME].Value as string;
                foreach (DirectoryEntry child in dirEntry.Children)
                {
                    if (child.SchemaClassName == ConfigMgrWebService.ORG_UNIT)
                    {
                        this.HasChildren = true;
                        break;
                    }
                }
                this.Path = dirEntry.Path;
            }
        }

        public IEnumerable<ADOrganizationalUnit> GetChildrenOUs()
        {
            if (this.HasChildren)
            {
                var list = new List<ADOrganizationalUnit>();
                using (DirectoryEntry de = this.GetDirectoryEntry())
                {
                    foreach (DirectoryEntry entry in de.Children)
                    {
                        if (entry.SchemaClassName == ConfigMgrWebService.ORG_UNIT)
                            list.Add(new ADOrganizationalUnit(entry));
                    }
                }
                return list;
            }
            else
                return null;
        }

        private DirectoryEntry GetDirectoryEntry() => new DirectoryEntry(this.Path);
    }
}