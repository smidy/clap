using Avalonia.Controls;
using clawapp.ViewModels;

namespace clawapp.Views;

public partial class MainView : UserControl
{
    /// <summary>
    /// Constructor for DI - called by mobile platform (ISingleViewApplicationLifetime).
    /// </summary>
    public MainView(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// Parameterless constructor for XAML designer/loader compatibility.
    /// Used when embedded in MainWindow.axaml.
    /// DataContext will be inherited from parent Window.
    /// </summary>
    public MainView()
    {
        InitializeComponent();
    }
}