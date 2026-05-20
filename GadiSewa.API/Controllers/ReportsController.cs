using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.FinancialReports;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "BackOfficeOnly")]
public sealed class ReportsController : ControllerBase
{
    private readonly ILogger<ReportsController> _logger;
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;

    public ReportsController(
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<Part> partRepository,
        IRepository<Customer> customerRepository,
        IRepository<Vehicle> vehicleRepository,
        ILogger<ReportsController> logger)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _partRepository = partRepository;
        _customerRepository = customerRepository;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get financial report - supports daily, monthly, yearly, or custom date range
    /// ?type=daily&from=2026-01-01&to=2026-01-31
    /// or ?type=monthly
    /// or ?type=yearly
    /// </summary>
    [HttpGet("financial")]
    public async Task<ActionResult<ApiResponse<FinancialSummaryDto>>> GetFinancialReport(
        [FromQuery] string? type,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset startDate;
            DateTimeOffset endDate;

            // Determine date range based on type or custom from/to
            if (!string.IsNullOrWhiteSpace(type))
            {
                (startDate, endDate) = type.ToLower() switch
                {
                    "daily" => (DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow),
                    "monthly" => (DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow),
                    "yearly" => (DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow),
                    _ => (from ?? DateTimeOffset.UtcNow.AddMonths(-1), to ?? DateTimeOffset.UtcNow)
                };
            }
            else
            {
                startDate = from ?? DateTimeOffset.UtcNow.AddMonths(-1);
                endDate = to ?? DateTimeOffset.UtcNow;
            }

            // Get all sales invoices in the date range
            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.InvoiceDate >= startDate && si.InvoiceDate < endDate)
                .ToListAsync(cancellationToken);

            var totalRevenue = invoices.Sum(i => i.TotalAmount);
            var totalTax = invoices.Sum(i => i.TaxAmount);
            var totalDiscounts = invoices.Sum(i => i.DiscountAmount);
            var netRevenue = totalRevenue - totalDiscounts;

