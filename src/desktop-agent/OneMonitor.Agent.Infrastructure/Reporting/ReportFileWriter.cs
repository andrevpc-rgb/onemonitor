using System.Net;
using System.Text.Json;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Infrastructure.Reporting;

/// <summary>Grava um <see cref="ReportData"/> como JSON e como HTML em disco.</summary>
public class ReportFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string WriteJson(ReportData report, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"report-{report.Date:yyyy-MM-dd}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
        return path;
    }

    /// <param name="organization">
    /// Nome/logo da organização (ver <see cref="OrganizationSettings.OrganizationName"/> e
    /// <see cref="OrganizationSettings.LogoUrl"/>), devolvidos pelo heartbeat de sync e
    /// cacheados localmente (ver FileWorkPolicyProvider) — null quando a sincronização está
    /// desabilitada ou nenhum heartbeat teve sucesso ainda; o relatório cai no cabeçalho
    /// genérico "OneMonitor" de sempre.
    /// </param>
    public string WriteHtml(ReportData report, string directory, OrganizationSettings? organization = null)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"report-{report.Date:yyyy-MM-dd}.html");
        File.WriteAllText(path, BuildHtml(report, organization));
        return path;
    }

    private static string BuildHtml(ReportData report, OrganizationSettings? organization)
    {
        var organizationName = string.IsNullOrWhiteSpace(organization?.OrganizationName)
            ? null
            : WebUtility.HtmlEncode(organization.OrganizationName);
        var logoUrl = string.IsNullOrWhiteSpace(organization?.LogoUrl) ? null : organization.LogoUrl;

        // Mesma URL pública usada no cabeçalho do dashboard web (ver OrgHeader em
        // app/page.tsx) — bucket de Storage público, carrega sem autenticação mesmo com
        // este HTML aberto localmente, fora do navegador logado.
        var logoImg = logoUrl is null
            ? ""
            : $"""<img src="{WebUtility.HtmlEncode(logoUrl)}" alt="" style="height: 2.5rem; vertical-align: middle; margin-right: 0.75rem;" />""";
        var heading = organizationName is null
            ? "OneMonitor - Relatório"
            : $"{organizationName} <span style=\"color: #666; font-weight: normal;\">— OneMonitor</span>";
        var rows = string.Join(Environment.NewLine, report.AppUsages.Select(a =>
            $"<tr><td>{WebUtility.HtmlEncode(a.ProcessName)}</td><td>{FormatDuration(a.Duration)}</td></tr>"));

        var windowRows = string.Join(Environment.NewLine, report.WindowUsages.Select(w =>
            $"<tr><td>{WebUtility.HtmlEncode(w.ProcessName)}</td><td>{WebUtility.HtmlEncode(w.WindowTitle)}</td><td>{FormatDuration(w.Duration)}</td></tr>"));

        var categoryRows = string.Join(Environment.NewLine, report.CategoryUsages.Select(c =>
            $"<tr><td>{WebUtility.HtmlEncode(c.Category)}</td><td>{FormatDuration(c.Duration)}</td></tr>"));

        var justificationRows = string.Join(Environment.NewLine, report.JustifiedAbsences.Select(j =>
            $"<tr><td>{WebUtility.HtmlEncode(j.Justification)}</td><td>{FormatDuration(j.Duration)}</td><td>{j.Occurrences}</td></tr>"));

        var timelineRows = string.Join(Environment.NewLine, report.Timeline.Select(r =>
            $"""
            <tr>
                <td>{FormatTimeRange(r.StartTime, r.EndTime)}</td>
                <td>{TranslateType(r.Type)}</td>
                <td>{WebUtility.HtmlEncode(r.ProcessName)}</td>
                <td>{WebUtility.HtmlEncode(r.WindowTitle)}</td>
                <td>{FormatDuration(r.Duration)}</td>
                <td>{WebUtility.HtmlEncode(r.Justification ?? "")}</td>
            </tr>
            """));

        return $$"""
            <!DOCTYPE html>
            <html lang="pt-br">
            <head>
                <meta charset="utf-8" />
                <title>{{organizationName ?? "OneMonitor"}} - Relatório {{report.Date:yyyy-MM-dd}}</title>
                <style>
                    body { font-family: "Segoe UI", Arial, sans-serif; margin: 2rem; color: #222; }
                    table { border-collapse: collapse; width: 100%; max-width: 640px; }
                    th, td { border: 1px solid #ccc; padding: 0.5rem; text-align: left; }
                    th { background: #f4f4f4; }
                    .summary span { display: inline-block; margin-right: 1.5rem; }
                    .report-header { display: flex; align-items: center; margin-bottom: 0; }
                </style>
            </head>
            <body>
                <h1 class="report-header">{{logoImg}}{{heading}} - Relatório de {{report.Date:yyyy-MM-dd}}</h1>
                <p class="summary">
                    <span><strong>Ativo:</strong> {{FormatDuration(report.TotalActive)}}</span>
                    <span><strong>Inativo:</strong> {{FormatDuration(report.TotalIdle)}}</span>
                    <span><strong>Justificado:</strong> {{FormatDuration(report.TotalJustified)}}</span>
                    <span><strong>Não classificado:</strong> {{FormatDuration(report.TotalUnclassified)}}</span>
                    <span><strong>Total monitorado:</strong> {{FormatDuration(report.TotalTracked)}}</span>
                </p>
                <h2>Linha do tempo</h2>
                <table>
                    <thead><tr><th>Horário</th><th>Tipo</th><th>Aplicativo</th><th>Janela / site</th><th>Duração</th><th>Justificativa</th></tr></thead>
                    <tbody>
                    {{timelineRows}}
                    </tbody>
                </table>
                <h2>Tempo por categoria</h2>
                <table>
                    <thead><tr><th>Categoria</th><th>Duração</th></tr></thead>
                    <tbody>
                    {{categoryRows}}
                    </tbody>
                </table>
                <h2>Tempo por aplicativo</h2>
                <table>
                    <thead><tr><th>Aplicativo</th><th>Duração</th></tr></thead>
                    <tbody>
                    {{rows}}
                    </tbody>
                </table>
                <h2>Detalhe por janela / site</h2>
                <table>
                    <thead><tr><th>Aplicativo</th><th>Janela / site</th><th>Duração</th></tr></thead>
                    <tbody>
                    {{windowRows}}
                    </tbody>
                </table>
                <h2>Ausências justificadas</h2>
                <table>
                    <thead><tr><th>Motivo</th><th>Duração</th><th>Ocorrências</th></tr></thead>
                    <tbody>
                    {{justificationRows}}
                    </tbody>
                </table>
            </body>
            </html>
            """;
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";

    // Os registros são armazenados em UTC; aqui convertemos para o horário local da
    // máquina (o mesmo fuso em que o usuário realmente viveu aquele período).
    private static string FormatTimeRange(DateTimeOffset start, DateTimeOffset end) =>
        $"{start.ToLocalTime():HH:mm:ss} – {end.ToLocalTime():HH:mm:ss}";

    private static string TranslateType(ActivityType type) => type switch
    {
        ActivityType.Active => "Ativo",
        ActivityType.Idle => "Inativo",
        ActivityType.Unclassified => "Não classificado",
        _ => type.ToString()
    };
}
