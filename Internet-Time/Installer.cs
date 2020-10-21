using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace Internet_Time
{
    [RunInstaller(true)]
    //    class Installer
    //    {
    //    }
    public class MyWindowsServiceInstaller : Installer
    {
        public MyWindowsServiceInstaller()
        {
            // https://stackoverflow.com/questions/3839854/difference-between-serviceprocessinstaller-and-serviceinstaller
            var processInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();

            //set the privileges
            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.DisplayName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            //must be the same as what was set in Program's constructor
            serviceInstaller.ServiceName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            this.Installers.Add(processInstaller);
            this.Installers.Add(serviceInstaller);
        }
    }
}
