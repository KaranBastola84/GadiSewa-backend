using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.SalesInvoices;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using GadiSewa.API.Extensions;

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
    private readonly IRepository<Staff> _staffRepository;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SalesInvoicesController> _logger;

    public SalesInvoicesController(
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<SalesInvoiceItem> salesInvoiceItemRepository,
        IRepository<Customer> customerRepository,
        IRepository<Part> partRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<CreditPayment> creditPaymentRepository,
        IRepository<Staff> staffRepository,
        IEmailService emailService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<SalesInvoicesController> logger)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _salesInvoiceItemRepository = salesInvoiceItemRepository;
        _customerRepository = customerRepository;
        _partRepository = partRepository;
        _appointmentRepository = appointmentRepository;
        _creditPaymentRepository = creditPaymentRepository;
        _staffRepository = staffRepository;
        _emailService = emailService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

            var userId = User.GetUserId();
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
            _logger.LogError(ex, "Error retrieving sales invoices");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<SalesInvoiceDto>>.Failure(
                    "An unexpected error occurred while loading sales invoices. Please try again.",
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
            var userId = User.GetUserId();
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
            _logger.LogError(ex, "Error retrieving sales invoice");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<SalesInvoiceDto>.Failure(
                    "An unexpected error occurred while loading the invoice. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Send sales invoice email to the customer
    /// </summary>
    [HttpPost("{id:guid}/send-email")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<string>>> SendInvoiceEmail(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.Id == id)
                .Include(si => si.Customer)
                    .ThenInclude(c => c.User)
                .Include(si => si.Items)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice is null)
            {
                return NotFound(ApiResponse<string>.Failure(
                    "Sales invoice not found.",
                    StatusCodes.Status404NotFound));
            }

            if (invoice.Customer?.User is null)
            {
                return NotFound(ApiResponse<string>.Failure(
                    "Customer email not found.",
                    StatusCodes.Status404NotFound));
            }

            var customerName = $"{invoice.Customer.User.FirstName} {invoice.Customer.User.LastName}".Trim();
            var emailBody = BuildInvoiceEmailBody(invoice, customerName);

            try
            {
                await _emailService.SendSalesInvoiceEmailAsync(
                    invoice.Customer.User.Email,
                    customerName,
                    invoice.InvoiceNumber,
                    emailBody,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending invoice email");
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<string>.Failure(
                    "An unexpected error occurred while sending the invoice email. Please try again.",
                    StatusCodes.Status500InternalServerError));
            }

            return Ok(ApiResponse<string>.Success("Invoice sent successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in send invoice email operation");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<string>.Failure(
                "An unexpected error occurred. Please try again.",
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
                .Select(i => i.PartId.GetValueOrDefault())
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

            // Resolve logged-in staff id
            var userId = User.GetUserId();
            var staffId = await User.GetStaffIdAsync(_staffRepository, cancellationToken);
            if (staffId == Guid.Empty)
            {
                return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Staff profile not found.", StatusCodes.Status404NotFound));
            }

            var invoice = new SalesInvoice
            {
                InvoiceNumber = invoiceNumber,
                CustomerId = request.CustomerId,
                CreatedByStaffId = staffId,
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
            await _notificationService.CheckAndNotifyLowStockAsync(
                items.Where(i => i.PartId.HasValue).Select(i => i.PartId!.Value),
                cancellationToken);
            await _notificationService.NotifySaleCreatedAsync(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.CustomerId,
                invoice.TotalAmount,
                cancellationToken);

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
            _logger.LogError(ex, "Error creating sales invoice");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<SalesInvoiceDto>.Failure(
                    "An unexpected error occurred while creating the invoice. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    private static string BuildInvoiceEmailBody(SalesInvoice invoice, string customerName)
    {
        var sb = new StringBuilder();
        var invoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd HH:mm");

        sb.AppendLine("<html><body style=\"font-family: Arial, sans-serif; color: #222;\">");
        sb.AppendLine($"<h2>Invoice {WebUtility.HtmlEncode(invoice.InvoiceNumber)}</h2>");
        sb.AppendLine($"<p><strong>Customer:</strong> {WebUtility.HtmlEncode(customerName)}</p>");
        sb.AppendLine($"<p><strong>Invoice Date:</strong> {WebUtility.HtmlEncode(invoiceDate)}</p>");
        sb.AppendLine("<table style=\"width:100%; border-collapse: collapse; margin-top: 16px;\" border=\"1\" cellpadding=\"8\">");
        sb.AppendLine("<thead><tr><th align=\"left\">Description</th><th align=\"right\">Qty</th><th align=\"right\">Unit Price</th><th align=\"right\">Line Total</th></tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var item in invoice.Items)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.Description)}</td>");
            sb.AppendLine($"<td align=\"right\">{item.Quantity}</td>");
            sb.AppendLine($"<td align=\"right\">{item.UnitPrice:F2}</td>");
            sb.AppendLine($"<td align=\"right\">{item.LineTotal:F2}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<div style=\"margin-top: 16px;\">");
        sb.AppendLine($"<p><strong>SubTotal:</strong> {invoice.SubTotal:F2}</p>");

        if (invoice.DiscountAmount > 0)
        {
            sb.AppendLine($"<p><strong>Discount Amount:</strong> {invoice.DiscountAmount:F2}</p>");
        }

        sb.AppendLine($"<p><strong>Tax Amount:</strong> {invoice.TaxAmount:F2}</p>");
        sb.AppendLine($"<p><strong>Total Amount:</strong> {invoice.TotalAmount:F2}</p>");
        sb.AppendLine($"<p><strong>Amount Due:</strong> {invoice.AmountDue:F2}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
