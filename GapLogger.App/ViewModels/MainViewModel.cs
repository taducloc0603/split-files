using System.ComponentModel;
using System.Runtime.CompilerServices;
using GapLogger.Commands;
using GapLogger.Services;
using System.Windows.Threading;

namespace GapLogger.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly GapLoggingSession _session;
    private readonly DispatcherTimer _statusTimer;

    private string _mapNameA = "";
    private string _mapNameB = "";
    private bool _isRunning;
    private string _status = "Idle";

    public MainViewModel(GapLoggingSession session)
    {
        _session = session;
        StartCommand = new AsyncRelayCommand(OnStart, () => !_isRunning && !string.IsNullOrWhiteSpace(_mapNameA) && !string.IsNullOrWhiteSpace(_mapNameB));
        StopCommand = new AsyncRelayCommand(OnStop, () => _isRunning);
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
    }

    public string MapNameA
    {
        get => _mapNameA;
        set { if (Set(ref _mapNameA, value)) StartCommand.RaiseCanExecuteChanged(); }
    }

    public string MapNameB
    {
        get => _mapNameB;
        set { if (Set(ref _mapNameB, value)) StartCommand.RaiseCanExecuteChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                StartCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }

    private async Task OnStart()
    {
        try
        {
            await _session.StartAsync(MapNameA, MapNameB);
            IsRunning = true;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Status = $"Start failed: {ex.Message}";
        }
    }

    private async Task OnStop()
    {
        try
        {
            await _session.StopAsync();
            IsRunning = false;
            Status = $"Stopped. Folder: {_session.FolderPath}";
        }
        catch (Exception ex)
        {
            Status = $"Stop failed: {ex.Message}";
        }
    }

    private void UpdateStatus()
    {
        if (!_isRunning) return;
        Status = $"Running | Folder: {_session.FolderPath} | TickA: {_session.TickARowCount} | TickB: {_session.TickBRowCount} | Gap: {_session.GapRowCount} | Events: {_session.EventRowCount}"
                 + (string.IsNullOrEmpty(_session.LastError) ? "" : $" | Err: {_session.LastError}");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}