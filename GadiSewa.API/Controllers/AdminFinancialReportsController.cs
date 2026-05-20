using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.FinancialReports;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/financial-reports")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminFinancialReportsController : ControllerBase
{
    private readonly ILogger<AdminFinancialReportsController> _logger;
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice> _purchaseInvoiceRepository;
    private readonly IRepository<SalesInvoiceItem> _salesInvoiceItemRepository;
    private readonly IRepository<PurchaseInvoiceItem> _purchaseInvoiceItemRepository;

    public AdminFinancialReportsController(
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<PurchaseInvoice> purchaseInvoiceRepository,
        IRepository<SalesInvoiceItem> salesInvoiceItemRepository,
        IRepository<PurchaseInvoiceItem> purchaseInvoiceItemRepository,
        ILogger<AdminFinancialReportsController> logger)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _salesInvoiceItemRepository = salesInvoiceItemRepository;
        _purchaseInvoiceItemRepository = purchaseInvoiceItemRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get daily P&L report
    /// </summary>
    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<FinancialReportDto>>> GetDailyReport(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var start = startDate ?? DateTimeOffset.UtcNow.AddDays(-30);
            var end = endDate ?? DateTimeOffset.UtcNow.AddDays(1);

            var salesInvoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.InvoiceDate >= start && si.InvoiceDate < end)
                .Include(si => si.Items)
                .ToListAsync(cancellationToken);

            var purchaseInvoices = await _purchaseInvoiceRepository.Query()
                .AsNoTracking()
                .Where(pi => pi.InvoiceDate >= start && pi.InvoiceDate < end)
                .Include(pi => pi.Items)
                .ToListAsync(cancellationToken);

            var dailyGroups = salesInvoices
                .GroupBy(si => si.InvoiceDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(si => si.TotalAmount)
                })
                .ToDictionary(x => x.Date, x => x.Revenue);

            var purchaseDailyGroups = purchaseInvoices
                .GroupBy(pi => pi.InvoiceDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Costs = g.Sum(pi => pi.TotalAmount)
                })
                .ToDictionary(x => x.Date, x => x.Costs);

            var allDates = new HashSet<DateTime>(dailyGroups.Keys.Union(purchaseDailyGroups.Keys));
            var lines = allDates
                .OrderBy(d => d)
                .Select(date =>
                {
                    var revenue = dailyGroups.TryGetValue(date, out var r) ? r : 0;
                    var costs = purchaseDailyGroups.TryGetValue(date, out var c) ? c : 0;
                    var profit = revenue - costs;
                    var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    return new FinancialReportLineDto
                    {
                        Period = date.ToString("yyyy-MM-dd"),
                        Revenue = revenue,
                        Costs = costs,
                        Profit = profit,
                        ProfitMargin = margin,
                        TransactionCount = dailyGroups.ContainsKey(date) ? 1 : 0
                    };
                })
                .ToList();

            var totalRevenue = lines.Sum(l => l.Revenue);
            var totalCosts = lines.Sum(l => l.Costs);
            var totalProfit = totalRevenue - totalCosts;
            var profitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;

            var report = new FinancialReportDto
            {
                ReportType = "Daily",
                StartDate = start,
                EndDate = end,
                Lines = lines,
                TotalRevenue = totalRevenue,
                TotalCosts = totalCosts,
                TotalProfit = totalProfit,
                ProfitMargin = profitMargin
            };

            return Ok(ApiResponse<FinancialReportDto>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminFinancialReportsController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<FinancialReportDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get monthly P&L report
    /// </summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<ApiResponse<FinancialReportDto>>> GetMonthlyReport(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var start = startDate ?? DateTimeOffset.UtcNow.AddYears(-1);
            var end = endDate ?? DateTimeOffset.UtcNow.AddMonths(1);

            var salesInvoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.InvoiceDate >= start && si.InvoiceDate < end)
                .Include(si => si.Items)
                .ToListAsync(cancellationToken);

            var purchaseInvoices = await _purchaseInvoiceRepository.Query()
                .AsNoTracking()
                .Where(pi => pi.InvoiceDate >= start && pi.InvoiceDate < end)
                .Include(pi => pi.Items)
                .ToListAsync(cancellationToken);

            var monthlyGroups = salesInvoices
                .GroupBy(si => new DateTime(si.InvoiceDate.Year, si.InvoiceDate.Month, 1))
                .Select(g => new
                {
                    Month = g.Key,
                    Revenue = g.Sum(si => si.TotalAmount),
                    Count = g.Count()
                })
                .ToDictionary(x => x.Month, x => (Revenue: x.Revenue, Count: x.Count));

            var purchaseMonthlyGroups = purchaseInvoices
                .GroupBy(pi => new DateTime(pi.InvoiceDate.Year, pi.InvoiceDate.Month, 1))
                .Select(g => new
                {
                    Month = g.Key,
                    Costs = g.Sum(pi => pi.TotalAmount)
                })
                .ToDictionary(x => x.Month, x => x.Costs);

            var allMonths = new HashSet<DateTime>(monthlyGroups.Keys.Union(purchaseMonthlyGroups.Keys));
            var lines = allMonths
                .OrderBy(m => m)
                .Select(month =>
                {
                    var (revenue, count) = monthlyGroups.TryGetValue(month, out var m) ? m : (0, 0);
                    var costs = purchaseMonthlyGroups.TryGetValue(month, out var c) ? c : 0;
                    var profit = revenue - costs;
                    var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    return new FinancialReportLineDto
                    {
                        Period = month.ToString("yyyy-MM"),
                        Revenue = revenue,
                        Costs = costs,
                        Profit = profit,
                        ProfitMargin = margin,
                        TransactionCount = count
                    };
                })
                .ToList();

            var totalRevenue = lines.Sum(l => l.Revenue);
            var totalCosts = lines.Sum(l => l.Costs);
            var totalProfit = totalRevenue - totalCosts;
            var profitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;

            var report = new FinancialReportDto
            {
                ReportType = "Monthly",
                StartDate = start,
                EndDate = end,
                Lines = lines,
                TotalRevenue = totalRevenue,
                TotalCosts = totalCosts,
                TotalProfit = totalProfit,
                ProfitMargin = profitMargin
            };

            return Ok(ApiResponse<FinancialReportDto>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminFinancialReportsController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<FinancialReportDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get yearly P&L report
    /// </summary>
    [HttpGet("yearly")]
    public async Task<ActionResult<ApiResponse<FinancialReportDto>>> GetYearlyReport(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var start = startDate ?? DateTimeOffset.UtcNow.AddYears(-5);
            var end = endDate ?? DateTimeOffset.UtcNow.AddYears(1);

            var salesInvoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(si => si.InvoiceDate >= start && si.InvoiceDate < end)
                .Include(si => si.Items)
                .ToListAsync(cancellationToken);

            var purchaseInvoices = await _purchaseInvoiceRepository.Query()
                .AsNoTracking()
                .Where(pi => pi.InvoiceDate >= start && pi.InvoiceDate < end)
                .Include(pi => pi.Items)
                .ToListAsync(cancellationToken);

            var yearlyGroups = salesInvoices
                .GroupBy(si => si.InvoiceDate.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Revenue = g.Sum(si => si.TotalAmount),
                    Count = g.Count()
                })
                .ToDictionary(x => x.Year, x => (Revenue: x.Revenue, Count: x.Count));

            var purchaseYearlyGroups = purchaseInvoices
                .GroupBy(pi => pi.InvoiceDate.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Costs = g.Sum(pi => pi.TotalAmount)
                })
                .ToDictionary(x => x.Year, x => x.Costs);

            var allYears = new HashSet<int>(yearlyGroups.Keys.Union(purchaseYearlyGroups.Keys));
            var lines = allYears
                .OrderBy(y => y)
                .Select(year =>
                {
                    var (revenue, count) = yearlyGroups.TryGetValue(year, out var y) ? y : (0, 0);
                    var costs = purchaseYearlyGroups.TryGetValue(year, out var c) ? c : 0;
                    var profit = revenue - costs;
                    var margin = revenue > 0 ? (profit / revenue) * 100 : 0;

                    return new FinancialReportLineDto
                    {
                        Period = year.ToString(),
                        Revenue = revenue,
                        Costs = costs,
                        Profit = profit,
                        ProfitMargin = margin,
                        TransactionCount = count
                    };
                })
                .ToList();

            var totalRevenue = lines.Sum(l => l.Revenue);
            var totalCosts = lines.Sum(l => l.Costs);
            var totalProfit = totalRevenue - totalCosts;
            var profitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;

            var report = new FinancialReportDto
            {
                ReportType = "Yearly",
                StartDate = start,
                EndDate = end,
                Lines = lines,
                TotalRevenue = totalRevenue,
                TotalCosts = totalCosts,
                TotalProfit = totalProfit,
                ProfitMargin = profitMargin
            };

            return Ok(ApiResponse<FinancialReportDto>.Success(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in AdminFinancialReportsController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<FinancialReportDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }
}