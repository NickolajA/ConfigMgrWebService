using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADDomain
    {
        private Domain _domain;

        public string DomainName { get; set; }
        public string DefaultNamingContext { get; set; }
        public string Path { get; set; }

        public ADDomain() { }

        private ADDomain(Domain domain)
        {
            this.DomainName = domain.Name;
            using (DirectoryEntry de = domain.GetDirectoryEntry())
            {
                this.DefaultNamingContext = de.Properties[ConfigMgrWebService.DISTINGUISHED_NAME].Value as string;
                this.Path = de.Path;
            }
            _domain = domain;
        }

        public DomainControllerCollection GetAllDomainControllers() => _domain.FindAllDomainControllers();

        public DomainController FindDomainController() => _domain.FindDomainController();



        public static implicit operator ADDomain(Domain domain) => new ADDomain(domain);

        public static implicit operator Domain(ADDomain adDomain) => adDomain._domain;
    }
}