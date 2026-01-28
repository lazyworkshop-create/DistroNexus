using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts step numbers to visibility based on current step.
/// </summary>
public class StepToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int currentStep || parameter is not string param)
            return Visibility.Collapsed;

        // Handle special cases
        if (param == "NotFinal")
            return currentStep < 4 ? Visibility.Visible : Visibility.Collapsed;
        
        if (param == "Final")
            return currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;

        // Normal step comparison
        if (int.TryParse(param, out int targetStep))
            return currentStep == targetStep ? Visibility.Visible : Visibility.Collapsed;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts step numbers to badge appearance based on current step.
/// </summary>
public class StepToBadgeAppearanceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int currentStep || parameter is not string param)
            return Wpf.Ui.Controls.ControlAppearance.Secondary;

        if (!int.TryParse(param, out int targetStep))
            return Wpf.Ui.Controls.ControlAppearance.Secondary;

        if (currentStep > targetStep)
            return Wpf.Ui.Controls.ControlAppearance.Success;
        else if (currentStep == targetStep)
            return Wpf.Ui.Controls.ControlAppearance.Primary;
        else
            return Wpf.Ui.Controls.ControlAppearance.Secondary;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to validation color (green for valid, red for invalid).
/// </summary>
public class BoolToValidationColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts integer to boolean for radio button binding.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string param && int.TryParse(param, out int targetValue))
        {
            return intValue == targetValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is string param && int.TryParse(param, out int targetValue))
        {
            return targetValue;
        }
        return Binding.DoNothing;
    }
}


/// <summary>
/// Converts string to boolean (true if not null or empty).
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string to visibility (Visible if not null or empty, Collapsed otherwise).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = !string.IsNullOrEmpty(value as string);
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to button appearance (Primary for true, Secondary for false).
/// </summary>
public class BoolToPrimaryAppearanceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPrimary && isPrimary)
        {
            return Wpf.Ui.Controls.ControlAppearance.Primary;
        }
        return Wpf.Ui.Controls.ControlAppearance.Secondary;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
