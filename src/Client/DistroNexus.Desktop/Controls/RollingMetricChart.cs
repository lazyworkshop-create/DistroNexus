using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using DistroNexus.Core.Models;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSystemColors = System.Windows.SystemColors;

namespace DistroNexus.Desktop.Controls;

/// <summary>A small dependency-free rolling line chart for the monitor tab.</summary>
public sealed class RollingMetricChart : FrameworkElement
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(IEnumerable), typeof(RollingMetricChart), new FrameworkPropertyMetadata(null, OnSamplesChanged));
    public static readonly DependencyProperty MetricProperty = DependencyProperty.Register(
        nameof(Metric), typeof(RollingMetric), typeof(RollingMetricChart), new FrameworkPropertyMetadata(RollingMetric.CpuPercent, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(System.Windows.Media.Brush), typeof(RollingMetricChart), new FrameworkPropertyMetadata(WpfBrushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    private INotifyCollectionChanged? _notifyingSamples;
    public IEnumerable? Samples { get => (IEnumerable?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public RollingMetric Metric { get => (RollingMetric)GetValue(MetricProperty); set => SetValue(MetricProperty, value); }
    public System.Windows.Media.Brush Stroke { get => (System.Windows.Media.Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 160 : availableSize.Width, 72);
    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(RenderSize);
        drawingContext.DrawRectangle(WpfBrushes.Transparent, new WpfPen(WpfSystemColors.ControlDarkBrush, 0.5), rect);
        var values = Samples?.OfType<MonitoringSample>().Select(sample => GetMetricValue(sample, Metric)).Where(x => x is not null).Select(x => x!.Value).TakeLast(60).ToArray() ?? [];
        if (values.Length < 2 || rect.Width < 2 || rect.Height < 2) return;
        var ceiling = Metric is RollingMetric.CpuPercent or RollingMetric.MemoryPercent or RollingMetric.FilesystemPercent ? 100d : Math.Max(1d, values.Max());
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i < values.Length; i++)
            {
                var x = rect.Left + (rect.Width - 2) * i / (values.Length - 1d) + 1;
                var y = rect.Bottom - 1 - Math.Clamp(values[i] / ceiling, 0, 1) * (rect.Height - 2);
                if (i == 0) context.BeginFigure(new WpfPoint(x, y), false, false); else context.LineTo(new WpfPoint(x, y), true, false);
            }
        }
        geometry.Freeze();
        drawingContext.DrawGeometry(null, new WpfPen(Stroke, 1.5), geometry);
    }
    protected override void OnVisualParentChanged(DependencyObject? oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        if (VisualParent is null) Detach();
    }
    private static void OnSamplesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var chart = (RollingMetricChart)dependencyObject;
        chart.Detach();
        chart._notifyingSamples = args.NewValue as INotifyCollectionChanged;
        if (chart._notifyingSamples is not null) chart._notifyingSamples.CollectionChanged += chart.OnSamplesCollectionChanged;
        chart.InvalidateVisual();
    }
    private void OnSamplesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    private void Detach()
    {
        if (_notifyingSamples is not null) _notifyingSamples.CollectionChanged -= OnSamplesCollectionChanged;
        _notifyingSamples = null;
    }
    /// <summary>Projects the monitor's core metrics without coupling the sampling model to WPF.</summary>
    public static double? GetMetricValue(MonitoringSample sample, RollingMetric metric) => metric switch
    {
        RollingMetric.CpuPercent => sample.CpuPercent,
        RollingMetric.MemoryPercent when sample.MemoryUsedBytes is not null && sample.MemoryTotalBytes > 0 => sample.MemoryUsedBytes.Value * 100d / sample.MemoryTotalBytes.Value,
        RollingMetric.FilesystemPercent when sample.FilesystemUsedBytes is not null && sample.FilesystemTotalBytes > 0 => sample.FilesystemUsedBytes.Value * 100d / sample.FilesystemTotalBytes.Value,
        RollingMetric.NetworkBytesPerSecond => (sample.NetworkReceiveBytesPerSecond ?? 0) + (sample.NetworkTransmitBytesPerSecond ?? 0),
        _ => null
    };
}

public enum RollingMetric { CpuPercent, MemoryPercent, FilesystemPercent, NetworkBytesPerSecond }
