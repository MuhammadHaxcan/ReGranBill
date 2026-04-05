namespace ReGranBill.Server.Entities;

public class VehicleOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public string NormalizedVehicleNumber { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
