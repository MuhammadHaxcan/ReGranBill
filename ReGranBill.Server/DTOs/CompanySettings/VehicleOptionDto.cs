namespace ReGranBill.Server.DTOs.CompanySettings;

public class VehicleOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class UpdateVehicleOptionsRequest
{
    public List<VehicleOptionUpsertItem> Vehicles { get; set; } = new();
}

public class VehicleOptionUpsertItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
