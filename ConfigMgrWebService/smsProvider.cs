using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;

namespace ConfigMgrWebService
{
    class SmsProvider
    {
        public WqlConnectionManager Connect(string serverName)
        {
            try
            {
                //' Connect to SMS Provider
                SmsNamedValuesDictionary namedValues = new SmsNamedValuesDictionary();
                WqlConnectionManager connection = new WqlConnectionManager(namedValues);
                connection.Connect(serverName);

                return connection;
            }
            catch (SmsException ex)
            {
                ConfigMgrWebService.WriteEventLog(String.Format("Unhandled expection thrown by SMS Provider: {0}", ex.Message), EventLogEntryType.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                ConfigMgrWebService.WriteEventLog(String.Format("Unathorized access exception thrown: {0}", ex.Message), EventLogEntryType.Error);
            }
            catch (Exception ex)
            {
                ConfigMgrWebService.WriteEventLog(String.Format("Unhandled expection thrown: {0}", ex.Message), EventLogEntryType.Error);
            }

            return null;
        }
    }
}