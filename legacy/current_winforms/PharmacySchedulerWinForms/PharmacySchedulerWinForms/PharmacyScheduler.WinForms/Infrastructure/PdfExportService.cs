using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PharmacyScheduler.WinForms.Infrastructure;

public sealed class PdfExportService
{
    private readonly ScheduleQueryService _queryService = new();

    public void Export(AppData data, SchedulePlan schedule, ValidationReport report, string filePath)
    {
        var rows = _queryService.FlattenSchedule(data, schedule);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("Gyógyszertári beosztás export").FontSize(18).Bold();
                    column.Item().Text($"{schedule.Name} • {schedule.PeriodStart:yyyy-MM-dd} - {schedule.PeriodEnd:yyyy-MM-dd} • {schedule.Status.ToDisplayText()}");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Telephely").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Dátum").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Kezd").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Vég").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Dolgozó").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Szerepkör").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Típus").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Óra").Bold();
                            header.Cell().Border(1).Background(Colors.Grey.Lighten2).Padding(4).Text("Megjegyzés").Bold();
                        });

                        foreach (var row in rows)
                        {
                            table.Cell().Border(1).Padding(3).Text(row.LocationName ?? string.Empty);
                            table.Cell().Border(1).Padding(3).Text(row.Date.ToString("yyyy-MM-dd"));
                            table.Cell().Border(1).Padding(3).Text(row.Start.ToString("HH:mm"));
                            table.Cell().Border(1).Padding(3).Text(row.End.ToString("HH:mm"));
                            table.Cell().Border(1).Padding(3).Text(row.EmployeeDisplayName);
                            table.Cell().Border(1).Padding(3).Text(row.RoleName);
                            table.Cell().Border(1).Padding(3).Text(row.TimeTypeName);
                            table.Cell().Border(1).Padding(3).Text(row.Hours.ToString("0.##"));
                            table.Cell().Border(1).Padding(3).Text(row.Note ?? string.Empty);
                        }
                    });

                    if (report.Issues.Count > 0)
                    {
                        column.Item().Text("Ellenőrzések").Bold().FontSize(12);
                        foreach (var issue in report.Issues.Take(25))
                        {
                            column.Item().Text($"• [{issue.Severity.ToDisplayText()}] {issue.Message}");
                        }

                        if (report.Issues.Count > 25)
                        {
                            column.Item().Text($"... további {report.Issues.Count - 25} ellenőrzési tétel");
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text($"Generálva: {DateTime.Now:yyyy-MM-dd HH:mm}");
            });
        }).GeneratePdf(filePath);
    }
}
