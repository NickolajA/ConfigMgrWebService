using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADOrganizationalUnit : BaseADObject
    {
        internal override string[] PropertiesToLoad => throw new NotImplementedException();

        private string Path { get; set; }

        public string Name { set; get; }
        public string DistinguishedName { set; get; }
        public bool HasChildren { set; get; }
        
        public ADOrganizationalUnit() { }

        public ADOrganizationalUnit(DirectoryEntry dirEntry)
        {
            using (dirEntry)
            {
                if (dirEntry.SchemaClassName != "organizationalUnit")
                    throw new ArgumentException("The provided DirectoryEntry is not a valid organizationalUnit!");

                this.HasChildren = false;
                this.Name = dirEntry.Properties["cn"].Value as string;
                this.DistinguishedName = dirEntry.Properties["distinguishedName"].Value as string;
                foreach (DirectoryEntry child in dirEntry.Children)
                {
                    if (child.SchemaClassName == "organizationalUnit")
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
            var list = new List<ADOrganizationalUnit>();
            using (DirectoryEntry de = this.GetDirectoryEntry())
            {
                foreach (DirectoryEntry entry in de.Children)
                {
                    if (entry.SchemaClassName == "organizationalUnit")
                        list.Add(new ADOrganizationalUnit(entry));
                }
            }
            return list;
        }

        private DirectoryEntry GetDirectoryEntry() => new DirectoryEntry(this.Path);
    }
}