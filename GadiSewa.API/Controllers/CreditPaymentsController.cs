using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.CreditPayments;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/credit-payments")]
[Authorize(Policy = "BackOfficeOnly")]
public sealed class CreditPaymentsController : ControllerBase
{
    private readonly IRepository<CreditPayment> _creditPaymentRepository;
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreditPaymentsController(
        IRepository<CreditPayment> creditPaymentRepository,
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _creditPaymentRepository = creditPaymentRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CreditPaymentDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var payments = await _creditPaymentRepository.Query()
            .AsNoTracking()
            .Include(p => p.Customer)
                .ThenInclude(c => c.User)
            .Include(p => p.SalesInvoice)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<CreditPaymentDto>>.Success(payments.Select(MapToDto).ToList()));
    }

    [HttpGet("customer/{customerId:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CustomerCreditHistoryDto>>> GetCustomerCreditHistory(Guid customerId, CancellationToken cancellationToken)
    {
        var role = GetCurrentRole();
        if (role == UserRole.Customer.ToString())
        {
            var currentCustomer = await _customerRepository.Query().FirstOrDefaultAsync(c => c.UserId == GetCurrentUserId(), cancellationToken);
            if (currentCustomer is null || currentCustomer.Id != customerId)
            {
                return Forbid();
            }
        }

        var customer = await _customerRepository.Query()
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return NotFound(ApiResponse<CustomerCreditHistoryDto>.Failure("Customer not found.", StatusCodes.Status404NotFound));
        }

        var payments = await _creditPaymentRepository.Query()
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .Include(p => p.SalesInvoice)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        var totalOutstanding = await _salesInvoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.CustomerId == customerId)
            .SumAsync(i => i.AmountDue, cancellationToken);

        var dto = new CustomerCreditHistoryDto
        {
            CustomerId = customer.Id,
            CustomerName = $"{customer.User.FirstName} {customer.User.LastName}".Trim(),
            TotalPaid = payments.Sum(p => p.Amount),
            TotalOutstanding = totalOutstanding,
            Payments = payments.Select(MapToDto).ToList()
        };

        return Ok(ApiResponse<CustomerCreditHistoryDto>.Success(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreditPaymentDto>>> RecordPayment([FromBody] CreateCreditPaymentRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(ApiResponse<CreditPaymentDto>.Failure("Payment amount must be greater than zero.", StatusCodes.Status400BadRequest));
        }

        var invoice = await _salesInvoiceRepository.Query()
            .Include(i => i.Customer)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(i => i.Id == request.SalesInvoiceId, cancellationToken);

        if (invoice is null)
        {
            return NotFound(ApiResponse<CreditPaymentDto>.Failure("Sales invoice not found.", StatusCodes.Status404NotFound));
        }

        if (request.Amount > invoice.AmountDue)
        {
            return BadRequest(ApiResponse<CreditPaymentDto>.Failure("Payment amount cannot exceed amount due.", StatusCodes.Status400BadRequest));
        }

        var amountBefore = invoice.AmountDue;
        var amountAfter = Math.Max(amountBefore - request.Amount, 0m);

        var payment = new CreditPayment
        {
            SalesInvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            Amount = request.Amount,
            AmountBeforePayment = amountBefore,
            AmountAfterPayment = amountAfter,
            PaymentDate = DateTimeOffset.UtcNow,
            PaymentMethod = request.PaymentMethod.Trim(),
            ReferenceNumber = request.ReferenceNumber.Trim(),
            IsVerified = request.IsVerified,
            Notes = request.Notes.Trim()
        };

        await _creditPaymentRepository.AddAsync(payment, cancellationToken);

        invoice.AmountPaid += request.Amount;
        invoice.AmountDue = amountAfter;
        if (amountAfter == 0m)
        {
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (invoice.DueDate.HasValue && invoice.DueDate.Value < DateTimeOffset.UtcNow)
        {
            invoice.Status = InvoiceStatus.Overdue;
        }
        else
        {
            invoice.Status = InvoiceStatus.Unpaid;
        }

        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        _salesInvoiceRepository.Update(invoice);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<CreditPaymentDto>.Success(MapToDto(payment), StatusCodes.Status201Created));
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new InvalidOperationException("Invalid user identity.");
        }

        return userId;
    }

    private string GetCurrentRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    private static CreditPaymentDto MapToDto(CreditPayment payment)
    {
        return new CreditPaymentDto
        {
            CreditPaymentId = payment.Id,
            SalesInvoiceId = payment.SalesInvoiceId,
            InvoiceNumber = payment.SalesInvoice?.InvoiceNumber ?? string.Empty,
            CustomerId = payment.CustomerId,
            CustomerName = payment.Customer is null ? string.Empty : $"{payment.Customer.User.FirstName} {payment.Customer.User.LastName}".Trim(),
            Amount = payment.Amount,
            AmountBeforePayment = payment.AmountBeforePayment,
            AmountAfterPayment = payment.AmountAfterPayment,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod,
            ReferenceNumber = payment.ReferenceNumber,
            IsVerified = payment.IsVerified,
            Notes = payment.Notes
        };
    }
}
