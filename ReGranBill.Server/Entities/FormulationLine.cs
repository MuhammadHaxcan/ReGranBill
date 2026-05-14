using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class FormulationLine
{
    public int Id { get; set; }
    public int FormulationId { get; set; }
    public ProductionLineKind LineKind { get; set; }
    public int AccountId { get; set; }
    public decimal AmountPerBase { get; set; }
    public decimal? BagsPerBase { get; set; }
    public int SortOrder { get; set; }

    public Formulation Formulation { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
