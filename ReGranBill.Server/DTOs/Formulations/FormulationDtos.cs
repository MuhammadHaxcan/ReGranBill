using ReGranBill.Server.Enums;

namespace ReGranBill.Server.DTOs.Formulations;

public class CreateFormulationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BaseInputKg { get; set; } = 100m;
    public bool IsActive { get; set; } = true;
    public List<FormulationLineRequest> Lines { get; set; } = new();
}

public class FormulationLineRequest
{
    public ProductionLineKind LineKind { get; set; }
    public int AccountId { get; set; }
    public decimal AmountPerBase { get; set; }
    public decimal? BagsPerBase { get; set; }
    public int SortOrder { get; set; }
}

public class FormulationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BaseInputKg { get; set; }
    public bool IsActive { get; set; }
    public List<FormulationLineDto> Lines { get; set; } = new();
    public DateOnly CreatedAt { get; set; }
    public DateOnly UpdatedAt { get; set; }
}

public class FormulationLineDto
{
    public int Id { get; set; }
    public ProductionLineKind LineKind { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal AmountPerBase { get; set; }
    public decimal? BagsPerBase { get; set; }
    public int SortOrder { get; set; }
}

public class ApplyFormulationRequest
{
    public decimal TotalInputKg { get; set; }
}

public class ApplyFormulationResponse
{
    public List<AppliedLineDto> Inputs { get; set; } = new();
    public List<AppliedLineDto> Outputs { get; set; } = new();
    public List<AppliedLineDto> Byproducts { get; set; } = new();
    public AppliedShortageDto? Shortage { get; set; }
}

public class AppliedLineDto
{
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public int Qty { get; set; }
    public decimal WeightKg { get; set; }
    public int SortOrder { get; set; }
}

public class AppliedShortageDto
{
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal WeightKg { get; set; }
}
