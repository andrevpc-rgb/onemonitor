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
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseResumeItem;
    private bool _isPaused;

    public TrayApplicationContext(ActivityTracker tracker, DailyReportService reportService, TimeSpan pollingInterval)
    {
        _tracker = tracker;
        _reportService = reportService;

        _statusItem = new ToolStripMenuItem("Status: iniciando...") { Enabled = false };
        _pauseResumeItem = new ToolStripMenuItem("Pausar", null, OnPauseResumeClicked);
        var reportItem = new ToolStripMenuItem("Gerar relatório de hoje", null, OnGenerateReportClicked);
        var exitItem = new ToolStripMenuItem("Encerrar", null, OnExitClicked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(reportItem);
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
    }

    private void OnIdleReturned(object? sender, IdleReturnedEventArgs e)
    {
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

    private void OnGenerateReportClicked(object? sender, EventArgs e)
    {
        var (jsonPath, htmlPath) = _reportService.GenerateForToday();
        MessageBox.Show(
            $"Relatório gerado em:\n{htmlPath}\n{jsonPath}",
            "OneMonitor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _tracker.Flush();
        _trayIcon.Visible = false;
        ExitThread();
    }

    // NotifyIcon.Text aceita no máximo 63 caracteres.
    private static string TrimToolTip(string text) => text.Length <= 63 ? text : text[..63];

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tracker.IdleReturned -= OnIdleReturned;
            _timer.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
