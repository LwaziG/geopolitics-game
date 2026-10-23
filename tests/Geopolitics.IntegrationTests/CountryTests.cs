using Geopolitics.Domain.Countries;
using Geopolitics.Domain.Games;
using Geopolitics.Infrastructure.Countries;
using Geopolitics.Infrastructure.Persistence;
using Geopolitics.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Geopolitics.IntegrationTests;

[Collection(MongoCollection.Name)]
public class CountryTests(MongoFixture mongo)
{
    private static IMongoCollection<CountryDocument> Countries(IMongoDatabase database) =>
        database.GetCollection<CountryDocument>(MongoCollections.Countries);

    [Fact]
    public async Task Seed_LoadsAll195CountriesWithSoldierValues()
    {
        var database = await mongo.NewDatabaseAsync();

        var result = await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);

        Assert.Equal((195, 0), (result.Upserted, result.Deleted));
        var stored = await Countries(database).Find(FilterDefinition<CountryDocument>.Empty).ToListAsync();
        Assert.Equal(195, stored.Count);
        var india = stored.Single(c => c.Code == "IN");
        Assert.Equal(("India", 9), (india.Name, india.SoldierValue));
        Assert.Equal(1, stored.Single(c => c.Code == "VA").SoldierValue);
        Assert.All(stored, c => Assert.Equal(new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc), c.SourceCapturedOn));
    }

    [Fact]
    public async Task Seed_WritesNothingWhenTheListAlreadyMatches()
    {
        var database = await mongo.NewDatabaseAsync();
        await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);

        var second = await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);

        Assert.False(second.Changed);
        Assert.Equal(195, await Countries(database).CountDocumentsAsync(FilterDefinition<CountryDocument>.Empty));
    }

    [Fact]
    public async Task Seed_RepairsMissingChangedAndExtraCountries()
    {
        var database = await mongo.NewDatabaseAsync();
        await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);
        var countries = Countries(database);
        await countries.DeleteOneAsync(c => c.Code == "ZA");
        await countries.UpdateOneAsync(c => c.Code == "IN", Builders<CountryDocument>.Update.Set(c => c.Population, 1));
        await countries.InsertOneAsync(new CountryDocument { Code = "XX", Name = "Atlantis", Population = 1, SoldierValue = 1 });

        var result = await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);

        Assert.Equal((2, 1), (result.Upserted, result.Deleted));
        Assert.Equal(195, await countries.CountDocumentsAsync(FilterDefinition<CountryDocument>.Empty));
        Assert.Equal(9, (await countries.Find(c => c.Code == "IN").SingleAsync()).SoldierValue);
        Assert.Equal(1, await countries.CountDocumentsAsync(c => c.Code == "ZA"));
        Assert.Equal(0, await countries.CountDocumentsAsync(c => c.Code == "XX"));
    }

    [Fact]
    public async Task Catalogue_ListsCountriesByNameAndFindsByCode()
    {
        var database = await mongo.NewDatabaseAsync();
        await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);
        var catalogue = new MongoCountryCatalogue(database);

        var all = await catalogue.GetAllAsync();

        Assert.Equal(195, all.Count);
        Assert.Equal("Afghanistan", all[0].Name);
        Assert.Equal("Zimbabwe", all[^1].Name);
        Assert.Equal("Vatican City", (await catalogue.FindAsync(" va "))?.Name);
        Assert.Null(await catalogue.FindAsync("XX"));
        Assert.Null(await catalogue.FindAsync(""));
    }

    [Fact]
    public async Task Catalogue_DoesNotCacheAnEmptyListReadBeforeSeeding()
    {
        var database = await mongo.NewDatabaseAsync();
        var catalogue = new MongoCountryCatalogue(database);

        Assert.Empty(await catalogue.GetAllAsync());
        await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);

        Assert.Equal(195, (await catalogue.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ANewGame_CanBeSetUpFromTheStoredCountries()
    {
        var database = await mongo.NewDatabaseAsync();
        await CountrySeeder.SeedAsync(database, CountrySeedFile.Current);
        var catalogue = new MongoCountryCatalogue(database);
        var southAfrica = (await catalogue.FindAsync("ZA"))!;

        var opponents = CountryDraw.PickOpponents(await catalogue.GetAllAsync(), southAfrica.Code, new Random(1));
        var game = Game.Create(Guid.NewGuid(), "user-1", southAfrica, opponents, new Random(1));

        Assert.Equal(10, game.Countries.Count);
        Assert.Equal(5, game.GetCountry("ZA").Soldiers);
    }
}
