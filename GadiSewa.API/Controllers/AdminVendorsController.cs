using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Vendors;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/vendors")]
[Authorize(Policy = "BackOfficeOnly")]
public sealed class AdminVendorsController : ControllerBase
{
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<PurchaseInvoice> _purchaseInvoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminVendorsController(
        IRepository<Vendor> vendorRepository,
        IRepository<PurchaseInvoice> purchaseInvoiceRepository,
        IUnitOfWork unitOfWork)
    {
        _vendorRepository = vendorRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VendorDto>>>> GetVendors(CancellationToken cancellationToken)
    {
        var vendors = await _vendorRepository.ListAsync(cancellationToken: cancellationToken);
        var dto = vendors.Select(VendorDto.FromVendor).ToList();
        return Ok(ApiResponse<IReadOnlyList<VendorDto>>.Success(dto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> GetVendor(Guid id, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(id, cancellationToken);
        if (vendor is null)
        {
            return NotFound(ApiResponse<VendorDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<VendorDto>.Success(VendorDto.FromVendor(vendor)));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> CreateVendor([FromBody] CreateVendorRequestDto request, CancellationToken cancellationToken)
    {
        var existingEmail = await _vendorRepository.ListAsync(x => x.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (existingEmail.Count > 0)
        {
            return Conflict(ApiResponse<VendorDto>.Failure("A vendor with this email already exists.", StatusCodes.Status409Conflict));
        }

        var vendor = new Vendor
        {
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Address = request.Address?.Trim() ?? string.Empty
        };

        await _vendorRepository.AddAsync(vendor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<VendorDto>.Success(VendorDto.FromVendor(vendor), StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<VendorDto>>> UpdateVendor(Guid id, [FromBody] UpdateVendorRequestDto request, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(id, cancellationToken);
        if (vendor is null)
        {
            return NotFound(ApiResponse<VendorDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
        }

        var conflictEmail = await _vendorRepository.ListAsync(x => x.Email == request.Email.Trim().ToLowerInvariant() && x.Id != id, cancellationToken);
        if (conflictEmail.Count > 0)
        {
            return Conflict(ApiResponse<VendorDto>.Failure("Another vendor with this email already exists.", StatusCodes.Status409Conflict));
        }

        vendor.Name = request.Name.Trim();
        vendor.ContactPerson = request.ContactPerson.Trim();
        vendor.Email = request.Email.Trim().ToLowerInvariant();
        vendor.PhoneNumber = request.PhoneNumber.Trim();
        vendor.Address = request.Address?.Trim() ?? string.Empty;
        vendor.UpdatedAt = DateTimeOffset.UtcNow;

        _vendorRepository.Update(vendor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<VendorDto>.Success(VendorDto.FromVendor(vendor)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteVendor(Guid id, CancellationToken cancellationToken)
    {
        var vendor = await _vendorRepository.GetByIdAsync(id, cancellationToken);
        if (vendor is null)
        {
            return NotFound(ApiResponse<object?>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
        }

        var invoices = await _purchaseInvoiceRepository
            .ListAsync(pi => pi.VendorId == id, cancellationToken);

        if (invoices.Count > 0)
        {
            return BadRequest(ApiResponse<object?>.Failure(
                "Cannot delete vendor with existing purchase invoices.",
                StatusCodes.Status400BadRequest));
        }

        _vendorRepository.Remove(vendor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
    }
}