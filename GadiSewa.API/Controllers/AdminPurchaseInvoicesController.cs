using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.PurchaseInvoices;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using GadiSewa.API.Extensions;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/purchase-invoices")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminPurchaseInvoicesController : ControllerBase
{
    private readonly ILogger<AdminPurchaseInvoicesController> _logger;
    private readonly IRepository<PurchaseInvoice> _purchaseInvoiceRepository;
    private readonly IRepository<PurchaseInvoiceItem> _purchaseInvoiceItemRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminPurchaseInvoicesController(
        IRepository<PurchaseInvoice> purchaseInvoiceRepository,
        IRepository<PurchaseInvoiceItem> purchaseInvoiceItemRepository,
        IRepository<Part> partRepository,
        IRepository<Vendor> vendorRepository,
        IRepository<Staff> staffRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<AdminPurchaseInvoicesController> logger)
    {
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _purchaseInvoiceItemRepository = purchaseInvoiceItemRepository;
        _partRepository = partRepository;
        _vendorRepository = vendorRepository;
        _staffRepository = staffRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }



    /// <summary>
    /// Get all purchase invoices with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PurchaseInvoiceDto>>>> GetPurchaseInvoices(
        [FromQuery] Guid? vendorId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            IQueryable<PurchaseInvoice> query = _purchaseInvoiceRepository.Query()
                .AsNoTracking()
                .Include(pi => pi.Vendor)
                .Include(pi => pi.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(pi => pi.Items)
                .ThenInclude(i => i.Part);

            if (vendorId.HasValue && vendorId != Guid.Empty)
            {
                query = query.Where(pi => pi.VendorId == vendorId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<InvoiceStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(pi => pi.Status == statusEnum);
                }
            }

            var invoices = await query
                .OrderByDescending(pi => pi.InvoiceDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var dtos = invoices.Select(pi => PurchaseInvoiceDto.FromEntity(
                pi,
                pi.Items.Select(PurchaseInvoiceItemDto.FromEntity)
            )).ToList();

            return Ok(ApiResponse<IReadOnlyList<PurchaseInvoiceDto>>.Success(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminPurchaseInvoicesController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<PurchaseInvoiceDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get purchase invoice details
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> GetPurchaseInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _purchaseInvoiceRepository.Query()
                .AsNoTracking()
                .Where(pi => pi.Id == id)
                .Include(pi => pi.Vendor)
                .Include(pi => pi.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(pi => pi.Items)
                .ThenInclude(i => i.Part)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice is null)
            {
                return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure(
                    "Purchase invoice not found.",
                    StatusCodes.Status404NotFound));
            }

            var dto = PurchaseInvoiceDto.FromEntity(
                invoice,
                invoice.Items.Select(PurchaseInvoiceItemDto.FromEntity)
            );

            return Ok(ApiResponse<PurchaseInvoiceDto>.Success(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminPurchaseInvoicesController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PurchaseInvoiceDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Create purchase invoice
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> CreatePurchaseInvoice(
        [FromBody] CreatePurchaseInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var vendor = await _vendorRepository.Query()
                .Where(v => v.Id == request.VendorId)
                .FirstOrDefaultAsync(cancellationToken);

            if (vendor is null)
            {
                throw new NotFoundException("Vendor not found.");
            }

            var partIds = request.Items.Select(i => i.PartId).Distinct().ToList();
            var parts = await _partRepository.Query()
                .Where(p => partIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (parts.Count != partIds.Count)
            {
                throw new NotFoundException("One or more parts not found.");
            }

            var subTotal = request.Items.Sum(i => i.Quantity * i.UnitCost);
            var totalAmount = subTotal + request.TaxAmount;

            var invoiceNumber = $"PUR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var userId = User.GetUserId();
            var staffId = await User.GetStaffIdAsync(_staffRepository, cancellationToken);
            if (staffId == Guid.Empty)
            {
                return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure("Staff profile not found.", StatusCodes.Status404NotFound));
            }
            var invoice = new PurchaseInvoice
            {
                InvoiceNumber = invoiceNumber,
                VendorId = request.VendorId,
                CreatedByStaffId = staffId,
                InvoiceDate = request.InvoiceDate,
                DueDate = request.DueDate,
                SubTotal = subTotal,
                TaxAmount = request.TaxAmount,
                TotalAmount = totalAmount,
                Status = InvoiceStatus.Unpaid
            };

            var items = request.Items.Select(i => new PurchaseInvoiceItem
            {
                PartId = i.PartId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost,
                LineTotal = i.Quantity * i.UnitCost
            }).ToList();

            await _purchaseInvoiceRepository.AddAsync(invoice, cancellationToken);
            foreach (var item in items)
            {
                item.PurchaseInvoice = invoice;
                await _purchaseInvoiceItemRepository.AddAsync(item, cancellationToken);
            }

            foreach (var item in items)
            {
                var part = parts.First(p => p.Id == item.PartId);
                part.StockQuantity += item.Quantity;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reload for response
            var created = await _purchaseInvoiceRepository.Query()
                .AsNoTracking()
                .Where(pi => pi.Id == invoice.Id)
                .Include(pi => pi.Vendor)
                .Include(pi => pi.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(pi => pi.Items)
                .ThenInclude(i => i.Part)
                .FirstOrDefaultAsync(cancellationToken);

            if (created is null)
            {
                throw new Exception("Failed to reload created purchase invoice.");
            }

            var dto = PurchaseInvoiceDto.FromEntity(
                created,
                created.Items.Select(PurchaseInvoiceItemDto.FromEntity)
            );

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<PurchaseInvoiceDto>.Success(dto, StatusCodes.Status201Created));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status404NotFound));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminPurchaseInvoicesController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PurchaseInvoiceDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Update purchase invoice (only for Unpaid invoices)
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> UpdatePurchaseInvoice(
        Guid id,
        [FromBody] UpdatePurchaseInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _purchaseInvoiceRepository.Query()
                .Where(pi => pi.Id == id)
                .Include(pi => pi.Vendor)
                .Include(pi => pi.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(pi => pi.Items)
                .ThenInclude(i => i.Part)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice is null)
            {
                throw new NotFoundException("Purchase invoice not found.");
            }

            if (invoice.Status != InvoiceStatus.Unpaid)
            {
                throw new ConflictException("Only unpaid invoices can be updated.");
            }

            if (request.VendorId != Guid.Empty && request.VendorId != invoice.VendorId)
            {
                var vendor = await _vendorRepository.Query()
                    .Where(v => v.Id == request.VendorId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (vendor is null)
                {
                    throw new NotFoundException("Vendor not found.");
                }

                invoice.VendorId = request.VendorId;
            }

            if (request.DueDate.HasValue)
            {
                invoice.DueDate = request.DueDate;
            }

            invoice.TaxAmount = request.TaxAmount;
            invoice.TotalAmount = invoice.SubTotal + request.TaxAmount;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var dto = PurchaseInvoiceDto.FromEntity(
                invoice,
                invoice.Items.Select(PurchaseInvoiceItemDto.FromEntity)
            );

            return Ok(ApiResponse<PurchaseInvoiceDto>.Success(dto));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status404NotFound));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<PurchaseInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status409Conflict));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminPurchaseInvoicesController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PurchaseInvoiceDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Receive stock from purchase invoice (updates Part stock quantities)
    /// </summary>
    [HttpPost("{id:guid}/receive-stock")]
    public async Task<ActionResult<ApiResponse<PurchaseInvoiceDto>>> ReceiveStock(
        Guid id,
        [FromBody] ReceiveStockRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _purchaseInvoiceRepository.Query()
                .Where(pi => pi.Id == id)
                .Include(pi => pi.Vendor)
                .Include(pi => pi.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(pi => pi.Items)
                .ThenInclude(i => i.Part)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice is null)
            {
                throw new NotFoundException("Purchase invoice not found.");
            }

            if (invoice.Status == InvoiceStatus.Paid)
            {
                // Already received or marked as paid
                if (!invoice.Items.All(i => request.Items.Any(ri => ri.ItemId == i.Id)))
                {
                    throw new ConflictException("Some items have already been received.");
                }
            }

            foreach (var receiveItem in request.Items)
            {
                var item = invoice.Items.FirstOrDefault(i => i.Id == receiveItem.ItemId);
                if (item is null)
                {
                    throw new NotFoundException($"Invoice item {receiveItem.ItemId} not found.");
                }

                if (receiveItem.QuantityReceived > item.Quantity)
                {
                    throw new ConflictException(
                        $"Received quantity {receiveItem.QuantityReceived} exceeds ordered quantity {item.Quantity} for part {item.Part.PartNumber}.");
                }

                // Update part stock
                item.Part.StockQuantity += receiveItem.QuantityReceived;
            }

            // Mark invoice as received (paid) if all items received
            if (request.Items.Count == invoice.Items.Count &&
                request.Items.All(ri => ri.QuantityReceived == invoice.Items.First(i => i.Id == ri.ItemId).Quantity))
            {
                invoice.Status = InvoiceStatus.Paid;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _notificationService.CheckAndNotifyLowStockAsync(
                request.Items.Select(ri => invoice.Items.First(i => i.Id == ri.ItemId).PartId),
                cancellationToken);

            var dto = PurchaseInvoiceDto.FromEntity(
                invoice,
                invoice.Items.Select(PurchaseInvoiceItemDto.FromEntity)
            );

            return Ok(ApiResponse<PurchaseInvoiceDto>.Success(dto));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<PurchaseInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status404NotFound));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<PurchaseInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status409Conflict));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminPurchaseInvoicesController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PurchaseInvoiceDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Delete purchase invoice (only for Unpaid invoices)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePurchaseInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _purchaseInvoiceRepository.Query()
                .Where(pi => pi.Id == id)
                .Include(pi => pi.Items)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice is null)
            {
                throw new NotFoundException("Purchase invoice not found.");
            }

            if (invoice.Status != InvoiceStatus.Unpaid)
            {
                throw new ConflictException("Only unpaid invoices can be deleted.");
            }

            foreach (var item in invoice.Items)
            {
                _purchaseInvoiceItemRepository.Remove(item);
            }

            _purchaseInvoiceRepository.Remove(invoice);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(ApiResponse<object>.Success(new { message = "Purchase invoice deleted successfully." }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Failure(
                ex.Message,
                StatusCodes.Status404NotFound));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<object>.Failure(
                ex.Message,
                StatusCodes.Status409Conflict));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminPurchaseInvoicesController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }
}