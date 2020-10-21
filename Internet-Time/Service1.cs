using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Internet_Time
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        public void onDebug()
        {
            OnStart(null);
        }
        protected override void OnStart(string[] args)
        {
            // Debugger.Break();
            // System.IO.File.Create(AppDomain.CurrentDomain.BaseDirectory + "OnStart.txt");
        }
        protected override void OnStop()
        {
            // System.IO.File.Create(AppDomain.CurrentDomain.BaseDirectory + "OnStop.txt");
        }
    }
}
