using ClosedXML.Excel;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;

namespace PharmacyScheduler.WinForms.Infrastructure;

public sealed class ExcelExportService
{
    private readonly ScheduleQueryService _queryService = new();

    public void Export(AppData data, SchedulePlan schedule, string filePath)
    {
        var rows = _queryService.FlattenSchedule(data, schedule);
        var summary = _queryService.BuildSummary(data, schedule);

        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Sorlista");
        WriteHeaders(ws, "Telephely", "Dátum", "Kezdés", "Vége", "Dolgozó", "Megjelenítési név", "Szerepkör", "Időtípus kód", "Időtípus", "Óraszám", "Megjegyzés");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowIndex, 1).Value = row.LocationName;
            ws.Cell(rowIndex, 2).Value = row.Date.ToDateTime(TimeOnly.MinValue);
            ws.Cell(rowIndex, 3).Value = row.Start.ToString("HH:mm");
            ws.Cell(rowIndex, 4).Value = row.End.ToString("HH:mm");
            ws.Cell(rowIndex, 5).Value = row.EmployeeFullName;
            ws.Cell(rowIndex, 6).Value = row.EmployeeDisplayName;
            ws.Cell(rowIndex, 7).Value = row.RoleName;
            ws.Cell(rowIndex, 8).Value = row.TimeTypeCode;
            ws.Cell(rowIndex, 9).Value = row.TimeTypeName;
            ws.Cell(rowIndex, 10).Value = row.Hours;
            ws.Cell(rowIndex, 11).Value = row.Note;
            rowIndex++;
        }

        ws.Columns().AdjustToContents();

        var sum = workbook.Worksheets.Add("Összesítő");
        WriteHeaders(sum, "Dolgozó", "Megjelenítési név", "Szerepkör", "Telephely(ek)", "Időtípus", "Óraszám");

        rowIndex = 2;
        foreach (var row in summary)
        {
            sum.Cell(rowIndex, 1).Value = row.EmployeeFullName;
            sum.Cell(rowIndex, 2).Value = row.EmployeeDisplayName;
            sum.Cell(rowIndex, 3).Value = row.RoleName;
            sum.Cell(rowIndex, 4).Value = row.LocationNames;
            sum.Cell(rowIndex, 5).Value = row.TimeTypeName;
            sum.Cell(rowIndex, 6).Value = row.Hours;
            rowIndex++;
        }

        sum.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    private static void WriteHeaders(IXLWorksheet ws, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
    }
}
