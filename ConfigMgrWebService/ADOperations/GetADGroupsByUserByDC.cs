using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Get Active Directory groups for a specific user on the specified domain controller.")]
        public List<ADGroup> GetADGroupsByUserByDC(string secret, string userName, string domainController)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Set return value variable
            var returnValue = new List<ADGroup>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                try
                {
                    //' Get AD user object
                    string userDistinguishedName = GetADObject(userName, ADObjectClass.User, ADObjectType.distinguishedName, domainController);

                    //' Get AD groups for user distinguished name
                    var groupMemberships = new ArrayList();
                    ArrayList groups = GetADAttributeValues("memberOf", userDistinguishedName, groupMemberships, true);

                    foreach (string group in groups)
                    {
                        string attributeValue = GetADAttributeValue(group, "samAccountName");
                        returnValue.Add(new ADGroup() { DistinguishedName = group, SamAccountName = attributeValue });
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog($"An error occurred while retrieving Active Directory group memberships for user. Error message: { ex.Message }", EventLogEntryType.Error);
                }
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}