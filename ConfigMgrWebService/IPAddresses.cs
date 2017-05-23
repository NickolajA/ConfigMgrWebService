using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Numerics;
using System.Net.Sockets;
using System.Globalization;

namespace ConfigMgrWebService
{
    //' Class originately posted on StackOverflow: http://stackoverflow.com/questions/1499269/how-to-check-if-an-ip-address-is-within-a-particular-subnet
    //' Modified by Nickolaj Andersen (2017-04-24)
    public static class IPAddresses
    {
        public static Tuple<IPAddress, IPAddress> GetSubnetAndMaskFromCidr(string cidr)
        {
            var delimiterIndex = cidr.IndexOf('/');
            string ipSubnet = cidr.Substring(0, delimiterIndex);
            string mask = cidr.Substring(delimiterIndex + 1);

            var subnetAddress = IPAddress.Parse(ipSubnet);
            uint ip = 0xFFFFFFFF << (32 - int.Parse(mask));

            var maskBytes = new[]
            {
                (byte)((ip & 0xFF000000) >> 24),
                (byte)((ip & 0x00FF0000) >> 16),
                (byte)((ip & 0x0000FF00) >> 8),
                (byte)((ip & 0x000000FF) >> 0),
            };

            return Tuple.Create(subnetAddress, new IPAddress(maskBytes));
        }

        public static bool IsAddressOnSubnet(IPAddress address, IPAddress subnet, IPAddress mask)
        {
            byte[] addressOctets = address.GetAddressBytes();
            byte[] subnetOctets = mask.GetAddressBytes();
            byte[] networkOctets = subnet.GetAddressBytes();

            //' Ensure that IPv4 isn't mixed with IPv6
            if (addressOctets.Length != subnetOctets.Length || addressOctets.Length != networkOctets.Length)
            {
                return false;
            }

            for (int i = 0; i < addressOctets.Length; i += 1)
            {
                var addressOctet = addressOctets[i];
                var subnetOctet = subnetOctets[i];
                var networkOctet = networkOctets[i];

                if (networkOctet != (addressOctet & subnetOctet))
                {
                    return false;
                }
            }

            return true;
        }
    }
}