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
        private static string _ticketNumber;
        private static string _subject;
        private static string _timestamp;

        /// <summary>
        /// Check if the receipt printer feature is enabled
        /// </summary>
        public static bool IsEnabled()
        {
            return CredentialManager.GetPrinterEnabled();
        }

        /// <summary>
        /// Print a ticket receipt to the receipt printer
        /// </summary>
        /// <param name="ticketNumber">The ticket number to print</param>
        /// <param name="subject">The ticket subject</param>
        /// <returns>True if print was successful, false otherwise</returns>
        public static bool PrintTicketNumber(string ticketNumber, string subject)
        {
            if (!IsEnabled())
            {
                System.Diagnostics.Debug.WriteLine("[ReceiptPrinter] Printing is disabled");
                return false;
            }

            try
            {
                // Store data for printing
                _ticketNumber = ticketNumber;
                _subject = subject;
                _timestamp = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt");

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
                float yPos = 20; // Start 20 pixels from top
                float centerX = e.PageBounds.Width / 2;
                Brush printBrush = Brushes.Black;

                // Try to load and print the image first (at the top)
                try
                {
                    string appPath = AppDomain.CurrentDomain.BaseDirectory;
                    string imagePath = System.IO.Path.Combine(appPath, "Assets", "printer_image.png");

                    if (System.IO.File.Exists(imagePath))
                    {
                        using (Image img = Image.FromFile(imagePath))
                        {
                            // Scale image to fit width (max 200px wide)
                            float maxImageWidth = 200;
                            float scale = Math.Min(1.0f, maxImageWidth / img.Width);
                            float imageWidth = img.Width * scale;
                            float imageHeight = img.Height * scale;

                            // Center the image
                            float imageX = centerX - (imageWidth / 2);

                            // Draw the image
                            e.Graphics.DrawImage(img, imageX, yPos, imageWidth, imageHeight);
                            yPos += imageHeight + 15; // Move down with 15px spacing after image
                        }
                        System.Diagnostics.Debug.WriteLine("[ReceiptPrinter] Image printed successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Image not found at: {imagePath}");
                    }
                }
                catch (Exception imgEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Error loading image: {imgEx.Message}");
                }

                // Print ticket number (bold, 16pt)
                Font ticketFont = new Font("Arial", 16, FontStyle.Bold);
                string ticketText = $"Ticket: {_ticketNumber}";
                SizeF ticketSize = e.Graphics.MeasureString(ticketText, ticketFont);
                e.Graphics.DrawString(ticketText, ticketFont, printBrush, centerX - (ticketSize.Width / 2), yPos);
                yPos += ticketSize.Height + 10; // Move down with 10px spacing

                // Print subject (regular, 12pt, centered)
                Font subjectFont = new Font("Arial", 12, FontStyle.Regular);
                float maxWidth = e.PageBounds.Width - 40; // 20px margin on each side

                // Wrap subject text if too long
                string wrappedSubject = WrapText(_subject, subjectFont, maxWidth, e.Graphics);

                // Center each line of the wrapped subject
                string[] subjectLines = wrappedSubject.Split('\n');
                foreach (string line in subjectLines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        SizeF lineSize = e.Graphics.MeasureString(line, subjectFont);
                        e.Graphics.DrawString(line, subjectFont, printBrush, centerX - (lineSize.Width / 2), yPos);
                        yPos += lineSize.Height;
                    }
                }
                yPos += 10; // Extra spacing after subject

                // Print timestamp (italic, 10pt)
                Font timestampFont = new Font("Arial", 10, FontStyle.Italic);
                SizeF timestampSize = e.Graphics.MeasureString(_timestamp, timestampFont);
                e.Graphics.DrawString(_timestamp, timestampFont, printBrush, centerX - (timestampSize.Width / 2), yPos);

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
        /// Wrap text to fit within a maximum width
        /// </summary>
        private static string WrapText(string text, Font font, float maxWidth, Graphics graphics)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string[] words = text.Split(' ');
            string line = string.Empty;
            string result = string.Empty;

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                SizeF size = graphics.MeasureString(testLine, font);

                if (size.Width > maxWidth && !string.IsNullOrEmpty(line))
                {
                    result += line + "\n";
                    line = word;
                }
                else
                {
                    line = testLine;
                }
            }

            if (!string.IsNullOrEmpty(line))
            {
                result += line;
            }

            return result;
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
