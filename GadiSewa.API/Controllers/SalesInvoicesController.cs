using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.SalesInvoices;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/sales-invoices")]
[Authorize]
public sealed class SalesInvoicesController : ControllerBase
{
    private const decimal LoyaltyDiscountThreshold = 5000m;
    private const decimal LoyaltyDiscountRate = 0.10m;

    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<SalesInvoiceItem> _salesInvoiceItemRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public SalesInvoicesController(
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<SalesInvoiceItem> salesInvoiceItemRepository,
        IRepository<Customer> customerRepository,
        IRepository<Part> partRepository,
        IRepository<Staff> staffRepository,
        IRepository<Appointment> appointmentRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _salesInvoiceItemRepository = salesInvoiceItemRepository;
        _customerRepository = customerRepository;
        _partRepository = partRepository;
        _staffRepository = staffRepository;
        _appointmentRepository = appointmentRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalesInvoiceDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var invoices = await _salesInvoiceRepository.Query()
            .AsNoTracking()
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

        var result = invoices.Select(MapToDto).ToList();
        return Ok(ApiResponse<IReadOnlyList<SalesInvoiceDto>>.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _salesInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Sales invoice not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<SalesInvoiceDto>.Success(MapToDto(invoice)));
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> Create(
        [FromBody] CreateSalesInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<SalesInvoiceDto>.Failure("At least one invoice item is required.", StatusCodes.Status400BadRequest));
        }

