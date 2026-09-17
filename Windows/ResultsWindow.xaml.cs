using LocalBlast.Models;
using LocalBlast.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalBlast.Windows;

public partial class ResultsWindow : FluentWindow
{
    private readonly ObservableCollection<BlastHit> _hits = new();
    private readonly double _minIdentity;
    private readonly double _minCoverage;
    private readonly string _queryLabel;
    private readonly string _program;
    private ICollectionView? _view;

    public ResultsWindow(string queryLabel, string program, double minIdentity, double minCoverage)
    {
        InitializeComponent();
        _minIdentity = minIdentity;
        _minCoverage = minCoverage;
        _queryLabel = queryLabel;
        _program = program;
        _view = CollectionViewSource.GetDefaultView(_hits);
        _view.Filter = MatchesFilter;
        ResultsGrid.ItemsSource = _view;
        ResultsTitleBar.Title = $"BLAST results · {queryLabel}";
        ThresholdText.Text = $"identity ≥ {minIdentity:g}% · coverage ≥ {minCoverage:g}%";
        UpdateSummary();
    }

    public void AddHit(BlastHit hit)
    {
        _hits.Add(hit);
        UpdateSummary();
    }

    public IReadOnlyList<BlastHit> Snapshot() => _hits.ToList();

    private bool MatchesFilter(object item)
    {
        if (item is not BlastHit hit) return false;
        var status = (StatusFilterComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrWhiteSpace(status) && status != "All statuses" && hit.Status != status) return false;
        var term = SearchTextBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(term) || string.Join(' ', hit.GenomeName, hit.GenomeFile, hit.SubjectId, hit.Status).Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private void FilterChanged(object sender, System.Windows.RoutedEventArgs e) => _view?.Refresh();

    private void UpdateSummary()
    {
        var present = _hits.Count(h => h.Status == "Present");
        var below = _hits.Count(h => h.Status == "Below thresholds");
        var noHit = _hits.Count(h => h.Status == "No hit");
        var errors = _hits.Count(h => h.Status == "Error");
        var detectionRate = _hits.Count == 0 ? 0 : present * 100.0 / _hits.Count;
        TotalMetricText.Text = _hits.Count.ToString();
        PresentMetricText.Text = present.ToString();
        DetectionMetricText.Text = $"{detectionRate:F0}%";
        BelowMetricText.Text = below.ToString();
        SummaryText.Text = $"{_hits.Count} analysed · {present} present · {below} below thresholds · {noHit} no hit" + (errors > 0 ? $" · {errors} errors" : "");
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not BlastHit hit)
            return;

        new AlignmentWindow(hit) { Owner = this }.ShowDialog();
    }

    private async void ExportCsv_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export BLAST results",
            Filter = "CSV files|*.csv",
            FileName = $"HelixBlast_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await ResultsExportService.ExportCsvAsync(dialog.FileName, _queryLabel, _program, _minIdentity, _minCoverage, _hits);
            await ShowInfoAsync("Export complete", $"Results saved to:\n{dialog.FileName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not export results.", ex);
            await ShowInfoAsync("Export failed", ex.Message);
        }
    }

    private async void ExportHtml_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Helix Blast HTML report",
            Filter = "HTML files|*.html",
            FileName = $"HelixBlast_Report_{DateTime.Now:yyyyMMdd_HHmm}.html"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await ResultsExportService.ExportHtmlAsync(dialog.FileName, _queryLabel, _program, _minIdentity, _minCoverage, _hits);
            await ShowOpenReportAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Could not export the HTML report.", ex);
            await ShowInfoAsync("Report export failed", ex.Message);
        }
    }

    private async void ExportPdf_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export Helix Blast PDF report", Filter = "PDF files|*.pdf", FileName = $"HelixBlast_Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await ResultsExportService.ExportPdfAsync(dialog.FileName, _queryLabel, _program, _minIdentity, _minCoverage, _hits);
            await ShowInfoAsync("Report ready", $"PDF report saved to:\n{dialog.FileName}");
        }
        catch (Exception ex) { AppLogger.Error("Could not export the PDF report.", ex); await ShowInfoAsync("PDF export failed", ex.Message); }
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox { Owner = this, WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner, Title = title, Content = message, CloseButtonText = "OK" };
        await box.ShowDialogAsync();
    }

    private async Task ShowOpenReportAsync(string path)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Title = "Report ready",
            Content = $"HTML report saved to:\n{path}",
            PrimaryButtonText = "Open report",
            CloseButtonText = "Close"
        };
        if (await box.ShowDialogAsync() == MessageBoxResult.Primary)
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
