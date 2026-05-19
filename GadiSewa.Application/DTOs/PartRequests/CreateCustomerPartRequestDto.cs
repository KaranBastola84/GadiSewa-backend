namespace GadiSewa.Application.DTOs.PartRequests;

public sealed class CreateCustomerPartRequestDto
{
    public string PartName { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string VehicleModel { get; init; } = string.Empty;
    public string Urgency { get; init; } = "Medium";
    public string Notes { get; init; } = string.Empty;
}
