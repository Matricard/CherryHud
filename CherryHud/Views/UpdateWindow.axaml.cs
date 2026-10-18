using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CherryHud.ViewModels;

namespace CherryHud.Views;

public partial class UpdateWindow : Window
{
    private static readonly IBrush ActiveBrush = Brush.Parse("#9E030408");
    private static readonly IBrush InactiveBrush = Brush.Parse("#F613141E");

    public UpdateWindow()
    {
        InitializeComponent();

        Activated += (_, _) =>
        {
            WindowBackgroundBorder.Background = ActiveBrush;
        };

        Deactivated += (_, _) =>
        {
            WindowBackgroundBorder.Background = InactiveBrush;
        };

        AddHandler(PointerPressedEvent, (sender, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var pt = e.GetCurrentPoint(this).Position;

                // Zone des feux tricolores macOS en haut à gauche (x: 0..80, y: 0..38)
                if (pt.Y <= 38 && pt.X <= 80)
                {
                    return;
                }

                if (e.Source is Button || (e.Source as Avalonia.Visual)?.FindAncestorOfType<Button>() != null ||
                    e.Source is TextBox || (e.Source as Avalonia.Visual)?.FindAncestorOfType<TextBox>() != null)
                {
                    return;
                }

                BeginMoveDrag(e);
            }
        }, RoutingStrategies.Bubble);
    }

    public UpdateWindow(UpdateViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseAction = Close;
    }
}
