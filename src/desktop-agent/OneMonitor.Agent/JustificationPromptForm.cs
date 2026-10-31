namespace OneMonitor.Agent;

/// <summary>
/// Pop-up leve exibido quando o usuário retorna de um período de inatividade, pedindo
/// um motivo. Um clique em qualquer motivo fecha o formulário já com a escolha feita;
/// "Pular" fecha sem justificar.
/// </summary>
public sealed class JustificationPromptForm : Form
{
    private static readonly string[] IdleReturnReasons =
    {
        "Reunião Presencial",
        "Suporte a Colega",
        "Visita a Cliente",
        "Treinamento",
        "Pausa"
    };

    /// <summary>
    /// Motivos para quem está prestes a pausar manualmente (ver TrayApplicationContext) — sem
    /// "Pausa" (redundante nesse contexto, já é o que o clique significa) nem "Suporte a
    /// Colega"/"Treinamento" (fazem mais sentido descobertos depois de já ter saído, não
    /// escolhidos de antemão); troca por um motivo pessoal, que é o caso mais comum de pausa
    /// deliberada que não é reunião nem visita.
    /// </summary>
    private static readonly string[] ManualPauseReasons =
    {
        "Almoço",
        "Reunião Presencial",
        "Visita a Cliente",
        "Assunto Pessoal"
    };

    public string? SelectedJustification { get; private set; }

    /// <summary>Retorno de um período de inatividade já ocorrido — mostra a duração.</summary>
    public static JustificationPromptForm ForIdleReturn(TimeSpan idleDuration) =>
        new($"Você esteve ausente por {FormatDuration(idleDuration)}. Qual foi o motivo?", IdleReturnReasons);

    /// <summary>Pausa manual que está prestes a começar (ver botão "Pausar") — motivo ainda não tem duração para mostrar.</summary>
    public static JustificationPromptForm ForManualPause() =>
        new("Pausando o monitoramento. Qual o motivo?", ManualPauseReasons);

    private JustificationPromptForm(string message, string[] reasons)
    {
        Text = "OneMonitor - Justificar ausência";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        TopMost = true;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        var messageLabel = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(messageLabel);

        foreach (var reason in reasons)
        {
            var button = new Button
            {
                Text = reason,
                AutoSize = true,
                Width = 240,
                Margin = new Padding(0, 0, 0, 4)
            };
            button.Click += (_, _) =>
            {
                SelectedJustification = reason;
                DialogResult = DialogResult.OK;
                Close();
            };
            layout.Controls.Add(button);
        }

        var skipButton = new Button
        {
            Text = "Pular / não justificar",
            AutoSize = true,
            Width = 240,
            Margin = new Padding(0, 12, 0, 0)
        };
        skipButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        layout.Controls.Add(skipButton);

        Controls.Add(layout);
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
}
