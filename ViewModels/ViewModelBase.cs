using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Minimal INotifyPropertyChanged base class. No external MVVM toolkit is used so the
/// project only depends on Avalonia + csDBPF (+ csDBPF's own SixLabors.ImageSharp dependency).
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
