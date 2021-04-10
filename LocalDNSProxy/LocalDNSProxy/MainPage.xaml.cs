using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace LocalDNSProxy
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void OnStart(object sender, EventArgs e)
        {
            var vs =
                (_hosts.Text ?? string.Empty)
                .Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
;


            _hosts.Text = string.Join("$", vs);
        }
    }
}
