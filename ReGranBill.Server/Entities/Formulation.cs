namespace ReGranBill.Server.Entities;

public class Formulation
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BaseInputKg { get; set; } = 100m;
    public bool IsActive { get; set; } = true;
    public int CreatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Creator { get; set; } = null!;
    public ICollection<FormulationLine> Lines { get; set; } = new List<FormulationLine>();
}
