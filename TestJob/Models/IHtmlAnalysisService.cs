namespace TestJob.Models;

public interface IHtmlAnalysisService
{
    Task<HtmlResponse> ProcessAsync(HtmlRequest request, CancellationToken cancellationToken);
}