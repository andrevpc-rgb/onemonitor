using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Services;

namespace OneMonitor.Agent.Infrastructure.Reporting;

/// <summary>Orquestra leitura dos registros do dia, agregação e escrita em disco.</summary>
public class DailyReportService
{
    private readonly IActivityRepository _repository;
    private readonly ReportFileWriter _writer;
    private readonly ReportGenerator _generator;
    private readonly string _outputDirectory;
    private readonly IWorkPolicyProvider? _workPolicyProvider;

    public DailyReportService(
        IActivityRepository repository,
        ReportFileWriter writer,
        string outputDirectory,
        ICategoryResolver categoryResolver,
        IWorkPolicyProvider? workPolicyProvider = null)
    {
        _repository = repository;
        _writer = writer;
        _outputDirectory = outputDirectory;
        _generator = new ReportGenerator(categoryResolver);
        _workPolicyProvider = workPolicyProvider;
    }

    public (string JsonPath, string HtmlPath) GenerateForToday() =>
        Generate(DateOnly.FromDateTime(DateTime.Now));

    public (string JsonPath, string HtmlPath) Generate(DateOnly date)
    {
        var records = _repository.GetRecordsForDate(date);
        var report = _generator.Generate(records, date);

        // Nome/logo da organização (null quando sync está desabilitado — ver
        // OrganizationSettings.Unrestricted) vêm do mesmo cache local que já guarda
        // expediente/pop-up (FileWorkPolicyProvider), preenchido no heartbeat de sync.
        var jsonPath = _writer.WriteJson(report, _outputDirectory);
        var htmlPath = _writer.WriteHtml(report, _outputDirectory, _workPolicyProvider?.Current);
        return (jsonPath, htmlPath);
    }
}
