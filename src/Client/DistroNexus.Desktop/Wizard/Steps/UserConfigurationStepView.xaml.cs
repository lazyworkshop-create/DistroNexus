using System.Windows.Controls;
using System.Security;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Interaction logic for UserConfigurationStepView.xaml
/// </summary>
public partial class UserConfigurationStepView : UserControl
{
    public UserConfigurationStepView()
    {
        InitializeComponent();
    }

    internal bool ValidatePassword(out string? error)
    {
        error = null;
        if (DataContext is not UserConfigurationStep step || step.Context is null)
            return true;

        using var password = PasswordInput.SecurePassword.Copy();
        using var confirmation = ConfirmPasswordInput.SecurePassword.Copy();
        if (password.Length == 0 && confirmation.Length == 0) return true;
        if (password.Length < 4) { error = Properties.Resources.ErrorPasswordMinLength; return false; }
        if (!SecurePasswordAdapter.AreEqual(password, confirmation)) { error = Properties.Resources.ErrorPasswordMismatch; return false; }
        return true;
    }

    internal SecureString? TakeConfirmedPassword()
    {
        using var password = PasswordInput.SecurePassword.Copy();
        using var confirmation = ConfirmPasswordInput.SecurePassword.Copy();
        if (password.Length == 0 || !SecurePasswordAdapter.AreEqual(password, confirmation)) return null;
        var result = password.Copy();
        PasswordInput.Clear();
        ConfirmPasswordInput.Clear();
        return result;
    }

    internal void ClearPassword()
    {
        SecurePasswordAdapter.ClearPassword(PasswordInput, ConfirmPasswordInput);
    }
}

internal static class SecurePasswordAdapter
{
    internal static void ClearPassword(PasswordBox password, PasswordBox confirmation)
    {
        password.Clear();
        confirmation.Clear();
    }

    internal static bool AreEqual(SecureString first, SecureString second)
    {
        if (first.Length != second.Length) return false;
        var left = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(first);
        var right = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(second);
        try
        {
            for (var index = 0; index < first.Length; index++)
                if (System.Runtime.InteropServices.Marshal.ReadInt16(left, index * sizeof(char)) != System.Runtime.InteropServices.Marshal.ReadInt16(right, index * sizeof(char))) return false;
            return true;
        }
        finally { System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(left); System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(right); }
    }
}
