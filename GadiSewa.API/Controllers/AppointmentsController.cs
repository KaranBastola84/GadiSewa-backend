using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Appointments;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<Vehicle> _vehicleRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AppointmentsController(
        IRepository<Appointment> appointmentRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Staff> staffRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _vehicleRepository = vehicleRepository;
        _staffRepository = staffRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("uid")?.Value;
        return Guid.Parse(userIdClaim!);
    }

    /// <summary>
    /// Customer creates a new appointment
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> CreateAppointment(
        [FromBody] CreateAppointmentRequestDto request,
        CancellationToken cancellationToken)
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
            return ApiResponse<AppointmentDto>.Failure($"Error creating appointment: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Customer gets their own appointments
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetMyAppointments(
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();

            var customers = await _customerRepository.ListAsync(c => c.UserId == userId, cancellationToken);
            var customer = customers.FirstOrDefault();
            if (customer == null)
                return ApiResponse<IEnumerable<AppointmentDto>>.Failure("Customer profile not found", 404);

            var appointments = _appointmentRepository.Query()
                .AsNoTracking()
                .Where(a => a.CustomerId == customer.Id)
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User)
                .OrderByDescending(a => a.ScheduledAt)
                .ToList();

            var appointmentDtos = appointments.Select(AppointmentDto.FromAppointment);
            return ApiResponse<IEnumerable<AppointmentDto>>.Success(appointmentDtos);
        }
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<AppointmentDto>>.Failure($"Error retrieving appointments: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Staff lists all appointments with optional filtering
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> ListAppointments(
        [FromQuery] int? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseQuery = _appointmentRepository.Query()
                .AsNoTracking()
                .Include(a => a.Customer)
                .ThenInclude(c => c.User)
                .Include(a => a.Vehicle)
                .Include(a => a.AssignedStaff)
                .ThenInclude(s => s!.User);

            // Filter by status if provided
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
        catch (Exception ex)
        {
            return ApiResponse<IEnumerable<AppointmentDto>>.Failure($"Error retrieving appointments: {ex.Message}", 500);
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
            return ApiResponse<AppointmentDto>.Failure($"Error retrieving appointment: {ex.Message}", 500);
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
            return ApiResponse<AppointmentDto>.Failure($"Error updating appointment: {ex.Message}", 500);
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
