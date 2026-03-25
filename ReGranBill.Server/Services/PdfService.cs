using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.DTOs.SOA;

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
        var logoPath = Path.Combine(env.ContentRootPath, "..", "regranbill.client", "public", "KPI_LOGO.svg");
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
                line.ProductName,
                line.Packing,
                line.PackingWeightKg,
                line.Rbp,
                line.Qty)).ToList());

    public byte[] GeneratePurchaseVoucherPdf(PurchaseVoucherDto dto) =>
        GenerateVoucherPdf(
            dto.VoucherNumber,
            dto.Date,
            dto.VendorName,
            dto.VehicleNumber,
            dto.Description,
            dto.Lines.Select(line => new VoucherPdfLine(
                line.ProductName,
                line.Packing,
                line.PackingWeightKg,
                line.Rbp,
                line.Qty)).ToList());

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

    private byte[] GenerateVoucherPdf(
        string voucherNumber,
        DateTime date,
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
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(90);
                            columns.RelativeColumn();
                            columns.ConstantColumn(90);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen).Padding(6).AlignCenter().AlignMiddle().Text("PACKAGES").Bold().FontSize(10).LetterSpacing(0.06f);
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen).Padding(6).AlignCenter().AlignMiddle().Text("DESCRIPTION").Bold().FontSize(10).LetterSpacing(0.06f);
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen).Padding(6).AlignCenter().AlignMiddle().Text("QTY").Bold().FontSize(10).LetterSpacing(0.06f);
                        });

                        foreach (var line in lines)
                        {
                            var isLoose = line.Rbp == "No";
                            var weightText = isLoose ? "Loose" : $"{line.PackingWeightKg * line.Qty} kg";
                            var desc = line.ProductName ?? string.Empty;
                            if (!string.IsNullOrEmpty(line.Packing))
                            {
                                desc += $" ({line.Packing})";
                            }

                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(weightText).FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).Text(desc).FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(isLoose ? $"{line.Qty} kg" : $"{line.Qty}").FontSize(11);
                        }

                        var emptyCount = Math.Max(0, MinRows - lines.Count);
                        for (var i = 0; i < emptyCount; i++)
                        {
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(" ").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).Text(" ").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen).Padding(5).AlignCenter().Text(" ").FontSize(11);
                        }

                        var totalQty = lines.Where(l => l.Rbp != "No").Sum(l => l.Qty);
                        var looseWeightTotal = lines.Where(l => l.Rbp == "No").Sum(l => l.Qty);
                        var packedWeightTotal = lines.Where(l => l.Rbp != "No").Sum(l => l.PackingWeightKg * l.Qty);
                        var totalWeightAll = packedWeightTotal + looseWeightTotal;

                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderLeft(1.5f).BorderRight(0.5f).BorderColor(Green).Padding(6).AlignCenter().Text($"{totalWeightAll} kg").Bold().FontSize(12);
                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderLeft(0.5f).BorderRight(0.5f).BorderColor(Green).Padding(6).Text("Total").Bold().FontSize(12);
                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderRight(1.5f).BorderLeft(0.5f).BorderColor(Green).Padding(6).AlignCenter().Text($"{totalQty}").Bold().FontSize(12);
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

    private static string FormatCurrency(decimal amount) => $"Rs. {amount:N2}";

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
        string? ProductName,
        string? Packing,
        decimal PackingWeightKg,
        string Rbp,
        int Qty);
}
