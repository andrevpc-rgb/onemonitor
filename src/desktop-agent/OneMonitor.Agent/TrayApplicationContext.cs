using System.Threading;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Infrastructure.Reporting;

namespace OneMonitor.Agent;

/// <summary>
/// Hospeda o ícone na bandeja do sistema e o timer que dirige o <see cref="ActivityTracker"/>.
/// Não há janela principal: o controle é todo pelo menu de contexto do ícone.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ActivityTracker _tracker;
    private readonly DailyReportService _reportService;
    private readonly SyncService? _syncService;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Threading.Timer? _syncTimer;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseResumeItem;
    private bool _isPaused;
    private int _syncInProgress;

    public TrayApplicationContext(
        ActivityTracker tracker,
        DailyReportService reportService,
        TimeSpan pollingInterval,
        SyncService? syncService = null,
        TimeSpan syncInterval = default)
    {
        _tracker = tracker;
        _reportService = reportService;
        _syncService = syncService;

        _statusItem = new ToolStripMenuItem("Status: iniciando...") { Enabled = false };
        _pauseResumeItem = new ToolStripMenuItem("Pausar", null, OnPauseResumeClicked);
        var reportItem = new ToolStripMenuItem("Gerar relatório de hoje", null, OnGenerateReportClicked);
        var syncNowItem = new ToolStripMenuItem("Sincronizar agora", null, OnSyncNowClicked)
        {
            Enabled = _syncService is not null
        };
        var exitItem = new ToolStripMenuItem("Encerrar", null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(reportItem);
        menu.Items.Add(syncNowItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OneMonitor",
            Visible = true,
            ContextMenuStrip = menu
        };

        _timer = new System.Windows.Forms.Timer { Interval = (int)pollingInterval.TotalMilliseconds };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        _tracker.IdleReturned += OnIdleReturned;
        _tracker.UnproductiveActivityDetected += OnUnproductiveActivityDetected;

        if (_syncService is not null)
        {
            // Timer de thread de background (não o Timer de WinForms): chamadas de rede não
            // devem rodar na thread de UI, sob risco de travar o ícone/menu enquanto aguardam.
            var intervalMs = (int)syncInterval.TotalMilliseconds;
            _syncTimer = new System.Threading.Timer(OnSyncTimerTick, null, intervalMs, intervalMs);

            // Fire-and-forget: um "Started" que falhar não deve impedir o agente de iniciar;
            // o próximo Heartbeat (ver SyncService.SyncPendingAsync) já cobre o caso de rede
            // instável logo na largada.
            _ = SendStatusSafeAsync(DeviceStatusEvent.Started);
        }
    }

    private void OnIdleReturned(object? sender, IdleReturnedEventArgs e)
    {
        if (!JustificationPolicy.ShouldPrompt(e, _tracker.WorkPolicyProvider.Current))
        {
            return;
        }

        // Pausa o polling enquanto o pop-up está aberto: evita que novos ticks mudem o
        // estado do tracker por baixo do diálogo modal.
        _timer.Stop();
        try
        {
            using var form = new JustificationPromptForm(e.Duration);
            var result = form.ShowDialog();
            if (result == DialogResult.OK && form.SelectedJustification is not null)
            {
                _tracker.SetJustification(e.RecordId, form.SelectedJustification);
            }
        }
        finally
        {
            _timer.Start();
        }
    }

    // Balão não-modal (ao contrário do pop-up de justificativa): só avisa, não interrompe o
    // que o usuário está fazendo nem pausa o polling. Dispara uma vez por transição (ver
    // ActivityTracker.RaiseIfUnproductive) — trocar de aba dentro do mesmo app/site não repete.
    private void OnUnproductiveActivityDetected(object? sender, UnproductiveActivityDetectedEventArgs e)
    {
        _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _trayIcon.BalloonTipTitle = "OneMonitor - Atividade improdutiva";
        _trayIcon.BalloonTipText = TrimToolTip(
            $"Você está em \"{e.ProcessName}\" ({e.WindowTitle}), classificado como improdutivo pela sua organização.",
            maxLength: 255);
        _trayIcon.ShowBalloonTip(8000);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        _tracker.Tick();

        var current = _tracker.CurrentRecord;
        if (current is null)
        {
            return;
        }

        _statusItem.Text = $"{current.Type}: {current.ProcessName} ({current.Duration:mm\\:ss})";
        _trayIcon.Text = TrimToolTip($"OneMonitor - {current.ProcessName}");
    }

    private void OnPauseResumeClicked(object? sender, EventArgs e)
    {
        _isPaused = !_isPaused;

        if (_isPaused)
        {
            _tracker.Flush();
            _pauseResumeItem.Text = "Retomar";
            _statusItem.Text = "Status: pausado";
        }
        else
        {
            _pauseResumeItem.Text = "Pausar";
        }
    }

    // Disparado pelo System.Threading.Timer numa thread do pool — nunca a thread de UI.
    private async void OnSyncTimerTick(object? state) => await RunSyncAsync();

    private async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        var result = await RunSyncAsync();
        if (result is null)
        {
            return;
        }

        var message = result.Success
            ? (result.EventsSynced > 0 ? $"{result.EventsSynced} evento(s) sincronizado(s)." : "Nada pendente para sincronizar.")
            : $"Falha ao sincronizar: {result.Error}";

        MessageBox.Show(message, "OneMonitor", MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private async Task<SyncCycleResult?> RunSyncAsync()
    {
        if (_syncService is null)
        {
            return null;
        }

        // Evita sobrepor um ciclo de sync com o anterior (ex.: rede lenta + timer periódico).
        if (Interlocked.CompareExchange(ref _syncInProgress, 1, 0) != 0)
        {
            return null;
        }

        try
        {
            var result = await _syncService.SyncPendingAsync();
            ApplySettingsIfPresent(result);
            return result;
        }
        catch (Exception)
        {
            // SyncService só deveria devolver falhas como SyncCycleResult, nunca lançar; isto
            // é uma rede de segurança para não derrubar o agente por um erro inesperado de sync.
            return SyncCycleResult.Failed("Erro inesperado durante a sincronização.");
        }
        finally
        {
            Interlocked.Exchange(ref _syncInProgress, 0);
        }
    }

    private void OnGenerateReportClicked(object? sender, EventArgs e)
    {
        var (jsonPath, htmlPath) = _reportService.GenerateForToday();
        MessageBox.Show(
            $"Relatório gerado em:\n{htmlPath}\n{jsonPath}",
            "OneMonitor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async void OnExitClicked(object? sender, EventArgs e)
    {
        _tracker.Flush();

        // Melhor esforço, com timeout curto: se o backend estiver fora do ar, o agente
        // precisa conseguir fechar mesmo assim — "Stopped" não pode travar o encerramento.
        await SendStatusSafeAsync(DeviceStatusEvent.Stopped, TimeSpan.FromSeconds(3));

        _trayIcon.Visible = false;
        ExitThread();
    }

    private async Task SendStatusSafeAsync(string state, TimeSpan? timeout = null)
    {
        if (_syncService is null)
        {
            return;
        }

        try
        {
            using var cts = timeout is { } t ? new CancellationTokenSource(t) : new CancellationTokenSource();
            var result = await _syncService.SendStatusAsync(state, cts.Token);
            ApplySettingsIfPresent(result);
        }
        catch (Exception)
        {
            // Melhor esforço: um "Started"/"Stopped" que falha não deve derrubar o agente nem
            // travar o fechamento — o próximo Heartbeat bem-sucedido já reflete o estado atual.
        }
    }

    // "Started" também devolve a OrganizationSettings vigente — aplicá-la já na largada,
    // sem esperar o primeiro ciclo periódico de Heartbeat, evita alguns minutos com a
    // política default/desatualizada logo depois do agente iniciar.
    private void ApplySettingsIfPresent(SyncCycleResult result)
    {
        if (result.Settings is not null)
        {
            _tracker.WorkPolicyProvider.Update(result.Settings);
        }
    }

    // NotifyIcon.Text aceita no máximo 63 caracteres; BalloonTipText aceita até 255 (ver
    // chamada em OnUnproductiveActivityDetected).
    private static string TrimToolTip(string text, int maxLength = 63) =>
        text.Length <= maxLength ? text : text[..maxLength];

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tracker.IdleReturned -= OnIdleReturned;
            _tracker.UnproductiveActivityDetected -= OnUnproductiveActivityDetected;
            _timer.Dispose();
            _syncTimer?.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
