using ReGranBill.Server.DTOs.AccountClosingReport;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.MasterReport;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.DTOs.PurchaseReturns;
using ReGranBill.Server.DTOs.SOA;
using ReGranBill.Server.DTOs.SaleReturns;
using ReGranBill.Server.DTOs.CustomerLedger;
using ReGranBill.Server.DTOs.WashingVouchers;

namespace ReGranBill.Server.Services;

public class PdfService : IPdfService
{
    private const string Green = "#1a5c2a";
    private const string LightGreen = "#f0f8f0";
    private const string BorderGreen = "#a0c8a0";
    private const int MinRows = 8;

    private readonly string _logoSvg;

    public PdfService(IWebHostEnvironment env)
    {
        var logoPath = Path.Combine(env.ContentRootPath, "KPI_LOGO.svg");
        if (!File.Exists(logoPath))
            logoPath = Path.Combine(env.ContentRootPath, "..", "regranbill.client", "public", "KPI_LOGO.svg");
        _logoSvg = File.ReadAllText(logoPath);
    }

    public byte[] GenerateDeliveryChallanPdf(DeliveryChallanDto dto) =>
        GenerateVoucherPdf(
            dto.DcNumber,
            dto.Date,
            dto.CustomerName,
            dto.VehicleNumber,
            dto.Description,
            dto.Lines.Select(line => new VoucherPdfLine(
                line.ProductId,
                line.ProductName,
                line.Packing,
                line.PackingWeightKg,
                line.Rbp,
                line.Qty,
                null)).ToList());

    public byte[] GeneratePurchaseVoucherPdf(PurchaseVoucherDto dto) =>
        GenerateVoucherPdf(
            dto.VoucherNumber,
            dto.Date,
            dto.VendorName,
            dto.VehicleNumber,
            dto.Description,
            dto.Lines.Select(line => new VoucherPdfLine(
                line.ProductId,
                line.ProductName,
                line.Packing,
                line.PackingWeightKg,
                "Yes",
                line.Qty,
                line.TotalWeightKg)).ToList());

    public byte[] GenerateSaleReturnPdf(SaleReturnDto dto) =>
        GenerateVoucherPdf(
            dto.SrNumber,
            dto.Date,
            dto.CustomerName,
            null,
            dto.Description,
            dto.Lines.Select(line => new VoucherPdfLine(
                line.ProductId,
                line.ProductName,
                line.Packing,
                line.PackingWeightKg,
                line.Rbp,
                line.Qty,
                null)).ToList());

    public byte[] GeneratePurchaseReturnPdf(PurchaseReturnDto dto) =>
        GenerateVoucherPdf(
            dto.PrNumber,
            dto.Date,
            dto.VendorName,
            dto.VehicleNumber,
            dto.Description,
            dto.Lines.Select(line => new VoucherPdfLine(
                line.ProductId,
                line.ProductName,
                line.Packing,
                line.PackingWeightKg,
                "Yes",
                line.Qty,
                line.TotalWeightKg)).ToList());

