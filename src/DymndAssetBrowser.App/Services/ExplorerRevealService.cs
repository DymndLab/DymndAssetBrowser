using System.Diagnostics;
using System.IO;

namespace DymndAssetBrowser.App.Services;

public static class ExplorerRevealService
{
    public static ProcessStartInfo CreateStartInfo(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("The source file is unavailable. Its drive may be disconnected or the file may have moved.", fullPath);
        // Launch Explorer directly, not a command shell. Quote the complete source
        // path so spaces, commas and ampersands remain part of the selected filename.
        return new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
            Arguments = "/select,\"" + fullPath + "\"",
            UseShellExecute = true
        };
    }
    public static void Reveal(string filePath) => Process.Start(CreateStartInfo(filePath));
}
