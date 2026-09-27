using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TestJob.Exceptions;
using TestJob.Models;
using TestJob.Services;

namespace TestJob.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HtmlAnalysisController : ControllerBase
{
    private readonly IHtmlAnalysisService _service;
    private readonly IValidator<HtmlRequest> _validator;
    private readonly ILogger<HtmlAnalysisController> _logger;

    public HtmlAnalysisController(
        IHtmlAnalysisService service,
        IValidator<HtmlRequest> validator,
        ILogger<HtmlAnalysisController> logger)
    {
        _service = service;
        _validator = validator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] HtmlRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST /api/testjob вызван");

        // Валидация
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.First();
            _logger.LogWarning("Validation failed: {Code}", firstError.ErrorCode);

            return Ok(new HtmlResponse
            {
                IsError = 1,
                ErrorCode = firstError.ErrorCode,
                ErrorMessage = firstError.ErrorMessage,
            });
        }

        // Основная логика
        try
        {
            var result = await _service.ProcessAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Business error: {Code}", ex.Code);
            return Ok(new HtmlResponse
            {
                IsError = 1,
                ErrorCode = ex.Code,
                ErrorMessage = ex.Message,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error");
            return Ok(new HtmlResponse
            {
                IsError = 1,
                ErrorCode = "INTERNAL_ERROR",
                ErrorMessage = ex.Message,
            });
        }
    }
}