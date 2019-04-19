using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        private string GetADObject(string name, ADObjectClass objectClass, ADObjectType objectType, string domainController = null)
        {
            //' Set empty value for return object and search result
            string returnValue = string.Empty;
            SearchResult searchResult = null;

            string ldapFormat = string.IsNullOrEmpty(domainController)
                ? "LDAP://{0}"
                : "LDAP://{0}/{1}";

            //' Get default naming context of current domain
            string defaultNamingContext = GetADDefaultNamingContext();

            string currentDomain = string.IsNullOrEmpty(domainController)
                ? string.Format(ldapFormat, defaultNamingContext)
                : string.Format(ldapFormat, domainController, defaultNamingContext);

            //' Construct directory entry for directory searcher
            var domain = new DirectoryEntry(currentDomain);
            var directorySearcher = new DirectorySearcher(domain);
            directorySearcher.PropertiesToLoad.Add("distinguishedName");

            switch (objectClass)
            {
                case ADObjectClass.DomainController:
                    directorySearcher.Filter = string.Format("(&(objectClass=computer)((dNSHostName={0})))", name);
                    break;
                case ADObjectClass.Computer:
                    directorySearcher.Filter = string.Format("(&(objectClass=computer)((sAMAccountName={0}$)))", name);
                    break;
                case ADObjectClass.Group:
                    directorySearcher.Filter = string.Format("(&(objectClass=group)((sAMAccountName={0})))", name);
                    break;
                case ADObjectClass.User:
                    directorySearcher.Filter = string.Format("(&(objectClass=user)((sAMAccountName={0})))", name);
                    break;
            }

            //' Invoke directory searcher
            try
            {
                searchResult = directorySearcher.FindOne();
            }
            catch (Exception ex)
            {
                WriteEventLog(string.Format("An error occured when attempting to locate Active Directory object. Error message: {0}", ex.Message), EventLogEntryType.Error);
                return returnValue;
            }

            //' Return selected object type value
            if (searchResult != null)
            {
                DirectoryEntry directoryObject = searchResult.GetDirectoryEntry();

                if (objectType.Equals(ADObjectType.objectGuid))
                {
                    returnValue = directoryObject.Guid.ToString();
                }

                if (objectType.Equals(ADObjectType.distinguishedName))
                {
                    returnValue = string.Format(ldapFormat, directoryObject.Properties["distinguishedName"].Value);
                }
            }

            //' Dispose objects
            directorySearcher.Dispose();
            domain.Dispose();

            return returnValue;
        }
    }
}