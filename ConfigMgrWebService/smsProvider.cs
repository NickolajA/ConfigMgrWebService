using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using Microsoft.ConfigurationManagement.ManagementProvider;
using Microsoft.ConfigurationManagement.ManagementProvider.WqlQueryEngine;

namespace ConfigMgrWebService
{
    class smsProvider
    {
        public WqlConnectionManager Connect(string serverName)
        {
            try
            {
                SmsNamedValuesDictionary namedValues = new SmsNamedValuesDictionary();
                WqlConnectionManager connection = new WqlConnectionManager(namedValues);
                connection.Connect(serverName);
                return connection;
            }
            catch (SmsException ex)
            {
                Trace.WriteLine(DateTime.Now + ": Unhandled expection thrown by SMS Provider: " + ex.ToString());
                throw ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.WriteLine(DateTime.Now + ": Unathorized access exception thrown: " + ex.ToString());
                throw ex;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(DateTime.Now + ": Unhandled expection thrown: " + ex.ToString());
                throw ex;
            }
        }
    }
}