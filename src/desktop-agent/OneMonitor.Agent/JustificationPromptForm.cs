namespace OneMonitor.Agent;

/// <summary>
/// Pop-up leve exibido quando o usuário retorna de um período de inatividade, pedindo
/// um motivo. Um clique em qualquer motivo fecha o formulário já com a escolha feita;
/// "Pular" fecha sem justificar.
/// </summary>
public sealed class JustificationPromptForm : Form
{
    private static readonly string[] PresetReasons =
    {
        "Reunião Presencial",
        "Suporte a Colega",
        "Visita a Cliente",
        "Pausa"
    };

    public string? SelectedJustification { get; private set; }

    public JustificationPromptForm(TimeSpan idleDuration)
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

        var message = new Label
        {
            Text = $"Você esteve ausente por {FormatDuration(idleDuration)}. Qual foi o motivo?",
            AutoSize = true,
            MaximumSize = new Size(260, 0),
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(message);

        foreach (var reason in PresetReasons)
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
