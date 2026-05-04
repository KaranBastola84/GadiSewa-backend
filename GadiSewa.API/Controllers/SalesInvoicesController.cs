using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.SalesInvoices;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/sales-invoices")]
public sealed class SalesInvoicesController : ControllerBase
{
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<SalesInvoiceItem> _salesInvoiceItemRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<CreditPayment> _creditPaymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SalesInvoicesController(
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<SalesInvoiceItem> salesInvoiceItemRepository,
        IRepository<Customer> customerRepository,
        IRepository<Part> partRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<CreditPayment> creditPaymentRepository,
        IUnitOfWork unitOfWork)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _salesInvoiceItemRepository = salesInvoiceItemRepository;
        _customerRepository = customerRepository;
        _partRepository = partRepository;
        _appointmentRepository = appointmentRepository;
        _creditPaymentRepository = creditPaymentRepository;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
    }

    /// <summary>
    /// Get sales invoices (staff can view all, customers view their own)
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalesInvoiceDto>>>> GetSalesInvoices(
        [FromQuery] Guid? customerId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var userId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            IQueryable<SalesInvoice> query = _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Include(si => si.Customer)
                .ThenInclude(c => c.User)
                .Include(si => si.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(si => si.Items);

            // If customer, only show their own invoices
            if (userRole != "Admin" && userRole != "Staff")
            {
                var customer = await _customerRepository.Query()
                    .Where(c => c.UserId == userId)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (customer == Guid.Empty)
                {
                    return Ok(ApiResponse<IReadOnlyList<SalesInvoiceDto>>.Success([]));
                }

                query = query.Where(si => si.CustomerId == customer);
            }
            else if (customerId.HasValue && customerId != Guid.Empty)
            {
                query = query.Where(si => si.CustomerId == customerId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<InvoiceStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(si => si.Status == statusEnum);
                }
            }

            var invoices = await query
                .OrderByDescending(si => si.InvoiceDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var dtos = invoices.Select(si => SalesInvoiceDto.FromEntity(
                si,
                si.Items.Select(SalesInvoiceItemDto.FromEntity),
                si.DiscountAmount > 0
            )).ToList();

            return Ok(ApiResponse<IReadOnlyList<SalesInvoiceDto>>.Success(dtos));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<SalesInvoiceDto>>.Failure(
                    $"Error retrieving sales invoices: {ex.Message}",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get sales invoice details
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> GetSalesInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var invoice = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.Id == id)
                .Include(si => si.Customer)
                .ThenInclude(c => c.User)
                .Include(si => si.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(si => si.Items)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice is null)
            {
                return NotFound(ApiResponse<SalesInvoiceDto>.Failure(
                    "Sales invoice not found.",
                    StatusCodes.Status404NotFound));
            }

            // Check authorization
            if (userRole != "Admin" && userRole != "Staff")
            {
                var customer = await _customerRepository.Query()
                    .Where(c => c.UserId == userId)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (customer != invoice.CustomerId)
                {
                    return Forbid();
                }
            }

            var dto = SalesInvoiceDto.FromEntity(
                invoice,
                invoice.Items.Select(SalesInvoiceItemDto.FromEntity),
                invoice.DiscountAmount > 0
            );

            return Ok(ApiResponse<SalesInvoiceDto>.Success(dto));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<SalesInvoiceDto>.Failure(
                    $"Error retrieving sales invoice: {ex.Message}",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Create sales invoice with automatic 10% loyalty discount if subtotal > 5000
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> CreateSalesInvoice(
        [FromBody] CreateSalesInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerRepository.Query()
                .Where(c => c.Id == request.CustomerId)
                .Include(c => c.User)
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is null)
            {
                throw new NotFoundException("Customer not found.");
            }

            // Validate parts if part-based items
            var partIds = request.Items
                .Where(i => i.PartId.HasValue)
                .Select(i => i.PartId.Value)
                .Distinct()
                .ToList();

            var parts = partIds.Count > 0
                ? await _partRepository.Query()
                    .Where(p => partIds.Contains(p.Id))
                    .ToListAsync(cancellationToken)
                : [];

            if (parts.Count != partIds.Count)
            {
                throw new NotFoundException("One or more parts not found.");
            }

            // Validate stock availability
            foreach (var item in request.Items.Where(i => i.PartId.HasValue))
            {
                var part = parts.First(p => p.Id == item.PartId);
                if (part.StockQuantity < item.Quantity)
                {
                    throw new ConflictException(
                        $"Insufficient stock for part {part.PartNumber}. Available: {part.StockQuantity}, Requested: {item.Quantity}");
                }
            }

            // Calculate subtotal
            var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);

            // Apply 10% loyalty discount if subtotal > 5000
            var discountAmount = subTotal > 5000 ? subTotal * 0.1m : 0;
            var totalBeforeTax = subTotal - discountAmount;
            var totalAmount = totalBeforeTax + request.TaxAmount;

            var invoiceNumber = $"SAL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var invoice = new SalesInvoice
            {
                InvoiceNumber = invoiceNumber,
                CustomerId = request.CustomerId,
                CreatedByStaffId = Guid.NewGuid(), // Placeholder - would use logged-in staff
                AppointmentId = request.AppointmentId,
                InvoiceDate = request.InvoiceDate,
                DueDate = request.DueDate,
                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                TaxAmount = request.TaxAmount,
                TotalAmount = totalAmount,
                Status = InvoiceStatus.Unpaid
            };

            var items = request.Items.Select(i => new SalesInvoiceItem
            {
                PartId = i.PartId,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.Quantity * i.UnitPrice
            }).ToList();

            await _salesInvoiceRepository.AddAsync(invoice, cancellationToken);
            foreach (var item in items)
            {
                item.SalesInvoice = invoice;
                await _salesInvoiceItemRepository.AddAsync(item, cancellationToken);
            }

            // Deduct stock from parts
            foreach (var item in items.Where(i => i.PartId.HasValue))
            {
                var part = parts.First(p => p.Id == item.PartId);
                part.StockQuantity -= item.Quantity;
            }

            // Update loyalty points (add 1 point per 100 rupees spent)
            customer.LoyaltyPoints += (int)(totalAmount / 100);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reload for response
            var created = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.Id == invoice.Id)
                .Include(si => si.Customer)
                .ThenInclude(c => c.User)
                .Include(si => si.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(si => si.Items)
                .FirstOrDefaultAsync(cancellationToken);

            if (created is null)
            {
                throw new Exception("Failed to reload created sales invoice.");
            }

            var dto = SalesInvoiceDto.FromEntity(
                created,
                created.Items.Select(SalesInvoiceItemDto.FromEntity),
                discountAmount > 0
            );

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<SalesInvoiceDto>.Success(dto, StatusCodes.Status201Created));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status404NotFound));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<SalesInvoiceDto>.Failure(
                ex.Message,
                StatusCodes.Status409Conflict));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<SalesInvoiceDto>.Failure(
                    $"Error creating sales invoice: {ex.Message}",
                    StatusCodes.Status500InternalServerError));
        }
    }
}
