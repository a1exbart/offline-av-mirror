using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OfflineAvMirror;

/// <summary>
/// What the program does, what it does not do, and what happens to the key.
/// </summary>
/// <remarks>
/// It outlives the job it was written for: whoever finds it on a USB drive a
/// year from now should be able to answer those three questions in ten seconds.
/// </remarks>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // Height is measured against the screen, never fixed: on an 800x600
        // display a 700-pixel window puts its close button off the bottom edge.
        if (Screens.Primary?.WorkingArea is { } area)
        {
            Height = Math.Min(Height, area.Height * 0.92);
            Width = Math.Min(Width, area.Width * 0.95);
        }

        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();
    }
}
