using System;
using System.Threading;
using System.Windows;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Controls;

/// <summary>
/// A reusable progress dialog for showing operation progress.
/// </summary>
public partial class ProgressDialog : FluentWindow
{
    private CancellationTokenSource? _cts;

    #region Dependency Properties

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ProgressDialog),
            new PropertyMetadata("Please wait..."));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressDialog),
            new PropertyMetadata(0.0, OnProgressChanged));

    public static readonly DependencyProperty ProgressTextProperty =
        DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(ProgressDialog),
            new PropertyMetadata("0%"));

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(nameof(StatusMessage), typeof(string), typeof(ProgressDialog),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(ProgressDialog),
            new PropertyMetadata(false, OnIsIndeterminateChanged));

    public static readonly DependencyProperty IsCancellableProperty =
        DependencyProperty.Register(nameof(IsCancellable), typeof(bool), typeof(ProgressDialog),
            new PropertyMetadata(true));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the main message displayed in the dialog.
    /// </summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the progress value (0-100).
    /// </summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Gets or sets the progress text displayed below the progress bar.
    /// </summary>
    public string ProgressText
    {
        get => (string)GetValue(ProgressTextProperty);
        set => SetValue(ProgressTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the status message displayed at the bottom.
    /// </summary>
    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the progress is indeterminate.
    /// </summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the operation can be cancelled.
    /// </summary>
    public bool IsCancellable
    {
        get => (bool)GetValue(IsCancellableProperty);
        set => SetValue(IsCancellableProperty, value);
    }

    /// <summary>
    /// Gets the cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken => _cts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Gets whether cancellation has been requested.
    /// </summary>
    public bool IsCancellationRequested => _cts?.IsCancellationRequested ?? false;

    #endregion

    /// <summary>
    /// Event raised when the user requests cancellation.
    /// </summary>
    public event EventHandler? CancellationRequested;

    public ProgressDialog()
    {
        InitializeComponent();
        _cts = new CancellationTokenSource();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressDialog dialog && e.NewValue is double progress)
        {
            dialog.ProgressText = $"{progress:F0}%";
        }
    }

    private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProgressDialog dialog && e.NewValue is bool isIndeterminate)
        {
            dialog.IndeterminateProgress.Visibility = isIndeterminate ? Visibility.Visible : Visibility.Collapsed;
            dialog.DeterminateProgress.Visibility = isIndeterminate ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancellationRequested?.Invoke(this, EventArgs.Empty);
        CancelButton.IsEnabled = false;
        CancelButton.Content = "Cancelling...";
    }

    /// <summary>
    /// Updates the progress and status message.
    /// </summary>
    /// <param name="progress">Progress value (0-100).</param>
    /// <param name="statusMessage">Status message to display.</param>
    public void UpdateProgress(double progress, string? statusMessage = null)
    {
        Progress = progress;
        if (statusMessage != null)
        {
            StatusMessage = statusMessage;
        }
    }

    /// <summary>
    /// Creates a progress reporter for async operations.
    /// </summary>
    /// <returns>An IProgress instance that updates the dialog.</returns>
    public IProgress<(double Percentage, string Message)> CreateProgressReporter()
    {
        return new Progress<(double Percentage, string Message)>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                Progress = p.Percentage;
                StatusMessage = p.Message;
            });
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Dispose();
        _cts = null;
        base.OnClosed(e);
    }
}
