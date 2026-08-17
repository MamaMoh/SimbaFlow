using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Renders a <see cref="ReportTable"/> to a downloadable file (Excel or PDF).
/// </summary>
public interface IReportExportService
{
    byte[] ToExcel(ReportTable report);
    byte[] ToPdf(ReportTable report);
}
