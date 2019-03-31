using System;

namespace ConfigMgrWebService
{
    public partial class ConfigMgrWebService
    {
        private const string COMMON_NAME = "cn";
        private const string DISTINGUISHED_NAME = "distinguishedName";
        private const string DNS_HOST_NAME = "dNSHostName";
        private const string SAM_ACCOUNT_NAME = "sAMAccountName";

        private static string[] COMPUTER_PROPERTIES => new string[4]
        {
            COMMON_NAME, DISTINGUISHED_NAME, DNS_HOST_NAME, SAM_ACCOUNT_NAME
        };
    }
}