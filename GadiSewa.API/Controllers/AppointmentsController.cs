using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Appointments;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
/// <summary>
/// Controller for managing vehicle service appointments
/// </summary>
public class AppointmentsController : ControllerBase
{
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly ILogger<AppointmentsController> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public AppointmentsController(
        IRepository<Appointment> appointmentRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Staff> staffRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppointmentsController> logger)
    {
        _appointmentRepository = appointmentRepository;
        _vehicleRepository = vehicleRepository;
        _staffRepository = staffRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdValue ?? Guid.Empty.ToString());
    }

    [HttpPost]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> CreateAppointment([FromBody] CreateAppointmentRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Get customer
            var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
            var customer = customers.FirstOrDefault();
            if (customer == null)
                return ApiResponse<AppointmentDto>.Failure("Customer profile not found", 404);

            // Verify vehicle exists and belongs to customer
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
            if (vehicle == null || vehicle.CustomerId != customer.Id)
                return ApiResponse<AppointmentDto>.Failure("Vehicle not found or does not belong to customer", 404);

            // Validate scheduled time is in future
            if (request.ScheduledAt <= DateTimeOffset.UtcNow.AddHours(1))
                return ApiResponse<AppointmentDto>.Failure(
                    "Appointment must be scheduled at least 1 hour in the future", 400);

            // Prevent double booking: same vehicle at same exact scheduled time (not cancelled)
            var conflict = await _appointmentRepository.Query()
                .AnyAsync(a => a.VehicleId == request.VehicleId
                               && a.ScheduledAt == request.ScheduledAt
                               && a.Status != AppointmentStatus.Cancelled, cancellationToken);

            if (conflict)
                return ApiResponse<AppointmentDto>.Failure("Vehicle already has an appointment at the requested time", 409);

            // Create appointment
            var appointment = new Appointment
            {
                AppointmentNumber = $"APT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}",
                CustomerId = customer.Id,
                VehicleId = request.VehicleId,
                ScheduledAt = request.ScheduledAt,
                ProblemDescription = request.ProblemDescription,
                Notes = request.Notes ?? string.Empty,
                Status = AppointmentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _appointmentRepository.AddAsync(appointment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reload with related data
            var created = _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.Id == appointment.Id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .FirstOrDefault();

            return CreatedAtAction(nameof(GetAppointmentDetails),
                new { id = created!.Id },
                ApiResponse<AppointmentDto>.Success(AppointmentDto.FromAppointment(created!), 201));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appointment");
            return ApiResponse<AppointmentDto>.Failure("An unexpected error occurred while creating the appointment. Please try again.", 500);
        }
    }

    /// <summary>
    /// List appointments. Customers get their own; back-office users get all with optional filters.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetAppointments(
        [FromQuery] int? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If user is staff or admin, return paged list with optional status filter
            if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                var baseQuery = _appointmentRepository.Query()
                    .AsNoTracking()
                    .Include(a => a.Customer)
                    .ThenInclude(c => c.User)
                    .Include(a => a.Vehicle)
                    .Include(a => a.AssignedStaff)
                    .ThenInclude(s => s!.User);

                IQueryable<Appointment> filteredQuery = baseQuery;
                if (status.HasValue && Enum.IsDefined(typeof(AppointmentStatus), status))
                {
                    var appointmentStatus = (AppointmentStatus)status.Value;
                    filteredQuery = filteredQuery.Where(a => a.Status == appointmentStatus);
                }

                var appointments = await filteredQuery
                    .OrderByDescending(a => a.ScheduledAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                var appointmentDtos = appointments.Select(AppointmentDto.FromAppointment);
                return ApiResponse<IEnumerable<AppointmentDto>>.Success(appointmentDtos);
            }

            // Otherwise assume customer - return only their appointments
            var userId = GetCurrentUserId();
            var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
            var customer = customers.FirstOrDefault();
            if (customer == null)
                return ApiResponse<IEnumerable<AppointmentDto>>.Failure("Customer profile not found", 404);

            var appointmentsForCustomer = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.CustomerId == customer.Id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(cancellationToken);

            var appointmentDtosCust = appointmentsForCustomer.Select(AppointmentDto.FromAppointment);
            return ApiResponse<IEnumerable<AppointmentDto>>.Success(appointmentDtosCust);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appointments");
            return ApiResponse<IEnumerable<AppointmentDto>>.Failure("An unexpected error occurred while loading appointments. Please try again.", 500);
        }
    }

    /// <summary>
    /// Staff gets single appointment details
    /// </summary>
    [HttpGet("staff/{id}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> GetAppointmentDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .FirstOrDefaultAsync(cancellationToken);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            return ApiResponse<AppointmentDto>.Success(AppointmentDto.FromAppointment(appointment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appointment details");
            return ApiResponse<AppointmentDto>.Failure("An unexpected error occurred while loading appointment details. Please try again.", 500);
        }
    }

    /// <summary>
    /// Get appointment details (customer can access their own appointment)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> GetAppointment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .FirstOrDefaultAsync(cancellationToken);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            // If user is back-office, allow; otherwise ensure appointment belongs to customer
            if (!(User.IsInRole("Admin") || User.IsInRole("Staff")))
            {
                var userId = GetCurrentUserId();
                var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
                var customer = customers.FirstOrDefault();
                if (customer == null || appointment.CustomerId != customer.Id)
                    return ApiResponse<AppointmentDto>.Failure("Not authorized to view this appointment", 403);
            }

            return ApiResponse<AppointmentDto>.Success(AppointmentDto.FromAppointment(appointment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer appointment");
            return ApiResponse<AppointmentDto>.Failure("An unexpected error occurred. Please try again.", 500);
        }
    }

    /// <summary>
    /// Staff updates appointment status and assigns staff
    /// </summary>
    [HttpPut("staff/{id}/status")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> UpdateAppointmentStatus(
        Guid id,
        [FromBody] UpdateAppointmentStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var appointment = _appointmentRepository.Query()
                .Where(a => a.Id == id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .FirstOrDefault();

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            // Validate status transition
            if (!IsValidStatusTransition(appointment.Status, request.Status))
                return ApiResponse<AppointmentDto>.Failure(
                    $"Invalid status transition from {appointment.Status} to {request.Status}", 400);

            // Validate assigned staff if provided
            if (request.AssignedStaffId.HasValue)
            {
                var staff = await _staffRepository.GetByIdAsync(request.AssignedStaffId.Value, cancellationToken);
                if (staff == null)
                    return ApiResponse<AppointmentDto>.Failure("Assigned staff not found", 404);
            }

            // Update appointment
            appointment.Status = request.Status;
            if (!string.IsNullOrWhiteSpace(request.Notes))
                appointment.Notes = request.Notes;

            if (request.AssignedStaffId.HasValue)
                appointment.AssignedStaffId = request.AssignedStaffId;

            if (request.Status == AppointmentStatus.Completed)
                appointment.CompletedAt = DateTimeOffset.UtcNow;

            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            _appointmentRepository.Update(appointment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reload with related data
            var updated = _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .FirstOrDefault();

            return ApiResponse<AppointmentDto>.Success(AppointmentDto.FromAppointment(updated!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appointment status");
            return ApiResponse<AppointmentDto>.Failure("An unexpected error occurred while updating the appointment. Please try again.", 500);
        }
    }

    /// <summary>
    /// Back-office update of appointment (status/assignment). Route matches API spec: PUT /api/appointments/{id}
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "BackOfficeOnly")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> UpdateAppointment(Guid id, [FromBody] UpdateAppointmentStatusRequestDto request, CancellationToken cancellationToken)
    {
        return await UpdateAppointmentStatus(id, request, cancellationToken);
    }

    /// <summary>
    /// Cancel appointment. Customers can cancel their own; back-office can cancel any.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> CancelAppointment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = _appointmentRepository.Query()
                .Where(a => a.Id == id)
                .FirstOrDefault();

            if (appointment == null)
                return ApiResponse<string>.Failure("Appointment not found", 404);

            // If back-office, allow; otherwise ensure customer owns it
            if (!(User.IsInRole("Admin") || User.IsInRole("Staff")))
            {
                var userId = GetCurrentUserId();
                var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
                var customer = customers.FirstOrDefault();
                if (customer == null || appointment.CustomerId != customer.Id)
                    return ApiResponse<string>.Failure("Not authorized to cancel this appointment", 403);
            }

            if (appointment.Status == AppointmentStatus.Completed)
                return ApiResponse<string>.Failure("Cannot cancel a completed appointment", 400);

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.UpdatedAt = DateTimeOffset.UtcNow;

            _appointmentRepository.Update(appointment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<string>.Success("Appointment cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling appointment");
            return ApiResponse<string>.Failure("An unexpected error occurred while cancelling the appointment. Please try again.", 500);
        }
    }

    /// <summary>
    /// Customer appointment history by customer id (accessible by back-office or the customer themselves)
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetCustomerAppointments(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            // If not back-office, ensure the requester is the customer
            if (!(User.IsInRole("Admin") || User.IsInRole("Staff")))
            {
                var userId = GetCurrentUserId();
                var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
                var customer = customers.FirstOrDefault();
                if (customer == null || customer.Id != customerId)
                    return ApiResponse<IEnumerable<AppointmentDto>>.Failure("Not authorized to view this customer's appointments", 403);
            }

            var appointments = await _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(cancellationToken);

            var dtos = appointments.Select(AppointmentDto.FromAppointment);
            return ApiResponse<IEnumerable<AppointmentDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer appointments");
            return ApiResponse<IEnumerable<AppointmentDto>>.Failure("An unexpected error occurred while loading customer appointments. Please try again.", 500);
        }
    }

    private static bool IsValidStatusTransition(AppointmentStatus from, AppointmentStatus to)
    {
        // Pending can go to Confirmed or Cancelled
        if (from == AppointmentStatus.Pending)
            return to is AppointmentStatus.Confirmed or AppointmentStatus.Cancelled;

        // Confirmed can go to Completed or Cancelled
        if (from == AppointmentStatus.Confirmed)
            return to is AppointmentStatus.Completed or AppointmentStatus.Cancelled;

        // Cancelled and Completed are final states
        return false;
    }
}
