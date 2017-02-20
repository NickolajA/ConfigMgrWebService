# ConfigMgr WebService

ConfigMgr WebService has been designed to extend the functionality of Operating System Deployment with Configuration Manager 2012 (and above). These methods provided by this web service enables you to build custom frontend / HTA applications.

The list below shows an overview of the available methods:

### Configuration Manager

- AddCMComputerToCollection 
- GetCMBootImageSourceVersion 
- GetCMCollectionVariables 
- GetCMDeployedApplicationsByDevice 
- GetCMDeployedApplicationsByUser 
- GetCMDeviceCollections 
- GetCMDeviceNameByUUID 
- GetCMDeviceResourceIDByMACAddress 
- GetCMDeviceResourceIDByUUID 
- GetCMDiscoveredUsers 
- GetCMDriverPackageByModel 
- GetCMHiddenTaskSequenceDeployments 
- GetCMHiddenTaskSequenceDeploymentsByResourceId 
- GetCMPackage 
- GetCMPrimaryDeviceByUser 
- GetCMPrimaryUserByDevice 
- GetCMUniqueUserName 
- ImportCMComputerByMacAddress 
- ImportCMComputerByUUID 
- UpdateCMCollectionMembership 

### Microsoft Deployment Toolkit

- AddMDTRoleMember 
- AddMDTRoleMemberByAssetTag 
- AddMDTRoleMemberByMacAddress 
- AddMDTRoleMemberBySerialNumber 
- AddMDTRoleMemberByUUID 
- GetMDTComputerByAssetTag 
- GetMDTComputerByMacAddress 
- GetMDTComputerBySerialNumber 
- GetMDTComputerByUUID 
- GetMDTComputerNameByIdentity 
- GetMDTComputerRoleMembership 
- GetMDTDetailedComputerRoleMembership 
- GetMDTRoles 
- RemoveMDTComputerFromRoles 

### Active Directory

N/A (Coming in version 1.2.0)

## Supported Configurations
This web service has been built to support the following versions of System Center Configuration Manager:

- Configuration Manager 2012 SP1
- Configuration Manager 2012 SP2
- Configuration Manager 2012 R2
- Configuration Manager 2012 R2 SP1
- Configuration Manager Current Branch (all currently supported versions released by Microsoft)

Make sure that .NET Framework 4.5.2 is available on the member server you intend to host this web service on.

## Installation instructions

To successfully run this web service, you'll need to have IIS installed on a member server with ASP.NET enabled. Easiest way to get going is to install the ConfigMgrWebService on the same server as where your Management Point role is hosted. You'll also need to have a service account for the application pool in IIS. It's recommended that you add the service account in ConfigMgr with Full Administrator privileges.

### 1 - Create folder structure
1. Download the project and compile the solution in Visual Studio (you can download the free version called Visual Studio Community Edition)
2. Create a folder in <b>C:\inetpub</b> called <b>ConfigMgrWebService</b>. Inside that folder, create a folder called <b>bin</b>.
3. Copy the compiled <b>ConfigMgrWebService.dll</b> to <b>C:\inetpub\ConfigMgrWebService\bin</b>.
4. Rename Web.Release.config to <b>Web.config</b> and copy it to <b>C:\inetpub\ConfigMgrWebService</b>.
5. Copy <b>ConfigMgrWebService.asmx</b> to <b>C:\inetpub\ConfigMgrWebService</b>.
6. Locate <b>AdminUI.WqlQueryEngine.dll</b> and <b>Microsoft.ConfigurationManagement.ManagementProvider.dll</b> in the ConfigMgr console installation location and copy them to <b>C:\inetpub\ConfigMgrWebService\bin</b>.

### 2 - Add an Application Pool in IIS
1. Open IIS management console, right click on <b>Application Pools</b> and select Add Application Pool.
2. Enter <b>ConfigMgrWebService</b> as name, select the .NET CLR version <b>.NET CLR Version v4.0.30319</b> and click OK.
3. Select the new <b>ConfigMgrWebService</b> application pool and select <b>Advanced Settings</b>.
4. In the <b>Process Model</b> section, specify the service account that will have access to ConfigMgr in the <b>Identity</b> field and click OK.

### 3 - Add an Application to Default Web Site
1. Open IIS management console, expand <b>Sites</b>, right click on <b>Default Web Site</b> and select <b>Add Application</b>.
2. As for <b>Alias</b>, enter <b>ConfigMgrWebService</b>.
3. Select <b>ConfigMgrWebService</b> as application pool.
4. Set the physical path to <b>C:\inetpub\ConfigMgrWebService</b> and click OK.

### 4 - Set Application Settings
1. Open IIS management console, expand <b>Sites</b> and <b>Default Web Site</b>.
2. Select <b>ConfigMgrWebService</b> application and go to <b>Application Settings</b>.
3. Enter values for each application settings, <b>SiteServer</b> being the server where the SMS Provider is installed, <b>SiteCode</b> being the site code of your site and <b>SecretKey</b> being a custom string that you create yourself.

## Documentation

### Application Settings
When calling the web service methods, you'll need to pass along a secret key that matches what's specified for the SecretKey application setting in web.config as a parameter. Without this parameter, the method will be invoked properly. This is a somewhat reasonable security mechanism (at least it's something) that prevents unathorized users to invoke the methods and retrieve data from your ConfigMgr environment. It's recommended that you generate a GUID and enter that as the SecretKey.
