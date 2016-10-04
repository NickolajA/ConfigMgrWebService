# ConfigMgrWebService

This web service was designed to extend the functionality of Operating System Deployment with Configuration Manager 2012 (and above). Included in this web service, the following methods are made available:

- Get Primary User by Device
- Get Primary Device by User
- Get Boot Image Source Version
- Get Deployed Applications by User
- Get Deployed Applications by Device
- Get Hidden Task Sequence Deployments

## Installation instructions

To successfully run this web service, you'll need to have IIS installed on a member server with ASP.NET enabled. Easiest way to get going is to install the ConfigMgrWebService on the same server as where your Management Point role is hosted. You'll also need to have a service account for the application pool in IIS. It's recommended that you add the service account in ConfigMgr with Full Administrator privileges.

1. Download the project and compile the solution in Visual Studio (you can download the free version called Visual Studio Community Edition)
2. Create a folder in <b>C:\inetpub</b> called <b>ConfigMgrWebService</b>. Inside that folder, create a folder called <b>bin</b>.
3. Copy the compiled <b>ConfigMgrWebService.dll</b> to <b>C:\inetpub\ConfigMgrWebService\bin</b>.
4. Rename Web.Release.config to <b>Web.config</b> and copy it to <b>C:\inetpub\ConfigMgrWebService</b>.
5. Copy <b>ConfigMgrWebService.asmx</b> to <b>C:\inetpub\ConfigMgrWebService</b>.
6. Locate <b>AdminUI.WqlQueryEngine.dll</b> and <b>Microsoft.ConfigurationManagement.ManagementProvider.dll</b> in the ConfigMgr console installation location and copy them to <b>C:\inetpub\ConfigMgrWebService\bin</b>.
