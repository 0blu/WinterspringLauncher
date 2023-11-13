using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Avalonia.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinterspringLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public LauncherLogic Logic { get; }

    public MainWindowViewModel()
    {
        Logic = new LauncherLogic(this);
    }

    [ObservableProperty]
    private LanguageHolder _language = new LanguageHolder();

    [ObservableProperty]
    private bool _inputIsAllowed = true;

    [ObservableProperty]
    public int _selectedServerIdx;

    [ObservableProperty]
    public string _thisLauncherVersion = LauncherVersion.ShortVersionString;

    [ObservableProperty]
    public string _thisLauncherVersionDetailed = LauncherVersion.DetailedVersionString;

    [ObservableProperty]
    private bool _gameFolderExists = false;

    [ObservableProperty]
    private bool _gameIsInstalled = false;

    [ObservableProperty]
    private string _gameVersion = "";

    [ObservableProperty]
    public string? _hermesPidToolTipString = null;

    [ObservableProperty]
    public bool _hermesIsRunning = false;

    [ObservableProperty]
    public string? _detectedHermesVersion;

    [ObservableProperty]
    public bool _hermesIsInstalled;
    
    public void SetHermesPid(int? pid)
    {
        // TODO How to I remove this function and just have a HermesProxyPid Property that will assign the other ones?
        HermesPidToolTipString = pid.HasValue ? $"Hermes PID: {pid.Value}" : null;
        HermesIsRunning = pid.HasValue;
    }

    public void SetHermesVersion(string? versionStr)
    {
        DetectedHermesVersion = versionStr;
        HermesIsInstalled = versionStr != null;
    }

    [ObservableProperty]
    public ObservableCollection<string> _knownServerList = new ObservableCollection<string>();

    public string LogEntriesCombined { get; private set; }

    public List<string> LogEntriesArray = new List<string>();

    public void AddLogEntry(string logEntry)
    {
        OnPropertyChanging(nameof(LogEntriesCombined));
        if (LogEntriesArray.Count > 50)
            LogEntriesArray.RemoveAt(0);
        LogEntriesArray.Add(logEntry);
        LogEntriesCombined = string.Join('\n', LogEntriesArray);
        OnPropertyChanged(nameof(LogEntriesCombined));
    }

    public class LanguageHolder
    {
        public void SetLanguage(string languageShortName)
        {
            
        }
    }


    [ObservableProperty]
    private string _progressbarText = "";

    // not observable
    private string _progressbarInternalTitle = "";

    // not observable
    private ProgressbarInternalTimeTracker _progressbarInternalTimeTracker;

    [ObservableProperty]
    private double _progressbarPercent = 0;

    [ObservableProperty]
    private IBrush _progressbarColor = Brush.Parse("#FFFFFF");

    public void SetProgressbar(string title, double progressPercent, IBrush color)
    {
        _progressbarInternalTitle = title;
        _progressbarInternalTimeTracker = new ProgressbarInternalTimeTracker();
        ProgressbarPercent = progressPercent;
        ProgressbarColor = color;
        ProgressbarText = $"{progressPercent:0}% {_progressbarInternalTitle}";
    }

    public void UpdateProgress(double progressPercent, string additionalText)
    {
        ProgressbarPercent = progressPercent;
        TimeSpan? estimatedTime = _progressbarInternalTimeTracker.GetEstimatedTimeAndUpdateRates(progressPercent);
        string timeLeft = estimatedTime.HasValue
            ? TimeSpan.FromSeconds((long) estimatedTime.Value.TotalSeconds).ToString()
            : "?".PadLeft("00:00:00".Length);

        ProgressbarText = $"{progressPercent:0}% {_progressbarInternalTitle} {additionalText} estimated time: {timeLeft}";
    }

    private class ProgressbarInternalTimeTracker
    {
        private double _lastPercent = 0;
        private DateTime? _lastUpdateTime = null;
        private int _lastProgressRatesIdx = 0;
        private readonly double?[] _lastProgressRates = new double?[15];

        public TimeSpan? GetEstimatedTimeAndUpdateRates(double percent)
        {
            var now = DateTime.Now;
            if (_lastUpdateTime != null)
            {
                TimeSpan timeDiff = now - _lastUpdateTime.Value;
                double progressDiff = percent - _lastPercent;
                double progressDiffPerSec = progressDiff / timeDiff.TotalSeconds;
                _lastProgressRates[_lastProgressRatesIdx] = progressDiffPerSec;
                _lastProgressRatesIdx = (_lastProgressRatesIdx + 1) % _lastProgressRates.Length;
            }
            _lastUpdateTime = now;
            _lastPercent = percent;

            var avgRate = _lastProgressRates.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(0).Average();
            if (avgRate == 0)
                return null;

            const double maxPercent = 100;
            double time = (maxPercent - percent) / avgRate;
            if (double.IsNaN(time))
                return null;

            return TimeSpan.FromSeconds(time);
        }
    }
}