            var report = new FinancialSummaryDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalRevenue = totalRevenue,
                TotalTax = totalTax,
                TotalDiscounts = totalDiscounts,
                NetRevenue = netRevenue,
                InvoiceCount = invoices.Count
            };

            return Ok(ApiResponse<FinancialSummaryDto>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ReportsController");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<FinancialSummaryDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get current inventory stock report
    /// </summary>
    [HttpGet("inventory")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InventoryReportDto>>>> GetInventoryReport(
        CancellationToken cancellationToken)
    {
        try
        {
            var parts = await _partRepository.Query()
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);

            var report = parts.Select(p => new InventoryReportDto
            {
                PartId = p.Id,
                PartName = p.Name,
                PartNumber = p.PartNumber,
                CurrentStock = p.StockQuantity,
                UnitCost = p.UnitPrice,
                StockValue = p.StockQuantity * p.UnitPrice,
                Category = "Parts"
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<InventoryReportDto>>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ReportsController");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<InventoryReportDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get top spending customers
    /// </summary>
    [HttpGet("customers/top-spenders")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TopSpenderDto>>>> GetTopSpenders(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var customers = await _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .OrderByDescending(c => c.TotalSpent)
                .Take(limit)
                .ToListAsync(cancellationToken);

            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => customers.Select(c => c.Id).Contains(si.CustomerId))
                .ToListAsync(cancellationToken);

            var report = customers.Select(c =>
            {
                var customerInvoices = invoices.Where(i => i.CustomerId == c.Id).ToList();
                var purchaseCount = customerInvoices.Count;
                var averageOrderValue = purchaseCount > 0 ? customerInvoices.Sum(i => i.TotalAmount) / purchaseCount : 0;
                var lastPurchaseDate = customerInvoices.MaxBy(i => i.InvoiceDate)?.InvoiceDate;

                return new TopSpenderDto
                {
                    CustomerId = c.Id,
                    CustomerName = $"{c.User.FirstName} {c.User.LastName}".Trim(),
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    TotalSpent = c.TotalSpent,
                    PurchaseCount = purchaseCount,
                    AverageOrderValue = averageOrderValue,
                    LastPurchaseDate = lastPurchaseDate
                };
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<TopSpenderDto>>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ReportsController");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<TopSpenderDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get regular customers (more than N purchases)
    /// </summary>
    [HttpGet("customers/regulars")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RegularCustomerDto>>>> GetRegularCustomers(
        [FromQuery] int minPurchases = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .GroupBy(i => i.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count(), LastDate = g.Max(i => i.InvoiceDate) })
                .Where(x => x.Count >= minPurchases)
                .ToListAsync(cancellationToken);

            var customerIds = invoices.Select(x => x.CustomerId).ToList();

            var customers = await _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .Where(c => customerIds.Contains(c.Id))
                .ToListAsync(cancellationToken);

            var allInvoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => customerIds.Contains(si.CustomerId))
                .ToListAsync(cancellationToken);

            var report = customers.Select(c =>
            {
                var customerInvoices = allInvoices.Where(i => i.CustomerId == c.Id).ToList();
                var firstPurchaseDate = customerInvoices.MinBy(i => i.InvoiceDate)?.InvoiceDate ?? DateTimeOffset.UtcNow;

                return new RegularCustomerDto
                {
                    CustomerId = c.Id,
                    CustomerName = $"{c.User.FirstName} {c.User.LastName}".Trim(),
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    PurchaseCount = customerInvoices.Count,
                    TotalSpent = c.TotalSpent,
                    FirstPurchaseDate = firstPurchaseDate,
                    LastPurchaseDate = customerInvoices.MaxBy(i => i.InvoiceDate)?.InvoiceDate,
                    LoyaltyPoints = c.LoyaltyPoints
                };
            })
            .OrderByDescending(x => x.PurchaseCount)
            .ToList();

            return Ok(ApiResponse<IReadOnlyList<RegularCustomerDto>>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ReportsController");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<RegularCustomerDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get customers with overdue/pending credits (AmountDue > 0 and DueDate passed)
    /// </summary>
    [HttpGet("customers/pending-credits")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PendingCreditDto>>>> GetPendingCredits(
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Include(i => i.Customer)
                    .ThenInclude(c => c.User)
                .Where(i => i.AmountDue > 0 && i.DueDate.HasValue && i.DueDate < now)
                .OrderByDescending(i => i.DueDate)
                .ToListAsync(cancellationToken);

            var report = invoices.Select(i =>
            {
                var dueDate = i.DueDate.GetValueOrDefault();
                var daysOverdue = (int)(now - dueDate).TotalDays;

                return new PendingCreditDto
                {
                    InvoiceId = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerId = i.CustomerId,
                    CustomerName = i.Customer.User is null
                        ? string.Empty
                        : $"{i.Customer.User.FirstName} {i.Customer.User.LastName}".Trim(),
                    Email = i.Customer.User?.Email ?? string.Empty,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = dueDate,
                    TotalAmount = i.TotalAmount,
                    AmountPaid = i.AmountPaid,
                    AmountDue = i.AmountDue,
                    DaysOverdue = daysOverdue
                };
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<PendingCreditDto>>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ReportsController");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<PendingCreditDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get low stock parts (below minimum threshold)
    /// </summary>
    [HttpGet("parts/low-stock")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LowStockPartDto>>>> GetLowStockParts(
        [FromQuery] int threshold = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = await _partRepository.Query()
                .AsNoTracking()
                .Where(p => p.StockQuantity <= threshold)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync(cancellationToken);

            var report = parts.Select(p => new LowStockPartDto
            {
                PartId = p.Id,
                PartName = p.Name,
                PartNumber = p.PartNumber,
                CurrentStock = p.StockQuantity,
                MinimumStockLevel = threshold,
                StockDeficit = Math.Max(0, threshold - p.StockQuantity),
                UnitCost = p.UnitPrice,
                Category = "Parts",
                Status = p.StockQuantity == 0 ? "Out of Stock" : "Low Stock"
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<LowStockPartDto>>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ReportsController");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<LowStockPartDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }
}
