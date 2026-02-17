using System.Windows;

namespace FreeFlow.Views;

public partial class RecordingOverlay : Window
{
    public RecordingOverlay()
    {
        InitializeComponent();
        this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
        this.Top = 10;
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
        if (status != "Recording...")
        {
            RecordingDot.Visibility = Visibility.Collapsed;
        }
        else
        {
            RecordingDot.Visibility = Visibility.Visible;
        }
    }
}
