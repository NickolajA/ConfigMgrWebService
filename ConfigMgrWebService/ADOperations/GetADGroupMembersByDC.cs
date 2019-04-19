using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.Reflection;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Get all members of an Active Directory group on the specified domain controller.")]
        public List<string> GetADGroupMembersByDC(string secret, string groupName, string domainController)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Set return value variable
            var returnValue = new List<string>();

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Get AD group object
                string groupDistinguishedName = GetADObject(groupName, ADObjectClass.Group, ADObjectType.distinguishedName, domainController);

                if (!string.IsNullOrEmpty(groupDistinguishedName))
                {
                    try
                    {
                        var groupEntry = new DirectoryEntry(groupDistinguishedName);
                        returnValue = GetADGroupMemberList(groupEntry);
                    }
                    catch (Exception ex)
                    {
                        WriteEventLog(string.Format("An error occured when retrieving Active Directory group members. Error message: {0}", ex.Message), EventLogEntryType.Error);
                    }
                }
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}