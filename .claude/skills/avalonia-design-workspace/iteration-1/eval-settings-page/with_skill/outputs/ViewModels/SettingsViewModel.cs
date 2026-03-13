// ViewModels/SettingsViewModel.cs
// Stub ViewModel — wire up to your preferred MVVM toolkit (CommunityToolkit.Mvvm recommended).

using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;

namespace UnrealTools.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// Exposes profile fields, dark-mode toggle, and destructive account action.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    // ---------------------------------------------------------------
    // Profile
    // ---------------------------------------------------------------

    private string _firstName = string.Empty;
    public string FirstName
    {
        get => _firstName;
        set => SetProperty(ref _firstName, value);
    }

    private string _lastName = string.Empty;
    public string LastName
    {
        get => _lastName;
        set => SetProperty(ref _lastName, value);
    }

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    // ---------------------------------------------------------------
    // Appearance
    // ---------------------------------------------------------------

    private bool _isDarkMode;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
                ApplyTheme(value);
        }
    }

    private static void ApplyTheme(bool dark)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    // ---------------------------------------------------------------
    // Commands
    // ---------------------------------------------------------------

    public ICommand SaveProfileCommand    { get; }
    public ICommand DiscardProfileCommand { get; }
    public ICommand DeleteAccountCommand  { get; }

    public SettingsViewModel()
    {
        SaveProfileCommand    = new RelayCommand(SaveProfile);
        DiscardProfileCommand = new RelayCommand(DiscardProfile);
        DeleteAccountCommand  = new RelayCommand(DeleteAccount);
    }

    private void SaveProfile()
    {
        // TODO: persist profile via service layer
    }

    private void DiscardProfile()
    {
        // TODO: reload from backing store
    }

    private void DeleteAccount()
    {
        // TODO: show confirmation dialog before destructive action
    }
}
