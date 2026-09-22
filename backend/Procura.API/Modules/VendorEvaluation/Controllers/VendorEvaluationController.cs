using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procura.API.Modules.VendorEvaluation.DTOs;
using Procura.API.Modules.VendorEvaluation.Services;

namespace Procura.API.Modules.VendorEvaluation.Controllers;

/// <summary>
/// Controller providing REST endpoints for vendor evaluation, scoring, ranking, and recommendations.
/// Enforces JWT authentication and Role-Based Access Control (PROCUREMENT_OFFICER, MANAGER).
/// </summary>
[ApiController]
[Route("api/vendor-evaluations")]
[Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN")]
[Produces("application/json")]
public class VendorEvaluationController : ControllerBase
{
    private readonly IVendorEvaluationService _evaluationService;
    private readonly ILogger<VendorEvaluationController> _logger;

    public VendorEvaluationController(
        IVendorEvaluationService evaluationService,
        ILogger<VendorEvaluationController> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Evaluates candidate vendors against procurement requirements, calculates weighted scores, assigns ranks, and saves results.
    /// </summary>
    /// <param name="request">Candidate vendors and procurement constraints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Executive recommendation summary with ranked candidates.</returns>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ProcurementEvaluationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProcurementEvaluationSummaryDto>> EvaluateCandidateVendors(
        [FromBody] EvaluateVendorsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Processing evaluation for ProcurementRequest {ProcurementRequestId}", request.ProcurementRequestId);
        var summary = await _evaluationService.EvaluateAndRankCandidateVendorsAsync(request, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Retrieves all evaluations associated with a given Procurement Request ID, ordered by rank.
    /// </summary>
    /// <param name="procurementRequestId">UUID of the procurement request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of vendor evaluations ordered by rank.</returns>
    [HttpGet("procurement-request/{procurementRequestId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<VendorEvaluationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<VendorEvaluationResponseDto>>> GetByProcurementRequestId(
        [FromRoute] Guid procurementRequestId,
        CancellationToken cancellationToken)
    {
        var evaluations = await _evaluationService.GetEvaluationsByProcurementRequestIdAsync(procurementRequestId, cancellationToken);
        return Ok(evaluations);
    }

    /// <summary>
    /// Retrieves the executive recommendation summary for human review and downstream Approval Workflow Management.
    /// </summary>
    /// <param name="procurementRequestId">UUID of the procurement request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recommendation summary or 404 if not yet evaluated.</returns>
    [HttpGet("procurement-request/{procurementRequestId:guid}/recommendation")]
    [ProducesResponseType(typeof(ProcurementEvaluationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProcurementEvaluationSummaryDto>> GetRecommendationSummary(
        [FromRoute] Guid procurementRequestId,
        CancellationToken cancellationToken)
    {
        var summary = await _evaluationService.GetRecommendationSummaryAsync(procurementRequestId, cancellationToken);
        if (summary == null)
        {
            return NotFound($"No evaluation recommendation found for ProcurementRequest {procurementRequestId}.");
        }

        return Ok(summary);
    }

    /// <summary>
    /// Retrieves a single evaluation by its ID with full criterion scores.
    /// </summary>
    /// <param name="id">UUID of the evaluation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evaluation details or 404 Not Found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VendorEvaluationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VendorEvaluationResponseDto>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var evaluation = await _evaluationService.GetEvaluationByIdAsync(id, cancellationToken);
        if (evaluation == null)
        {
            return NotFound($"Evaluation with ID {id} not found.");
        }

        return Ok(evaluation);
    }

    /// <summary>
    /// Retrieves all historical evaluations for a specific Vendor.
    /// </summary>
    /// <param name="vendorId">UUID of the vendor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of vendor evaluations.</returns>
    [HttpGet("vendor/{vendorId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<VendorEvaluationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<VendorEvaluationResponseDto>>> GetByVendorId(
        [FromRoute] Guid vendorId,
        CancellationToken cancellationToken)
    {
        var evaluations = await _evaluationService.GetEvaluationsByVendorIdAsync(vendorId, cancellationToken);
        return Ok(evaluations);
    }

    /// <summary>
    /// Creates a manual vendor evaluation record.
    /// </summary>
    /// <param name="dto">Creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created evaluation response.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(VendorEvaluationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VendorEvaluationResponseDto>> CreateEvaluation(
        [FromBody] CreateVendorEvaluationDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var created = await _evaluationService.CreateEvaluationAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing vendor evaluation record.
    /// </summary>
    /// <param name="id">UUID of the evaluation.</param>
    /// <param name="dto">Update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated evaluation response or 404 Not Found.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VendorEvaluationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VendorEvaluationResponseDto>> UpdateEvaluation(
        [FromRoute] Guid id,
        [FromBody] UpdateVendorEvaluationDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var updated = await _evaluationService.UpdateEvaluationAsync(id, dto, cancellationToken);
        if (updated == null)
        {
            return NotFound($"Evaluation with ID {id} not found.");
        }

        return Ok(updated);
    }

    /// <summary>
    /// Deletes an evaluation record. Requires MANAGER role.
    /// </summary>
    /// <param name="id">UUID of the evaluation to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content or 404 Not Found.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "MANAGER")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteEvaluation(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _evaluationService.DeleteEvaluationAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound($"Evaluation with ID {id} not found.");
        }

        return NoContent();
    }

}
