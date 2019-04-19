using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Reflection;
using System.Web.Services;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        [WebMethod(Description = "Check if a computer object exists in Active Directory on the specified domain controller")]
        public ADComputerFromDC GetADComputerByDC(string secret, string computerName, string dc)
        {
            var method = MethodBase.GetCurrentMethod();
            MethodBegin(method);

            //' Instatiate return value variable
            ADComputerFromDC returnValue = null;

            //' Validate secret key
            if (secret == secretKey)
            {
                //' Log that secret key was accepted
                WriteEventLog("Secret key was accepted", EventLogEntryType.Information);

                //' Set empty value for search result
                SearchResult searchResult = null;
                DirectoryEntry directoryObject = null;

                //' Get default naming context of current domain
                var domain = Domain.GetComputerDomain();
                string respondingDC = GetRespondingDomainController(domain, dc);

                //' Construct directory entry for directory searcher

                string sFilter = string.Format("(&(objectClass=computer)((sAMAccountName={0}$)))", computerName);
                var directorySearcher = new DirectorySearcher(domain.GetDirectoryEntry(), sFilter, COMPUTER_PROPERTIES);

                //' Invoke directory searcher
                try
                {
                    searchResult = directorySearcher.FindOne();
                    if (searchResult != null)
                    {
                        //' Get computer object from search result
                        directoryObject = searchResult.GetDirectoryEntry();

                        if (directoryObject != null)
                        {
                            returnValue = new ADComputerFromDC(directoryObject, respondingDC);

                            // Dispose directory object
                            directoryObject.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteEventLog(
                        string.Format(
                            "An error occured when attempting to locate Active Directory object. Error message: {0}",
                            ex.Message
                        ), 
                        EventLogEntryType.Error
                    );
                }

                //' Dispose objects
                directorySearcher.Dispose();
                domain.Dispose();
            }

            MethodEnd(method);
            return returnValue;
        }
    }
}