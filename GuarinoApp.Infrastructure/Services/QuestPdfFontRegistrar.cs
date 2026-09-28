using QuestPDF.Drawing;
using QuestPDF.Infrastructure; // <- para LicenseType
using System.Diagnostics;

namespace GuarinoApp.Infrastructure.Services
    {
    public static class QuestPdfFontRegistrar
        {
        public static void RegisterFonts()
            {
            QuestPDF.Settings.License = LicenseType.Community; // <-
            try
                {
                string fontsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts"));

                if (!Directory.Exists(fontsDir))
                    return;

                foreach (string file in Directory.GetFiles(fontsDir, "*.ttf"))
                    {
                    try
                        {
                        using (FileStream stream = File.OpenRead(file))
                            {
                            FontManager.RegisterFont(stream);
                            }
                        }
                    catch (Exception ex)
                        {
                        Debug.WriteLine($"Error al registrar la fuente '{file}': {ex.Message}");
                        Console.Error.WriteLine($"Error al registrar la fuente '{file}': {ex.Message}");
                        }
                    }
                }
            catch (Exception ex)
                {
                Debug.WriteLine($"Error al acceder al directorio de fuentes: {ex.Message}");
                Console.Error.WriteLine($"Error al acceder al directorio de fuentes: {ex.Message}");
                }
            }
        }
    }
