using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.PurchaseInvoices;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/purchase-invoices")]
[Authorize]
public sealed class PurchaseInvoicesController : ControllerBase
{
    private readonly IRepository<PurchaseInvoice> _purchaseInvoiceRepository;
    private readonly IRepository<PurchaseInvoiceItem> _purchaseInvoiceItemRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public PurchaseInvoicesController(
        IRepository<PurchaseInvoice> purchaseInvoiceRepository,
        IRepository<PurchaseInvoiceItem> purchaseInvoiceItemRepository,
        IRepository<Part> partRepository,
        IRepository<Vendor> vendorRepository,
        IRepository<Staff> staffRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _purchaseInvoiceItemRepository = purchaseInvoiceItemRepository;
        _partRepository = partRepository;
        _vendorRepository = vendorRepository;
        _staffRepository = staffRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PurchaseInvoiceDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var invoices = await _purchaseInvoiceRepository.Query()
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

        var result = invoices.Select(MapToDto).ToList();
        return Ok(ApiResponse<IReadOnlyList<PurchaseInvoiceDto>>.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _purchaseInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Include(i => i.Vendor)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("Purchase invoice not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<PurchaseInvoiceDto>.Success(MapToDto(invoice)));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> Create(
        [FromBody] CreatePurchaseInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<PurchaseInvoiceDto>.Failure("At least one invoice item is required.", StatusCodes.Status400BadRequest));
        }

        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
        }

        var staff = await _staffRepository.Query()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == GetCurrentUserId(), cancellationToken);

        if (staff is null)
        {
            return BadRequest(ApiResponse<PurchaseInvoiceDto>.Failure("Current admin does not have a linked staff profile.", StatusCodes.Status400BadRequest));
        }

        var generatedInvoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken);
        var partIds = request.Items.Select(i => i.PartId).Distinct().ToList();

        var parts = await _partRepository.Query()
            .Where(p => partIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (parts.Count != partIds.Count)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("One or more parts were not found.", StatusCodes.Status404NotFound));
        }

        var subTotal = request.Items.Sum(i => i.Quantity * i.UnitCost);
        var taxAmount = Math.Round(subTotal * (request.TaxRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = subTotal + taxAmount;

        var invoice = new PurchaseInvoice
        {
            InvoiceNumber = generatedInvoiceNumber,
            VendorId = vendor.Id,
            CreatedByStaffId = staff.Id,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            SubTotal = subTotal,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            Status = request.Status
        };

        await _purchaseInvoiceRepository.AddAsync(invoice, cancellationToken);

        foreach (var item in request.Items)
        {
            var lineTotal = item.Quantity * item.UnitCost;
            var invoiceItem = new PurchaseInvoiceItem
            {
                PurchaseInvoiceId = invoice.Id,
                PartId = item.PartId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                LineTotal = lineTotal
            };

            await _purchaseInvoiceItemRepository.AddAsync(invoiceItem, cancellationToken);

            // Purchase invoice adds inventory.
            var part = parts[item.PartId];
            part.StockQuantity += item.Quantity;
            part.UpdatedAt = DateTimeOffset.UtcNow;
            _partRepository.Update(part);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _notificationService.CheckAndNotifyLowStockAsync(parts.Keys, cancellationToken);

        var created = await _purchaseInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.Id == invoice.Id)
            .Include(i => i.Vendor)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .FirstOrDefaultAsync(cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<PurchaseInvoiceDto>.Success(MapToDto(created!), StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> Update(
        Guid id,
        [FromBody] UpdatePurchaseInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        var invoice = await _purchaseInvoiceRepository.Query()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("Purchase invoice not found.", StatusCodes.Status404NotFound));
        }

        var vendor = await _vendorRepository.GetByIdAsync(request.VendorId, cancellationToken);
        if (vendor is null)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<PurchaseInvoiceDto>.Failure("At least one invoice item is required.", StatusCodes.Status400BadRequest));
        }

        var partIds = request.Items.Select(i => i.PartId).Distinct().ToList();
        var parts = await _partRepository.Query()
            .Where(p => partIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (parts.Count != partIds.Count)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("One or more parts were not found.", StatusCodes.Status404NotFound));
        }

        var subTotal = request.Items.Sum(i => i.Quantity * i.UnitCost);
        var taxAmount = Math.Round(subTotal * (request.TaxRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = subTotal + taxAmount;

        invoice.VendorId = vendor.Id;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.DueDate = request.DueDate;
        invoice.SubTotal = subTotal;
        invoice.TaxAmount = taxAmount;
        invoice.TotalAmount = totalAmount;
        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        // Update invoice items without changing inventory history on update.
        foreach (var existingItem in invoice.Items.ToList())
        {
            _purchaseInvoiceItemRepository.Remove(existingItem);
        }

        foreach (var item in request.Items)
        {
            var lineTotal = item.Quantity * item.UnitCost;
            await _purchaseInvoiceItemRepository.AddAsync(new PurchaseInvoiceItem
            {
                PurchaseInvoiceId = invoice.Id,
                PartId = item.PartId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                LineTotal = lineTotal
            }, cancellationToken);
        }

        _purchaseInvoiceRepository.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _purchaseInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Include(i => i.Vendor)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(ApiResponse<PurchaseInvoiceDto>.Success(MapToDto(updated!)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _purchaseInvoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound(ApiResponse<object?>.Failure("Purchase invoice not found.", StatusCodes.Status404NotFound));
        }

        _purchaseInvoiceRepository.Remove(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedException("Invalid user identity.");
        }

        return userId;
    }

    private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"PINV-{DateTime.UtcNow:yyyyMMdd}-";

        var latest = await _purchaseInvoiceRepository.Query()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(latest))
        {
            var suffix = latest[prefix.Length..];
            if (int.TryParse(suffix, out var parsed))
            {
                nextSequence = parsed + 1;
            }
        }

        return $"{prefix}{nextSequence:D4}";
    }

    private static PurchaseInvoiceDto MapToDto(PurchaseInvoice invoice)
    {
        return new PurchaseInvoiceDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            VendorId = invoice.VendorId,
            VendorName = invoice.Vendor?.Name ?? string.Empty,
            CreatedByStaffId = invoice.CreatedByStaffId,
            CreatedByStaffName = invoice.CreatedByStaff is null
                ? string.Empty
                : $"{invoice.CreatedByStaff.User.FirstName} {invoice.CreatedByStaff.User.LastName}".Trim(),
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString(),
            Items = invoice.Items.Select(it => new PurchaseInvoiceItemDto
            {
                ItemId = it.Id,
                PartId = it.PartId,
                PartName = it.Part?.Name ?? string.Empty,
                PartNumber = it.Part?.PartNumber ?? string.Empty,
                Quantity = it.Quantity,
                UnitCost = it.UnitCost,
                LineTotal = it.LineTotal
            }).ToList()
        };
    }
}