        var customer = await _customerRepository.Query()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Customer not found.", StatusCodes.Status404NotFound));
        }

        if (request.AppointmentId.HasValue)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId.Value, cancellationToken);
            if (appointment is null)
            {
                return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Appointment not found.", StatusCodes.Status404NotFound));
            }
        }

        var staff = await ResolveCurrentStaffAsync(cancellationToken);

        var partIds = request.Items.Where(i => i.PartId.HasValue).Select(i => i.PartId!.Value).Distinct().ToList();
        var parts = await _partRepository.Query().Where(p => partIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        if (parts.Count != partIds.Count)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure("One or more parts were not found.", StatusCodes.Status404NotFound));
        }

        // Validate stock before completing sale.
        foreach (var item in request.Items.Where(i => i.PartId.HasValue))
        {
            var part = parts[item.PartId!.Value];
            if (part.StockQuantity < item.Quantity)
            {
                return BadRequest(ApiResponse<SalesInvoiceDto>.Failure($"Insufficient stock for part {part.PartNumber}. Available: {part.StockQuantity}, required: {item.Quantity}.", StatusCodes.Status400BadRequest));
            }
        }

        var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var loyaltyApplied = subTotal > LoyaltyDiscountThreshold;
        var discountAmount = loyaltyApplied ? Math.Round(subTotal * LoyaltyDiscountRate, 2, MidpointRounding.AwayFromZero) : 0m;
        var taxableAmount = subTotal - discountAmount;
        var taxAmount = Math.Round(taxableAmount * (request.TaxRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = taxableAmount + taxAmount;

        var invoice = new SalesInvoice
        {
            InvoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken),
            CustomerId = customer.Id,
            CreatedByStaffId = staff.Id,
            AppointmentId = request.AppointmentId,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            LoyaltyApplied = loyaltyApplied,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            AmountDue = totalAmount,
            Status = request.Status
        };

        await _salesInvoiceRepository.AddAsync(invoice, cancellationToken);

        foreach (var item in request.Items)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            await _salesInvoiceItemRepository.AddAsync(new SalesInvoiceItem
            {
                SalesInvoiceId = invoice.Id,
                PartId = item.PartId,
                Description = item.Description.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = lineTotal
            }, cancellationToken);

            if (item.PartId.HasValue)
            {
                var part = parts[item.PartId.Value];
                part.StockQuantity -= item.Quantity;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
            }
        }

        customer.TotalSpent += totalAmount;
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        _customerRepository.Update(customer);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await LoadInvoiceAsync(invoice.Id, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SalesInvoiceDto>.Success(MapToDto(created!), StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> Update(
        Guid id,
        [FromBody] UpdateSalesInvoiceRequestDto request,
        CancellationToken cancellationToken)
    {
        var invoice = await _salesInvoiceRepository.Query()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Sales invoice not found.", StatusCodes.Status404NotFound));
        }

        var customer = await _customerRepository.Query()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Customer not found.", StatusCodes.Status404NotFound));
        }

        if (request.AppointmentId.HasValue)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId.Value, cancellationToken);
            if (appointment is null)
            {
                return NotFound(ApiResponse<SalesInvoiceDto>.Failure("Appointment not found.", StatusCodes.Status404NotFound));
            }
        }

        var partIds = request.Items.Where(i => i.PartId.HasValue).Select(i => i.PartId!.Value).Distinct().ToList();
        var parts = await _partRepository.Query().Where(p => partIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        if (parts.Count != partIds.Count)
        {
            return NotFound(ApiResponse<SalesInvoiceDto>.Failure("One or more parts were not found.", StatusCodes.Status404NotFound));
        }

        // Revert old stock movements before re-applying the new ones.
        var existingPartItems = invoice.Items.Where(i => i.PartId.HasValue).ToList();
        if (existingPartItems.Count > 0)
        {
            var existingPartIds = existingPartItems.Select(i => i.PartId!.Value).Distinct().ToList();
            var existingParts = await _partRepository.Query().Where(p => existingPartIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var oldItem in existingPartItems)
            {
                var part = existingParts[oldItem.PartId!.Value];
                part.StockQuantity += oldItem.Quantity;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
            }
        }

        foreach (var newItem in request.Items.Where(i => i.PartId.HasValue))
        {
            var part = parts[newItem.PartId!.Value];
            if (part.StockQuantity < newItem.Quantity)
            {
                return BadRequest(ApiResponse<SalesInvoiceDto>.Failure($"Insufficient stock for part {part.PartNumber}. Available: {part.StockQuantity}, required: {newItem.Quantity}.", StatusCodes.Status400BadRequest));
            }
        }

        var oldTotalAmount = invoice.TotalAmount;

        var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var loyaltyApplied = subTotal > LoyaltyDiscountThreshold;
        var discountAmount = loyaltyApplied ? Math.Round(subTotal * LoyaltyDiscountRate, 2, MidpointRounding.AwayFromZero) : 0m;
        var taxableAmount = subTotal - discountAmount;
        var taxAmount = Math.Round(taxableAmount * (request.TaxRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalAmount = taxableAmount + taxAmount;

        foreach (var existing in invoice.Items.ToList())
        {
            _salesInvoiceItemRepository.Remove(existing);
        }

        foreach (var item in request.Items)
        {
            await _salesInvoiceItemRepository.AddAsync(new SalesInvoiceItem
            {
                SalesInvoiceId = invoice.Id,
                PartId = item.PartId,
                Description = item.Description.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.Quantity * item.UnitPrice
            }, cancellationToken);

            if (item.PartId.HasValue)
            {
                var part = parts[item.PartId.Value];
                part.StockQuantity -= item.Quantity;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
            }
        }

        invoice.CustomerId = customer.Id;
        invoice.AppointmentId = request.AppointmentId;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.DueDate = request.DueDate;
        invoice.SubTotal = subTotal;
        invoice.DiscountAmount = discountAmount;
        invoice.LoyaltyApplied = loyaltyApplied;
        invoice.TaxAmount = taxAmount;
        invoice.TotalAmount = totalAmount;
        invoice.AmountDue = totalAmount;
        invoice.Status = request.Status;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        _salesInvoiceRepository.Update(invoice);

        customer.TotalSpent = customer.TotalSpent - oldTotalAmount + totalAmount;
        if (customer.TotalSpent < 0) customer.TotalSpent = 0;
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        _customerRepository.Update(customer);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await LoadInvoiceAsync(invoice.Id, cancellationToken);
        return Ok(ApiResponse<SalesInvoiceDto>.Success(MapToDto(updated!)));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _salesInvoiceRepository.Query()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<object?>.Failure("Sales invoice not found.", StatusCodes.Status404NotFound));
        }

        // Revert stock because sale is being deleted.
        var partItems = invoice.Items.Where(i => i.PartId.HasValue).ToList();
        if (partItems.Count > 0)
        {
            var partIds = partItems.Select(i => i.PartId!.Value).Distinct().ToList();
            var parts = await _partRepository.Query().Where(p => partIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var item in partItems)
            {
                var part = parts[item.PartId!.Value];
                part.StockQuantity += item.Quantity;
                part.UpdatedAt = DateTimeOffset.UtcNow;
                _partRepository.Update(part);
            }
        }

        var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId, cancellationToken);
        if (customer is not null)
        {
            customer.TotalSpent -= invoice.TotalAmount;
            if (customer.TotalSpent < 0) customer.TotalSpent = 0;
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            _customerRepository.Update(customer);
        }

        _salesInvoiceRepository.Remove(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
    }

    [HttpPost("{id:guid}/send-email")]
    public async Task<ActionResult<ApiResponse<object?>>> SendInvoiceEmail(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _salesInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<object?>.Failure("Sales invoice not found.", StatusCodes.Status404NotFound));
        }

        var customerEmail = invoice.Customer.User.Email;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return BadRequest(ApiResponse<object?>.Failure("Customer email is not available.", StatusCodes.Status400BadRequest));
        }

        var customerName = $"{invoice.Customer.User.FirstName} {invoice.Customer.User.LastName}".Trim();
        var invoiceHtml = BuildInvoiceHtml(invoice);

        await _emailService.SendSalesInvoiceEmailAsync(customerEmail, customerName, invoice.InvoiceNumber, invoiceHtml, cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
    }

    private async Task<Staff> ResolveCurrentStaffAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var staff = await _staffRepository.Query()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (staff is null)
        {
            throw new ValidationException("Current user does not have a linked staff profile.");
        }

        return staff;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedException("Invalid user identity.");
        }

        return userId;
    }

    private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var prefix = $"SINV-{DateTime.UtcNow:yyyyMMdd}-";

        var latest = await _salesInvoiceRepository.Query()
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

    private async Task<SalesInvoice?> LoadInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _salesInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
            .Include(i => i.Items)
                .ThenInclude(it => it.Part)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static SalesInvoiceDto MapToDto(SalesInvoice invoice)
    {
        return new SalesInvoiceDto
        {
            SalesInvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.Customer is null ? string.Empty : $"{invoice.Customer.User.FirstName} {invoice.Customer.User.LastName}".Trim(),
            CustomerEmail = invoice.Customer?.User.Email ?? string.Empty,
            CreatedByStaffId = invoice.CreatedByStaffId,
            CreatedByStaffName = invoice.CreatedByStaff is null ? string.Empty : $"{invoice.CreatedByStaff.User.FirstName} {invoice.CreatedByStaff.User.LastName}".Trim(),
            AppointmentId = invoice.AppointmentId,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            DiscountAmount = invoice.DiscountAmount,
            LoyaltyApplied = invoice.LoyaltyApplied,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            AmountDue = invoice.AmountDue,
            Status = invoice.Status.ToString(),
            Items = invoice.Items.Select(it => new SalesInvoiceItemDto
            {
                SalesInvoiceItemId = it.Id,
                PartId = it.PartId,
                PartName = it.Part?.Name ?? string.Empty,
                PartNumber = it.Part?.PartNumber ?? string.Empty,
                Description = it.Description,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                LineTotal = it.LineTotal
            }).ToList()
        };
    }

    private static string BuildInvoiceHtml(SalesInvoice invoice)
    {
        var sb = new StringBuilder();
        var customerName = invoice.Customer is null ? "Customer" : $"{invoice.Customer.User.FirstName} {invoice.Customer.User.LastName}".Trim();
        var staffName = invoice.CreatedByStaff is null ? string.Empty : $"{invoice.CreatedByStaff.User.FirstName} {invoice.CreatedByStaff.User.LastName}".Trim();

        sb.AppendLine("<html><body style='font-family:Segoe UI,Arial,sans-serif;'>");
        sb.AppendLine($"<h2>Invoice {invoice.InvoiceNumber}</h2>");
        sb.AppendLine($"<p><strong>Date:</strong> {invoice.InvoiceDate:yyyy-MM-dd}</p>");
        sb.AppendLine($"<p><strong>Customer:</strong> {customerName}</p>");
        sb.AppendLine($"<p><strong>Prepared By:</strong> {staffName}</p>");
        sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0' style='border-collapse:collapse;width:100%;'>");
        sb.AppendLine("<thead><tr><th>Description</th><th>Part No</th><th>Qty</th><th>Unit Price</th><th>Line Total</th></tr></thead><tbody>");

        foreach (var item in invoice.Items)
        {
            sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(item.Description)}</td><td>{System.Net.WebUtility.HtmlEncode(item.Part?.PartNumber ?? string.Empty)}</td><td>{item.Quantity}</td><td>{item.UnitPrice:N2}</td><td>{item.LineTotal:N2}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p><strong>SubTotal:</strong> {invoice.SubTotal:N2}</p>");
        sb.AppendLine($"<p><strong>Discount:</strong> {invoice.DiscountAmount:N2} {(invoice.LoyaltyApplied ? "(Loyalty Applied)" : string.Empty)}</p>");
        sb.AppendLine($"<p><strong>Tax:</strong> {invoice.TaxAmount:N2}</p>");
        sb.AppendLine($"<p><strong>Total:</strong> {invoice.TotalAmount:N2}</p>");
        sb.AppendLine($"<p><strong>Amount Due:</strong> {invoice.AmountDue:N2}</p>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
