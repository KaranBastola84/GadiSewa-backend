using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Application.DTOs.Parts;
using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GadiSewa.API.Controllers;

[ApiController]
[Route("api/admin/parts")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminPartsController : ControllerBase
{
    private readonly IRepository<Part> _partRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminPartsController(IRepository<Part> partRepository, IUnitOfWork unitOfWork)
    {
        _partRepository = partRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PartDto>>>> GetParts(CancellationToken cancellationToken)
    {
        var parts = await _partRepository.ListAsync(cancellationToken: cancellationToken);
        var dto = parts.Select(PartDto.FromPart).ToList();
        return Ok(ApiResponse<IReadOnlyList<PartDto>>.Success(dto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PartDto>>> GetPart(Guid id, CancellationToken cancellationToken)
    {
        var part = await _partRepository.GetByIdAsync(id, cancellationToken);
        if (part is null)
        {
            return NotFound(ApiResponse<PartDto>.Failure("Part not found.", StatusCodes.Status404NotFound));
        }

        return Ok(ApiResponse<PartDto>.Success(PartDto.FromPart(part)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PartDto>>> CreatePart([FromBody] CreatePartRequestDto request, CancellationToken cancellationToken)
    {
        var existing = await _partRepository.ListAsync(x => x.PartNumber == request.PartNumber.Trim(), cancellationToken);
        if (existing.Count > 0)
        {
            return Conflict(ApiResponse<PartDto>.Failure("Part with this part number already exists.", StatusCodes.Status409Conflict));
        }

        var part = new Part
        {
            Name = request.Name.Trim(),
            PartNumber = request.PartNumber.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            UnitPrice = request.UnitPrice,
            StockQuantity = request.StockQuantity,
            ReorderLevel = request.ReorderLevel
        };

        await _partRepository.AddAsync(part, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<PartDto>.Success(PartDto.FromPart(part), StatusCodes.Status201Created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PartDto>>> UpdatePart(Guid id, [FromBody] UpdatePartRequestDto request, CancellationToken cancellationToken)
    {
        var part = await _partRepository.GetByIdAsync(id, cancellationToken);
        if (part is null)
        {
            return NotFound(ApiResponse<PartDto>.Failure("Part not found.", StatusCodes.Status404NotFound));
        }

        var conflict = await _partRepository.ListAsync(x => x.PartNumber == request.PartNumber.Trim() && x.Id != id, cancellationToken);
        if (conflict.Count > 0)
        {
            return Conflict(ApiResponse<PartDto>.Failure("Another part with this part number already exists.", StatusCodes.Status409Conflict));
        }

        part.Name = request.Name.Trim();
        part.PartNumber = request.PartNumber.Trim();
        part.Description = request.Description?.Trim() ?? string.Empty;
        part.UnitPrice = request.UnitPrice;
        part.StockQuantity = request.StockQuantity;
        part.ReorderLevel = request.ReorderLevel;
        part.UpdatedAt = DateTimeOffset.UtcNow;

        _partRepository.Update(part);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<PartDto>.Success(PartDto.FromPart(part)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeletePart(Guid id, CancellationToken cancellationToken)
    {
        var part = await _partRepository.GetByIdAsync(id, cancellationToken);
        if (part is null)
        {
            return NotFound(ApiResponse<object?>.Failure("Part not found.", StatusCodes.Status404NotFound));
        }

        _partRepository.Remove(part);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object?>.Success(null));
    }
}