using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.CreditPayments;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/credit-payments")]
[Authorize(Policy = "BackOfficeOnly")]
public sealed class CreditPaymentsController : ControllerBase
{
    private readonly IRepository<CreditPayment> _creditPaymentRepository;
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreditPaymentsController(
        IRepository<CreditPayment> creditPaymentRepository,
        IRepository<SalesInvoice> salesInvoiceRepository,
        IUnitOfWork unitOfWork)
    {
        _creditPaymentRepository = creditPaymentRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
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
