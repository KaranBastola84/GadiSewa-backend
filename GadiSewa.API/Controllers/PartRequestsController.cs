using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.PartRequests;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using GadiSewa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/part-requests")]
[Authorize(Policy = "StaffOnly")]
public sealed class PartRequestsController : ControllerBase
{
    private readonly IRepository<PartRequest> _partRequestRepository;
    private readonly IRepository<Part> _partRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<Staff> _staffRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PartRequestsController(
        IRepository<PartRequest> partRequestRepository,
        IRepository<Part> partRepository,
        IRepository<Vendor> vendorRepository,
        IRepository<Staff> staffRepository,
        IUnitOfWork unitOfWork)
    {
        _partRequestRepository = partRequestRepository;
        _partRepository = partRepository;
        _vendorRepository = vendorRepository;
        _staffRepository = staffRepository;
        _unitOfWork = unitOfWork;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("uid")?.Value;
        return Guid.Parse(userIdClaim!);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PartRequestDto>>>> GetPartRequests(
        [FromQuery] int? status,
        CancellationToken cancellationToken)
    {
        try
        {
            IQueryable<PartRequest> query = _partRequestRepository.Query()
                .AsNoTracking()
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor);

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
            var partRequest = await _partRequestRepository.Query()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Include(x => x.RequestedByStaff)
                    .ThenInclude(x => x.User)
                .Include(x => x.Part)
                .Include(x => x.Vendor)
                .FirstOrDefaultAsync(cancellationToken);

            if (partRequest is null)
            {
                return NotFound(ApiResponse<PartRequestDto>.Failure("Part request not found.", StatusCodes.Status404NotFound));
            }

            return Ok(ApiResponse<PartRequestDto>.Success(PartRequestDto.FromPartRequest(partRequest)));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PartRequestDto>.Failure($"Error retrieving part request: {ex.Message}", StatusCodes.Status500InternalServerError));
        }
    }

    [HttpPost]
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
                    .ThenInclude(x => x.User)
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
                    .ThenInclude(x => x.User)
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
                    .ThenInclude(x => x.User)
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
