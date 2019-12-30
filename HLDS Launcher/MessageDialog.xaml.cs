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
using System.Windows.Shapes;

namespace HLDS_Launcher
{
    /// <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : Window
    {
        public MessageDialog(string message, string title, bool isYesNo = true, bool showInTaskbar = false)
        {
            InitializeComponent();

            if (isYesNo == false)
            {
                buttonYes.IsEnabled = false;
                buttonYes.Visibility = Visibility.Hidden;
                buttonClose.Content = "Close";
                messageImageError.Visibility = Visibility.Visible;
                messageImageWarning.Visibility = Visibility.Hidden;
            }
            this.Title = title;
            windowTitle.Text = title;
            textContent.Text = message;
            this.ShowInTaskbar = showInTaskbar;
        }

        private void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
