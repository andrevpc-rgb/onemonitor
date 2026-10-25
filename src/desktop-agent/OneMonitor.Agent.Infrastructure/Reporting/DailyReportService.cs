using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Services;

namespace OneMonitor.Agent.Infrastructure.Reporting;

/// <summary>Orquestra leitura dos registros do dia, agregação e escrita em disco.</summary>
public class DailyReportService
{
    private readonly IActivityRepository _repository;
    private readonly ReportFileWriter _writer;
    private readonly ReportGenerator _generator = new();
    private readonly string _outputDirectory;

    public DailyReportService(IActivityRepository repository, ReportFileWriter writer, string outputDirectory)
    {
        _repository = repository;
        _writer = writer;
        _outputDirectory = outputDirectory;
    }

    public (string JsonPath, string HtmlPath) GenerateForToday() =>
        Generate(DateOnly.FromDateTime(DateTime.Now));

    public (string JsonPath, string HtmlPath) Generate(DateOnly date)
    {
        var records = _repository.GetRecordsForDate(date);
        var report = _generator.Generate(records, date);

        var jsonPath = _writer.WriteJson(report, _outputDirectory);
        var htmlPath = _writer.WriteHtml(report, _outputDirectory);
        return (jsonPath, htmlPath);
    }
}
