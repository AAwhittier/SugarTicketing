using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Threading;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Utility class for printing ticket receipts to a Zebra KR203 receipt printer
    /// </summary>
    public static class ReceiptPrinter
    {
        private const string DEFAULT_PRINTER_NAME = "Zebra KR203";
        private static Image _cachedPrinterImage = null;
        private static readonly object _cacheLock = new object();

        // Use ThreadLocal to prevent race conditions when printing multiple tickets
        private static ThreadLocal<PrintData> _printData = new ThreadLocal<PrintData>(() => new PrintData());

        private class PrintData
        {
            public string TicketNumber { get; set; } = string.Empty;
            public string Device { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Timestamp { get; set; } = string.Empty;
        }

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
        /// <param name="device">The device name/number</param>
        /// <param name="subject">The ticket subject</param>
        /// <returns>True if print was successful, false otherwise</returns>
        public static bool PrintTicketNumber(string ticketNumber, string device, string subject)
        {
            if (!IsEnabled())
            {
                System.Diagnostics.Debug.WriteLine("[ReceiptPrinter] Printing is disabled");
                return false;
            }

            PrintDocument printDoc = null;
            try
            {
                // Store data for printing in thread-local storage
                _printData.Value.TicketNumber = ticketNumber;
                _printData.Value.Device = device ?? "Not specified";
                _printData.Value.Subject = subject;
                _printData.Value.Timestamp = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt");

                // Create print document
                printDoc = new PrintDocument();

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
            finally
            {
                // Clean up PrintDocument and unsubscribe event handler
                if (printDoc != null)
                {
                    printDoc.PrintPage -= PrintPage;
                    printDoc.Dispose();
                }
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
                const float TOP_MARGIN = 20f;
                const float IMAGE_MAX_WIDTH = 200f;
                const float IMAGE_SPACING = 15f;
                const float TEXT_SPACING = 10f;

                float yPos = TOP_MARGIN;
                float centerX = e.PageBounds.Width / 2;
                Brush printBrush = Brushes.Black;

                // Get print data from thread-local storage
                var data = _printData.Value;

                // Try to load and print the image first (at the top)
                Image printerImage = LoadPrinterImage();
                if (printerImage != null)
                {
                    try
                    {
                        // Scale image to fit width
                        float scale = Math.Min(1.0f, IMAGE_MAX_WIDTH / printerImage.Width);
                        float imageWidth = printerImage.Width * scale;
                        float imageHeight = printerImage.Height * scale;

                        // Center the image
                        float imageX = centerX - (imageWidth / 2);

                        // Draw the image
                        e.Graphics.DrawImage(printerImage, imageX, yPos, imageWidth, imageHeight);
                        yPos += imageHeight + IMAGE_SPACING;
                    }
                    catch (Exception imgEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Error drawing image: {imgEx.Message}");
                    }
                }

                // Print ticket number (bold, 16pt)
                using (Font ticketFont = new Font("Arial", 16, FontStyle.Bold))
                {
                    string ticketText = $"Ticket: {data.TicketNumber}";
                    SizeF ticketSize = e.Graphics.MeasureString(ticketText, ticketFont);
                    e.Graphics.DrawString(ticketText, ticketFont, printBrush, centerX - (ticketSize.Width / 2), yPos);
                    yPos += ticketSize.Height + TEXT_SPACING;
                }

                // Print device (bold, 16pt, same as ticket)
                using (Font deviceFont = new Font("Arial", 16, FontStyle.Bold))
                {
                    string deviceText = $"Device: {data.Device}";
                    SizeF deviceSize = e.Graphics.MeasureString(deviceText, deviceFont);
                    e.Graphics.DrawString(deviceText, deviceFont, printBrush, centerX - (deviceSize.Width / 2), yPos);
                    yPos += deviceSize.Height + TEXT_SPACING;
                }

                // Print subject (regular, 12pt, centered)
                using (Font subjectFont = new Font("Arial", 12, FontStyle.Regular))
                {
                    float maxWidth = e.PageBounds.Width - 40; // 20px margin on each side

                    // Wrap subject text if too long
                    string wrappedSubject = WrapText(data.Subject, subjectFont, maxWidth, e.Graphics);

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
                    yPos += TEXT_SPACING;
                }

                // Print timestamp (italic, 10pt)
                using (Font timestampFont = new Font("Arial", 10, FontStyle.Italic))
                {
                    SizeF timestampSize = e.Graphics.MeasureString(data.Timestamp, timestampFont);
                    e.Graphics.DrawString(data.Timestamp, timestampFont, printBrush, centerX - (timestampSize.Width / 2), yPos);
                }

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
        /// Load printer image from disk or cache
        /// </summary>
        private static Image LoadPrinterImage()
        {
            lock (_cacheLock)
            {
                // Return cached image if available
                if (_cachedPrinterImage != null)
                {
                    return _cachedPrinterImage;
                }

                // Try to load image from disk
                try
                {
                    string appPath = AppDomain.CurrentDomain.BaseDirectory;
                    string imagePath = System.IO.Path.Combine(appPath, "Assets", "printer_image.png");

                    if (System.IO.File.Exists(imagePath))
                    {
                        _cachedPrinterImage = Image.FromFile(imagePath);
                        System.Diagnostics.Debug.WriteLine("[ReceiptPrinter] Image loaded and cached successfully");
                        return _cachedPrinterImage;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Image not found at: {imagePath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReceiptPrinter] Error loading image: {ex.Message}");
                }

                return null;
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
