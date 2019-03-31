using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Web;

namespace ConfigMgrWebService
{
    public class ADDomain : IDisposable
    {
        private Domain _domain;
        private string _typeName => this.GetType().FullName;
        private bool _isDisp;

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

        public DomainControllerCollection GetAllDomainControllers()
        {
            this.CheckIfDisposed();
            return _domain.FindAllDomainControllers();
        }

        public DomainController FindDomainController()
        {
            this.CheckIfDisposed();
            return _domain.FindDomainController();
        }

        #region IDISPOSABLE METHODS
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisp)
                return;

            if (disposing)
                _domain.Dispose();

            _isDisp = true;
        }

        private void CheckIfDisposed()
        {
            if (_isDisp)
                throw new ObjectDisposedException(_typeName);
        }

        #endregion

        public static implicit operator ADDomain(Domain domain) => new ADDomain(domain);

        public static implicit operator Domain(ADDomain adDomain)
        {
            adDomain.CheckIfDisposed();
            return adDomain._domain;
        }
    }
}