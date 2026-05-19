using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Customers;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/customers/history")]
[Authorize(Policy = "CustomerOnly")]
public sealed class CustomerHistoryController : ControllerBase
{
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<CreditPayment> _creditPaymentRepository;

    public CustomerHistoryController(
        IRepository<Customer> customerRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<CreditPayment> creditPaymentRepository)
    {
        _customerRepository = customerRepository;
        _appointmentRepository = appointmentRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _creditPaymentRepository = creditPaymentRepository;
    }

    private Guid GetCurrentUserId()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
    }

    /// <summary>
    /// Get my service and purchase history
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CustomerHistorySummaryDto>>> GetMyHistory(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(ApiResponse<CustomerHistorySummaryDto>.Failure(
                    "User not authenticated.",
                    StatusCodes.Status401Unauthorized));
            }

            var customer = await _customerRepository.Query()
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Include(c => c.User)
                .Include(c => c.Appointments)
                .Include(c => c.SalesInvoices)
                .Include(c => c.CreditPayments)
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is null)
            {
                return NotFound(ApiResponse<CustomerHistorySummaryDto>.Failure(
                    "Customer profile not found.",
                    StatusCodes.Status404NotFound));
            }

            var appointments = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.CustomerId == customer.Id)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .Include(a => a.Reviews)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(cancellationToken);

            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(i => i.CustomerId == customer.Id)
                .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(i => i.Items)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            var creditPayments = await _creditPaymentRepository.Query()
                .AsNoTracking()
                .Where(cp => cp.CustomerId == customer.Id)
                .OrderByDescending(cp => cp.PaymentDate)
                .ToListAsync(cancellationToken);

            var totalSpent = invoices.Sum(i => i.TotalAmount);
            var totalUnpaid = invoices
                .Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Overdue)
                .Sum(i => i.TotalAmount - creditPayments.Where(cp => cp.SalesInvoiceId == i.Id).Sum(cp => cp.Amount));

            var dto = new CustomerHistorySummaryDto
            {
                CustomerId = customer.Id,
                FullName = $"{customer.User.FirstName} {customer.User.LastName}".Trim(),
                TotalAppointments = appointments.Count,
                CompletedAppointments = appointments.Count(a => a.Status == AppointmentStatus.Completed),
                CancelledAppointments = appointments.Count(a => a.Status == AppointmentStatus.Cancelled),
                TotalInvoices = invoices.Count,
                TotalSpent = totalSpent,
                TotalUnpaid = totalUnpaid,
                TotalLoyaltyPoints = customer.LoyaltyPoints,
                FirstAppointmentDate = appointments.OrderBy(a => a.ScheduledAt).FirstOrDefault()?.ScheduledAt,
                LastAppointmentDate = appointments.OrderByDescending(a => a.ScheduledAt).FirstOrDefault()?.ScheduledAt,
                FirstPurchaseDate = invoices.OrderBy(i => i.InvoiceDate).FirstOrDefault()?.InvoiceDate,
                LastPurchaseDate = invoices.OrderByDescending(i => i.InvoiceDate).FirstOrDefault()?.InvoiceDate,
                RecentAppointments = appointments
                    .Take(10)
                    .Select(a => new AppointmentHistoryItemDto
                    {
                        AppointmentId = a.Id,
                        AppointmentNumber = a.AppointmentNumber,
                        VehicleRegistration = a.Vehicle.RegistrationNumber,
                        ScheduledAt = a.ScheduledAt,
                        CompletedAt = a.CompletedAt,
                        Status = a.Status.ToString(),
                        ProblemDescription = a.ProblemDescription,
                        Notes = a.Notes,
                        AssignedStaffName = a.AssignedStaff?.User is null ? "Unassigned" : $"{a.AssignedStaff.User.FirstName} {a.AssignedStaff.User.LastName}".Trim(),
                        ReviewCount = a.Reviews.Count
                    })
                    .ToList(),
                RecentInvoices = invoices
                    .Take(10)
                    .Select(i => new SalesInvoiceHistoryItemDto
                    {
                        InvoiceId = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        InvoiceDate = i.InvoiceDate,
                        Status = i.Status.ToString(),
                        SubTotal = i.SubTotal,
                        DiscountAmount = i.DiscountAmount,
                        TaxAmount = i.TaxAmount,
                        TotalAmount = i.TotalAmount,
                        CreatedByStaffName = i.CreatedByStaff?.User is null ? "Unknown" : $"{i.CreatedByStaff.User.FirstName} {i.CreatedByStaff.User.LastName}".Trim(),
                        Items = i.Items.Select(it => new SalesInvoiceItemDetailDto
                        {
                            Description = it.Description,
                            Quantity = it.Quantity,
                            UnitPrice = it.UnitPrice,
                            LineTotal = it.LineTotal
                        }).ToList(),
                        AmountPaid = creditPayments.Where(cp => cp.SalesInvoiceId == i.Id).Sum(cp => cp.Amount),
                        AmountDue = i.TotalAmount - creditPayments.Where(cp => cp.SalesInvoiceId == i.Id).Sum(cp => cp.Amount),
                        AppointmentId = i.AppointmentId
                    })
                    .ToList()
            };

            return Ok(ApiResponse<CustomerHistorySummaryDto>.Success(dto));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<CustomerHistorySummaryDto>.Failure(
                    $"Error retrieving history: {ex.Message}",
                    StatusCodes.Status500InternalServerError));
        }
    }
}
