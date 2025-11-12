using System.Windows;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Dialog result for ticket choice
    /// </summary>
    public enum TicketChoice
    {
        Continue,
        FileNew
    }

    /// <summary>
    /// Dialog for choosing between continuing an existing ticket or filing a new one
    /// </summary>
    public partial class TicketChoiceDialog : Window
    {
        public TicketChoice Choice { get; private set; }
        public int TicketId { get; private set; }
        public string TicketSubject { get; private set; }

        public TicketChoiceDialog(int ticketId, string ticketSubject)
        {
            InitializeComponent();

            TicketId = ticketId;
            TicketSubject = ticketSubject;

            // Display ticket information
            TicketIdTextBlock.Text = $"Ticket #{ticketId}";
            TicketSubjectTextBlock.Text = ticketSubject;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position dialog at top-center of screen
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Center horizontally
            Left = (screenWidth - ActualWidth) / 2;

            // Position near top (100 pixels from top)
            Top = 100;
        }

        private void ContinueTicketButton_Click(object sender, RoutedEventArgs e)
        {
            Choice = TicketChoice.Continue;
            DialogResult = true;
            Close();
        }

        private void NewTicketButton_Click(object sender, RoutedEventArgs e)
        {
            Choice = TicketChoice.FileNew;
            DialogResult = true;
            Close();
        }
    }
}
