using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReGranBill.Server.DTOs.DeliveryChallans;

namespace ReGranBill.Server.Services;

public class PdfService : IPdfService
{
    private const string Green = "#1a5c2a";
    private const string LightGreen = "#f0f8f0";
    private const string BorderGreen = "#a0c8a0";
    private const int MinRows = 8;

    public byte[] GenerateDeliveryChallanPdf(DeliveryChallanDto dto)
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

                    // Header
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Row(logoRow =>
                        {
                            logoRow.ConstantItem(55).Height(55).Svg(DiamondLogoSvg());

                            logoRow.RelativeItem().PaddingLeft(10).Column(c =>
                            {
                                c.Item().Text("KARACHI PLASTIC INDUSTRIES")
                                    .Bold().FontSize(16).FontColor(Green);
                                c.Item().PaddingTop(2).Text(t =>
                                {
                                    t.Span("Manufacturer of Quality Recycled Polymers").FontSize(9);
                                });
                                c.Item().Text(t =>
                                {
                                    t.Span("ABS, AS, PS, HI, PP & Much More").FontSize(9);
                                });
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

                    // Divider
                    col.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Green);

                    // Meta row: No. / Date / Vehicle No.
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        row.RelativeItem().Row(r =>
                        {
                            r.AutoItem().AlignBottom().Text("No.").Bold().FontSize(11);
                            r.AutoItem().PaddingLeft(6).BorderBottom(1).BorderColor(Green)
                                .PaddingHorizontal(4).PaddingBottom(2)
                                .Text(dto.DcNumber).Bold().FontSize(16);
                        });

                        row.RelativeItem().AlignCenter().Row(r =>
                        {
                            r.AutoItem().AlignBottom().Text("Date:").Bold().FontSize(11);
                            r.AutoItem().PaddingLeft(6).BorderBottom(1).BorderColor(Green)
                                .PaddingHorizontal(4).PaddingBottom(2)
                                .Text(dto.Date.ToString("dd/MM/yyyy")).FontSize(12);
                        });

                        row.RelativeItem().AlignRight().Row(r =>
                        {
                            r.AutoItem().AlignBottom().Text("Vehicle No.").Bold().FontSize(11);
                            r.AutoItem().PaddingLeft(6).BorderBottom(1).BorderColor(Green)
                                .PaddingHorizontal(4).PaddingBottom(2)
                                .Text(dto.VehicleNumber ?? "—").FontSize(12);
                        });
                    });

                    // Customer row
                    col.Item().PaddingBottom(14).Row(row =>
                    {
                        row.AutoItem().AlignBottom().Text("To M/s.").Bold().FontSize(11);
                        row.RelativeItem().PaddingLeft(8).BorderBottom(1).BorderColor(Green)
                            .PaddingHorizontal(4).PaddingBottom(2)
                            .Text(dto.CustomerName ?? "—").Bold().FontSize(13);
                    });

                    // Table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(90);   // PACKAGES
                            columns.RelativeColumn();      // DESCRIPTION
                            columns.ConstantColumn(90);    // QTY
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen)
                                .Padding(6).AlignCenter().AlignMiddle()
                                .Text("PACKAGES").Bold().FontSize(10).LetterSpacing(0.06f);
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen)
                                .Padding(6).AlignCenter().AlignMiddle()
                                .Text("DESCRIPTION").Bold().FontSize(10).LetterSpacing(0.06f);
                            header.Cell().Border(1.5f).BorderColor(Green).Background(LightGreen)
                                .Padding(6).AlignCenter().AlignMiddle()
                                .Text("QTY").Bold().FontSize(10).LetterSpacing(0.06f);
                        });

                        // Data rows
                        var lines = dto.Lines ?? new List<DcLineDto>();
                        foreach (var line in lines)
                        {
                            var isLoose = line.Rbp == "No";
                            var weightText = isLoose
                                ? "Loose"
                                : $"{line.PackingWeightKg * line.Qty} kg";
                            var desc = line.ProductName ?? "";
                            if (!string.IsNullOrEmpty(line.Packing))
                                desc += $" ({line.Packing})";

                            table.Cell().Border(0.5f).BorderColor(BorderGreen)
                                .Padding(5).AlignCenter()
                                .Text(weightText).FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen)
                                .Padding(5)
                                .Text(desc).FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen)
                                .Padding(5).AlignCenter()
                                .Text(isLoose ? $"{line.Qty} kg" : $"{line.Qty}").FontSize(11);
                        }

                        // Empty rows to pad to minimum
                        var emptyCount = Math.Max(0, MinRows - lines.Count);
                        for (int i = 0; i < emptyCount; i++)
                        {
                            table.Cell().Border(0.5f).BorderColor(BorderGreen)
                                .Padding(5).AlignCenter().Text(" ").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen)
                                .Padding(5).Text(" ").FontSize(11);
                            table.Cell().Border(0.5f).BorderColor(BorderGreen)
                                .Padding(5).AlignCenter().Text(" ").FontSize(11);
                        }

                        // Total row
                        var totalQty = lines.Where(l => l.Rbp != "No").Sum(l => l.Qty);
                        var looseWeightTotal = lines.Where(l => l.Rbp == "No").Sum(l => l.Qty);
                        var packedWeightTotal = lines.Where(l => l.Rbp != "No").Sum(l => l.PackingWeightKg * l.Qty);
                        var totalWeightAll = packedWeightTotal + looseWeightTotal;

                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderLeft(1.5f).BorderRight(0.5f).BorderColor(Green)
                            .Padding(6).AlignCenter()
                            .Text($"{totalWeightAll} kg").Bold().FontSize(12);
                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderLeft(0.5f).BorderRight(0.5f).BorderColor(Green)
                            .Padding(6)
                            .Text("Total").Bold().FontSize(12);
                        table.Cell().BorderTop(2).BorderBottom(1.5f).BorderRight(1.5f).BorderLeft(0.5f).BorderColor(Green)
                            .Padding(6).AlignCenter()
                            .Text($"{totalQty}").Bold().FontSize(12);
                    });

                    // Description / Note
                    if (!string.IsNullOrWhiteSpace(dto.Description))
                    {
                        col.Item().PaddingTop(14).Row(row =>
                        {
                            row.AutoItem().Text("Note:").Bold().FontSize(11);
                            row.RelativeItem().PaddingLeft(8).Text(dto.Description).FontSize(11);
                        });
                    }

                    // Signatures
                    col.Item().PaddingTop(50).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().BorderBottom(1.5f).BorderColor(Green).Height(35);
                            c.Item().PaddingTop(4).AlignCenter().Text("Prepared by").Bold().FontSize(11);
                        });
                        row.ConstantItem(150); // spacer
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

    private static string DiamondLogoSvg()
    {
        return @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 300 300"" width=""300"" height=""300"">
  <g transform=""translate(85, 150)"">
    <polygon fill=""#2E7D32"" points=""0,-65 65,0 0,65 -65,0"" />
  </g>
  <g transform=""translate(150, 85)"">
    <polygon fill=""#2E7D32"" points=""0,-65 65,0 0,65 -65,0"" />
  </g>
  <g transform=""translate(215, 150)"">
    <polygon fill=""#2E7D32"" points=""0,-65 65,0 0,65 -65,0"" />
  </g>
</svg>";
    }
}
