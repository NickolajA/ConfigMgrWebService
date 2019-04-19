using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Web;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Move a computer in Active Directory to a specific organizational unit via the specified domain controller.")]
        public bool SetADOrganizationalUnitForComputerByDC(string secret, string organizationalUnitLocation, string computerName, string domainController)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Variable for return value
            bool returnValue = false;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Determine if ldap prefix needs to be appended
                if (organizationalUnitLocation.StartsWith("LDAP://") == false && string.IsNullOrEmpty(domainController))
                {
                    organizationalUnitLocation = string.Format("LDAP://{0}", organizationalUnitLocation);
                }
                else if (!string.IsNullOrEmpty(domainController))
                {
                    string fullLDAP = "LDAP://{0}/{1}";
                    organizationalUnitLocation = string.Format(fullLDAP, domainController, organizationalUnitLocation);
                }

                //' Get AD object distinguished name
                string currentDistinguishedName = GetADObject(computerName, ADObjectClass.Computer, ADObjectType.distinguishedName, domainController);

                if (!string.IsNullOrEmpty(currentDistinguishedName))
                {
                    try
                    {
                        //' Move current object to new location
                        var currentObject = new DirectoryEntry(currentDistinguishedName);
                        var newLocation = new DirectoryEntry(organizationalUnitLocation);
                        currentObject.MoveTo(newLocation, currentObject.Name);

                        returnValue = true;
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(string.Format("An error occured when attempting to move Active Directory object. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}