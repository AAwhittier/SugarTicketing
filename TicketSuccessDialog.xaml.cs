using System.Windows;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Dialog for displaying ticket submission success
    /// </summary>
    public partial class TicketSuccessDialog : Window
    {
        public string TicketId { get; private set; }
        public bool ReceiptPrinted { get; private set; }

        public TicketSuccessDialog(string ticketId, bool receiptPrinted)
        {
            InitializeComponent();

            TicketId = ticketId;
            ReceiptPrinted = receiptPrinted;

            // Display ticket information
            TicketIdTextBlock.Text = $"Ticket #{ticketId}";

            // Display receipt status
            if (receiptPrinted)
            {
                ReceiptStatusTextBlock.Text = "A receipt has been printed for your records.";
            }
            else
            {
                ReceiptStatusTextBlock.Text = "Receipt printing is disabled.";
                ReceiptStatusTextBlock.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position dialog at top-center of screen
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Center horizontally
            Left = (screenWidth - ActualWidth) / 2;

            // Position near top (50 pixels from top)
            Top = 50;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
