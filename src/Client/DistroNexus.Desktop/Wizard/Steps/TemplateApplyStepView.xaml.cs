using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Interaction logic for TemplateApplyStepView.xaml
/// </summary>
public partial class TemplateApplyStepView : UserControl
{
    private INotifyCollectionChanged? _currentCollection;

    public TemplateApplyStepView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_currentCollection != null)
        {
            _currentCollection.CollectionChanged -= OnOutputCollectionChanged;
            _currentCollection = null;
        }

        if (DataContext is TemplateApplyStep step)
        {
            _currentCollection = step.FilteredTemplateOutputLines;
            _currentCollection.CollectionChanged += OnOutputCollectionChanged;
        }
    }

    private void OnOutputCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not TemplateApplyStep step || step.FilteredTemplateOutputLines.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            var last = step.FilteredTemplateOutputLines[^1];
            TemplateOutputListBox.ScrollIntoView(last);
        });
    }
}
