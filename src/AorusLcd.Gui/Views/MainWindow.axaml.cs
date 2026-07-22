using System.Linq;
using System.Threading.Tasks;
using AorusLcd.Gui.ViewModels;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AorusLcd.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ImagePicker = () => PickFileAsync("Choose an image for the LCD",
                ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"]);
            vm.GifPicker = () => PickFileAsync("Choose a GIF for the LCD", ["*.gif"]);
        }
    }

    private async Task<string?> PickFileAsync(string title, string[] patterns)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Files") { Patterns = patterns }],
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}