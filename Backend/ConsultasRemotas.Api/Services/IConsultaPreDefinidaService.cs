using ConsultasRemotas.Api.Models;

namespace ConsultasRemotas.Api.Services;

public interface IConsultaPreDefinidaService
{
    List<ConsultaPreDefinidaInfo> ListarConsultas();
    Task<ConsultaPreDefinidaResponse> ExecutarAsync(ExecutarConsultaPreDefinidaRequest request, CancellationToken cancellationToken = default);
}
