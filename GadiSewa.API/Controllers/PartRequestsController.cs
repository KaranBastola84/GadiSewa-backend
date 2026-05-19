using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.PartRequests;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/part-requests")]
[Authorize]
public sealed class PartRequestsController : ControllerBase
{
    private readonly IRepository<PartRequest> _partRequestRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PartRequestsController(
        IRepository<PartRequest> partRequestRepository,
        IRepository<Part> partRepository,
        IRepository<Vendor> vendorRepository,
        IRepository<Staff> staffRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _partRequestRepository = partRequestRepository;
        _partRepository = partRepository;
        _vendorRepository = vendorRepository;
        _staffRepository = staffRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdValue ?? Guid.Empty.ToString());
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PartRequestDto>>>> GetPartRequests(
        [FromQuery] int? status,
        CancellationToken cancellationToken)
    {
        try
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            var userId = GetCurrentUserId();

            IQueryable<PartRequest> query = _partRequestRepository.Query()
                .AsNoTracking()
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x!.User)
                .Include(x => x.RequestedByCustomer)
                    .ThenInclude(x => x!.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor);

            if (role == UserRole.Customer.ToString())
            {
                var customer = await _customerRepository.Query()
                    .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
                
                if (customer is null)
                {
                    return NotFound(ApiResponse<IReadOnlyList<PartRequestDto>>.Failure("Customer profile not found.", StatusCodes.Status404NotFound));
                }

                query = query.Where(x => x.RequestedByCustomerId == customer.Id);
            }

