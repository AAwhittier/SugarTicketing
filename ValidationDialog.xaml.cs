using System.Windows;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Custom validation/error dialog with consistent styling
    /// </summary>
    public partial class ValidationDialog : Window
    {
        public ValidationDialog(string title, string message)
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            ContentTextBlock.Text = message;

            // Set icon based on title type
            if (title.Contains("Error") || title.Contains("Failed"))
            {
                IconTextBlock.Text = "❌";
            }
            else if (title.Contains("Warning"))
            {
                IconTextBlock.Text = "⚠️";
            }
            else if (title.Contains("Missing") || title.Contains("Required"))
            {
                IconTextBlock.Text = "❗";
            }
            else if (title.Contains("Too Short") || title.Contains("Too Long"))
            {
                IconTextBlock.Text = "📏";
            }
            else
            {
                IconTextBlock.Text = "ℹ️";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position dialog in center of owner window
            if (Owner != null)
            {
                Left = Owner.Left + (Owner.Width - Width) / 2;
                Top = Owner.Top + (Owner.Height - Height) / 2;
            }

            // Focus the OK button
            OkButton.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
