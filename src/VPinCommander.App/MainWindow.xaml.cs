using System.Windows;
using VPinCommander.App.ViewModels;

namespace VPinCommander.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
