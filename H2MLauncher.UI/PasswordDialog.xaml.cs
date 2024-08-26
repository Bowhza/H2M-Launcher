using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace H2MLauncher.UI
{
    public partial class PasswordDialog : UserControl
    {
        public string Password { get; private set; }

        public PasswordDialog()
        {
            InitializeComponent();
        }

        public string GetPassword()
        {
            var dialogWindow = new Window
            {
                Title = string.Empty, // No title
                Content = this, // El UserControl es el contenido de la ventana
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow, // O cualquier otra ventana que sea la propietaria
                WindowStyle = WindowStyle.None, // Sin barra de título
                AllowsTransparency = true, // Permitir la transparencia para hacer las esquinas redondeadas
                Background = Brushes.Transparent, // Fondo transparente
                ResizeMode = ResizeMode.NoResize, // No permitir el redimensionado
                BorderBrush = Brushes.Transparent, // Sin bordes visibles
            };

            bool? result = dialogWindow.ShowDialog();
            return result == true ? Password : null;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            Window.GetWindow(this).DialogResult = true;
            Window.GetWindow(this).Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = false;
            Window.GetWindow(this).Close();
        }
    }
}
