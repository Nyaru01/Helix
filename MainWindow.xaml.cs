using LocalBlast.Services;
using LocalBlast.Windows;
using LocalBlast.Models;
using Microsoft.Win32;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using System.IO;

namespace LocalBlast;

public partial class MainWindow : FluentWindow
{
    private readonly LocalBlastAnalysisService _analysisService = new();
    private CancellationTokenSource? _searchCts;
    private ResultsWindow? _resultsWindow;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private string SelectedProgram => (ProgramComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "blastn";

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = UserSettingsService.Load();
        FolderTextBox.Text = settings.FolderPath;
        IdentityTextBox.Text = settings.MinimumIdentity;
        CoverageTextBox.Text = settings.MinimumCoverage;
        EvalueTextBox.Text = settings.Evalue;
        ProgramComboBox.SelectedItem = ProgramComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), settings.Program, StringComparison.OrdinalIgnoreCase))
            ?? ProgramComboBox.Items.OfType<ComboBoxItem>().First();
        UpdateFolderInfo();
        if (!string.IsNullOrWhiteSpace(settings.FolderPath))
            StatusText.Text = "Last analysis settings restored.";
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        UserSettingsService.Save(new UserSettings(
            FolderTextBox.Text.Trim(), SelectedProgram, IdentityTextBox.Text.Trim(),
            CoverageTextBox.Text.Trim(), EvalueTextBox.Text.Trim()));
    }

    private async void LoadQuery_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose query FASTA",
            Filter = "FASTA files|*.fa;*.fasta;*.fna;*.ffn;*.faa;*.fas;*.fsa|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            QueryTextBox.Text = await File.ReadAllTextAsync(dialog.FileName);
            QueryInfoText.Text = Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open query", ex.Message);
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose FASTA collection",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            FolderTextBox.Text = dialog.FolderName;
            UpdateFolderInfo();
        }
    }

    private void FolderTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdateFolderInfo();

    private void FileDrop_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void QueryTextBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
        if (Directory.Exists(files[0])) { await ShowErrorAsync("Query FASTA", "Drop a FASTA file here, not a folder."); return; }
        try { QueryTextBox.Text = await File.ReadAllTextAsync(files[0]); QueryInfoText.Text = Path.GetFileName(files[0]); }
        catch (Exception ex) { await ShowErrorAsync("Could not open query", ex.Message); }
    }

    private void FolderTextBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
        var candidate = Directory.Exists(files[0]) ? files[0] : Path.GetDirectoryName(files[0]);
        if (string.IsNullOrWhiteSpace(candidate)) return;
        FolderTextBox.Text = candidate;
        UpdateFolderInfo();
    }

    private void ProgramComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ProgramHelpText.Text = SelectedProgram switch
        {
            "tblastn" => "tblastn: protein query → nucleotide FASTA files",
            "blastp" => "blastp: protein query → protein FASTA files",
            _ => "blastn: nucleotide query → nucleotide FASTA files"
        };
        UpdateFolderInfo();
    }

    private void UpdateFolderInfo()
    {
        var folder = FolderTextBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            FolderInfoText.Text = "";
            return;
        }

        try
        {
            var (_, subjectType) = ExpectedTypes();
            var (selected, ignored) = FastaService.DiscoverFastaFiles(folder, subjectType);
            FolderInfoText.Text = $"{selected.Count} compatible FASTA file(s) detected" +
                                  (ignored.Count > 0 ? $" · {ignored.Count} incompatible FASTA-like file(s) ignored" : "");
        }
        catch (Exception ex)
        {
            FolderInfoText.Text = ex.Message;
        }
    }

    private async void CheckEnvironment_Click(object sender, RoutedEventArgs e)
    {
        EnvironmentText.Text = "Checking…";
        try
        {
            var (ok, message) = await _analysisService.CheckEnvironmentAsync(SelectedProgram);
            EnvironmentText.Text = ok ? "Bundled BLAST ready" : "BLAST unavailable";
            if (ok)
                await ShowInfoAsync("Environment OK", message);
            else
                await ShowErrorAsync("BLAST unavailable", message);
        }
        catch (Exception ex)
        {
            EnvironmentText.Text = "Check failed";
            await ShowErrorAsync("Environment check", ex.Message);
        }
    }

    private async void PrepareIndexes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var analysis = _analysisService.Prepare(new AnalysisRequest(SelectedProgram, QueryTextBox.Text, FolderTextBox.Text.Trim(), 0, 0, 1));
            SearchProgressBar.Maximum = analysis.GenomeFiles.Count;
            SearchProgressBar.Value = 0;
            StatusText.Text = "Preparing local BLAST indexes…";
            var progress = new Progress<SearchProgress>(p => { SearchProgressBar.Value = p.Completed; StatusText.Text = $"Index {p.Completed}/{p.Total} · {p.CurrentFile}"; });
            await _analysisService.PrepareIndexesAsync(analysis, progress, CancellationToken.None);
            StatusText.Text = "Local indexes ready. Subsequent analyses of unchanged files will be faster.";
        }
        catch (Exception ex) { AppLogger.Error("Could not prepare local indexes.", ex); await ShowErrorAsync("Prepare local indexes", ex.Message); }
    }

    private async void RunBlast_Click(object sender, RoutedEventArgs e)
    {
        if (_searchCts is not null)
            return;

        try
        {
            var folder = FolderTextBox.Text.Trim();
            if (!double.TryParse(IdentityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var identity) || identity is < 0 or > 100)
                throw new InvalidOperationException("Minimum identity must be between 0 and 100.");
            if (!double.TryParse(CoverageTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var coverage) || coverage is < 0 or > 100)
                throw new InvalidOperationException("Minimum query coverage must be between 0 and 100.");
            if (!double.TryParse(EvalueTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var evalue) || evalue <= 0)
                throw new InvalidOperationException("Enter a valid E-value such as 1e-10.");

            var analysis = _analysisService.Prepare(new Models.AnalysisRequest(
                SelectedProgram, QueryTextBox.Text, folder, identity, coverage, evalue));

            var queryLabel = analysis.QueryHeader.StartsWith('>') ? analysis.QueryHeader[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "query" : "query";
            _resultsWindow = new ResultsWindow(queryLabel, SelectedProgram, identity, coverage) { Owner = this };
            _resultsWindow.Show();

            _searchCts = new CancellationTokenSource();
            RunButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            SearchProgressBar.Maximum = analysis.GenomeFiles.Count;
            SearchProgressBar.Value = 0;
            StatusText.Text = $"{analysis.GenomeFiles.Count} compatible FASTA files detected" + (analysis.IgnoredFiles.Count > 0 ? $" · {analysis.IgnoredFiles.Count} ignored" : "");

            var progress = new Progress<Models.SearchProgress>(p =>
            {
                SearchProgressBar.Maximum = p.Total;
                SearchProgressBar.Value = p.Completed;
                StatusText.Text = $"{p.Completed}/{p.Total} · {p.CurrentFile}";
                if (p.Hit is not null)
                    _resultsWindow?.AddHit(p.Hit);
            });

            await _analysisService.SearchAsync(analysis, progress, _searchCts.Token);
            // Results can be closed while a search is still running. History must never turn
            // an otherwise successful BLAST search into an application error.
            var completedHits = _resultsWindow?.Snapshot();
            if (completedHits is not null)
            {
                try
                {
                    await AnalysisHistoryService.SaveAsync(new AnalysisHistoryEntry(DateTimeOffset.Now, queryLabel,
                        SelectedProgram, identity, coverage, folder, completedHits.ToList()));
                }
                catch (Exception historyException)
                {
                    AppLogger.Error("The search succeeded but its history entry could not be saved.", historyException);
                }
            }
            StatusText.Text = "Search finished. Double-click a result to inspect the BLAST alignment.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Search cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Search failed.";
            AppLogger.Error("The search could not be started or completed.", ex);
            await ShowErrorAsync("LocalBlast", ex.Message);
        }
        finally
        {
            _searchCts?.Dispose();
            _searchCts = null;
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;

        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling…";
        _searchCts?.Cancel();
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var item = AnalysisHistoryService.Load().FirstOrDefault();
        if (item is null) { _ = ShowInfoAsync("Analysis history", "No completed analysis has been saved yet."); return; }
        var window = new ResultsWindow(item.QueryLabel, item.Program, item.MinimumIdentity, item.MinimumCoverage) { Owner = this };
        foreach (var hit in item.Hits) window.AddHit(hit);
        window.Show();
        StatusText.Text = $"Reopened analysis from {item.CreatedAt.LocalDateTime:g}.";
    }

    private async void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var (ok, message) = await _analysisService.CheckEnvironmentAsync(SelectedProgram);
        await ShowInfoAsync("Helix Blast diagnostics", $"Application: Helix Blast {typeof(MainWindow).Assembly.GetName().Version}\nBLAST: {(ok ? "ready" : "unavailable")}\nLogs: {AppLogger.LogDirectory}\nHistory: {AnalysisHistoryService.GetStoragePath()}\n\n{message}");
    }

    private (SequenceType Query, SequenceType Subject) ExpectedTypes() => SelectedProgram switch
    {
        "tblastn" => (SequenceType.Protein, SequenceType.Nucleotide),
        "blastp" => (SequenceType.Protein, SequenceType.Protein),
        _ => (SequenceType.Nucleotide, SequenceType.Nucleotide)
    };

    private async Task ShowInfoAsync(string title, string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await box.ShowDialogAsync();
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await box.ShowDialogAsync();
    }
}
