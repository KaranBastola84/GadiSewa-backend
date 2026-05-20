using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Auth;
using GadiSewa.Application.DTOs.Customers;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/staff/customers")]
[Authorize(Policy = "BackOfficeOnly")]
public sealed class StaffCustomersController : ControllerBase
{
    private readonly ILogger<StaffCustomersController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<SalesInvoice> _salesInvoiceRepository;
    private readonly IRepository<CreditPayment> _creditPaymentRepository;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public StaffCustomersController(
        IUserRepository userRepository,
        IRepository<Customer> customerRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<SalesInvoice> salesInvoiceRepository,
        IRepository<CreditPayment> creditPaymentRepository,
        IPasswordHasherService passwordHasherService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<StaffCustomersController> logger)
    {
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _vehicleRepository = vehicleRepository;
        _appointmentRepository = appointmentRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _creditPaymentRepository = creditPaymentRepository;
        _passwordHasherService = passwordHasherService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerRegistrationResponseDto>>> CreateCustomer(
        [FromBody] CreateCustomerRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Vehicles is null || request.Vehicles.Count == 0)
            {
                return BadRequest(ApiResponse<CustomerRegistrationResponseDto>.Failure("At least one vehicle is required.", StatusCodes.Status400BadRequest));
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existingUser is not null)
            {
                throw new ConflictException("A user with this email already exists.");
            }

            var normalizedVehicles = request.Vehicles
                .Select(vehicle => new
                {
                    Original = vehicle,
                    RegistrationNumber = vehicle.RegistrationNumber.Trim().ToUpperInvariant()
                })
                .ToList();

            var duplicateRegistration = normalizedVehicles
                .GroupBy(x => x.RegistrationNumber)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateRegistration is not null)
            {
                throw new ConflictException($"Duplicate vehicle registration number in request: {duplicateRegistration.Key}.");
            }

            foreach (var vehicle in normalizedVehicles)
            {
                var existingVehicle = await _vehicleRepository.ListAsync(x => x.RegistrationNumber == vehicle.RegistrationNumber, cancellationToken);
                if (existingVehicle.Count > 0)
                {
                    throw new ConflictException($"A vehicle with registration number {vehicle.RegistrationNumber} already exists.");
                }
            }

