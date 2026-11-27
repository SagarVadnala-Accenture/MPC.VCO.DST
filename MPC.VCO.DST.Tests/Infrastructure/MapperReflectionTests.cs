using System.Text.Json;
using MPC.VCO.DST.Domain.Models;
using MPC.VCO.DST.Infrastructure.Entities;
using MPC.VCO.DST.Infrastructure.Mapper;

namespace MPC.VCO.DST.Tests.Infrastructure;

[TestClass]
public sealed class MapperReflectionTests
{
    [TestMethod]
    public void ModelMapper_ToDomain_MapsEntityFields()
    {
        ModelEntity entity = new()
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "PC-2026-07-001",
            CreatedOn = "2026-07-02",
            Status = "Draft",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.23m,
            NaturalGasPriceUsdPerMmbtu = 2.34m,
            ExpectedValueMonthlyUsd = 219000m,
            Scope = new ModelScopeEntity
            {
                Type = "Refinery",
                Name = "TX" 
            }
        };

        DstModel model = entity.ToDomain();

        Assert.AreEqual("PC-2026-07", model.PlanningCycleId);
        Assert.AreEqual("PC-2026-07-001", model.ModelId);
        Assert.AreEqual(new DateOnly(2026, 7, 2), model.CreatedOn);
        Assert.AreEqual("Draft", model.Status);
        Assert.AreEqual("Refinery", model.Scope.Type);
        Assert.AreEqual("TX", model.Scope.Name);
        Assert.AreEqual(1.23m, model.EthanePriceUsdPerGal);
        Assert.AreEqual(2.34m, model.NaturalGasPriceUsdPerMmbtu);
        Assert.AreEqual(219000m, model.ExpectedValueMonthlyUsd);
    }

    [TestMethod]
    public void ModelMapper_ToEntity_MapsDomainFieldsAndFormatsDate()
    {
        DstModel model = new()
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "PC-2026-07-001",
            CreatedOn = new DateOnly(2026, 7, 2),
            Status = "Reviewed",
            Scope = new ModelScope
            {
                Type = "Refinery",
                Name = "TX"
            },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.23m,
            NaturalGasPriceUsdPerMmbtu = 2.34m,
            ExpectedValueMonthlyUsd = 219000m
        };

        ModelEntity entity = model.ToEntity("doc-1");

        Assert.AreEqual("doc-1", entity.Id);
        Assert.AreEqual("model", entity.DocumentType);
        Assert.AreEqual("PC-2026-07", entity.PlanningCycleId);
        Assert.AreEqual("PC-2026-07-001", entity.ModelId);
        Assert.AreEqual("2026-07-02", entity.CreatedOn);
        Assert.AreEqual("Reviewed", entity.Status);
        Assert.AreEqual("Refinery", entity.Scope.Type);
        Assert.AreEqual("TX", entity.Scope.Name);
    }

    [TestMethod]
    public void PlanningCycleEntity_JsonDeserialization_ParsesDateOnlyFromIsoDateStrings()
    {
        string json =
            """
            {
              "id": "pc-2026-07",
              "documentType": "planningCycle",
              "planningCycleId": "PC-2026-07",
              "year": 2026,
              "month": 7,
              "name": "July, 2026",
              "startDate": "2026-07-01",
              "endDate": "2026-07-31",
              "cycleState": "Current"
            }
            """;

        PlanningCycleEntity? entity = JsonSerializer.Deserialize(
            json,
            typeof(PlanningCycleEntity),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) as PlanningCycleEntity;

        Assert.IsNotNull(entity);
        Assert.AreEqual(new DateOnly(2026, 7, 1), entity.StartDate);
        Assert.AreEqual(new DateOnly(2026, 7, 31), entity.EndDate);
    }

    [TestMethod]
    public void PlanningCycleMapper_ToDomain_MapsBestModel_WhenPresent()
    {
        PlanningCycleEntity entity = new()
        {
            PlanningCycleId = "PC-2026-07",
            Year = 2026,
            Month = 7,
            Name = "July, 2026",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            CycleState = "Current",
            BestModel = new BestModelEntity
            {
                ModelId = "PC-2026-07-001",
                ExpectedValueMonthlyUsd = 219000m
            }
        };

        PlanningCycle cycle = entity.ToDomain();

        Assert.AreEqual("PC-2026-07", cycle.PlanningCycleId);
        Assert.AreEqual(new DateOnly(2026, 7, 1), cycle.StartDate);
        Assert.AreEqual(new DateOnly(2026, 7, 31), cycle.EndDate);
        Assert.IsNotNull(cycle.BestModel);
        Assert.AreEqual("PC-2026-07-001", cycle.BestModel!.ModelId);
        Assert.AreEqual(219000m, cycle.BestModel.ExpectedValueMonthlyUsd);
    }

    [TestMethod]
    public void ModelNoteMapper_ToDomain_MapsAuthorAndMentions()
    {
        ModelNoteEntity entity = new()
        {
            Id = "note-1",
            ModelId = "PC-2026-07-001",
            NoteText = "Hello @User",
            CreatedAtUtc = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SchemaVersion = 1,
            Author = new ModelNoteAuthorEntity
            {
                UserId = "u1",
                DisplayName = "User One",
                UserPrincipalName = "u1@company.com",
                Initials = "UO"
            },
            Mentions =
            [
                new ModelNoteMentionEntity
                {
                    UserId = "u2",
                    DisplayName = "User Two",
                    Email = "u2@company.com",
                    Start = 6,
                    Length = 5
                }
            ]
        };

        ModelNote note = entity.ToDomain();

        Assert.AreEqual("note-1", note.Id);
        Assert.AreEqual("u1", note.Author.UserId);
        Assert.AreEqual(1, note.Mentions.Count);
        Assert.AreEqual("u2", note.Mentions[0].UserId);
        Assert.AreEqual(6, note.Mentions[0].Start);
    }

    [TestMethod]
    public void ModelNoteMapper_ToEntity_MapsDomainFields()
    {
        ModelNote note = new()
        {
            Id = "note-1",
            ModelId = "PC-2026-07-001",
            NoteText = "Hello",
            Author = new ModelNoteAuthor
            {
                UserId = "u1",
                DisplayName = "User One",
                UserPrincipalName = "u1@company.com",
                Initials = "UO"
            },
            Mentions =
            [
                new NoteMention
                {
                    UserId = "u2",
                    DisplayName = "User Two",
                    Email = "u2@company.com",
                    Start = 0,
                    Length = 4
                }
            ],
            CreatedAtUtc = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SchemaVersion = 2
        };

        ModelNoteEntity entity = note.ToEntity();

        Assert.AreEqual("note-1", entity.Id);
        Assert.AreEqual("PC-2026-07-001", entity.ModelId);
        Assert.AreEqual("Hello", entity.NoteText);
        Assert.AreEqual("u1", entity.Author.UserId);
        Assert.AreEqual(1, entity.Mentions.Count);
    }
}
