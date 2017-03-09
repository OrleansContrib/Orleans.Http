using System;
using Orleans.Runtime.Configuration;
using Orleans.Runtime.Host;
using OrleansHttp;

namespace TestHost
{
    internal class OrleansHostWrapper : IDisposable
    {
        public bool Debug
        {
            get { return siloHost != null && siloHost.Debug; }
            set { siloHost.Debug = value; }
        }

        private SiloHost siloHost;

        /// <summary>
        /// start primary
        /// </summary>
        public OrleansHostWrapper()
        {
            siloHost = new SiloHost("primary", ClusterConfiguration.LocalhostPrimarySilo());
        }

     

        public bool Run()
        {
            var ok = false;

            try
            {
                siloHost.InitializeOrleansSilo();

                siloHost.Config.Globals.RegisterBootstrapProvider<Bootstrap>("http");
                // register the dashboard

                ok = siloHost.StartOrleansSilo();
                if (!ok) throw new SystemException(string.Format("Failed to start Orleans silo '{0}' as a {1} node.", siloHost.Name, siloHost.Type));
            }
            catch (Exception exc)
            {
                siloHost.ReportStartupError(exc);
                var msg = string.Format("{0}:\n{1}\n{2}", exc.GetType().FullName, exc.Message, exc.StackTrace);
                Console.WriteLine(msg);
            }

            return ok;
        }

        public bool Stop()
        {
            var ok = false;

            try
            {
                siloHost.StopOrleansSilo();
            }
            catch (Exception exc)
            {
                siloHost.ReportStartupError(exc);
                var msg = string.Format("{0}:\n{1}\n{2}", exc.GetType().FullName, exc.Message, exc.StackTrace);
                Console.WriteLine(msg);
            }

            return ok;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool dispose)
        {
            siloHost.Dispose();
            siloHost = null;
        }
    }
}
