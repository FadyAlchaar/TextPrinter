using System;
using System.Management;

namespace TextPrinter
{
    internal class ManagementObjectSearcher
    {
        private string v;

        public ManagementObjectSearcher(string v)
        {
            this.v = v;
        }

        internal System.Collections.Generic.IEnumerable<ManagementObject> Get()
        {
            throw new NotImplementedException();
        }
    }
}