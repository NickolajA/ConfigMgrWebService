using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class CMPackage
    {
        public string PackageName { get; set; }
        public string PackageID { get; set; }
        public string PackageDescription { get; set; }
        public string PackageManufacturer { get; set; }
        public string PackageLanguage { get; set; }
        public string PackageVersion { get; set; }
        public DateTime PackageCreated { get; set; }
    }
}