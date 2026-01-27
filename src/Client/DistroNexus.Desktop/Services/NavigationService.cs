using System;
using System.Collections.Generic;
using System.Windows.Controls;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DistroNexus.Desktop.Services;

/// <summary>
/// Navigation service implementation for WPF page navigation.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<Type> _navigationStack = new();
    private Frame? _frame;

    /// <inheritdoc/>
    public Type? CurrentPage { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<NavigationEventArgs>? Navigated;

    /// <inheritdoc/>
    public bool CanGoBack => _navigationStack.Count > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving pages.</param>
    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Sets the frame used for navigation.
    /// </summary>
    /// <param name="frame">The WPF frame control.</param>
    public void SetFrame(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    /// <inheritdoc/>
    public void NavigateTo<TPage>() where TPage : class
    {
        NavigateTo(typeof(TPage), null);
    }

    /// <inheritdoc/>
    public void NavigateTo<TPage>(object parameter) where TPage : class
    {
        NavigateTo(typeof(TPage), parameter);
    }

    /// <inheritdoc/>
    public void NavigateTo(Type pageType)
    {
        NavigateTo(pageType, null);
    }

    /// <inheritdoc/>
    public void NavigateTo(Type pageType, object? parameter)
    {
        if (pageType == null)
            throw new ArgumentNullException(nameof(pageType));

        if (_frame == null)
            throw new InvalidOperationException("Navigation frame has not been set. Call SetFrame first.");

        // Save current page to navigation stack
        if (CurrentPage != null)
        {
            _navigationStack.Push(CurrentPage);
        }

        // Resolve and navigate to the page
        var page = _serviceProvider.GetRequiredService(pageType);
        
        // If the page has a ViewModel, pass the parameter
        if (parameter != null && page is Page wpfPage && wpfPage.DataContext is INavigationAware navigationAware)
        {
            navigationAware.OnNavigatedTo(parameter);
        }

        _frame.Navigate(page);
        CurrentPage = pageType;

        // Raise navigation event
        Navigated?.Invoke(this, new NavigationEventArgs(pageType, parameter));
    }

    /// <inheritdoc/>
    public bool GoBack()
    {
        if (!CanGoBack || _frame == null)
            return false;

        var previousPage = _navigationStack.Pop();
        var page = _serviceProvider.GetRequiredService(previousPage);
        
        _frame.Navigate(page);
        CurrentPage = previousPage;

        // Raise navigation event
        Navigated?.Invoke(this, new NavigationEventArgs(previousPage));

        return true;
    }
}

/// <summary>
/// Interface for ViewModels that need to be notified of navigation events.
/// </summary>
public interface INavigationAware
{
    /// <summary>
    /// Called when navigated to this page with a parameter.
    /// </summary>
    /// <param name="parameter">The navigation parameter.</param>
    void OnNavigatedTo(object parameter);
}
