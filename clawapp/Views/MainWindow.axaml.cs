using Avalonia.Controls;
using clawapp.ViewModels;

namespace clawapp.Views;

public partial class MainWindow : Window
{
    /// <summary>
    /// Constructor for DI - called by the application startup code.
    /// </summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// Parameterless constructor for XAML designer/loader compatibility.
    /// This is called by the XAML runtime when instantiating the window.
    /// The DataContext will be set by the DI container after instantiation.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}