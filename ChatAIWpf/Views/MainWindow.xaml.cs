using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChatAIWpf.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // configure azure environment variables.
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", Properties.Settings.Default.AzureClientID);
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", Properties.Settings.Default.AzureTenantID);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", Properties.Settings.Default.AzureClientSecret);
        }
    }
}