    public byte[] GenerateWashingVoucherPdf(WashingVoucherDto dto)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(28);
                page.MarginVertical(24);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(22).Column(column =>
                {
                    column.Spacing(0);

                    column.Item().Element(ComposeCompanyHeader);
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Green);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("WASHING VOUCHER").Bold().FontSize(17).FontColor(Green);
                            info.Item().PaddingTop(5).Text(dto.SourceVendorName ?? "-").Bold().FontSize(13);
                            info.Item().PaddingTop(3).Text($"Unwashed: {dto.UnwashedAccountName ?? "-"}").FontSize(10);
                            info.Item().PaddingTop(2).Text(dto.OutputLines.Count <= 1
                                ? $"Output: {dto.WashedAccountName ?? "-"}"
                                : $"Outputs: {dto.OutputLines.Count} raw-material lines").FontSize(10);
                        });

                        row.ConstantItem(185).AlignRight().Border(1.2f).BorderColor(Green).Padding(8).Column(summary =>
                        {
                            summary.Item().AlignRight().Text($"Voucher No: {dto.VoucherNumber}").Bold().FontSize(10);
                            summary.Item().PaddingTop(4).AlignRight().Text($"Date: {dto.Date:dd/MM/yyyy}").FontSize(10);
                            summary.Item().AlignRight().Text($"Vendor Rate: {FormatDecimal(dto.SourceRate)} / kg").FontSize(10);
                            summary.Item().PaddingTop(3).AlignRight().Text($"Washed Rate: {FormatDecimal(dto.WashedRate)} / kg").Bold().FontSize(10);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(dto.Description))
                    {
                        column.Item().PaddingTop(10).Border(1).BorderColor(BorderGreen).Background(LightGreen).Padding(8).Text(t =>
                        {
                            t.Span("Note: ").Bold();
                            t.Span(dto.Description);
                        });
                    }

                    column.Item().PaddingTop(12).Text("Material Flow").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.8f);
                        });

                        AddKeyValueRow(table, "Source Vendor", dto.SourceVendorName ?? "-");
                        AddKeyValueRow(table, "Unwashed Material", dto.UnwashedAccountName ?? "-");
                        AddKeyValueRow(table, "Output Accounts", dto.OutputLines.Count == 0
                            ? "-"
                            : string.Join(", ", dto.OutputLines.Select(line => line.AccountName)));
                    });

                    column.Item().PaddingTop(12).Text("Output Breakdown").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(34);
                            columns.RelativeColumn(2.4f);
                            columns.ConstantColumn(88);
                            columns.ConstantColumn(88);
                            columns.ConstantColumn(96);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("#").Bold();
                            header.Cell().Element(CellStyle).Text("Account").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("Weight").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("Rate").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("Amount").Bold();
                        });

                        foreach (var outputLine in dto.OutputLines.Select((line, index) => new { line, index }))
                        {
                            table.Cell().Element(CellStyle).Text((outputLine.index + 1).ToString());
                            table.Cell().Element(CellStyle).Text(outputLine.line.AccountName);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{FormatDecimal(outputLine.line.WeightKg)} kg");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{FormatDecimal(outputLine.line.Rate)} / kg");
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(outputLine.line.Debit));
                        }
                    });

                    column.Item().PaddingTop(12).Text("Weight / Shortage Details").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.5f);
                            columns.ConstantColumn(92);
                            columns.RelativeColumn(1.5f);
                            columns.ConstantColumn(92);
                        });

                        AddMetricRow(table,
                            "Input Weight", $"{FormatDecimal(dto.InputWeightKg)} kg",
                            "Output Weight", $"{FormatDecimal(dto.OutputWeightKg)} kg");
                        AddMetricRow(table,
                            "Total Wastage / Shortage", $"{FormatDecimal(dto.WastageKg)} kg",
                            "Wastage %", $"{FormatDecimal(dto.WastagePct)}%");
                        AddMetricRow(table,
                            "Allowed Threshold", $"{FormatDecimal(dto.ThresholdPct)}%",
                            "Allowed Wastage", $"{FormatDecimal(dto.WastageKg - dto.ExcessWastageKg)} kg");
                        AddMetricRow(table,
                            "Excess Wastage", $"{FormatDecimal(dto.ExcessWastageKg)} kg",
                            "Recovered from Vendor", FormatCurrency(dto.ExcessWastageValue));
                    });

                    column.Item().PaddingTop(12).Text("Costing Summary").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.5f);
                            columns.ConstantColumn(110);
                            columns.RelativeColumn(1.5f);
                            columns.ConstantColumn(110);
                        });

                        AddMetricRow(table,
                            "Source Rate", $"{FormatDecimal(dto.SourceRate)} / kg",
                            "Washed Rate", $"{FormatDecimal(dto.WashedRate)} / kg");
                        AddMetricRow(table,
                            "Input Cost", FormatCurrency(dto.InputCost),
                            "Washed Stock Cost", FormatCurrency(dto.WashedDebit));
                        AddMetricRow(table,
                            "Excess Recovery", FormatCurrency(dto.ExcessWastageValue),
                            "Net Capitalized Value", FormatCurrency(dto.WashedDebit));
                    });

                    if (dto.ExcessWastageKg > 0)
                    {
                        column.Item().PaddingTop(12).Border(1).BorderColor(BorderGreen).Padding(8).Text(t =>
                        {
                            t.Span("Recovery Note: ").Bold();
                            t.Span($"Excess washing loss of {FormatDecimal(dto.ExcessWastageKg)} kg beyond the {FormatDecimal(dto.ThresholdPct)}% threshold has been charged back to {dto.SourceVendorName ?? "the vendor"}.");
                        });
                    }

                    column.Item().PaddingTop(42).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().BorderBottom(1.5f).BorderColor(Green).Height(34);
                            c.Item().PaddingTop(4).AlignCenter().Text("Prepared by").Bold().FontSize(11);
                        });
                        row.ConstantItem(140);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().BorderBottom(1.5f).BorderColor(Green).Height(34);
                            c.Item().PaddingTop(4).AlignCenter().Text("Received by").Bold().FontSize(11);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();

        IContainer CellStyle(IContainer container) =>
            container.Border(1).BorderColor(BorderGreen).PaddingVertical(5).PaddingHorizontal(6);
    }

    public byte[] GenerateStatementOfAccountPdf(StatementOfAccountDto dto)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(32);
                page.MarginVertical(26);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(24).Column(column =>
                {
                    column.Spacing(0);

                    column.Item().Element(ComposeCompanyHeader);
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Green);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("STATEMENT OF ACCOUNT").Bold().FontSize(16).FontColor(Green);
                            info.Item().PaddingTop(4).Text(dto.AccountName).Bold().FontSize(13);

                            var contactLine = BuildContactLine(dto);
                            if (!string.IsNullOrWhiteSpace(contactLine))
                            {
                                info.Item().PaddingTop(2).Text(contactLine).FontSize(9);
                            }

                            if (!string.IsNullOrWhiteSpace(dto.Address))
                            {
                                info.Item().PaddingTop(2).Text(dto.Address).FontSize(9);
                            }
                        });

                        row.ConstantItem(170).AlignRight().Column(summary =>
                        {
                            summary.Item().AlignRight().Text($"Date Range: {BuildDateRangeLabel(dto)}").FontSize(10).Bold();
                            summary.Item().PaddingTop(8).AlignRight().Border(1.2f).BorderColor(Green).Padding(8).Column(box =>
                            {
                                box.Item().AlignRight().Text("Net Balance").FontSize(9);
                                box.Item().AlignRight().Text(FormatCurrency(dto.NetBalance)).Bold().FontSize(15);
                            });
                        });
                    });

                    column.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(56);
                            columns.ConstantColumn(64);
                            columns.ConstantColumn(60);
                            columns.RelativeColumn();
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(60);
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DATE").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("VOUCHER").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("TYPE").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DESCRIPTION").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DEBIT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("CREDIT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("BALANCE").Bold().FontSize(9).LetterSpacing(0.04f);
                        });

                        foreach (var entry in dto.Entries)
                        {
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().AlignMiddle().Text(entry.Date.ToString("dd/MM/yyyy")).FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().AlignMiddle().Text(entry.VoucherNumber).FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().AlignMiddle().Text(entry.VoucherType).FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignMiddle().Text(entry.Description ?? "-").FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().AlignMiddle().Text(entry.Debit > 0 ? FormatCurrency(entry.Debit) : "-").FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().AlignMiddle().Text(entry.Credit > 0 ? FormatCurrency(entry.Credit) : "-").FontSize(9);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().AlignMiddle().Text(FormatCurrency(entry.RunningBalance)).FontSize(9);
                        }

                        if (dto.Entries.Count == 0)
                        {
                            table.Cell().ColumnSpan(7)
                                .Border(0.5f).BorderColor(BorderGreen).Padding(10)
                                .AlignCenter().Text("No entries found for the selected period.").FontSize(10);
                        }

                        table.Cell().ColumnSpan(4).BorderTop(1.5f).BorderBottom(1.5f).BorderLeft(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text("Totals").Bold().FontSize(11);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalDebit)).Bold().FontSize(11);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalCredit)).Bold().FontSize(11);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderRight(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.NetBalance)).Bold().FontSize(11);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateMasterReportPdf(MasterReportDto dto, IReadOnlyCollection<string>? visibleColumns = null)
    {
        var columns = NormalizeMasterReportColumns(visibleColumns);
        var accountSummaries = dto.AccountSummaries
            .OrderBy(summary => summary.AccountName)
            .ThenBy(summary => summary.AccountId)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(20).Column(column =>
                {
                    column.Spacing(0);

                    column.Item().Element(ComposeCompanyHeader);
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Green);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("MASTER REPORT").Bold().FontSize(16).FontColor(Green);
                            info.Item().PaddingTop(4).Text(BuildMasterReportScope(dto)).Bold().FontSize(11);
                            info.Item().PaddingTop(4).Text($"Date Range: {BuildMasterReportDateRange(dto)}").FontSize(9);
                        });

                        row.ConstantItem(180).AlignRight().Border(1.2f).BorderColor(Green).Padding(8).Column(summary =>
                        {
                            summary.Item().AlignRight().Text($"Entries: {dto.TotalEntries}").Bold().FontSize(10);
                            summary.Item().PaddingTop(4).AlignRight().Text($"Debit: {FormatCurrency(dto.TotalDebit)}").FontSize(10);
                            summary.Item().AlignRight().Text($"Credit: {FormatCurrency(dto.TotalCredit)}").FontSize(10);
                            summary.Item().PaddingTop(2).AlignRight().Text($"Net: {FormatCurrency(dto.NetBalance)}").Bold().FontSize(11);
                        });
                    });

                    column.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(definition =>
                        {
                            foreach (var columnKey in columns)
                            {
                                ConfigureMasterReportColumn(definition, columnKey);
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (var columnKey in columns)
                            {
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5)
                                    .AlignCenter().AlignMiddle().Text(GetMasterReportHeader(columnKey)).Bold().FontSize(9).LetterSpacing(0.04f);
                            }
                        });

                        foreach (var entry in dto.Entries)
                        {
                            foreach (var columnKey in columns)
                            {
                                AddBodyCell(table, GetMasterReportValue(entry, columnKey), GetMasterReportAlignment(columnKey));
                            }
                        }

                        if (dto.Entries.Count == 0)
                        {
                            table.Cell().ColumnSpan((uint)columns.Count)
                                .Border(0.5f).BorderColor(BorderGreen).Padding(10)
                                .AlignCenter().Text("No entries found for the selected filters.").FontSize(10);
                        }

                        AddMasterReportTotalsRow(table, dto, columns);
                    });

                    column.Item().PaddingTop(14).Text("Account Summary").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(definition =>
                        {
                            definition.RelativeColumn(1.8f);
                            definition.ConstantColumn(96);
                            definition.ConstantColumn(96);
                            definition.ConstantColumn(96);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("ACCOUNT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("TOTAL DEBIT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("TOTAL CREDIT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("BALANCE").Bold().FontSize(9).LetterSpacing(0.04f);
                        });

                        foreach (var summary in accountSummaries)
                        {
                            AddBodyCell(table, summary.AccountName, CellTextAlign.Left);
                            AddBodyCell(table, FormatCurrency(summary.TotalDebit), CellTextAlign.Right);
                            AddBodyCell(table, FormatCurrency(summary.TotalCredit), CellTextAlign.Right);
                            AddBodyCell(table, FormatCurrency(summary.Balance), CellTextAlign.Right);
                        }

                        if (accountSummaries.Count == 0)
                        {
                            table.Cell().ColumnSpan(4).Border(0.5f).BorderColor(BorderGreen).Padding(10)
                                .AlignCenter().Text("No account totals found for the selected filters.").FontSize(10);
                        }

                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderLeft(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text("Totals").Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalDebit)).Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalCredit)).Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderRight(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.NetBalance)).Bold().FontSize(10);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateAccountClosingReportPdf(AccountClosingReportDto dto)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(18).Column(column =>
                {
                    column.Item().Element(ComposeCompanyHeader);
                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Green);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("ACCOUNT CLOSING REPORT").Bold().FontSize(16).FontColor(Green);
                            info.Item().PaddingTop(4).Text($"Date Range: {BuildAccountClosingDateRange(dto)}").FontSize(9);
                            info.Item().PaddingTop(4).Text(BuildAccountClosingScope(dto)).Bold().FontSize(10);
                        });

                        row.ConstantItem(180).AlignRight().Border(1.2f).BorderColor(Green).Padding(8).Column(summary =>
                        {
                            summary.Item().AlignRight().Text($"Accounts: {dto.TotalAccounts}").Bold().FontSize(10);
                            summary.Item().PaddingTop(4).AlignRight().Text($"Opening: {FormatCurrency(dto.TotalOpeningBalance)}").FontSize(9);
                            summary.Item().AlignRight().Text($"Debit: {FormatCurrency(dto.TotalDebit)}").FontSize(9);
                            summary.Item().AlignRight().Text($"Credit: {FormatCurrency(dto.TotalCredit)}").FontSize(9);
                            summary.Item().PaddingTop(2).AlignRight().Text($"Closing: {FormatCurrency(dto.TotalClosingBalance)}").Bold().FontSize(10);
                        });
                    });

                    column.Item().PaddingTop(12).Text("Summary").Bold().FontSize(12);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.7f);
                            columns.ConstantColumn(95);
                            columns.ConstantColumn(95);
                            columns.ConstantColumn(95);
                            columns.ConstantColumn(95);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("ACCOUNT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("OPENING").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DEBIT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("CREDIT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("CLOSING").Bold().FontSize(9).LetterSpacing(0.04f);
                        });

                        foreach (var row in dto.Accounts)
                        {
                            AddBodyCell(table, row.AccountName, CellTextAlign.Left);
                            AddBodyCell(table, FormatCurrency(row.OpeningBalance), CellTextAlign.Right);
                            AddBodyCell(table, FormatCurrency(row.PeriodDebit), CellTextAlign.Right);
                            AddBodyCell(table, FormatCurrency(row.PeriodCredit), CellTextAlign.Right);
                            AddBodyCell(table, FormatCurrency(row.ClosingBalance), CellTextAlign.Right);
                        }

                        if (dto.Accounts.Count == 0)
                        {
                            table.Cell().ColumnSpan(5).Border(0.5f).BorderColor(BorderGreen).Padding(10)
                                .AlignCenter().Text("No account rows found for the selected filters.").FontSize(10);
                        }

                        table.Cell().ColumnSpan(1).BorderTop(1.5f).BorderBottom(1.5f).BorderLeft(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text("Totals").Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalOpeningBalance)).Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalDebit)).Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalCredit)).Bold().FontSize(10);
                        table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderRight(1.5f).BorderColor(Green).Padding(6)
                            .AlignRight().Text(FormatCurrency(dto.TotalClosingBalance)).Bold().FontSize(10);
                    });

                    if (dto.History.Count > 0)
                    {
                        column.Item().PaddingTop(14).Text($"History: {dto.HistoryAccountName ?? "Selected Account"}").Bold().FontSize(12);
                        column.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(78);
                                columns.RelativeColumn(1.9f);
                                columns.ConstantColumn(52);
                                columns.ConstantColumn(62);
                                columns.ConstantColumn(82);
                                columns.ConstantColumn(82);
                                columns.ConstantColumn(88);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DATE").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("VOUCHER").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DESCRIPTION").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("QTY").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("RATE").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DEBIT").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("CREDIT").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("BALANCE").Bold().FontSize(9).LetterSpacing(0.04f);
                            });

                            foreach (var entry in dto.History)
                            {
                                AddBodyCell(table, entry.Date.ToString("dd/MM/yyyy"), CellTextAlign.Center);
                                AddBodyCell(table, entry.VoucherNumber, CellTextAlign.Center);
                                AddBodyCell(table, entry.Description ?? "-", CellTextAlign.Left);
                                AddBodyCell(table, entry.Quantity?.ToString() ?? "-", CellTextAlign.Center);
                                AddBodyCell(table, entry.Rate.HasValue ? entry.Rate.Value.ToString("N2") : "-", CellTextAlign.Right);
                                AddBodyCell(table, entry.Debit > 0 ? FormatCurrency(entry.Debit) : "-", CellTextAlign.Right);
                                AddBodyCell(table, entry.Credit > 0 ? FormatCurrency(entry.Credit) : "-", CellTextAlign.Right);
                                AddBodyCell(table, FormatCurrency(entry.RunningBalance), CellTextAlign.Right);
                            }
                        });
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateProductStockReportPdf(ProductStockReportDto dto, int? selectedMovementProductId = null)
    {
        var selectedProduct = selectedMovementProductId.HasValue
            ? dto.Products.FirstOrDefault(product => product.ProductId == selectedMovementProductId.Value)
            : null;
        var selectedMovements = selectedMovementProductId.HasValue
            ? dto.Movements.Where(movement => movement.ProductId == selectedMovementProductId.Value).ToList()
            : new List<ProductStockMovementDto>();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(18).Column(column =>
                {
                    column.Item().Element(ComposeCompanyHeader);
                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Green);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("PRODUCT STOCK REPORT").Bold().FontSize(16).FontColor(Green);
                            info.Item().PaddingTop(4).Text($"Date Range: {BuildProductStockDateRange(dto)}").FontSize(9);
                            info.Item().PaddingTop(4).Text(BuildProductStockScope(dto)).Bold().FontSize(10);
                        });

                        row.ConstantItem(180).AlignRight().Border(1.2f).BorderColor(Green).Padding(8).Column(summary =>
                        {
                            summary.Item().AlignRight().Text($"Products: {dto.Totals.ProductCount}").Bold().FontSize(10);
                            summary.Item().PaddingTop(4).AlignRight().Text($"Closing Kg: {dto.Totals.Closing.Kg:N2}").FontSize(9);
                            summary.Item().PaddingTop(2).AlignRight().Text($"Closing Value: {FormatCurrency(dto.Totals.Closing.Value)}").Bold().FontSize(10);
                        });
                    });

                    column.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.2f);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(86);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("PRODUCT").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("OPEN KG").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("IN KG").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("OUT KG").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("CLOSE KG").Bold().FontSize(9).LetterSpacing(0.04f);
                            header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("CLOSE VALUE").Bold().FontSize(9).LetterSpacing(0.04f);
                        });

                        foreach (var row in dto.Products)
                        {
                            AddBodyCell(table, string.IsNullOrWhiteSpace(row.Packing) ? row.ProductName : $"{row.ProductName} ({row.Packing})", CellTextAlign.Left);
                            AddBodyCell(table, row.Opening.Kg.ToString("N2"), CellTextAlign.Right);
                            AddBodyCell(table, row.Inward.Kg.ToString("N2"), CellTextAlign.Right);
                            AddBodyCell(table, row.Outward.Kg.ToString("N2"), CellTextAlign.Right);
                            AddBodyCell(table, row.Closing.Kg.ToString("N2"), CellTextAlign.Right);
                            AddBodyCell(table, FormatCurrency(row.Closing.Value), CellTextAlign.Right);
                        }

                        if (dto.Products.Count == 0)
                        {
                            table.Cell().ColumnSpan(7).Border(0.5f).BorderColor(BorderGreen).Padding(10)
                                .AlignCenter().Text("No products matched the selected filters.").FontSize(10);
                        }
                    });

                    if (selectedProduct != null && selectedMovements.Count > 0)
                    {
                        column.Item().PaddingTop(14).Text($"Movement Drilldown: {selectedProduct.ProductName}").Bold().FontSize(12);
                        column.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(74);
                                columns.ConstantColumn(76);
                                columns.ConstantColumn(72);
                                columns.ConstantColumn(58);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(72);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(70);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DATE").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("VOUCHER").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("TYPE").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("QTY").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("WEIGHT").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("VALUE").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("DIRECTION").Bold().FontSize(9).LetterSpacing(0.04f);
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5).AlignCenter().AlignMiddle().Text("EDITED").Bold().FontSize(9).LetterSpacing(0.04f);
                            });

                            foreach (var movement in selectedMovements)
                            {
                                AddBodyCell(table, movement.Date.ToString("dd/MM/yyyy"), CellTextAlign.Center);
                                AddBodyCell(table, movement.VoucherNumber, CellTextAlign.Center);
                                AddBodyCell(table, movement.VoucherType, CellTextAlign.Center);
                                AddBodyCell(table, movement.Qty?.ToString() ?? "-", CellTextAlign.Center);
                                AddBodyCell(table, movement.WeightKg.ToString("N2"), CellTextAlign.Right);
                                AddBodyCell(table, FormatCurrency(movement.Value), CellTextAlign.Right);
                                AddBodyCell(table, movement.Direction, CellTextAlign.Center);
                                AddBodyCell(table, movement.IsEdited ? "Yes" : "No", CellTextAlign.Center);
                            }
                        });
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[] GenerateVoucherPdf(
        string voucherNumber,
        DateOnly date,
        string? partyName,
        string? vehicleNumber,
        string? description,
        IReadOnlyList<VoucherPdfLine> lines)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(30).Column(col =>
                {
                    col.Spacing(0);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Row(logoRow =>
                        {
                            logoRow.ConstantItem(55).Height(55).Svg(_logoSvg);

                            logoRow.RelativeItem().PaddingLeft(10).Column(c =>
                            {
                                c.Item().Text("KARACHI PLASTIC INDUSTRIES").Bold().FontSize(16).FontColor(Green);
                                c.Item().PaddingTop(2).Text(t => t.Span("Manufacturer of Quality Recycled Polymers").FontSize(9));
                                c.Item().Text(t => t.Span("ABS, AS, PS, HI, PP & Much More").FontSize(9));
                            });
                        });

                        row.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Border(1.5f).BorderColor(Green).Padding(4)
                                .Text(t =>
                                {
                                    t.AlignCenter();
                                    t.Span("OUTWARD").Bold().FontSize(10).LetterSpacing(0.08f);
                                    t.EmptyLine();
                                    t.Span("GATE PASS").Bold().FontSize(10).LetterSpacing(0.08f);
                                });
                            c.Item().PaddingTop(6).AlignRight().Text(t =>
                            {
                                t.AlignRight();
                                t.Span("Moin Niazi Qawwal Street").FontSize(8);
                                t.EmptyLine();
                                t.Span("Opposite Meezan Bank").FontSize(8);
                                t.EmptyLine();
                                t.Span("Nishter Road Main Shoe").FontSize(8);
                                t.EmptyLine();
                                t.Span("Market, Karachi.").FontSize(8);
                            });
                        });
                    });

                    col.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Green);

                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Row(r =>
                        {
                            r.AutoItem().AlignBottom().Text("No.").Bold().FontSize(11);
                            r.AutoItem().PaddingLeft(6).BorderBottom(1).BorderColor(Green)
                                .PaddingHorizontal(4).PaddingBottom(2)
                                .Text(voucherNumber).Bold().FontSize(16);
                        });

                        row.RelativeItem().AlignCenter().Row(r =>
                        {
                            r.AutoItem().AlignBottom().Text("Date:").Bold().FontSize(11);
                            r.AutoItem().PaddingLeft(6).BorderBottom(1).BorderColor(Green)
                                .PaddingHorizontal(4).PaddingBottom(2)
                                .Text(date.ToString("dd/MM/yyyy")).FontSize(12);
                        });

                        row.RelativeItem().AlignRight().Row(r =>
                        {
                            r.AutoItem().AlignBottom().Text("Vehicle No.").Bold().FontSize(11);
                            r.AutoItem().PaddingLeft(6).BorderBottom(1).BorderColor(Green)
                                .PaddingHorizontal(4).PaddingBottom(2)
                                .Text(vehicleNumber ?? "-").FontSize(12);
                        });
                    });

                    col.Item().PaddingBottom(14).Row(row =>
                    {
                        row.AutoItem().AlignBottom().Text("To M/s.").Bold().FontSize(11);
                        row.RelativeItem().PaddingLeft(8).BorderBottom(1).BorderColor(Green)
                            .PaddingHorizontal(4).PaddingBottom(2)
                            .Text(partyName ?? "-").Bold().FontSize(13);
                    });

                    col.Item().Table(table =>
                    {
                        var mergedLines = BuildMergedVoucherLines(lines);

                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(90);
                            columns.ConstantColumn(90);
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen).Padding(6).AlignCenter().AlignMiddle().Text("BAGS").Bold().FontSize(10).LetterSpacing(0.06f);
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen).Padding(6).AlignCenter().AlignMiddle().Text("QTY").Bold().FontSize(10).LetterSpacing(0.06f);
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen).Padding(6).AlignCenter().AlignMiddle().Text("DESCRIPTION").Bold().FontSize(10).LetterSpacing(0.06f);
                        });

                        foreach (var line in mergedLines)
                        {
                            var desc = line.ProductName ?? string.Empty;
                            if (!string.IsNullOrEmpty(line.Packing))
                            {
                                desc += $" ({line.Packing})";
                            }
                            if (line.LooseWeightKg > 0)
                            {
                                desc += $" - Loose {FormatDecimal(line.LooseWeightKg)} kg";
                            }

                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(line.PackedBags > 0 ? $"{line.PackedBags}" : "-").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text($"{FormatDecimal(line.TotalWeightKg)} kg").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).Text(desc).FontSize(11);
                        }

                        var emptyCount = Math.Max(0, MinRows - mergedLines.Count);
                        for (var i = 0; i < emptyCount; i++)
                        {
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(" ").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(" ").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).Text(" ").FontSize(11);
                        }

                        var totalBags = mergedLines.Sum(line => line.PackedBags);
                        var totalWeightAll = mergedLines.Sum(line => line.TotalWeightKg);

                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderLeft(1.5f).BorderRight(0.5f).BorderColor(Green).Padding(6).AlignCenter().Text($"{totalBags}").Bold().FontSize(12);
                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderLeft(0.5f).BorderRight(0.5f).BorderColor(Green).Padding(6).AlignCenter().Text($"{FormatDecimal(totalWeightAll)} kg").Bold().FontSize(12);
                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderRight(1.5f).BorderLeft(0.5f).BorderColor(Green).Padding(6).Text("Total").Bold().FontSize(12);
                    });

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        col.Item().PaddingTop(14).Row(row =>
                        {
                            row.AutoItem().Text("Note:").Bold().FontSize(11);
                            row.RelativeItem().PaddingLeft(8).Text(description).FontSize(11);
                        });
                    }

                    col.Item().PaddingTop(50).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().BorderBottom(1.5f).BorderColor(Green).Height(35);
                            c.Item().PaddingTop(4).AlignCenter().Text("Prepared by").Bold().FontSize(11);
                        });
                        row.ConstantItem(150);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().BorderBottom(1.5f).BorderColor(Green).Height(35);
                            c.Item().PaddingTop(4).AlignCenter().Text("Received by").Bold().FontSize(11);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeCompanyHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Row(logoRow =>
            {
                logoRow.ConstantItem(55).Height(55).Svg(_logoSvg);

                logoRow.RelativeItem().PaddingLeft(10).Column(c =>
                {
                    c.Item().Text("KARACHI PLASTIC INDUSTRIES").Bold().FontSize(16).FontColor(Green);
                    c.Item().PaddingTop(2).Text(t => t.Span("Manufacturer of Quality Recycled Polymers").FontSize(9));
                    c.Item().Text(t => t.Span("ABS, AS, PS, HI, PP & Much More").FontSize(9));
                });
            });

            row.ConstantItem(170).AlignRight().Text(t =>
            {
                t.AlignRight();
                t.Span("Moin Niazi Qawwal Street").FontSize(8);
                t.EmptyLine();
                t.Span("Opposite Meezan Bank").FontSize(8);
                t.EmptyLine();
                t.Span("Nishter Road Main Shoe").FontSize(8);
                t.EmptyLine();
                t.Span("Market, Karachi.").FontSize(8);
            });
        });
    }

    private static readonly string[] MasterReportColumnOrder =
    [
        "voucher", "date", "description", "account", "quantity", "rate", "debit", "credit", "balance"
    ];

    private static string FormatCurrency(decimal amount) => $"Rs. {amount:N2}";

    private static void AddKeyValueRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(0.5f).BorderColor(BorderGreen).Background(LightGreen).Padding(6)
            .AlignLeft().Text(label).Bold().FontSize(10);
        table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(6)
            .AlignLeft().Text(value).FontSize(10);
    }

    private static void AddMetricRow(TableDescriptor table, string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        table.Cell().Border(0.5f).BorderColor(BorderGreen).Background(LightGreen).Padding(6)
            .AlignLeft().Text(leftLabel).Bold().FontSize(10);
        table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(6)
            .AlignRight().Text(leftValue).FontSize(10);
        table.Cell().Border(0.5f).BorderColor(BorderGreen).Background(LightGreen).Padding(6)
            .AlignLeft().Text(rightLabel).Bold().FontSize(10);
        table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(6)
            .AlignRight().Text(rightValue).FontSize(10);
    }

    private enum CellTextAlign
    {
        Left,
        Center,
        Right
    }

    private static void AddBodyCell(TableDescriptor table, string text, CellTextAlign align)
    {
        var cell = table.Cell()
            .Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignMiddle();

        cell = align switch
        {
            CellTextAlign.Right => cell.AlignRight(),
            CellTextAlign.Center => cell.AlignCenter(),
            _ => cell.AlignLeft()
        };

        cell.Text(text).FontSize(9);
    }

    private static IReadOnlyList<string> NormalizeMasterReportColumns(IReadOnlyCollection<string>? visibleColumns)
    {
        if (visibleColumns == null || visibleColumns.Count == 0)
            return MasterReportColumnOrder;

        var selected = new HashSet<string>(visibleColumns, StringComparer.OrdinalIgnoreCase);
        var normalized = MasterReportColumnOrder
            .Where(column => selected.Contains(column))
            .ToArray();

        return normalized.Length > 0 ? normalized : MasterReportColumnOrder;
    }

    private static void ConfigureMasterReportColumn(TableColumnsDefinitionDescriptor columns, string columnKey)
    {
        switch (columnKey)
        {
            case "voucher":
                columns.ConstantColumn(72);
                break;
            case "date":
                columns.ConstantColumn(68);
                break;
            case "description":
                columns.RelativeColumn(1.8f);
                break;
            case "account":
                columns.RelativeColumn(1.3f);
                break;
            case "quantity":
                columns.ConstantColumn(52);
                break;
            case "rate":
                columns.ConstantColumn(58);
                break;
            case "debit":
            case "credit":
                columns.ConstantColumn(74);
                break;
            case "balance":
                columns.ConstantColumn(78);
                break;
            default:
                columns.RelativeColumn();
                break;
        }
    }

    private static string GetMasterReportHeader(string columnKey) => columnKey switch
    {
        "voucher" => "VOUCHER",
        "date" => "DATE",
        "description" => "DESCRIPTION",
        "account" => "ACCOUNT",
        "quantity" => "QTY",
        "rate" => "RATE",
        "debit" => "DEBIT",
        "credit" => "CREDIT",
        "balance" => "BALANCE",
        _ => columnKey.ToUpperInvariant()
    };

    private static string GetMasterReportValue(MasterReportEntryDto entry, string columnKey) => columnKey switch
    {
        "voucher" => entry.VoucherNumber,
        "date" => entry.Date.ToString("dd/MM/yyyy"),
        "description" => entry.Description ?? "-",
        "account" => entry.AccountName,
        "quantity" => entry.Quantity?.ToString() ?? "-",
        "rate" => entry.Rate.HasValue ? entry.Rate.Value.ToString("N2") : "-",
        "debit" => entry.Debit > 0 ? FormatCurrency(entry.Debit) : "-",
        "credit" => entry.Credit > 0 ? FormatCurrency(entry.Credit) : "-",
        "balance" => FormatCurrency(entry.RunningBalance),
        _ => "-"
    };

    private static CellTextAlign GetMasterReportAlignment(string columnKey) => columnKey switch
    {
        "voucher" => CellTextAlign.Center,
        "date" => CellTextAlign.Center,
        "quantity" => CellTextAlign.Center,
        "debit" => CellTextAlign.Right,
        "credit" => CellTextAlign.Right,
        "rate" => CellTextAlign.Right,
        "balance" => CellTextAlign.Right,
        _ => CellTextAlign.Left
    };

    private static void AddMasterReportTotalsRow(TableDescriptor table, MasterReportDto dto, IReadOnlyList<string> columns)
    {
        var totalColumns = columns
            .Where(column => column is "debit" or "credit" or "balance")
            .ToList();
        var leadingSpan = columns.Count - totalColumns.Count;

        if (leadingSpan > 0)
        {
            table.Cell().ColumnSpan((uint)leadingSpan).BorderTop(1.5f).BorderBottom(1.5f).BorderLeft(1.5f).BorderColor(Green).Padding(6)
                .AlignRight().Text("Totals").Bold().FontSize(11);
        }

        foreach (var totalColumn in totalColumns)
        {
            var isLast = totalColumn == totalColumns[^1];
            var cell = table.Cell().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Green).Padding(6).AlignRight();
            if (isLast)
                cell = cell.BorderRight(1.5f);

            var value = totalColumn switch
            {
                "debit" => FormatCurrency(dto.TotalDebit),
                "credit" => FormatCurrency(dto.TotalCredit),
                "balance" => FormatCurrency(dto.NetBalance),
                _ => "-"
            };

            cell.Text(value).Bold().FontSize(11);
        }
    }

    private static string BuildMasterReportScope(MasterReportDto dto)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(dto.CategoryName))
            parts.Add($"Category: {dto.CategoryName}");
        if (!string.IsNullOrWhiteSpace(dto.AccountName))
            parts.Add($"Account: {dto.AccountName}");

        return parts.Count == 0 ? "All Categories / All Accounts" : string.Join(" | ", parts);
    }

    private static string BuildMasterReportDateRange(MasterReportDto dto)
    {
        if (dto.FromDate.HasValue && dto.ToDate.HasValue)
            return $"{dto.FromDate.Value:dd/MM/yyyy} to {dto.ToDate.Value:dd/MM/yyyy}";
        if (dto.FromDate.HasValue)
            return $"From {dto.FromDate.Value:dd/MM/yyyy}";
        if (dto.ToDate.HasValue)
            return $"Up to {dto.ToDate.Value:dd/MM/yyyy}";
        return "Full Report";
    }

    private static string BuildAccountClosingScope(AccountClosingReportDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.SelectedAccountName))
            return $"Account Filter: {dto.SelectedAccountName}";

        return "All Closing Accounts";
    }

    private static string BuildAccountClosingDateRange(AccountClosingReportDto dto)
    {
        if (dto.FromDate.HasValue && dto.ToDate.HasValue)
            return $"{dto.FromDate.Value:dd/MM/yyyy} to {dto.ToDate.Value:dd/MM/yyyy}";
        if (dto.FromDate.HasValue)
            return $"From {dto.FromDate.Value:dd/MM/yyyy}";
        if (dto.ToDate.HasValue)
            return $"Up to {dto.ToDate.Value:dd/MM/yyyy}";
        return "Full Report";
    }

    private static string BuildProductStockScope(ProductStockReportDto dto)
    {
        if (dto.CategoryId.HasValue || dto.ProductId.HasValue)
            return "Filtered Inventory View";

        return "All Categories / All Products";
    }

    private static string BuildProductStockDateRange(ProductStockReportDto dto)
    {
        if (dto.From.HasValue && dto.To.HasValue)
            return $"{dto.From.Value:dd/MM/yyyy} to {dto.To.Value:dd/MM/yyyy}";
        if (dto.From.HasValue)
            return $"From {dto.From.Value:dd/MM/yyyy}";
        if (dto.To.HasValue)
            return $"Up to {dto.To.Value:dd/MM/yyyy}";
        return "Full Report";
    }

    private static string BuildDateRangeLabel(StatementOfAccountDto dto)
    {
        if (dto.FromDate.HasValue && dto.ToDate.HasValue)
            return $"{dto.FromDate.Value:dd/MM/yyyy} to {dto.ToDate.Value:dd/MM/yyyy}";

        if (dto.FromDate.HasValue)
            return $"From {dto.FromDate.Value:dd/MM/yyyy}";

        if (dto.ToDate.HasValue)
            return $"Up to {dto.ToDate.Value:dd/MM/yyyy}";

        return "Full Statement";
    }

    private static List<MergedVoucherPdfLine> BuildMergedVoucherLines(IReadOnlyList<VoucherPdfLine> lines)
    {
        return lines
            .GroupBy(line => new
            {
                line.ProductId,
                Name = line.ProductName ?? string.Empty
            })
            .Select(group =>
            {
                var first = group.First();
                var packedBags = 0;
                decimal looseWeightKg = 0m;
                decimal totalWeightKg = 0m;

                foreach (var line in group)
                {
                    var isLoose = line.Rbp == "No" && !line.ActualWeightKg.HasValue;
                    var lineWeight = line.ActualWeightKg ?? (isLoose ? line.Qty : line.PackingWeightKg * line.Qty);
                    totalWeightKg += lineWeight;

                    if (isLoose)
                    {
                        looseWeightKg += lineWeight;
                    }
                    else
                    {
                        packedBags += line.Qty;
                    }
                }

                return new MergedVoucherPdfLine(
                    first.ProductName,
                    first.Packing,
                    packedBags,
                    Round2(looseWeightKg),
                    Round2(totalWeightKg));
            })
            .ToList();
    }

    private static string FormatDecimal(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString("0")
            : value.ToString("0.##");

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string BuildContactLine(StatementOfAccountDto dto)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(dto.City))
            parts.Add(dto.City);
        if (!string.IsNullOrWhiteSpace(dto.Phone))
            parts.Add(dto.Phone);
        if (!string.IsNullOrWhiteSpace(dto.ContactPerson))
            parts.Add(dto.ContactPerson);

        return string.Join(" | ", parts);
    }

    private sealed record VoucherPdfLine(
        int ProductId,
        string? ProductName,
        string? Packing,
        decimal PackingWeightKg,
        string Rbp,
        int Qty,
        decimal? ActualWeightKg);

    private sealed record MergedVoucherPdfLine(
        string? ProductName,
        string? Packing,
        int PackedBags,
        decimal LooseWeightKg,
        decimal TotalWeightKg);

    public byte[] GenerateCustomerLedgerPdf(CustomerLedgerDto dto)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Green).FontFamily("Times New Roman"));

                page.Content().Border(1.5f).BorderColor(Green).Padding(20).Column(column =>
                {
                    column.Spacing(0);

                    column.Item().Element(ComposeCompanyHeader);
                    column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Green);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(info =>
                        {
                            info.Item().Text("CUSTOMER / VENDOR LEDGER").Bold().FontSize(16).FontColor(Green);
                            info.Item().PaddingTop(4).Text(dto.AccountName).Bold().FontSize(13);
                            info.Item().PaddingTop(4).Text(dto.PartyType).FontSize(10);
                        });

                        row.ConstantItem(200).AlignRight().Border(1.2f).BorderColor(Green).Padding(8).Column(summary =>
                        {
                            summary.Item().AlignRight().Text($"Entries: {dto.Entries.Count}").Bold().FontSize(10);
                            summary.Item().PaddingTop(4).AlignRight().Text(dto.HasOpeningBalance
                                ? $"Opening: {FormatCurrency(dto.OpeningBalance)}"
                                : "Opening: ").FontSize(10);
                            summary.Item().AlignRight().Text($"Closing: {FormatCurrency(dto.ClosingBalance)}").Bold().FontSize(10);
                        });
                    });

                    column.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(def =>
                        {
                            def.ConstantColumn(46); // #
                            def.ConstantColumn(72); // Date
                            def.ConstantColumn(70); // Voucher #
                            def.ConstantColumn(80); // Type
                            def.RelativeColumn(3);   // Description
                            def.RelativeColumn(2);   // Product
                            def.ConstantColumn(46); // Qty
                            def.ConstantColumn(56); // Weight
                            def.ConstantColumn(56); // Rate
                            def.ConstantColumn(80); // Debit
                            def.ConstantColumn(80); // Credit
                            def.ConstantColumn(90); // Balance
                        });

                        table.Header(header =>
                        {
                            var headers = new[] { "#", "Date", "Vchr #", "Type", "Description", "Product", "Qty", "Wt(Kg)", "Rate", "Debit", "Credit", "Balance" };
                            foreach (var h in headers)
                                header.Cell().Border(1.2f).BorderColor(Green).Background(LightGreen).Padding(5)
                                    .AlignCenter().AlignMiddle().Text(h).Bold().FontSize(9).LetterSpacing(0.04f);
                        });

                        var runningBalance = dto.OpeningBalance;
                        var idx = 1;
                        foreach (var entry in dto.Entries)
                        {
                            var rowBg = idx % 2 == 0 ? Color.FromHex("#f0f8f0") : Color.FromHex("#ffffff");
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(idx.ToString()).FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(entry.Date.ToString("yyyy-MM-dd")).FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(entry.VoucherNumber).FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(entry.VoucherType).FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).Text(entry.Description ?? "-").FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).Text(entry.ProductName ?? "-").FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().Text(entry.Qty?.ToString() ?? "-").FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().Text(entry.Weight?.ToString("0.##") ?? "-").FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().Text(entry.Rate?.ToString("0.##") ?? "-").FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().Text(entry.Debit > 0 ? FormatCurrency(entry.Debit) : "-").FontSize(9);
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().Text(entry.Credit > 0 ? FormatCurrency(entry.Credit) : "-").FontSize(9);

                            runningBalance += entry.Debit - entry.Credit;
                            table.Cell().Background(rowBg).Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignRight().Text(FormatCurrency(runningBalance)).Bold().FontSize(9);

                            idx++;
                        }
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}
