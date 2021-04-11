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

        void CatchException(Action<StartInfo> action)
        {
            try
            {
                action(new StartInfo(_ipaddress.Text, _hosts.Text));
            }
            catch (Exception ex)
            {
                DisplayAlert("错误", ex.Message, "确定");
            }
        }

        private void OnStart(object sender, EventArgs e)
        {

            CatchException(_info.StartAction);

        }

        private void OnSave(object sender, EventArgs e)
        {
            CatchException(_info.SaveAction);
        }

        private void OnHelp(object sender, EventArgs e)
        {
            const string MESSAGE = 
                "假如本地没有匹配项，就从设置的DNS服务器查询，" +
                "类似于一个DNS代理，假如DNS服务器地址无效，" +
                "程序就会无效，即便是有本地匹配项也不行，这好像是我用的DNS类库的原因。" +
                "（重要）只有APP使用操作系统提供的DNS查询接口才会生效，应用自己实现DNS查询的本应用对其无效。" +
                "该程序虽然创建VPN，但是只是使用该API来实现功能而已，" +
                "正常的流量都不会流向我的应用，只有192.168.254.254 IP地址例外，" +
                "因为该ip被我设置为应用看到的DNS服务器IP，也就是其他应用的DNS请求会发给本应用，" +
                "但是本应用会占用VPN，运行本应用就无法同时运行其他VPN应用。" +
                "关闭服务需要手动杀后台，" +
                "假如服务已经在运行需要先杀后台再重启才会使更改生效，" +
                "假如切换网络可能需要重启本应用，重启需要先杀后台";



            DisplayAlert("帮助与说明", MESSAGE, "确定");
        }
    }
}