            if (status.HasValue && Enum.IsDefined(typeof(PartRequestStatus), status))
            {
                query = query.Where(x => x.Status == (PartRequestStatus)status.Value);
            }

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return Ok(ApiResponse<IReadOnlyList<PartRequestDto>>.Success(items.Select(PartRequestDto.FromPartRequest).ToList()));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<IReadOnlyList<PartRequestDto>>.Failure($"Error retrieving part requests: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PartRequestDto>>> GetPartRequest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            var userId = GetCurrentUserId();

            var partRequest = await _partRequestRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x!.User)
                .Include(x => x.RequestedByCustomer)
                    .ThenInclude(x => x!.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(cancellationToken);

            if (partRequest is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Part request not found.", StatusCodes.Status404NotFound));
            }

            if (role == UserRole.Customer.ToString())
            {
                var customer = await _customerRepository.Query()
                    .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

                if (customer is null || partRequest.RequestedByCustomerId != customer.Id)
                {
                    return Forbid();
                }
            }

            return Ok(ApiResponse<PartRequestDto>.Success(PartRequestDto.FromPartRequest(partRequest)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PartRequestDto>.Failure($"Error retrieving part request: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPost]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<PartRequestDto>>> CreatePartRequest(
        [FromBody] CreatePartRequestRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var staff = await _staffRepository.Query()
                .AsNoTracking()
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.UserId == GetCurrentUserId(), cancellationToken);

            if (staff is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Staff profile not found.", StatusCodes.Status404NotFound));
            }

            var part = await _partRepository.GetByIdAsync(request.PartId, cancellationToken);
            if (part is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Part not found.", StatusCodes.Status404NotFound));
            }

            if (request.VendorId.HasValue)
            {
                var vendor = await _vendorRepository.GetByIdAsync(request.VendorId.Value, cancellationToken);
                if (vendor is null)
                {
                    return NotFound(ApiResponse<PartRequestDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
                }
            }

            var partRequest = new PartRequest
            {
                RequestNumber = $"PR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}",
                RequestedByStaffId = staff.Id,
                PartId = request.PartId,
                VendorId = request.VendorId,
                QuantityRequested = request.QuantityRequested,
                NeededBy = request.NeededBy,
                Status = PartRequestStatus.Requested,
                Notes = request.Notes?.Trim() ?? string.Empty
            };

            await _partRequestRepository.AddAsync(partRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var created = await _partRequestRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == partRequest.Id)
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x!.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<PartRequestDto>.Success(PartRequestDto.FromPartRequest(created!), StatusCodes.Status201Created));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PartRequestDto>.Failure($"Error creating part request: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPost("customer")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ApiResponse<PartRequestDto>>> CreateCustomerPartRequest(
        [FromBody] CreateCustomerPartRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerRepository.Query()
                .AsNoTracking()
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.UserId == GetCurrentUserId(), cancellationToken);

            if (customer is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Customer profile not found.", StatusCodes.Status404NotFound));
            }

            if (string.IsNullOrWhiteSpace(request.PartName))
            {
                return BadRequest(ApiResponse<PartRequestDto>.Failure("Part name is required.", StatusCodes.Status400BadRequest));
            }

            // Find existing part or create dynamic temp Part
            var part = await _partRepository.Query()
                .FirstOrDefaultAsync(p => p.Name.ToLower() == request.PartName.Trim().ToLower(), cancellationToken);

            if (part is null)
            {
                part = new Part
                {
                    Name = request.PartName.Trim(),
                    PartNumber = $"TEMP-PR-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}",
                    Description = $"Dynamically created for customer request (Vehicle: {request.VehicleModel})",
                    UnitPrice = 0,
                    StockQuantity = 0,
                    ReorderLevel = 0,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _partRepository.AddAsync(part, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var partRequest = new PartRequest
            {
                RequestNumber = $"PR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}",
                RequestedByStaffId = null,
                RequestedByCustomerId = customer.Id,
                PartId = part.Id,
                VendorId = null,
                QuantityRequested = 1,
                NeededBy = DateTimeOffset.UtcNow.AddDays(7), // Default: needed in 7 days
                Status = PartRequestStatus.Requested,
                Notes = $"Vehicle: {request.VehicleModel} | Brand: {request.Brand} | Urgency: {request.Urgency} | Notes: {request.Notes}"
            };

            await _partRequestRepository.AddAsync(partRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var created = await _partRequestRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == partRequest.Id)
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x!.User)
                .Include(x => x.RequestedByCustomer)
                    .ThenInclude(x => x!.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<PartRequestDto>.Success(PartRequestDto.FromPartRequest(created!), StatusCodes.Status201Created));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PartRequestDto>.Failure($"Error creating part request: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<PartRequestDto>>> UpdatePartRequest(
        Guid id,
        [FromBody] UpdatePartRequestRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var partRequest = await _partRequestRepository.GetByIdAsync(id, cancellationToken);
            if (partRequest is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Part request not found.", StatusCodes.Status404NotFound));
            }

            if (partRequest.Status is PartRequestStatus.Fulfilled or PartRequestStatus.Rejected)
            {
                return BadRequest(ApiResponse<PartRequestDto>.Failure("Completed part requests cannot be edited.", StatusCodes.Status400BadRequest));
            }

            var part = await _partRepository.GetByIdAsync(request.PartId, cancellationToken);
            if (part is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Part not found.", StatusCodes.Status404NotFound));
            }

            if (request.VendorId.HasValue)
            {
                var vendor = await _vendorRepository.GetByIdAsync(request.VendorId.Value, cancellationToken);
                if (vendor is null)
                {
                    return NotFound(ApiResponse<PartRequestDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
                }
            }

            partRequest.PartId = request.PartId;
            partRequest.VendorId = request.VendorId;
            partRequest.QuantityRequested = request.QuantityRequested;
            partRequest.NeededBy = request.NeededBy;
            partRequest.Notes = request.Notes?.Trim() ?? string.Empty;
            partRequest.UpdatedAt = DateTimeOffset.UtcNow;

            _partRequestRepository.Update(partRequest);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var updated = await _partRequestRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x!.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(ApiResponse<PartRequestDto>.Success(PartRequestDto.FromPartRequest(updated!)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PartRequestDto>.Failure($"Error updating part request: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<PartRequestDto>>> UpdatePartRequestStatus(
        Guid id,
        [FromBody] UpdatePartRequestStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var partRequest = await _partRequestRepository.GetByIdAsync(id, cancellationToken);
            if (partRequest is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Part request not found.", StatusCodes.Status404NotFound));
            }

            if (!IsValidStatusTransition(partRequest.Status, request.Status))
            {
                return BadRequest(ApiResponse<PartRequestDto>.Failure($"Invalid status transition from {partRequest.Status} to {request.Status}.", StatusCodes.Status400BadRequest));
            }

            if (request.VendorId.HasValue)
            {
                var vendor = await _vendorRepository.GetByIdAsync(request.VendorId.Value, cancellationToken);
                if (vendor is null)
                {
                    return NotFound(ApiResponse<PartRequestDto>.Failure("Vendor not found.", StatusCodes.Status404NotFound));
                }

                partRequest.VendorId = request.VendorId;
            }

            partRequest.Status = request.Status;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                partRequest.Notes = request.Notes.Trim();
            }
            partRequest.UpdatedAt = DateTimeOffset.UtcNow;

            _partRequestRepository.Update(partRequest);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var updated = await _partRequestRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x!.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(ApiResponse<PartRequestDto>.Success(PartRequestDto.FromPartRequest(updated!)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PartRequestDto>.Failure($"Error updating part request status: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<ActionResult<ApiResponse<object?>>> DeletePartRequest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var partRequest = await _partRequestRepository.GetByIdAsync(id, cancellationToken);
            if (partRequest is null)
            {
                return NotFound(ApiResponse<object?>.Failure("Part request not found.", StatusCodes.Status404NotFound));
            }

            _partRequestRepository.Remove(partRequest);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(ApiResponse<object?>.Success(null));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object?>.Failure($"Error deleting part request: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    private static bool IsValidStatusTransition(PartRequestStatus from, PartRequestStatus to)
    {
        return from switch
        {
            PartRequestStatus.Requested => to is PartRequestStatus.Approved or PartRequestStatus.Rejected,
            PartRequestStatus.Approved => to is PartRequestStatus.Ordered or PartRequestStatus.Rejected,
            PartRequestStatus.Ordered => to is PartRequestStatus.Fulfilled or PartRequestStatus.Rejected,
            _ => false
        };
    }
}
