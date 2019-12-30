using System.Windows;
using System.Windows.Input;

namespace HLDS_Launcher
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        // Close window.
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Github link.
        private void GoToGithub_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/curdh/HLDS-Launcher");
        }

        // Creative Commons license link.
        private void LicenseText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://creativecommons.org/licenses/by-nc-sa/4.0/");
        }
    }
}
