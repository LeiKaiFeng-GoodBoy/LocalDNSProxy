using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LocalDNSProxy
{
    public partial class App : Application
    {
        public App(MainPageInfo info)
        {
            InitializeComponent();

            MainPage = new MainPage(info);
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
