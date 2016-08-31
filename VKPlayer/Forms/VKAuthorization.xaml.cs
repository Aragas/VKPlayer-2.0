using System;
using System.Windows;

namespace VKPlayer.Forms
{
    /// <summary>
    /// Interaction logic for VKAuthorization.xaml
    /// </summary>
    public partial class VKAuthorization : Window
    {
        public event Action<string, string, string> ConfirmClicked;

        public VKAuthorization()
        {
            InitializeComponent();
        }

        private void confirmButton_Click(object sender, RoutedEventArgs e) { ConfirmClicked?.Invoke(loginTextBox.Text, passTextBox.Password, twofacorTextBox.Text); }
    }
}