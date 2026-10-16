using Avalonia.Controls;
using CherryHud.ViewModels;

namespace CherryHud.Views;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();
    }

    public UpdateWindow(UpdateViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseAction = Close;
    }
}
