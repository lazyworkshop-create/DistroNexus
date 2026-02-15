using System;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Service interface for page navigation within the application.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the currently displayed page type.
    /// </summary>
    Type? CurrentPage { get; }

    /// <summary>
    /// Event raised when navigation occurs.
    /// </summary>
    event EventHandler<NavigationEventArgs>? Navigated;

    /// <summary>
    /// Navigates to the specified page type.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to.</typeparam>
    void NavigateTo<TPage>() where TPage : class;

    /// <summary>
    /// Navigates to the specified page type with a parameter.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to.</typeparam>
    /// <param name="parameter">The navigation parameter.</param>
    void NavigateTo<TPage>(object parameter) where TPage : class;

    /// <summary>
    /// Navigates to the specified page type.
    /// </summary>
    /// <param name="pageType">The type of page to navigate to.</param>
    void NavigateTo(Type pageType);

    /// <summary>
    /// Navigates to the specified page type with a parameter.
    /// </summary>
    /// <param name="pageType">The type of page to navigate to.</param>
    /// <param name="parameter">The navigation parameter.</param>
    void NavigateTo(Type pageType, object parameter);

    /// <summary>
    /// Navigates back to the previous page.
    /// </summary>
    /// <returns>True if navigation was successful, false otherwise.</returns>
    bool GoBack();

    /// <summary>
    /// Gets a value indicating whether backward navigation is possible.
    /// </summary>
    bool CanGoBack { get; }
}

/// <summary>
/// Event arguments for navigation events.
/// </summary>
public class NavigationEventArgs : EventArgs
{
    /// <summary>
    /// Gets the page type that was navigated to.
    /// </summary>
    public Type PageType { get; }

    /// <summary>
    /// Gets the navigation parameter, if any.
    /// </summary>
    public object? Parameter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationEventArgs"/> class.
    /// </summary>
    /// <param name="pageType">The page type navigated to.</param>
    /// <param name="parameter">The navigation parameter.</param>
    public NavigationEventArgs(Type pageType, object? parameter = null)
    {
        PageType = pageType ?? throw new ArgumentNullException(nameof(pageType));
        Parameter = parameter;
    }
}
