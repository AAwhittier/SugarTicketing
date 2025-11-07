using System;
using System.Drawing;
using System.Drawing.Printing;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Utility class for printing ticket receipts to a Zebra KR203 receipt printer
    /// </summary>
    public static class ReceiptPrinter
    {
        private const string DEFAULT_PRINTER_NAME = "Zebra KR203";
        private static string _textToPrint;

        /// <summary>
        /// Check if the receipt printer feature is enabled
        /// </summary>
        public static bool IsEnabled()
        {
            return CredentialManager.GetPrinterEnabled();
        }

        /// <summary>
        /// Print a ticket number to the receipt printer
        /// </summary>
        /// <param name="ticketNumber">The ticket number to print</param>
        /// <returns>True if print was successful, false otherwise</returns>
        public static bool PrintTicketNumber(string ticketNumber)
        {
            if (!IsEnabled())
            {
                System.Diagnostics.Debug.WriteLine("[ReceiptPrinter] Printing is disabled");
                return false;
            }

            try
            {
                // Format the text to print
                _textToPrint = $"Ticket: {ticketNumber}";

                // Create print document
                PrintDocument printDoc = new PrintDocument();

                // Try to find the Zebra KR203 printer
                string printerName = FindZebraPrinter();
                if (!string.IsNullOrEmpty(printerName))
                {
                    printDoc.PrinterSettings.PrinterName = printerName;
                    System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Using printer: {printerName}");
                }
                else
                {
                    // Use default printer if Zebra not found
                    System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Zebra KR203 not found, using default printer");
                }

                // Subscribe to print page event
                printDoc.PrintPage += PrintPage;

                // Print the document
                printDoc.Print();

                System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Successfully printed ticket: {ticketNumber}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] ERROR: Failed to print ticket: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find the Zebra KR203 printer in installed printers
        /// </summary>
        private static string FindZebraPrinter()
        {
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    // Check for exact match or partial match with "Zebra" and "KR203"
                    if (printer.Contains("Zebra") && printer.Contains("KR203"))
                    {
                        return printer;
                    }
                }

                // Try just "Zebra KR203" as fallback
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.Equals(DEFAULT_PRINTER_NAME, StringComparison.OrdinalIgnoreCase))
                    {
                        return printer;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Error finding printer: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Handle the print page event
        /// </summary>
        private static void PrintPage(object sender, PrintPageEventArgs e)
        {
            try
            {
                // Set up font and brush for printing
                Font printFont = new Font("Arial", 16, FontStyle.Bold);
                Brush printBrush = Brushes.Black;

                // Calculate center position for text
                SizeF textSize = e.Graphics.MeasureString(_textToPrint, printFont);
                float x = (e.PageBounds.Width - textSize.Width) / 2;
                float y = 20; // 20 pixels from top

                // Draw the text
                e.Graphics.DrawString(_textToPrint, printFont, printBrush, x, y);

                // No more pages to print
                e.HasMorePages = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Error in PrintPage: {ex.Message}");
                e.HasMorePages = false;
            }
        }

        /// <summary>
        /// Get a list of all installed printers (for debugging/diagnostics)
        /// </summary>
        public static string[] GetInstalledPrinters()
        {
            try
            {
                string[] printers = new string[PrinterSettings.InstalledPrinters.Count];
                PrinterSettings.InstalledPrinters.CopyTo(printers, 0);
                return printers;
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
