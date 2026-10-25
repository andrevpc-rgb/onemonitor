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

    public string WriteHtml(ReportData report, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"report-{report.Date:yyyy-MM-dd}.html");
        File.WriteAllText(path, BuildHtml(report));
        return path;
    }

    private static string BuildHtml(ReportData report)
    {
        var rows = string.Join(Environment.NewLine, report.AppUsages.Select(a =>
            $"<tr><td>{WebUtility.HtmlEncode(a.ProcessName)}</td><td>{FormatDuration(a.Duration)}</td></tr>"));

        return $$"""
            <!DOCTYPE html>
            <html lang="pt-br">
            <head>
                <meta charset="utf-8" />
                <title>OneMonitor - Relatório {{report.Date:yyyy-MM-dd}}</title>
                <style>
                    body { font-family: "Segoe UI", Arial, sans-serif; margin: 2rem; color: #222; }
                    table { border-collapse: collapse; width: 100%; max-width: 640px; }
                    th, td { border: 1px solid #ccc; padding: 0.5rem; text-align: left; }
                    th { background: #f4f4f4; }
                    .summary span { display: inline-block; margin-right: 1.5rem; }
                </style>
            </head>
            <body>
                <h1>OneMonitor - Relatório de {{report.Date:yyyy-MM-dd}}</h1>
                <p class="summary">
                    <span><strong>Ativo:</strong> {{FormatDuration(report.TotalActive)}}</span>
                    <span><strong>Inativo:</strong> {{FormatDuration(report.TotalIdle)}}</span>
                    <span><strong>Não classificado:</strong> {{FormatDuration(report.TotalUnclassified)}}</span>
                    <span><strong>Total monitorado:</strong> {{FormatDuration(report.TotalTracked)}}</span>
                </p>
                <h2>Tempo por aplicativo</h2>
                <table>
                    <thead><tr><th>Aplicativo</th><th>Duração</th></tr></thead>
                    <tbody>
                    {{rows}}
                    </tbody>
                </table>
            </body>
            </html>
            """;
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
}