            var user = new User
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber.Trim(),
                PasswordHash = _passwordHasherService.HashPassword(request.Password),
                Role = UserRole.Customer,
                IsActive = true,
                IsEmailVerified = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow
            };

            var customer = new Customer
            {
                UserId = user.Id,
                Address = request.Address.Trim(),
                LoyaltyPoints = 0
            };

            var vehicles = normalizedVehicles.Select(vehicle => new Vehicle
            {
                CustomerId = customer.Id,
                RegistrationNumber = vehicle.RegistrationNumber,
                Make = vehicle.Original.Make.Trim(),
                Model = vehicle.Original.Model.Trim(),
                Year = vehicle.Original.Year,
                Mileage = vehicle.Original.Mileage,
                Color = vehicle.Original.Color.Trim()
            }).ToList();

            await _userRepository.AddAsync(user, cancellationToken);
            await _customerRepository.AddAsync(customer, cancellationToken);
            foreach (var vehicle in vehicles)
            {
                await _vehicleRepository.AddAsync(vehicle, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _emailService.SendWelcomeEmailAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<CustomerRegistrationResponseDto>.Success(CustomerRegistrationResponseDto.FromEntities(user, customer, vehicles), StatusCodes.Status201Created));
        }
        catch (ConflictException ex)
        {
            return Conflict(ApiResponse<CustomerRegistrationResponseDto>.Failure(ex.Message, StatusCodes.Status409Conflict));
        }
    }

    /// <summary>
    /// Search customers by vehicle registration, phone, name, or ID
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerSearchResultDto>>>> SearchCustomers(
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IQueryable<Customer> customersQuery = _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Vehicles)
                .Include(c => c.Appointments)
                .Include(c => c.Reviews);

            // Apply filter if query provided
            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.Trim().ToLowerInvariant();
                var customerIdMatch = Guid.TryParse(q.Trim(), out var customerId) ? customerId : Guid.Empty;

                customersQuery = customersQuery.Where(c =>
                    c.User.FirstName.ToLower().Contains(searchTerm) ||
                    c.User.LastName.ToLower().Contains(searchTerm) ||
                    c.User.PhoneNumber.ToLower().Contains(searchTerm) ||
                    (customerIdMatch != Guid.Empty && c.Id == customerIdMatch) ||
                    c.Vehicles.Any(v => v.RegistrationNumber.ToLower().Contains(searchTerm)));
            }

            var results = await customersQuery
                .OrderBy(c => c.User.LastName)
                .ThenBy(c => c.User.FirstName)
                .ToListAsync(cancellationToken);

            var dtos = results.Select(c => new CustomerSearchResultDto
            {
                CustomerId = c.Id,
                UserId = c.UserId,
                FirstName = c.User.FirstName,
                LastName = c.User.LastName,
                Email = c.User.Email,
                PhoneNumber = c.User.PhoneNumber,
                Address = c.Address,
                LoyaltyPoints = c.LoyaltyPoints,
                VehicleCount = c.Vehicles.Count,
                AppointmentCount = c.Appointments.Count,
                ReviewCount = c.Reviews.Count,
                IsActive = c.User.IsActive,
                Vehicles = c.Vehicles.Select(v => new CustomerVehicleDto
                {
                    VehicleId = v.Id,
                    RegistrationNumber = v.RegistrationNumber,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color
                }).ToList()
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Success(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in StaffCustomersController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get customer details by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerSearchResultDto>>> GetCustomerDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<Customer> query = _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Vehicles)
                .Include(c => c.Appointments)
                .Include(c => c.Reviews);

            var customer = await query
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is null)
            {
                return NotFound(ApiResponse<CustomerSearchResultDto>.Failure(
                    "Customer not found.",
                    StatusCodes.Status404NotFound));
            }

            var dto = new CustomerSearchResultDto
            {
                CustomerId = customer.Id,
                UserId = customer.UserId,
                FirstName = customer.User.FirstName,
                LastName = customer.User.LastName,
                Email = customer.User.Email,
                PhoneNumber = customer.User.PhoneNumber,
                Address = customer.Address,
                LoyaltyPoints = customer.LoyaltyPoints,
                VehicleCount = customer.Vehicles.Count,
                AppointmentCount = customer.Appointments.Count,
                ReviewCount = customer.Reviews.Count,
                IsActive = customer.User.IsActive,
                Vehicles = customer.Vehicles.Select(v => new CustomerVehicleDto
                {
                    VehicleId = v.Id,
                    RegistrationNumber = v.RegistrationNumber,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color
                }).ToList()
            };

            return Ok(ApiResponse<CustomerSearchResultDto>.Success(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in StaffCustomersController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<CustomerSearchResultDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Search by vehicle registration number
    /// </summary>
    [HttpGet("by-vehicle/{registrationNumber}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerSearchResultDto>>>> SearchByVehicleRegistration(
        string registrationNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(registrationNumber))
            {
                return BadRequest(ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Failure(
                    "Registration number is required.",
                    StatusCodes.Status400BadRequest));
            }

            var normalizedRegNo = registrationNumber.Trim().ToUpperInvariant();

            IQueryable<Customer> query = _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Vehicles)
                .Include(c => c.Appointments)
                .Include(c => c.Reviews);

            var results = await query
                .Where(c => c.Vehicles.Any(v => v.RegistrationNumber.ToUpper() == normalizedRegNo))
                .ToListAsync(cancellationToken);

            var dtos = results.Select(c => new CustomerSearchResultDto
            {
                CustomerId = c.Id,
                UserId = c.UserId,
                FirstName = c.User.FirstName,
                LastName = c.User.LastName,
                Email = c.User.Email,
                PhoneNumber = c.User.PhoneNumber,
                Address = c.Address,
                LoyaltyPoints = c.LoyaltyPoints,
                VehicleCount = c.Vehicles.Count,
                AppointmentCount = c.Appointments.Count,
                ReviewCount = c.Reviews.Count,
                IsActive = c.User.IsActive,
                Vehicles = c.Vehicles.Select(v => new CustomerVehicleDto
                {
                    VehicleId = v.Id,
                    RegistrationNumber = v.RegistrationNumber,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color
                }).ToList()
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Success(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in StaffCustomersController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Search by phone number
    /// </summary>
    [HttpGet("by-phone/{phoneNumber}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerSearchResultDto>>>> SearchByPhone(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return BadRequest(ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Failure(
                    "Phone number is required.",
                    StatusCodes.Status400BadRequest));
            }

            var normalizedPhone = phoneNumber.Trim();

            IQueryable<Customer> query = _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Vehicles)
                .Include(c => c.Appointments)
                .Include(c => c.Reviews);

            var results = await query
                .Where(c => c.User.PhoneNumber.Contains(normalizedPhone))
                .ToListAsync(cancellationToken);

            var dtos = results.Select(c => new CustomerSearchResultDto
            {
                CustomerId = c.Id,
                UserId = c.UserId,
                FirstName = c.User.FirstName,
                LastName = c.User.LastName,
                Email = c.User.Email,
                PhoneNumber = c.User.PhoneNumber,
                Address = c.Address,
                LoyaltyPoints = c.LoyaltyPoints,
                VehicleCount = c.Vehicles.Count,
                AppointmentCount = c.Appointments.Count,
                ReviewCount = c.Reviews.Count,
                IsActive = c.User.IsActive,
                Vehicles = c.Vehicles.Select(v => new CustomerVehicleDto
                {
                    VehicleId = v.Id,
                    RegistrationNumber = v.RegistrationNumber,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color
                }).ToList()
            }).ToList();

            return Ok(ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Success(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in StaffCustomersController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<IReadOnlyList<CustomerSearchResultDto>>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    [HttpGet("{id:guid}/full-profile")]
    [Authorize(Policy = "BackOfficeOnly")]
    public async Task<ActionResult<ApiResponse<CustomerFullProfileDto>>> GetCustomerFullProfile(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<Customer> query = _customerRepository.Query()
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Vehicles)
                .Include(c => c.Appointments)
                .Include(c => c.Reviews);

            var customer = await query
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is null)
            {
                return NotFound(ApiResponse<CustomerFullProfileDto>.Failure(
                    "Customer not found.",
                    StatusCodes.Status404NotFound));
            }

            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(i => i.CustomerId == id)
                .OrderByDescending(i => i.InvoiceDate)
                .Take(10)
                .ToListAsync(cancellationToken);

            var appointments = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.CustomerId == id)
                .Include(a => a.Vehicle)
                .OrderByDescending(a => a.ScheduledAt)
                .Take(10)
                .ToListAsync(cancellationToken);

            var dto = new CustomerFullProfileDto
            {
                CustomerInfo = new CustomerInfoDto
                {
                    Id = customer.Id,
                    Name = $"{customer.User.FirstName} {customer.User.LastName}".Trim(),
                    Email = customer.User.Email,
                    Phone = customer.User.PhoneNumber,
                    Address = customer.Address,
                    LoyaltyPoints = customer.LoyaltyPoints,
                    TotalSpent = customer.TotalSpent
                },
                Vehicles = customer.Vehicles
                    .Select(VehicleDto.FromVehicle)
                    .ToList(),
                RecentInvoices = invoices
                    .Select(i => new RecentInvoiceDto
                    {
                        Id = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        InvoiceDate = i.InvoiceDate,
                        TotalAmount = i.TotalAmount,
                        Status = i.Status.ToString()
                    })
                    .ToList(),
                RecentAppointments = appointments
                    .Select(a => new RecentAppointmentDto
                    {
                        Id = a.Id,
                        AppointmentNumber = a.AppointmentNumber,
                        ScheduledAt = a.ScheduledAt,
                        Status = a.Status.ToString(),
                        VehicleRegistration = a.Vehicle.RegistrationNumber
                    })
                    .ToList()
            };

            return Ok(ApiResponse<CustomerFullProfileDto>.Success(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in StaffCustomersController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<CustomerFullProfileDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>
    /// Get customer history (appointments and invoices)
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<CustomerHistorySummaryDto>>> GetCustomerHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerRepository.Query()
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Include(c => c.User)
                .Include(c => c.Appointments)
                .Include(c => c.SalesInvoices)
                .Include(c => c.CreditPayments)
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is null)
            {
                return NotFound(ApiResponse<CustomerHistorySummaryDto>.Failure(
                    "Customer not found.",
                    StatusCodes.Status404NotFound));
            }

            var appointments = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.CustomerId == id)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .Include(a => a.Reviews)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(cancellationToken);

            var invoices = await _salesInvoiceRepository.Query()
                .AsNoTracking()
                .Where(i => i.CustomerId == id)
                .Include(i => i.CreatedByStaff)
                .ThenInclude(s => s.User)
                .Include(i => i.Items)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            var creditPayments = await _creditPaymentRepository.Query()
                .AsNoTracking()
                .Where(cp => cp.CustomerId == id)
                .OrderByDescending(cp => cp.PaymentDate)
                .ToListAsync(cancellationToken);

            var totalSpent = invoices.Sum(i => i.TotalAmount);
            var totalUnpaid = invoices
                .Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Overdue)
                .Sum(i => i.TotalAmount - creditPayments.Where(cp => cp.SalesInvoiceId == i.Id).Sum(cp => cp.Amount));

            var dto = new CustomerHistorySummaryDto
            {
                CustomerId = id,
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
                        AmountDue = i.TotalAmount - creditPayments.Where(cp => cp.SalesInvoiceId == i.Id).Sum(cp => cp.Amount)
                    })
                    .ToList()
            };

            return Ok(ApiResponse<CustomerHistorySummaryDto>.Success(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in StaffCustomersController");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<CustomerHistorySummaryDto>.Failure(
                    "An unexpected error occurred. Please try again.",
                    StatusCodes.Status500InternalServerError));
        }
    }
}