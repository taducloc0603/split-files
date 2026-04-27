using System.ComponentModel;
using System.Runtime.CompilerServices;
using GapLogger.Commands;
using GapLogger.Services;
using GapLogger.SharedMemory;
using System.Windows.Threading;

namespace GapLogger.ViewModels;

public enum MapStatus { Unchecked, Ok, NotFound, Invalid }

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly GapLoggingSession _session;
    private readonly DispatcherTimer _statusTimer;

    private string _mapNameA = "";
    private string _mapNameB = "";
    private MapStatus _mapStatusA = MapStatus.Unchecked;
    private MapStatus _mapStatusB = MapStatus.Unchecked;
    private string _mapInfoA = "Unchecked";
    private string _mapInfoB = "Unchecked";
    private bool _isRunning;
    private string _status = "Idle";

    public MainViewModel(GapLoggingSession session)
    {
        _session = session;

        var (a, b) = MapNamePersistence.Load();
        _mapNameA = a;
        _mapNameB = b;

        StartCommand = new AsyncRelayCommand(OnStart,
            () => !_isRunning
                  && _mapStatusA == MapStatus.Ok
                  && _mapStatusB == MapStatus.Ok);
        StopCommand = new AsyncRelayCommand(OnStop, () => _isRunning);
        CheckCommand = new AsyncRelayCommand(OnCheck,
            () => !_isRunning
                  && !string.IsNullOrWhiteSpace(_mapNameA)
                  && !string.IsNullOrWhiteSpace(_mapNameB));

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
    }

    public string MapNameA
    {
        get => _mapNameA;
        set
        {
            if (Set(ref _mapNameA, value))
            {
                ResetStatusA();
                CheckCommand.RaiseCanExecuteChanged();
                StartCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string MapNameB
    {
        get => _mapNameB;
        set
        {
            if (Set(ref _mapNameB, value))
            {
                ResetStatusB();
                CheckCommand.RaiseCanExecuteChanged();
                StartCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public MapStatus MapStatusA
    {
        get => _mapStatusA;
        private set => Set(ref _mapStatusA, value);
    }

    public MapStatus MapStatusB
    {
        get => _mapStatusB;
        private set => Set(ref _mapStatusB, value);
    }

    public string MapInfoA
    {
        get => _mapInfoA;
        private set => Set(ref _mapInfoA, value);
    }

    public string MapInfoB
    {
        get => _mapInfoB;
        private set => Set(ref _mapInfoB, value);
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
                CheckCommand.RaiseCanExecuteChanged();
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
    public AsyncRelayCommand CheckCommand { get; }

    private async Task OnCheck()
    {
        var nameA = MapNameA;
        var nameB = MapNameB;
        var (resA, resB) = await Task.Run(() => (TickShmReader.Probe(nameA), TickShmReader.Probe(nameB)));
        ApplyProbe(resA, isA: true);
        ApplyProbe(resB, isA: false);
        StartCommand.RaiseCanExecuteChanged();
    }

    private void ApplyProbe(MapProbeResult result, bool isA)
    {
        var status = result.Status switch
        {
            MapProbeStatus.Ok => MapStatus.Ok,
            MapProbeStatus.NotFound => MapStatus.NotFound,
            _ => MapStatus.Invalid
        };
        var info = result.Status switch
        {
            MapProbeStatus.Ok => $"Ok ({result.Symbol})",
            MapProbeStatus.NotFound => "Not found",
            _ => $"Invalid: {result.Error}"
        };
        if (isA) { MapStatusA = status; MapInfoA = info; }
        else { MapStatusB = status; MapInfoB = info; }
    }

    private void ResetStatusA()
    {
        MapStatusA = MapStatus.Unchecked;
        MapInfoA = "Unchecked";
    }

    private void ResetStatusB()
    {
        MapStatusB = MapStatus.Unchecked;
        MapInfoB = "Unchecked";
    }

    private async Task OnStart()
    {
        try
        {
            await _session.StartAsync(MapNameA, MapNameB);
            IsRunning = true;
            MapNamePersistence.Save(MapNameA, MapNameB);
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
