using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace LocalDNSProxy
{



    public sealed class StartInfo
    {
        public StartInfo(string iPAddress, string hosts)
        {
            IPAddress = iPAddress ?? throw new ArgumentNullException(nameof(iPAddress));
            Hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
        }

        public string IPAddress { get; }

        public string Hosts { get; }
    }

    public sealed class MainPageInfo
    {
        public MainPageInfo(StartInfo startInfo, Action<StartInfo> saveAction, Action<StartInfo> startAction)
        {
            StartInfo = startInfo ?? throw new ArgumentNullException(nameof(startInfo));
            SaveAction = saveAction ?? throw new ArgumentNullException(nameof(saveAction));
            StartAction = startAction ?? throw new ArgumentNullException(nameof(startAction));
        }

        public StartInfo StartInfo { get; }


        public Action<StartInfo> SaveAction { get; }

        public Action<StartInfo> StartAction { get; }

    }




    public partial class MainPage : ContentPage
    {
        readonly MainPageInfo _info;

        public MainPage(MainPageInfo info)
        {
            InitializeComponent();

            _info = info;


            _hosts.Text = _info.StartInfo.Hosts;

            _ipaddress.Text = _info.StartInfo.IPAddress;
        }

        private void OnStart(object sender, EventArgs e)
        {
            try
            {
                _info.StartAction(new StartInfo(_ipaddress.Text, _hosts.Text));
            }
            catch (FormatException ex)
            {
                DisplayAlert("错误", ex.Message, "确定");
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            try
            {
                _info.SaveAction(new StartInfo(_ipaddress.Text, _hosts.Text));
            }
            catch (FormatException ex)
            {
                DisplayAlert("错误", ex.Message, "确定");
            }
        }
    }
}