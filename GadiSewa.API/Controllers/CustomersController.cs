using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Customers;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;
    private readonly IRepository<SalesInvoice> _invoiceRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public CustomersController(
        IUserRepository userRepository,
        IRepository<Customer> customerRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<SalesInvoice> invoiceRepository,
        IRepository<Appointment> appointmentRepository,
        IPasswordHasherService passwordHasherService,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _vehicleRepository = vehicleRepository;
        _invoiceRepository = invoiceRepository;
        _appointmentRepository = appointmentRepository;
        _passwordHasherService = passwordHasherService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedException("Invalid user identity.");
        }

        return userId;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerSearchResultDto>>>> GetAllCustomers(CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        if (role != UserRole.Admin.ToString() && role != UserRole.Staff.ToString())
        {
            return Forbid();
        }

        IQueryable<Customer> query = _customerRepository.Query()
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Vehicles)
            .Include(c => c.Appointments)
            .Include(c => c.Reviews);

        var results = await query
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerSearchResultDto>>> GetCustomerById(Guid id, CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        // Customers can view only their own record
        if (role == UserRole.Customer.ToString())
        {
            var customerOfUser = await _customerRepository.Query().FirstOrDefaultAsync(c => c.UserId == currentUserId, cancellationToken);
            if (customerOfUser is null || customerOfUser.Id != id)
            {
                return Forbid();
            }
        }

        // Staff/Admin can view any
        var query = _customerRepository.Query()
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Vehicles)
            .Include(c => c.Appointments)
            .Include(c => c.Reviews);

        var customer = await query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound(ApiResponse<CustomerSearchResultDto>.Failure("Customer not found.", StatusCodes.Status404NotFound));
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

    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<CustomerRegistrationResponseDto>>> RegisterCustomer(
        [FromBody] CreateCustomerRequestDto request,
        CancellationToken cancellationToken)
    {
        // Reuse StaffCustomersController logic by duplicating necessary checks
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerSearchResultDto>>> UpdateCustomer(
        Guid id,
        [FromBody] UpdateCustomerRequestDto request,
        CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        var customer = await _customerRepository.Query().Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return NotFound(ApiResponse<CustomerSearchResultDto>.Failure("Customer not found.", StatusCodes.Status404NotFound));
        }

        // If customer role, ensure they update only their own profile
        if (role == UserRole.Customer.ToString() && customer.UserId != currentUserId)
        {
            return Forbid();
        }

        customer.User.FirstName = request.FirstName.Trim();
        customer.User.LastName = request.LastName.Trim();
        customer.User.PhoneNumber = request.PhoneNumber.Trim();
        customer.Address = request.Address.Trim();
        customer.User.UpdatedAt = DateTimeOffset.UtcNow;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedDto = new CustomerSearchResultDto
        {
            CustomerId = customer.Id,
            UserId = customer.UserId,
            FirstName = customer.User.FirstName,
            LastName = customer.User.LastName,
            Email = customer.User.Email,
            PhoneNumber = customer.User.PhoneNumber,
            Address = customer.Address,
            LoyaltyPoints = customer.LoyaltyPoints,
            VehicleCount = customer.Vehicles?.Count ?? 0,
            AppointmentCount = customer.Appointments?.Count ?? 0,
            ReviewCount = customer.Reviews?.Count ?? 0,
            IsActive = customer.User.IsActive,
            Vehicles = customer.Vehicles?.Select(v => new CustomerVehicleDto
            {
                VehicleId = v.Id,
                RegistrationNumber = v.RegistrationNumber,
                Make = v.Make,
                Model = v.Model,
                Year = v.Year,
                Color = v.Color
            }).ToList() ?? new List<CustomerVehicleDto>()
        };

        return Ok(ApiResponse<CustomerSearchResultDto>.Success(updatedDto));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object?>>> DeactivateCustomer(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound(ApiResponse<object?>.Failure("User not found.", StatusCodes.Status404NotFound));
        }

        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        _userRepository.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object?>.Success(null));
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerSearchResultDto>>>> SearchCustomers(
        [FromQuery] string? q,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Allow staff/admin search; customers cannot search other customers
        var role = GetCurrentUserRole();
        if (role == UserRole.Customer.ToString()) return Forbid();

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        IQueryable<Customer> customersQuery = _customerRepository.Query()
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Vehicles)
            .Include(c => c.Appointments)
            .Include(c => c.Reviews);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchTerm = q.Trim().ToLowerInvariant();
            customersQuery = customersQuery.Where(c =>
                c.User.FirstName.ToLower().Contains(searchTerm) ||
                c.User.LastName.ToLower().Contains(searchTerm) ||
                c.User.Email.ToLower().Contains(searchTerm) ||
                c.User.PhoneNumber.Contains(searchTerm) ||
                c.Vehicles.Any(v => v.RegistrationNumber.ToLower().Contains(searchTerm)) ||
                c.Id.ToString().StartsWith(searchTerm));
        }

        var results = await customersQuery
            .OrderBy(c => c.User.LastName)
            .ThenBy(c => c.User.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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

    [HttpGet("{id:guid}/vehicles")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerVehicleDto>>>> GetCustomerVehicles(Guid id, CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (role == UserRole.Customer.ToString())
        {
            var cust = await _customerRepository.Query().FirstOrDefaultAsync(c => c.UserId == currentUserId, cancellationToken);
            if (cust is null || cust.Id != id) return Forbid();
        }

        var vehicles = await _vehicleRepository.Query().AsNoTracking().Where(v => v.CustomerId == id).ToListAsync(cancellationToken);
        var dtos = vehicles.Select(v => new CustomerVehicleDto
        {
            VehicleId = v.Id,
            RegistrationNumber = v.RegistrationNumber,
            Make = v.Make,
            Model = v.Model,
            Year = v.Year,
            Color = v.Color
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<CustomerVehicleDto>>.Success(dtos));
    }

    [HttpPost("{id:guid}/vehicles")]
    public async Task<ActionResult<ApiResponse<CustomerVehicleDto>>> AddVehicleToCustomer(Guid id, [FromBody] CreateVehicleRequestDto request, CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (role == UserRole.Customer.ToString())
        {
            var cust = await _customerRepository.Query().FirstOrDefaultAsync(c => c.UserId == currentUserId, cancellationToken);
            if (cust is null || cust.Id != id) return Forbid();
        }

        var regNo = request.RegistrationNumber.Trim().ToUpperInvariant();
        var existing = await _vehicleRepository.ListAsync(v => v.RegistrationNumber == regNo, cancellationToken);
        if (existing.Count > 0) return Conflict(ApiResponse<CustomerVehicleDto>.Failure("A vehicle with this registration already exists.", StatusCodes.Status409Conflict));

        var vehicle = new Vehicle
        {
            CustomerId = id,
            RegistrationNumber = regNo,
            Make = request.Make.Trim(),
            Model = request.Model.Trim(),
            Year = request.Year,
            Mileage = request.Mileage,
            Color = request.Color.Trim()
        };

        await _vehicleRepository.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new CustomerVehicleDto
        {
            VehicleId = vehicle.Id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            Color = vehicle.Color
        };

        return Ok(ApiResponse<CustomerVehicleDto>.Success(dto));
    }

    [HttpPut("{id:guid}/vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerVehicleDto>>> UpdateVehicle(Guid id, Guid vehicleId, [FromBody] UpdateVehicleRequestDto request, CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        var vehicle = await _vehicleRepository.Query().Include(v => v.Customer).FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
        if (vehicle is null || vehicle.CustomerId != id) return NotFound(ApiResponse<CustomerVehicleDto>.Failure("Vehicle not found.", StatusCodes.Status404NotFound));

        if (role == UserRole.Customer.ToString() && vehicle.Customer.UserId != currentUserId) return Forbid();

        var regNo = request.RegistrationNumber.Trim().ToUpperInvariant();
        var other = await _vehicleRepository.ListAsync(v => v.RegistrationNumber == regNo && v.Id != vehicleId, cancellationToken);
        if (other.Count > 0) return Conflict(ApiResponse<CustomerVehicleDto>.Failure("Another vehicle with this registration exists.", StatusCodes.Status409Conflict));

        vehicle.RegistrationNumber = regNo;
        vehicle.Make = request.Make.Trim();
        vehicle.Model = request.Model.Trim();
        vehicle.Year = request.Year;
        vehicle.Mileage = request.Mileage;
        vehicle.Color = request.Color.Trim();
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;

        _vehicleRepository.Update(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new CustomerVehicleDto
        {
            VehicleId = vehicle.Id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            Color = vehicle.Color
        };

        return Ok(ApiResponse<CustomerVehicleDto>.Success(dto));
    }

    [HttpDelete("{id:guid}/vehicles/{vehicleId:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> RemoveVehicle(Guid id, Guid vehicleId, CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        var vehicle = await _vehicleRepository.Query().Include(v => v.Customer).FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
        if (vehicle is null || vehicle.CustomerId != id) return NotFound(ApiResponse<object?>.Failure("Vehicle not found.", StatusCodes.Status404NotFound));

        if (role == UserRole.Customer.ToString() && vehicle.Customer.UserId != currentUserId) return Forbid();

        _vehicleRepository.Remove(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<CustomerHistorySummaryDto>>> GetCustomerHistory(Guid id, CancellationToken cancellationToken)
    {
        var role = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (role == UserRole.Customer.ToString())
        {
            var cust = await _customerRepository.Query().FirstOrDefaultAsync(c => c.UserId == currentUserId, cancellationToken);
            if (cust is null || cust.Id != id) return Forbid();
        }

        var appointments = await _appointmentRepository.Query()
            .AsNoTracking()
            .Where(a => a.CustomerId == id)
            .Include(a => a.Vehicle)
            .Include(a => a.AssignedStaff).ThenInclude(s => s.User)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(cancellationToken);

        var invoices = await _invoiceRepository.Query()
            .AsNoTracking()
            .Where(i => i.CustomerId == id)
            .Include(i => i.CreatedByStaff).ThenInclude(s => s.User)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

        var dto = new CustomerHistorySummaryDto
        {
            CustomerId = id,
            Appointments = appointments.Select(a => new AppointmentHistoryItemDto
            {
                AppointmentId = a.Id,
                AppointmentNumber = a.AppointmentNumber,
                ScheduledAt = a.ScheduledAt,
                CompletedAt = a.CompletedAt,
                ProblemDescription = a.ProblemDescription,
                Notes = a.Notes,
                VehicleRegistration = a.Vehicle.RegistrationNumber,
                AssignedStaffName = a.AssignedStaff is not null ? $"{a.AssignedStaff.User.FirstName} {a.AssignedStaff.User.LastName}".Trim() : ""
            }).ToList(),
            Invoices = invoices.Select(i => new SalesInvoiceHistoryItemDto
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                TotalAmount = i.TotalAmount,
                Status = i.Status.ToString(),
                CreatedByStaffName = i.CreatedByStaff is not null ? $"{i.CreatedByStaff.User.FirstName} {i.CreatedByStaff.User.LastName}".Trim() : ""
            }).ToList()
        };

        return Ok(ApiResponse<CustomerHistorySummaryDto>.Success(dto));
    }
}
