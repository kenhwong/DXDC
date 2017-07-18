using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DevExpress.Xpf.Core;

namespace DXDC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : DXWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            XGlobal.CurrentContext.IsUseProxy = false;
            checkProxy.Content = "PROXY OFF";
            checkProxy.Foreground = new SolidColorBrush(Colors.Gray);
        }

        private void checkProxy_Click(object sender, EventArgs e)
        {
            if (!XGlobal.CurrentContext.IsUseProxy)
            {
                XGlobal.CurrentContext.IsUseProxy = true;
                XGlobal.CurrentContext.SSProxy = new System.Net.WebProxy("http://127.0.0.1:10800");
                checkProxy.ToolTip = "Proxy: Socket5/127.0.0.1:10800";
                checkProxy.Content = "PROXY ON";
                checkProxy.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                XGlobal.CurrentContext.IsUseProxy = false;
                XGlobal.CurrentContext.SSProxy = null;
                checkProxy.ToolTip = null;
                checkProxy.Content = "PROXY OFF";
                checkProxy.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }
    }
}
