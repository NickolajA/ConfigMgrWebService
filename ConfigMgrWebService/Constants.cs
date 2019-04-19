using System;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        internal const string COMMON_NAME = "cn";
        internal const string COMPUTER = "computer";
        internal const string DISTINGUISHED_NAME = "distinguishedName";
        internal const string DNS_HOST_NAME = "dNSHostName";
        internal const string NAME = "name";
        internal const string ORG_UNIT = "organizationalUnit";
        internal const string PATH = "path";
        internal const string SAM_ACCOUNT_NAME = "sAMAccountName";

        private static string[] COMPUTER_PROPERTIES => new string[4]
        {
            COMMON_NAME, DISTINGUISHED_NAME, DNS_HOST_NAME, SAM_ACCOUNT_NAME
        };

        private static string[] GROUP_PROPERTIES => new string[2]
        {
            DISTINGUISHED_NAME, SAM_ACCOUNT_NAME
        };

        private static string[] ORG_UNIT_PROPERTIES => new string[3]
        {
            DISTINGUISHED_NAME, NAME, PATH
        };
    }
}