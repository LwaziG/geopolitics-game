using Geopolitics.Domain.Countries;

namespace Geopolitics.Application.Countries;

/// <summary>The playable countries (the 195 sovereign states) that games are set up from.</summary>
public interface ICountryCatalogue
{
    Task<IReadOnlyList<CountryProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CountryProfile?> FindAsync(string code, CancellationToken cancellationToken = default);
}
