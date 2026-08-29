using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TextPrinter
{
    class PrintConfig
    {
        //public string FontName { get; set; } = "Arial";
        public string FontName { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 12;
        public bool FontBold { get; set; } = false;
        public float LeftMargin { get; set; } = 10;
        public float RightMargin { get; set; } = 10;
        public float TopMargin { get; set; } = 10;
        public float LineSpacing { get; set; } = 2;
    }

    class Program
    {
        // Covers Arabic, Arabic Supplement, Arabic Extended-A, and the
        // Arabic Presentation Forms blocks (common in some fonts/legacy text).
        private static readonly Regex ArabicRegex = new Regex(
            @"[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]");

        // Extensions treated as "print this as an image" instead of text.
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: TextPrinter.exe <printerName> <textOrFilePathOrImagePath> [paperSizeName]");
                return 1;
            }

            string printerName = args[0];
            string textOrPath = args[1];
            string paperSizeName = args.Length >= 3 ? args[2] : null;

            try
            {
                if (File.Exists(textOrPath) && Array.IndexOf(ImageExtensions, Path.GetExtension(textOrPath).ToLowerInvariant()) >= 0)
                {
                    PrintImageToPrinter(printerName, textOrPath, paperSizeName);
                    return 0;
                }

                string text;
                if (File.Exists(textOrPath))
                {
                    text = File.ReadAllText(textOrPath, Encoding.UTF8);
                }
                else
                {
                    text = textOrPath;
                }

                PrintConfig config = LoadConfig();
                PrintToPrinter(printerName, text, config);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Print error: {ex.Message}");
                return 1;
            }
        }

        static PrintConfig LoadConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextPrinter.config.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<PrintConfig>(json) ?? new PrintConfig();
            }
            return new PrintConfig();
        }

        static void PrintToPrinter(string printerName, string text, PrintConfig config)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = printerName;

            // Build font style
            FontStyle style = config.FontBold ? FontStyle.Bold : FontStyle.Regular;
            Font font = new Font(config.FontName, config.FontSize, style);

            pd.PrintPage += (sender, e) =>
            {
                float leftMargin = config.LeftMargin;
                float rightMargin = config.RightMargin;
                float topMargin = config.TopMargin;
                float lineHeight = font.GetHeight(e.Graphics) + config.LineSpacing;
                float y = topMargin;
                float lineWidth = e.MarginBounds.Width - leftMargin - rightMargin;
                if (lineWidth < 0) lineWidth = 0;

                using (StringReader reader = new StringReader(text))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (ArabicRegex.IsMatch(line))
                        {
                            // Right-to-left paragraph direction + right alignment,
                            // so Arabic lines read naturally instead of drawing
                            // left-aligned/left-to-right like plain DrawString does.
                            RectangleF lineRect = new RectangleF(leftMargin, y, lineWidth, lineHeight);
                            using (StringFormat sf = new StringFormat(StringFormatFlags.DirectionRightToLeft))
                            {
                                // With DirectionRightToLeft, Near = right edge (reading start for RTL).
                                sf.Alignment = StringAlignment.Near;
                                e.Graphics.DrawString(line, font, Brushes.Black, lineRect, sf);
                            }
                        }
                        else
                        {
                            e.Graphics.DrawString(line, font, Brushes.Black, leftMargin, y);
                        }

                        y += lineHeight;

                        // Check for page overflow (optional)
                        if (y + lineHeight > e.MarginBounds.Bottom)
                        {
                            e.HasMorePages = true;
                            return;
                        }
                    }
                }
                e.HasMorePages = false;
            };

            pd.Print();
        }

        // Prints an image (e.g. a receipt rendered to PNG by wkhtmltoimage)
        // by scaling it to the printer's actual reported printable width
        // (e.MarginBounds) and drawing it via Graphics.DrawImage — the same
        // PrintDocument/PrinterSettings mechanism the text path above uses,
        // which is already known to position correctly on the physical
        // printer. This avoids depending on any external PDF-printing
        // tool's own paper/DEVMODE handling.
        static void PrintImageToPrinter(string printerName, string imagePath, string paperSizeName)
        {
            // Read into memory first rather than Image.FromFile() directly,
            // so the file isn't left locked for the process lifetime.
            byte[] bytes = File.ReadAllBytes(imagePath);
            using (MemoryStream ms = new MemoryStream(bytes))
            using (Image image = Image.FromStream(ms))
            {
                PrintDocument pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = printerName;

                // Explicitly select the driver's own registered paper size
                // (by exact name, e.g. "80mm Receipt") rather than trusting
                // whatever DefaultPageSettings.PaperSize happens to already
                // be — that ambient default is not reliably the one you
                // configured in Printing Preferences, and a mismatch here
                // (e.g. falling back to Letter/A4) makes the driver shrink
                // the whole page to fit the real narrow paper, which is
                // what caused the still-too-small result.
                if (!string.IsNullOrWhiteSpace(paperSizeName))
                {
                    foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
                    {
                        if (string.Equals(ps.PaperName, paperSizeName, StringComparison.OrdinalIgnoreCase))
                        {
                            pd.DefaultPageSettings.PaperSize = ps;
                            break;
                        }
                    }
                }

                // .NET defaults PrintDocument margins to 1 inch on all four
                // sides. On an 80mm (~3.15in) wide receipt, that alone eats
                // ~2 of those 3.15 inches before anything is even drawn,
                // leaving MarginBounds only ~1.15in wide — which is exactly
                // why the image was scaled down to a tiny fraction of the
                // page. Zero them out so MarginBounds reflects (close to)
                // the printer's actual full printable width.
                pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                pd.PrintPage += (sender, e) =>
                {
                    float targetWidth = e.MarginBounds.Width;
                    float scale = targetWidth / image.Width;
                    float targetHeight = image.Height * scale;

                    RectangleF destRect = new RectangleF(
                        e.MarginBounds.Left, e.MarginBounds.Top, targetWidth, targetHeight);

                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    e.Graphics.DrawImage(image, destRect);
                    e.HasMorePages = false;
                };

                pd.Print();
            }
        }
    }
}
