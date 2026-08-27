using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TextPrinter
{
    class PrintConfig
    {
        public string FontName { get; set; } = "Arial";
        public float FontSize { get; set; } = 12;
        public bool FontBold { get; set; } = false;
        public float LeftMargin { get; set; } = 10;
        public float RightMargin { get; set; } = 10;
        public float TopMargin { get; set; } = 10;
        public float LineSpacing { get; set; } = 2;
    }

    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: TextPrinter.exe <printerName> <textOrFilePath>");
                return 1;
            }

            string printerName = args[0];
            string textOrPath = args[1];
            string text;

            if (File.Exists(textOrPath))
            {
                text = File.ReadAllText(textOrPath, Encoding.UTF8);
            }
            else
            {
                text = textOrPath;
            }

            // Load configuration
            PrintConfig config = LoadConfig();

            try
            {
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
                float topMargin = config.TopMargin;
                float lineHeight = font.GetHeight(e.Graphics) + config.LineSpacing;
                float y = topMargin;
                int count = 0;

                using (StringReader reader = new StringReader(text))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        e.Graphics.DrawString(line, font, Brushes.Black, leftMargin, y);
                        y += lineHeight;
                        count++;

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
    }
}