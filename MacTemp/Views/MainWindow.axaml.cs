using System;
using Avalonia.Controls;
using Avalonia.Input;
using MacTemp.ViewModels;

namespace MacTemp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        };

        Closed += (_, _) =>
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
    }
}